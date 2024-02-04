using _1RM.Model.Protocol;
using _1RM.Service.Locality;
using _1RM.View.Host.ProtocolHosts;
using AxMSTSCLib;
using Shawn.Utils.Wpf;
using Stylet;
using System.Windows.Forms;

namespace _1RM.View.Host
{
    public partial class RdpFullScreenHostWindow : Form
    {
        private HostBase? _host = null;
        private AxMsRdpClient9NotSafeForScripting? _rdp = null;
        public string LastTabToken { get; set; } = "";

        public RdpFullScreenHostWindow()
        {
            InitializeComponent();
        }

        public static RdpFullScreenHostWindow Create(string token, HostBase host, TabWindowView? fromTab)
        {
            RdpFullScreenHostWindow? view = null;
            Execute.OnUIThreadSync(() =>
            {
                view = new RdpFullScreenHostWindow();
                view.LastTabToken = token;

                // full screen placement
                ScreenInfoEx? screenEx;
                if (fromTab != null)
                {
                    screenEx = ScreenInfoEx.GetCurrentScreen(fromTab);
                }
                else if (host.ProtocolServer is RDP { RdpFullScreenFlag: ERdpFullScreenFlag.EnableFullScreen } rdp
                         && LocalityConnectRecorder.RdpCacheGet(rdp.Id) is { } setting
                         && setting.FullScreenLastSessionScreenIndex >= 0
                         && setting.FullScreenLastSessionScreenIndex < System.Windows.Forms.Screen.AllScreens.Length)
                {
                    screenEx = ScreenInfoEx.GetCurrentScreen(setting.FullScreenLastSessionScreenIndex);
                }
                else
                {
                    screenEx = ScreenInfoEx.GetCurrentScreen(IoC.Get<MainWindowView>());
                }


                if (screenEx != null)
                {
                    view.Top = screenEx.VirtualWorkingAreaCenter.Y - view.Height / 2;
                    view.Left = screenEx.VirtualWorkingAreaCenter.X - view.Width / 2;
                }
            });
            return view!;
        }


        private void SetContent()
        {
            Execute.OnUIThread(() =>
            {
                if (_rdp == null || _host == null)
                    return;

                if (this.Controls.Contains(_rdp))
                    return;

                // check window is loaded
                if (this.IsHandleCreated == false)
                    return;

                // set title
                this.Text = _host.ProtocolServer.DisplayName + " - " + _host.ProtocolServer.SubTitle;

                this.Controls.Add(_rdp);
            });
        }

        public void ShowOrHide(HostBase? host)
        {
            Execute.OnUIThreadSync(() =>
            {
                _host = host;
                if (host == null)
                {
                    this.Hide();
                }
                else
                {
                    this.Show();
                }
            });
        }
    }
}
