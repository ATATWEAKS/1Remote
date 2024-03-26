using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Model.ProtocolRunner;
using _1RM.Model.ProtocolRunner.Default;
using _1RM.Utils;
using _1RM.View;
using _1RM.View.Host;
using _1RM.View.Host.ProtocolHosts;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Stylet;
using ProtocolHostStatus = _1RM.View.Host.ProtocolHosts.ProtocolHostStatus;
using _1RM.Service.DataSource;
using System.Collections.Generic;
using System.Windows.Forms;
using _1RM.Service.Locality;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace _1RM.Service
{
    public partial class SessionControlService
    {
        public void AddTab(TabWindowView tab)
        {
            lock (_dictLock)
            {
                var token = tab.Token;
                Debug.Assert(!_token2TabWindows.ContainsKey(token));
                Debug.Assert(!string.IsNullOrEmpty(token));
                _token2TabWindows.TryAdd(token, tab);
                tab.Activated += (sender, args) =>
                    _lastTabToken = tab.Token;
            }
        }


        public void MoveSessionToFullScreen(string connectionId)
        {
            HostBaseWinform? host = null;
            if (_connectionId2WinFormHosts.ContainsKey(connectionId))
            {
                host = _connectionId2WinFormHosts[connectionId];
            }
            else if (_connectionId2TabHosts.ContainsKey(connectionId)
                     && _connectionId2TabHosts[connectionId] is IntegrateHostForWinFrom { Form: HostBaseWinform hbw })
            {
                _connectionId2WinFormHosts.TryAdd(connectionId, hbw);
                host = hbw;
            }
            else
                throw new NullReferenceException($"can not find host by connectionId = `{connectionId}`");

            host.DetachFromHostBase();
            host.GoFullScreen();

            // remove from tab
            var tab = GetTabByConnectionId(connectionId);
            if (tab != null)
            {
                host.LastTabToken = tab.Token;
                // if tab is not loaded, do not allow move to full-screen, 防止 loaded 事件中的逻辑覆盖
                if (tab.IsLoaded == false)
                    return;

                tab.GetViewModel().TryRemoveItem(connectionId);
                SimpleLogHelper.Debug($@"MoveSessionToFullScreen: remove connectionId = {connectionId} from tab({tab.GetHashCode()}) ");
            }
            _connectionId2TabHosts.TryRemove(connectionId, out _);



            this.CleanupProtocolsAndWindows();
            PrintCacheCount();
        }



        public void MoveSessionToTabWindow(string connectionId)
        {
            Debug.Assert(_connectionId2WinFormHosts.ContainsKey(connectionId) == true);
            var host = _connectionId2WinFormHosts[connectionId];

            SimpleLogHelper.Debug($@"MoveSessionToTabWindow: Moving host({host.GetHashCode()}) to any tab");
            // get tab
            TabWindowView tab = GetOrCreateTabWindow(host.LastTabToken);

            var h = host.AttachToHostBase();
            //tab.GetViewModel().AddItem(new TabItemViewModel(h, host.ProtocolServer.DisplayName));
            host.FormBorderStyle = FormBorderStyle.Sizable;
            host.ShowInTaskbar = true;
            host.Width = 800;
            host.Height = 600;

            tab.Activate();
            SimpleLogHelper.Debug($@"MoveSessionToTabWindow: Moved host({host.GetHashCode()}) to tab({tab.GetHashCode()})");
            PrintCacheCount();
        }


        /// <summary>
        /// get a tab for server,
        /// if assignTabToken == null, create a new tab
        /// if assignTabToken != null, find _token2tabWindows[assignTabToken], if _token2tabWindows[assignTabToken] is null, then create a new tab
        /// </summary>
        /// <param name="assignTabToken"></param>
        /// <returns></returns>
        private TabWindowView GetOrCreateTabWindow(string assignTabToken = "")
        {
            TabWindowView? ret = null;
            lock (_dictLock)
            {
                // find existed
                if (_token2TabWindows.ContainsKey(assignTabToken))
                {
                    ret = _token2TabWindows[assignTabToken];
                }
                else if (string.IsNullOrEmpty(assignTabToken))
                {
                    if (_token2TabWindows.ContainsKey(_lastTabToken))
                    {
                        ret = _token2TabWindows[_lastTabToken];
                    }
                    else if (_token2TabWindows.IsEmpty == false)
                    {
                        ret = _token2TabWindows.Last().Value;
                    }
                }

                // create new
                if (ret == null)
                {
                    Execute.OnUIThreadSync(() =>
                    {
                        ret = new TabWindowView();
                        AddTab(ret);
                        ret.Show();
                        _lastTabToken = ret.Token;

                        int loopCount = 0;
                        while (ret.IsLoaded == false)
                        {
                            ++loopCount;
                            Thread.Sleep(100);
                            if (loopCount > 50)
                                break;
                        }
                    });
                }
                Debug.Assert(ret != null);
                return ret!;
            }
        }

        public TabWindowView? GetTabByConnectionId(string connectionId)
        {
            lock (_dictLock)
                return _token2TabWindows.Values.FirstOrDefault(x => x.GetViewModel().Items.Any(y => y.Content.ConnectionId == connectionId));
        }
    }
}