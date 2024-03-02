using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using _1RM.Model.Protocol.Base;
using Org.BouncyCastle.Asn1.X509;
using Stylet;

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
    public partial class IntegrateHostForWinFrom : HostBase, IDisposable
    {
        #region API

        [DllImport("User32.dll")]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll")]
        public static extern int ShowWindow(IntPtr hwnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        public delegate bool WndEnumProc(IntPtr hWnd, int lParam);
        [DllImport("user32.dll")]
        public static extern int EnumWindows(WndEnumProc lpEnumFunc, int lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern int SetForegroundWindow(IntPtr hwnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        public static extern IntPtr SetFocus(HandleRef hWnd);
        [DllImport("user32")]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32")]
        public static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        // https://stackoverflow.com/a/57819801/8629624
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern int GetWindowTextLength(IntPtr hWnd);

        public static string GetWindowTitle(IntPtr hWnd)
        {
            var length = GetWindowTextLength(hWnd) + 1;
            var title = new StringBuilder(length);
            GetWindowText(hWnd, title, length);
            return title.ToString();
        }

        internal enum GetWindowLongIndex
        {
            GWL_STYLE = -16,
            GWL_EXSTYLE = -20
        }
        internal enum ShowWindowStyles : short
        {
            SW_HIDE = 0,
            SW_SHOWNORMAL = 1,
            SW_NORMAL = 1,
            SW_SHOWMINIMIZED = 2,
            SW_SHOWMAXIMIZED = 3,
            SW_MAXIMIZE = 3,
            SW_SHOWNOACTIVATE = 4,
            SW_SHOW = 5,
            SW_MINIMIZE = 6,
            SW_SHOWMINNOACTIVE = 7,
            SW_SHOWNA = 8,
            SW_RESTORE = 9,
            SW_SHOWDEFAULT = 10,
            SW_FORCEMINIMIZE = 11,
            SW_MAX = 11
        }
        internal enum WindowStyles : uint
        {
            WS_OVERLAPPED = 0x00000000,
            WS_POPUP = 0x80000000,
            WS_CHILD = 0x40000000,
            WS_MINIMIZE = 0x20000000,
            WS_VISIBLE = 0x10000000,
            WS_DISABLED = 0x08000000,
            WS_CLIPSIBLINGS = 0x04000000,
            WS_CLIPCHILDREN = 0x02000000,
            WS_MAXIMIZE = 0x01000000,
            WS_CAPTION = 0x00C00000,      // 	创建一个有标题框的窗口
            WS_BORDER = 0x00800000,       // 	创建一个单边框的窗口
            WS_DLGFRAME = 0x00400000,
            WS_VSCROLL = 0x00200000,      // 创建一个有垂直滚动条的窗口。
            WS_HSCROLL = 0x00100000,
            WS_SYSMENU = 0x00080000,
            WS_THICKFRAME = 0x00040000,   // 创建一个具有可调边框的窗口
            WS_GROUP = 0x00020000,
            WS_TABSTOP = 0x00010000,
            WS_MINIMIZEBOX = 0x00020000,
            WS_MAXIMIZEBOX = 0x00010000,
            WS_TILED = 0x00000000,
            WS_ICONIC = 0x20000000,
            WS_SIZEBOX = 0x00040000,
            WS_POPUPWINDOW = 0x80880000,
            WS_OVERLAPPEDWINDOW = 0x00CF0000,
            WS_TILEDWINDOW = 0x00CF0000,
            WS_CHILDWINDOW = 0x40000000
        }

        [Flags]
        internal enum WindowExStyles
        {
            WS_EX_DLGMODALFRAME = 0x00000001,
            WS_EX_NOPARENTNOTIFY = 0x00000004,
            WS_EX_TOPMOST = 0x00000008,
            WS_EX_ACCEPTFILES = 0x00000010,
            WS_EX_TRANSPARENT = 0x00000020,
            WS_EX_MDICHILD = 0x00000040,
            WS_EX_TOOLWINDOW = 0x00000080,
            WS_EX_WINDOWEDGE = 0x00000100,
            WS_EX_CLIENTEDGE = 0x00000200,
            WS_EX_CONTEXTHELP = 0x00000400,
            WS_EX_RIGHT = 0x00001000,
            WS_EX_LEFT = 0x00000000,
            WS_EX_RTLREADING = 0x00002000,
            WS_EX_LTRREADING = 0x00000000,
            WS_EX_LEFTSCROLLBAR = 0x00004000,
            WS_EX_RIGHTSCROLLBAR = 0x00000000,
            WS_EX_CONTROLPARENT = 0x00010000,
            WS_EX_STATICEDGE = 0x00020000,
            WS_EX_APPWINDOW = 0x00040000,
            WS_EX_OVERLAPPEDWINDOW = 0x00000300,
            WS_EX_PALETTEWINDOW = 0x00000188,
            WS_EX_LAYERED = 0x00080000,
            WS_EX_NOACTIVATE = 0x08000000
        }

        #endregion
        public new ProtocolHostStatus Status => _form.Status;

        private readonly RdpHostForm _form;
        private readonly System.Windows.Forms.Panel _panel;

        public IntegrateHostForWinFrom(ProtocolBase protocol, RdpHostForm form) : base(protocol, true)
        {
            this._form = form;
            InitializeComponent();

            _panel = new System.Windows.Forms.Panel
            {
                BackColor = System.Drawing.Color.Transparent,
                Dock = System.Windows.Forms.DockStyle.Fill,
                BorderStyle = BorderStyle.None
            };
            _panel.SizeChanged += PanelOnSizeChanged;

            FormsHost.Child = _panel;
            _form.Closed += FormOnClosed;
        }

        private void FormOnClosed(object? sender, EventArgs e)
        {
            this.Close();
        }

        #region Resize

        private void SetToPanelSize()
        {
            if (_form.IsLoaded)
                MoveWindow(_form.Handle, 0, 0, (int)(_panel.Width), (int)(_panel.Height), true);
        }

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
        /// remove title border frame scroll of the process
        /// </summary>
        private void SetExeWindowStyle()
        {
            Dispatcher.Invoke(() =>
            {
                SetParent(_form.Handle, _panel.Handle);
                _form.FormBorderStyle = FormBorderStyle.None;
                _form.ShowInTaskbar = false;
                _panel.Controls.Add(_form);
                SetToPanelSize();
            });
        }

        public override void Conn()
        {
            _form.FormBorderStyle = FormBorderStyle.None;
            SetParent(_form.Handle, _panel.Handle);
            ShowWindow(_form.Handle, (int)ShowWindowStyles.SW_SHOWMAXIMIZED);
            //_panel.Controls.Add(_form);
            Debug.Assert(ParentWindow != null);
            _form.Show();
            _form.Conn();
        }

        public override void ReConn()
        {
            _form.ReConn();
        }

        public override void Close()
        {
            _form.Closed -= FormOnClosed;
            if (_form.Status == ProtocolHostStatus.Connected)
            {
                _form.Close();
            }
            Dispose();
            base.Close();
        }


        public void Dispose()
        {
            Execute.OnUIThread(() =>
            {
                _panel.Dispose();
                FormsHost?.Dispose();
                GC.SuppressFinalize(this);
            });
        }

        public override void FocusOnMe()
        {
            SetForegroundWindow(this.GetHostHwnd());
        }

        public override ProtocolHostType GetProtocolHostType()
        {
            return ProtocolHostType.Integrate;
        }

        public override IntPtr GetHostHwnd()
        {
            return _form.Handle;
        }
    }
}