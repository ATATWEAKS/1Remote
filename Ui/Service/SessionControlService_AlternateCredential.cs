﻿using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Utils;
using Shawn.Utils;
using Stylet;
using _1RM.View.Utils;

namespace _1RM.Service
{
    public partial class SessionControlService
    {
        public static void CredentialTest()
        {
            var pingCredentials = new List<Credential>
            {
                new Credential()
                {
                    Name = "asfasdas",
                    Address = "127.0.1.1", Port = "5000",
                },
                //new Credential()
                //{
                //    Address = "127.0.0.1", Port = "5000",
                //},
                new Credential()
                {
                    Name = "xcv1",
                    Address = "192.168.100.1", Port = "3389",
                },
                //new Credential()
                //{
                //    Address = "172.20.65.31", Port = "3389",
                //},
                new Credential()
                {
                    Name = "98vs",
                    Address = "172.20.65.64", Port = "3389",
                },
                new Credential()
                {
                    Name = "ggg232",
                    Address = "172.20.65.65", Port = "3389",
                },
                new Credential()
                {
                    Name = "zxd11",
                    Address = "172.20.65.66", Port = "3389",
                },
            };
            Task.Factory.StartNew(async () =>
            {
                var c = await FindFirstConnectableAddressAsync(pingCredentials, "test");
                if (c != null)
                {
                    SimpleLogHelper.Debug($"Connected to {c.Address}:{c.Port}");
                }
            });
        }

        /// <summary>
        /// Find the first connectable address from the given credentials. if return null then no address is connectable.
        /// </summary>
        private static async Task<Credential?> FindFirstConnectableAddressAsync(IEnumerable<Credential> pingCredentials, string protocolDisplayName)
        {
            var credentials = pingCredentials.ToList();
            const int maxWaitSeconds = 10;
            var cts = new CancellationTokenSource();

            var uiPingItems = new List<PingTestItem>();
            foreach (var credential in credentials)
            {
                uiPingItems.Add(new PingTestItem(credential.Name, credential.Address)
                {
                    Status = PingStatus.None,
                });
            }

            var dlg = new AlternateAddressSwitchingViewModel(cts)
            {
                Title = protocolDisplayName + ": " + IoC.Get<LanguageService>().Translate("Availability detection"),
                PingTestItems = uiPingItems
            };

            await Execute.OnUIThreadAsync(() =>
            {
                IoC.Get<IWindowManager>().ShowWindow(dlg);
            });

            var tasks = new List<Task<bool?>>();
            // add tasks to ping each credential
            for (int i = 0; i < credentials.Count; i++)
            {
                // give each task a different sleep time to avoid all tasks start at the same time
                var credential = credentials[i];
                var pingTestItem = uiPingItems[i];
                tasks.Add(Task.Factory.StartNew(() =>
                {
                    pingTestItem.Status = PingStatus.Pinging;
                    var startTime = DateTime.UtcNow;
                    var ret = TcpHelper.TestConnectionAsync(credential.Address, credential.Port, cts.Token, maxWaitSeconds * 1000).Result;
                    pingTestItem.Ping = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
                    pingTestItem.Status = ret switch
                    {
                        null => PingStatus.Canceled,
                        true => PingStatus.Success,
                        _ => PingStatus.Failed
                    };
                    Task.Delay(200).Wait(); // 避免界面关闭太快，根本看不清
                    return ret;
                }, cts.Token));
            }

            // an extra task to update the message
            tasks.Add(Task.Factory.StartNew(() =>
            {
                for (int i = 0; i < maxWaitSeconds; i++)
                {
                    dlg.Eta = maxWaitSeconds - i;
                    Task.Delay(1000, cts.Token).Wait(cts.Token);
                }
                bool? ret = null;
                return ret;
            }, cts.Token));

            var delay = Task.Delay(500);

            // wait for the first task with result true or all tasks completed
            int completedTaskIndex = -1;
            var ts = tasks.ToArray();
            while (ts.Any())
            {
                var completedTask = await Task.WhenAny(ts);
                if (completedTask?.Result == true)
                {
                    completedTaskIndex = tasks.IndexOf(completedTask);
                    SimpleLogHelper.DebugInfo($"Task{completedTaskIndex} completed first. Cancelling remaining tasks.");
                    break;
                }
                ts = ts.Where(t => t != completedTask).ToArray();
            }

            // cancel all tasks
            dlg.Eta = 0;
            if (ts.Any())
            {
                try
                {
                    cts.Cancel();
                    Task.WaitAll(tasks.ToArray());
                }
                catch (Exception e)
                {
                    // ignored
                }
            }

            await delay;
            if (dlg.IsCanceled)
            {
                return null;
            }

            // return the first credential when ping success
            if (completedTaskIndex >= 0 && completedTaskIndex < tasks.Count)
            {
                // close the pop window
                await Execute.OnUIThreadAsync(() => { dlg.RequestClose(); });
                return credentials[completedTaskIndex].CloneMe();
            }
            else
            {
                // none of the address is connectable
                // show error message
                await Execute.OnUIThreadAsync(() =>
                {
                    dlg.Message = IoC.Get<LanguageService>().Translate("We are not able to connect to xxx", protocolDisplayName);
                });
            }
            return null;
        }


        /// <summary>
        /// if return null then no address is connectable.
        /// </summary>
        private static async Task<Credential?> GetCredential(ProtocolBaseWithAddressPort protocol, string assignCredentialName)
        {
            var newCredential = protocol.GetCredential();
            // use assign credential 应用指定的 credential
            var assignCredential = protocol.AlternateCredentials.FirstOrDefault(x => x.Name == assignCredentialName);
            if (assignCredential != null)
            {
                newCredential.SetCredential(assignCredential);
            }


            // check if need to ping before connect
            bool isPingBeforeConnect = protocol.IsPingBeforeConnect == true
                                       // do not ping if rdp protocol and gateway is used
                                       && protocol is not RDP { GatewayMode: EGatewayMode.DoNotUseGateway };
            var isAutoAlternateAddressSwitching = protocol.IsAutoAlternateAddressSwitching == true
                                                  // if any host or port in assignCredential，then disabled `AutoAlternateAddressSwitching`
                                                  && string.IsNullOrEmpty(assignCredential?.Address) && string.IsNullOrEmpty(assignCredential?.Port)
                                                  // if none of the alternate credential has host or port，then disabled `AutoAlternateAddressSwitching`
                                                  && protocol.AlternateCredentials.Any(x => !string.IsNullOrEmpty(x.Address) || !string.IsNullOrEmpty(x.Port));

            // if both `IsPingBeforeConnect` and `IsAutoAlternateAddressSwitching` are false, then return directly
            if (isPingBeforeConnect == false && isAutoAlternateAddressSwitching == false)
                return newCredential;


            // a quick test for the first credential, if pass return directly to avoid window pop
            var ret = await TcpHelper.TestConnectionAsync(newCredential.Address, newCredential.Port, null, 100);
            if (ret == true)
                return newCredential;

            var credentials = new List<Credential> { newCredential };
            if (isAutoAlternateAddressSwitching)
                credentials.AddRange(protocol.AlternateCredentials.Where(x => !string.IsNullOrEmpty(x.Address) || !string.IsNullOrEmpty(x.Port)));

            var connectableAddress = await FindFirstConnectableAddressAsync(credentials, protocol.DisplayName);
            if (connectableAddress != null)
            {
                newCredential.SetAddress(connectableAddress);
                newCredential.SetPort(connectableAddress);
                return newCredential;
            }
            return null;
        }
    }
}