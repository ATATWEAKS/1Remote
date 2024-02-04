using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Ribbon;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Interop;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using _1RM.View.Settings;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Shawn.Utils.WpfResources.Theme.Styles;
using Stylet;
using Binding = System.Windows.Data.Binding;
using CheckBox = System.Windows.Controls.CheckBox;
using UserControl = System.Windows.Controls.UserControl;

namespace _1RM.View.Host.ProtocolHosts
{
    public enum ProtocolHostStatus
    {
        NotInit,
        Initializing,
        Initialized,
        Connecting,
        Connected,
        Disconnected,
        WaitingForReconnect
    }

    public enum ProtocolHostType
    {
        Native,
        Integrate
    }

    public interface IHostBase
    {
        public ProtocolBase ProtocolServer { get; }
        public WindowBase? ParentWindow { get; }
        public void SetParentWindow(WindowBase? value);

        public IntPtr ParentWindowHandle { get; }

        public ProtocolHostStatus Status { get; }

        public string ConnectionId { get; }


        public Action<string>? OnClosed { get; set; }
        public Action<string>? OnFullScreen2Window { get; set; }

        public bool CanFullScreen { get; }

        public List<MenuItem> MenuItems { get; }
        public bool CanResizeNow();
        /// <summary>
        /// in rdp, tab window cannot resize until rdp is connected. or rdp will not fit window size.
        /// </summary>
        public Action? OnCanResizeNowChanged { get; set; }

        public void ToggleAutoResize(bool isEnable);
        public abstract void Conn();

        public abstract void ReConn();

        /// <summary>
        /// disconnect the session and close host window
        /// </summary>
        public void Close();

        public void GoFullScreen();
        public void FocusOnMe();

        public ProtocolHostType GetProtocolHostType();

        public IntPtr GetHostHwnd();
    }


    public abstract class HostBaseWinform : Form, IHostBase
    {
        public ProtocolBase ProtocolServer { get; }
        private WindowBase? _parentWindow;
        public WindowBase? ParentWindow => _parentWindow;

        public virtual void SetParentWindow(WindowBase? value)
        {
            if (_parentWindow == value) return;
            _parentWindow = value;
            ParentWindowHandle = IntPtr.Zero;

            if (null == value) return;
            var window = Window.GetWindow(value);
            if (window != null)
            {
                var wih = new WindowInteropHelper(window);
                ParentWindowHandle = wih.Handle;
            }
        }

        public IntPtr ParentWindowHandle { get; private set; } = IntPtr.Zero;

        private ProtocolHostStatus _status = ProtocolHostStatus.NotInit;
        public ProtocolHostStatus Status
        {
            get => _status;
            protected set
            {
                if (_status != value)
                {
                    SimpleLogHelper.Debug(this.GetType().Name + ": Status => " + value);
                    _status = value;
                    OnCanResizeNowChanged?.Invoke();
                }
            }
        }

        protected HostBaseWinform(ProtocolBase protocolServer, bool canFullScreen = false)
        {
            ProtocolServer = protocolServer;
            CanFullScreen = canFullScreen;

            // Add right click menu
            {
                var tb = new TextBlock();
                tb.SetResourceReference(TextBlock.TextProperty, "Reconnect");
                MenuItems.Add(new System.Windows.Controls.MenuItem()
                {
                    Header = tb,
                    Command = new RelayCommand((o) => { ReConn(); })
                });
            }

            {
                var tb = new TextBlock();
                tb.SetResourceReference(TextBlock.TextProperty, "Close");
                MenuItems.Add(new System.Windows.Controls.MenuItem()
                {
                    Header = tb,
                    Command = new RelayCommand((o) => { Close(); }),
                });
            }

            {
                var cb = new CheckBox
                {
                    Content = IoC.Translate("Show XXX button", IoC.Translate("Reconnect") ?? ""),
                    IsHitTestVisible = false,
                };

                var binding = new Binding("TabHeaderShowReConnectButton")
                {
                    Source = SettingsPage,
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                };
                cb.SetBinding(CheckBox.IsCheckedProperty, binding);
                MenuItems.Add(new System.Windows.Controls.MenuItem()
                {
                    Header = cb,
                    Command = new RelayCommand((_) => { SettingsPage.TabHeaderShowReConnectButton = !SettingsPage.TabHeaderShowReConnectButton; }),
                });
            }

            {
                var cb = new CheckBox
                {
                    Content = IoC.Translate("Show XXX button", IoC.Translate("Close") ?? ""),
                    IsHitTestVisible = false,
                };

                var binding = new Binding("TabHeaderShowCloseButton")
                {
                    Source = SettingsPage,
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                };
                cb.SetBinding(CheckBox.IsCheckedProperty, binding);
                MenuItems.Add(new System.Windows.Controls.MenuItem()
                {
                    Header = cb,
                    Command = new RelayCommand((_) => { SettingsPage.TabHeaderShowCloseButton = !SettingsPage.TabHeaderShowCloseButton; }),
                });
            }

            //MenuItems.Add(new RibbonApplicationSplitMenuItem());

            var actions = protocolServer.GetActions(true);
            foreach (var action in actions)
            {
                var tb = new TextBlock()
                {
                    Text = action.ActionName,
                };
                MenuItems.Add(new System.Windows.Controls.MenuItem()
                {
                    Header = tb,
                    Command = new RelayCommand((o) => { action.Run(); }),
                });
            }
        }

        public string ConnectionId
        {
            get
            {
                if (ProtocolServer.IsOnlyOneInstance())
                {
                    return ProtocolServer.BuildConnectionId();
                }
                else
                {
                    return ProtocolServer.BuildConnectionId() + "_" + this.GetHashCode();
                }
            }
        }

        /// <summary>
        /// for xaml binding
        /// </summary>
        public SettingsPageViewModel SettingsPage => IoC.Get<SettingsPageViewModel>();

        public bool CanFullScreen { get; protected set; }

        /// <summary>
        /// special menu for tab
        /// </summary>
        public List<MenuItem> MenuItems { get; set; } = new List<MenuItem>();

        /// <summary>
        /// since resizing when rdp is connecting would not tiger the rdp size change event
        /// then I let rdp host return false when rdp is on connecting to prevent TabWindow resize or maximize.
        /// </summary>
        /// <returns></returns>
        public virtual bool CanResizeNow()
        {
            return true;
        }

        /// <summary>
        /// in rdp, tab window cannot resize until rdp is connected. or rdp will not fit window size.
        /// </summary>
        public Action? OnCanResizeNowChanged { get; set; } = null;

        public virtual void ToggleAutoResize(bool isEnable)
        {
        }

        public abstract void Conn();

        public abstract void ReConn();

        /// <summary>
        /// disconnect the session and close host window
        /// </summary>
        public virtual void Close()
        {
            this.OnClosed?.Invoke(ConnectionId);
        }

        public virtual void GoFullScreen()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// call to focus the AxRdp or putty
        /// </summary>
        public virtual void FocusOnMe()
        {
            // do nothing
        }

        public abstract ProtocolHostType GetProtocolHostType();

        /// <summary>
        /// if it is a Integrate host, then return process's hwnd.
        /// </summary>
        /// <returns></returns>
        public abstract IntPtr GetHostHwnd();

        public Action<string>? OnClosed { get; set; } = null;
        public Action<string>? OnFullScreen2Window { get; set; } = null;
    }


    public abstract class HostBase : UserControl, IHostBase
    {
        public ProtocolBase ProtocolServer { get; }
        private WindowBase? _parentWindow;
        public WindowBase? ParentWindow => _parentWindow;

        public virtual void SetParentWindow(WindowBase? value)
        {
            if (_parentWindow == value) return;
            _parentWindow = value;
            ParentWindowHandle = IntPtr.Zero;

            if (null == value) return;
            var window = Window.GetWindow(value);
            if (window != null)
            {
                var wih = new WindowInteropHelper(window);
                ParentWindowHandle = wih.Handle;
            }
        }

        public IntPtr ParentWindowHandle { get; private set; } = IntPtr.Zero;

        private ProtocolHostStatus _status = ProtocolHostStatus.NotInit;
        public ProtocolHostStatus Status
        {
            get => _status;
            protected set
            {
                if (_status != value)
                {
                    SimpleLogHelper.Debug(this.GetType().Name + ": Status => " + value);
                    _status = value;
                    OnCanResizeNowChanged?.Invoke();
                }
            }
        }

        protected HostBase(ProtocolBase protocolServer, bool canFullScreen = false)
        {
            ProtocolServer = protocolServer;
            CanFullScreen = canFullScreen;

            // Add right click menu
            {
                var tb = new TextBlock();
                tb.SetResourceReference(TextBlock.TextProperty, "Reconnect");
                MenuItems.Add(new System.Windows.Controls.MenuItem()
                {
                    Header = tb,
                    Command = new RelayCommand((o) => { ReConn(); })
                });
            }

            {
                var tb = new TextBlock();
                tb.SetResourceReference(TextBlock.TextProperty, "Close");
                MenuItems.Add(new System.Windows.Controls.MenuItem()
                {
                    Header = tb,
                    Command = new RelayCommand((o) => { Close(); }),
                });
            }

            {
                var cb = new CheckBox
                {
                    Content = IoC.Translate("Show XXX button", IoC.Translate("Reconnect") ?? ""),
                    IsHitTestVisible = false,
                };

                var binding = new Binding("TabHeaderShowReConnectButton")
                {
                    Source = SettingsPage,
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                };
                cb.SetBinding(CheckBox.IsCheckedProperty, binding);
                MenuItems.Add(new System.Windows.Controls.MenuItem()
                {
                    Header = cb,
                    Command = new RelayCommand((_) => { SettingsPage.TabHeaderShowReConnectButton = !SettingsPage.TabHeaderShowReConnectButton; }),
                });
            }

            {
                var cb = new CheckBox
                {
                    Content = IoC.Translate("Show XXX button", IoC.Translate("Close") ?? ""),
                    IsHitTestVisible = false,
                };

                var binding = new Binding("TabHeaderShowCloseButton")
                {
                    Source = SettingsPage,
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                };
                cb.SetBinding(CheckBox.IsCheckedProperty, binding);
                MenuItems.Add(new System.Windows.Controls.MenuItem()
                {
                    Header = cb,
                    Command = new RelayCommand((_) => { SettingsPage.TabHeaderShowCloseButton = !SettingsPage.TabHeaderShowCloseButton; }),
                });
            }

            //MenuItems.Add(new RibbonApplicationSplitMenuItem());

            var actions = protocolServer.GetActions(true);
            foreach (var action in actions)
            {
                var tb = new TextBlock()
                {
                    Text = action.ActionName,
                };
                MenuItems.Add(new System.Windows.Controls.MenuItem()
                {
                    Header = tb,
                    Command = new RelayCommand((o) => { action.Run(); }),
                });
            }
        }

        public string ConnectionId
        {
            get
            {
                if (ProtocolServer.IsOnlyOneInstance())
                {
                    return ProtocolServer.BuildConnectionId();
                }
                else
                {
                    return ProtocolServer.BuildConnectionId() + "_" + this.GetHashCode();
                }
            }
        }

        /// <summary>
        /// for xaml binding
        /// </summary>
        public SettingsPageViewModel SettingsPage => IoC.Get<SettingsPageViewModel>();

        public bool CanFullScreen { get; protected set; }

        /// <summary>
        /// special menu for tab
        /// </summary>
        public List<MenuItem> MenuItems { get; set; } = new List<MenuItem>();

        /// <summary>
        /// since resizing when rdp is connecting would not tiger the rdp size change event
        /// then I let rdp host return false when rdp is on connecting to prevent TabWindow resize or maximize.
        /// </summary>
        /// <returns></returns>
        public virtual bool CanResizeNow()
        {
            return true;
        }

        /// <summary>
        /// in rdp, tab window cannot resize until rdp is connected. or rdp will not fit window size.
        /// </summary>
        public Action? OnCanResizeNowChanged { get; set; } = null;

        public virtual void ToggleAutoResize(bool isEnable)
        {
        }

        public abstract void Conn();

        public abstract void ReConn();

        /// <summary>
        /// disconnect the session and close host window
        /// </summary>
        public virtual void Close()
        {
            this.OnClosed?.Invoke(ConnectionId);
        }

        public virtual void GoFullScreen()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// call to focus the AxRdp or putty
        /// </summary>
        public virtual void FocusOnMe()
        {
            // do nothing
        }

        public abstract ProtocolHostType GetProtocolHostType();

        /// <summary>
        /// if it is a Integrate host, then return process's hwnd.
        /// </summary>
        /// <returns></returns>
        public abstract IntPtr GetHostHwnd();

        public Action<string>? OnClosed { get; set; } = null;
        public Action<string>? OnFullScreen2Window { get; set; } = null;
    }
}