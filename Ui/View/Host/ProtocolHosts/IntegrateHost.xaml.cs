using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Model.ProtocolRunner;
using _1RM.Model.ProtocolRunner.Default;
using _1RM.Utils;
using _1RM.Utils.KiTTY;
using Shawn.Utils;
using Shawn.Utils.Wpf.Controls;
using Stylet;
using Path = System.IO.Path;
using Timer = System.Timers.Timer;

/*
 * Note:

We should add <UseWindowsForms>true</UseWindowsForms> in the csproj.

<PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>netcoreapp3.0</TargetFramework>
    <UseWpf>true</UseWpf>
    <UseWindowsForms>true</UseWindowsForms>
</PropertyGroup>


 */

namespace _1RM.View.Host.ProtocolHosts
{
    public partial class IntegrateHost : HostBase, IDisposable
    {

        private Timer? _timer;
        private Process? _process;
        private readonly System.Windows.Forms.Panel _panel;
        private readonly HashSet<IntPtr> _exeHandles = new();
        public readonly string ExeFullName;
        public string ExeArguments { get; private set; }
        private readonly Dictionary<string, string> _environmentVariables;
        private readonly Runner _runner;
        private readonly string _sessionName = "";

        public static IntegrateHost Create(ProtocolBase protocol, Runner runner, string exeFullName, string exeArguments, Dictionary<string, string>? environmentVariables = null)
        {
            IntegrateHost? view = null;
            Execute.OnUIThreadSync(() =>
            {
                view = new IntegrateHost(protocol, runner, exeFullName, exeArguments, environmentVariables);
            });
            return view!;
        }

        private IntegrateHost(ProtocolBase protocol, Runner runner, string exeFullName, string exeArguments, Dictionary<string, string>? environmentVariables = null) : base(protocol, false)
        {
            ExeFullName = exeFullName;
            ExeArguments = exeArguments;
            _runner = runner;
            _environmentVariables = environmentVariables ?? new Dictionary<string, string>();
            InitializeComponent();

            _panel = new System.Windows.Forms.Panel
            {
                BackColor = System.Drawing.Color.Transparent,
                Dock = System.Windows.Forms.DockStyle.Fill,
                BorderStyle = BorderStyle.None
            };
            _panel.SizeChanged += PanelOnSizeChanged;

            FormsHost.Child = _panel;


            if (runner is KittyRunner kittyRunner)
            {
                _sessionName = $"{Assert.APP_NAME}_{protocol.Protocol}_{protocol.Id}_{DateTimeOffset.Now.ToUnixTimeSeconds()}";
                RunAfterConnected += () => PuttyConnectableExtension.DelKittySessionConfig(_sessionName, kittyRunner.PuttyExePath);
            }
        }

        #region Resize

        private void SetToPanelSize()
        {
            if (_process != null)
            {
                CleanupClosedHandle();
                foreach (var exeHandle in _exeHandles)
                {
                    WindowExtensions.MoveWindow(exeHandle, 0, 0, (int)(_panel.Width), (int)(_panel.Height), true);
                }
            }
        }

        // not work with UHD + scaling, e.g. 4k+150%, the ActualWidth will return 1500 / 150% = 1000pix while the real width is 1500pix.
        //protected override void OnRender(DrawingContext drawingContext)
        //{
        //    if (_process != null)
        //    {
        //        CleanupClosedHandle();
        //        SimpleLogHelper.Debug($"ActualWidth = {(int)(FormsHost.ActualWidth)}, ActualHeight = {(int)(FormsHost.ActualHeight)}");
        //        SimpleLogHelper.Debug($"GridActualWidth = {(int)(Grid.ActualWidth)}, GridActualHeight = {(int)(Grid.ActualHeight)}");
        //        foreach (var exeHandle in _exeHandles)
        //        {
        //            MoveWindow(exeHandle, 0, 0, (int)(Grid.ActualWidth), (int)(Grid.ActualHeight), true);
        //        }
        //        //MoveWindow(_exeHandle, 0, 0, (int)(FormsHost.ActualWidth), (int)(FormsHost.ActualHeight), true);
        //    }
        //    base.OnRender(drawingContext);
        //}

        private void PanelOnSizeChanged(object? sender, EventArgs e)
        {
            SetToPanelSize();
        }
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            this.InvalidateVisual();
            base.OnRenderSizeChanged(sizeInfo);
        }
        #endregion

        /// <summary>
        /// remove the handles in _exeHandles which is not  window
        /// </summary>
        private void CleanupClosedHandle()
        {
            foreach (var handle in _exeHandles.ToArray())
            {
                if (handle.IsWindowEx() == false)
                {
                    SimpleLogHelper.Debug($"_exeHandles remove {handle}");
                    _exeHandles.Remove(handle);
                }
            }
        }

        /// <summary>
        /// remove title border frame scroll of the process
        /// </summary>
        private void SetExeWindowStyle()
        {
            CleanupClosedHandle();
            Dispatcher.Invoke(() =>
            {
                foreach (var exeHandle in _exeHandles)
                {
                    // must be set or exe will be shown out of panel
                    exeHandle.SetParentEx(_panel.Handle);
                    exeHandle.ShowWindowEx(WindowExtensions.ShowWindowStyles.SW_MAXIMIZE);
                    int lStyle = exeHandle.GetWindowLongEx(WindowExtensions.GetWindowLongIndex.GWL_STYLE);
                    lStyle &= ~(int)WindowExtensions.WindowStyles.WS_CAPTION; // no title
                    lStyle &= ~(int)WindowExtensions.WindowStyles.WS_BORDER; // no border
                    lStyle &= ~(int)WindowExtensions.WindowStyles.WS_THICKFRAME;
                    lStyle &= ~(int)WindowExtensions.WindowStyles.WS_VSCROLL;
                    //lStyle |= (int)WindowExStyles.WS_EX_TOOLWINDOW;
                    WindowExtensions.SetWindowLong(exeHandle, (int)WindowExtensions.GetWindowLongIndex.GWL_STYLE, lStyle);
                }
                SetToPanelSize();
            });
        }

        public override void Conn()
        {
            SetStatus(ProtocolHostStatus.Connecting);
            var tsk = new Task(StartExe);
            tsk.Start();
        }

        public override void ReConn()
        {
            CloseIntegrate();
            Conn();
        }

        public override void CloseConn()
        {
            Dispose();
            base.CloseConn();
        }

        public void ShowWindow(bool isShow)
        {
            foreach (var exeHandle in _exeHandles)
            {
                exeHandle.ShowWindowEx((isShow ? WindowExtensions.ShowWindowStyles.SW_SHOWMAXIMIZED : WindowExtensions.ShowWindowStyles.SW_HIDE));
            }
        }


        public void Dispose()
        {
            Execute.OnUIThread(() =>
            {
                CloseIntegrate();
                _timer?.Dispose();
                _process?.Dispose();
                _panel.Dispose();
                FormsHost?.Dispose();
                GC.SuppressFinalize(this);
            });
        }

        private void CloseIntegrate()
        {
            Execute.OnUIThread(() =>
            {
                _timer?.Stop();
                _timer?.Dispose();
                _timer = null;
                if (_process != null)
                {
                    try
                    {
                        _process.Exited -= ProcessOnExited;
                        _process.Kill();
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }

                SetStatus(ProtocolHostStatus.Disconnected);
            });
        }

        private void StartExe()
        {
            RunBeforeConnect?.Invoke();
            var exeFullName = ExeFullName;

            if (ProtocolServer is IKittyConnectable kittyConnectable && _runner is KittyRunner kittyRunner)
            {
                ExeArguments = kittyConnectable.GetExeArguments(_sessionName);
                if(ProtocolServer is SSH ssh)
                    ssh.ConfigKitty(_sessionName, kittyRunner, ssh.PrivateKey);
                else
                    kittyConnectable.ConfigKitty(_sessionName, kittyRunner, "");
            }

            if (Path.IsPathRooted(exeFullName)
                && File.Exists(ExeFullName) == false)
            {
                MessageBoxHelper.ErrorAlert($"Exe file '{ExeFullName}' does not existed! We can not start the connection!");
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = exeFullName,
                WorkingDirectory = new FileInfo(exeFullName).DirectoryName,
                Arguments = ExeArguments,
                WindowStyle = ProcessWindowStyle.Normal
            };

            // Set environment variables
            if (this._environmentVariables?.Count > 0)
            {
                startInfo.UseShellExecute = false;
                foreach (var kv in this._environmentVariables)
                {
                    if (startInfo.EnvironmentVariables.ContainsKey(kv.Key) == false)
                        startInfo.EnvironmentVariables.Add(kv.Key, kv.Value);
                    startInfo.EnvironmentVariables[kv.Key] = kv.Value;
                }
            }

            _process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };
            _process.Exited += ProcessOnExited;
            _process.Start();

            SimpleLogHelper.Debug($"{nameof(IntegrateHost)}: Start process {exeFullName}");

            Task.Factory.StartNew(() =>
            {
                Thread.Sleep(5 * 1000);
                RunAfterConnected?.Invoke();
            });

            // keep detect MainWindowHandle in next 10 seconds.
            var endTime = DateTime.Now.AddSeconds(10);
            _timer?.Dispose();
            _timer = new Timer { Interval = 100, AutoReset = false };
            _timer.Elapsed += (sender, args) =>
            {
                _process.Refresh();
                if (_process == null)
                {
                    return;
                }
                else if (_process.MainWindowHandle != IntPtr.Zero
                    && _exeHandles.Contains(_process.MainWindowHandle) == false)
                {
                    _exeHandles.Add(_process.MainWindowHandle);
                    SimpleLogHelper.Debug($"new _process.MainWindowHandle = {_process.MainWindowHandle}");
                    SetExeWindowStyle();
                }

                if (DateTime.Now > endTime && _exeHandles.Count > 0)
                    return;
                _timer?.Start();
            };
            _timer.Start();
        }

        private void ProcessOnExited(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                _timer?.Stop();
                _timer?.Dispose();
                _timer = null;
                _process = null;
                FormsHost.Visibility = Visibility.Collapsed;
            });
            _process = null;
            CloseConn();
        }

        public override void FocusOnMe()
        {
            WindowExtensions.SetForegroundWindow(this.GetHostHwnd());
        }

        public override ProtocolHostType GetProtocolHostType()
        {
            return ProtocolHostType.Integrate;
        }

        public override IntPtr GetHostHwnd()
        {
            if (_exeHandles.Count > 0)
                return _exeHandles.Last();
            return _process?.MainWindowHandle ?? IntPtr.Zero;
        }

        public Action? RunBeforeConnect { get; set; }
        public Action? RunAfterConnected { get; set; }
    }
}