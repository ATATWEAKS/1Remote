using _1RM.Model.Protocol;
using AxMSTSCLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Windows.Media.Media3D;
using Shawn.Utils;
using _1RM.Utils;
using System.Diagnostics;

namespace _1RM.View.Host.ProtocolHosts
{
    public partial class RdpHostForm : Form
    {
        private readonly AxMsRdpClient9NotSafeForScripting _rdpClient;
        private readonly RDP _rdpSettings;
        public RdpHostForm()
        {
            InitializeComponent();

            _rdpSettings = new RDP()
            {
                Address = "192.168.200.24",
                Port = "3389",
                UserName = "Administrator",
                Password = "bmebme2009.fo"
            };

            _rdpClient = new AxMsRdpClient9NotSafeForScripting();
            ((System.ComponentModel.ISupportInitialize)(_rdpClient)).BeginInit();
            // set fill to make rdp widow, so that we can enable RDP SmartSizing
            _rdpClient.Dock = DockStyle.Fill;
            _rdpClient.Enabled = true;
            _rdpClient.BackColor = Color.White;
            // set call back
            _rdpClient.OnRequestGoFullScreen += (sender, args) =>
            {
                MakeForm2FullScreen();
            };
            _rdpClient.OnRequestLeaveFullScreen += (sender, args) => { MakeForm2Normal(); };
            _rdpClient.OnRequestContainerMinimize += (sender, args) => { MakeForm2Minimize(); };
            _rdpClient.OnDisconnected += RdpcOnDisconnected;
            _rdpClient.OnConnected += RdpClientOnConnected;
            _rdpClient.AutoSize = true;   // make _rdpClient resize to content size
            ((System.ComponentModel.ISupportInitialize)(_rdpClient)).EndInit();
            this.Controls.Add(_rdpClient);
            //this.Controls.Add(new Button() { Text = "test", Width = 100, Height = 100 });
            this.Show();

            RdpInit();

            //_rdpClient.SetExtendedProperty("DesktopScaleFactor", this.GetDesktopScaleFactor()); this.SetExtendedProperty("DeviceScaleFactor", this.GetDeviceScaleFactor());


            _rdpClient.Connect();
        }

        private void RdpInit()
        {
            try
            {
                //Status = ProtocolHostStatus.Initializing;
                //RdpClientDispose();
                //CreateRdpClient();
                RdpInitServerInfo();
                //RdpInitStatic();
                //RdpInitConnBar();
                //RdpInitRedirect();
                //RdpInitDisplay(width, height, isReconnecting);
                //RdpInitPerformance();
                //RdpInitGateway();
                //Status = ProtocolHostStatus.Initialized;
            }
            catch (Exception e)
            {
                // TODO show error
                //GridMessageBox.Visibility = Visibility.Visible;
                //TbMessageTitle.Visibility = Visibility.Collapsed;
                //TbMessage.Text = e.Message;

                //Status = ProtocolHostStatus.NotInit;

                SimpleLogHelper.Error(e);
            }
        }

        /// <summary>
        /// init server connection info: user name\ psw \ port \ LoadBalanceInfo...
        /// </summary>
        private void RdpInitServerInfo()
        {
            #region server info
            Debug.Assert(_rdpClient != null); if (_rdpClient == null) return;
            // server connection info: user name\ psw \ port ...
            _rdpClient.Server = _rdpSettings.Address;
            _rdpClient.Domain = _rdpSettings.Domain;
            _rdpClient.UserName = _rdpSettings.UserName;
            _rdpClient.AdvancedSettings2.RDPPort = _rdpSettings.GetPort();

            if (string.IsNullOrWhiteSpace(_rdpSettings.LoadBalanceInfo) == false)
            {
                var loadBalanceInfo = _rdpSettings.LoadBalanceInfo;
                if (loadBalanceInfo.Length % 2 == 1)
                    loadBalanceInfo += " ";
                loadBalanceInfo += "\r\n";
                var bytes = Encoding.UTF8.GetBytes(loadBalanceInfo);
                _rdpClient.AdvancedSettings2.LoadBalanceInfo = Encoding.Unicode.GetString(bytes);
            }



            var secured = (MSTSCLib.IMsTscNonScriptable)_rdpClient.GetOcx();
            secured.ClearTextPassword = UnSafeStringEncipher.DecryptOrReturnOriginalString(_rdpSettings.Password);
            _rdpClient.FullScreenTitle = _rdpSettings.DisplayName + " - " + _rdpSettings.SubTitle;

            #endregion server info
        }


        private void RdpClientOnConnected(object? sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        private void RdpcOnDisconnected(object sender, IMsTscAxEvents_OnDisconnectedEvent e)
        {
            throw new NotImplementedException();
        }

        private void MakeForm2Minimize()
        {
            throw new NotImplementedException();
        }

        private void MakeForm2Normal()
        {
            throw new NotImplementedException();
        }

        private void MakeForm2FullScreen()
        {
            throw new NotImplementedException();
        }
    }
}
