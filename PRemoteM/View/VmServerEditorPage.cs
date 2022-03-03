﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using PRM.Controls;
using PRM.I;
using PRM.Model;
using PRM.Model.Protocol.Base;
using PRM.Model.Protocol.Extend;
using PRM.Model.Protocol.FileTransmit;
using PRM.Model.Protocol.Putty;
using PRM.Model.Protocol.RDP;
using PRM.Model.Protocol.VNC;
using PRM.View.ProtocolEditors;
using Shawn.Utils;
using Shawn.Utils.Wpf;

namespace PRM.View
{
    public class VmServerEditorPage : NotifyPropertyChangedBase
    {
        //private readonly PrmContext _context;
        private readonly GlobalData _globalData;
        private readonly IDataService _dataService;
        private readonly ILanguageService _languageService;

        public bool IsAddMode => _orgServers == null && Server.Id == 0;
        public bool IsBuckEdit => IsAddMode == false && _orgServers?.Count() > 1;

        #region single edit
        /// <summary>
        /// to remember original protocol's options, for restore use
        /// </summary>
        private readonly ProtocolServerBase _orgServer = null;
        public VmServerEditorPage(GlobalData globalData, IDataService dataService, ILanguageService languageService, ProtocolServerBase server, bool isDuplicate = false)
        {
            _globalData = globalData;
            _dataService = dataService;
            _languageService = languageService;
            Server = (ProtocolServerBase)server.Clone();
            if (isDuplicate)
            {
                Server.Id = 0; // set id = 0 and turn into edit mode
            }
            _orgServer = (ProtocolServerBase)Server.Clone();
            Title = "";
            Init();
        }
        #endregion

        #region Buck edit
        /// <summary>
        /// to remember original protocols' options, for restore use
        /// </summary>
        private readonly IEnumerable<ProtocolServerBase> _orgServers = null;
        /// <summary>
        /// the common parent class of _orgServers
        /// </summary>
        private readonly Type _orgServersCommonType = null;
        private readonly List<string> _commonTags;

        public VmServerEditorPage(GlobalData globalData, IDataService dataService, ILanguageService languageService, IEnumerable<ProtocolServerBase> servers)
        {
            _globalData = globalData;
            _dataService = dataService;
            _languageService = languageService;
            var serverBases = servers as ProtocolServerBase[] ?? servers.ToArray();
            // must be bulk edit
            Debug.Assert(serverBases.Count() > 1);
            // init title
            Title = _languageService.Translate("server_editor_bulk_editing_title") + " ";
            foreach (var serverBase in serverBases)
            {
                Title += serverBase.DisplayName;
                if (serverBases.Last() != serverBase)
                    Title += ", ";
            }


            Server = (ProtocolServerBase)serverBases.First().Clone();
            _orgServers = serverBases;


            // find the common base class
            var types = new List<Type>();
            foreach (var server in serverBases)
            {
                if (types.All(x => x != server.GetType()))
                    types.Add(server.GetType());
            }

            var type = types.First();
            for (int i = 1; i < types.Count; i++)
            {
                type = AssemblyHelper.FindCommonBaseClass(type, types[i]);
            }
            Debug.Assert(type.IsSubclassOf(typeof(ProtocolServerBase)));
            _orgServersCommonType = type;

            // copy the same value properties
            // set the different options to `Server_editor_different_options` or null
            var properties = _orgServersCommonType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var property in properties)
            {
                if (property.SetMethod?.IsPublic != true || property.SetMethod.IsAbstract != false) continue;
                var x = serverBases.Select(x => property.GetValue(x)).ToArray();
                if (x.Distinct().Count() <= 1) continue;
                if (property.PropertyType == typeof(string))
                    property.SetValue(Server, Server.Server_editor_different_options);
                else
                    property.SetValue(Server, null);
            }

            // tags
            _commonTags = new List<string>(); // remember the common tags
            bool isAllTagsSameFlag = true;
            for (var i = 0; i < serverBases.Length; i++)
            {
                foreach (var tagName in serverBases[i].Tags)
                {
                    if (serverBases.All(x => x.Tags.Contains(tagName)))
                    {
                        _commonTags.Add(tagName);
                    }
                    else
                    {
                        isAllTagsSameFlag = false;  
                    }
                }
            }
            Server.Tags = new List<string>(_commonTags.Count + 1);
            if (isAllTagsSameFlag == false)
                Server.Tags.Add(Server.Server_editor_different_options);
            Server.Tags.AddRange(_commonTags);

            _orgServer = Server.Clone();
            // init ui
            ReflectProtocolEditControl(_orgServersCommonType);

            Init();
        }

        #endregion


        private void Init()
        {
            ProtocolList.Clear();
            // init protocol list for single add / edit mode
            if (IsBuckEdit == false)
            {
                // reflect remote protocols
                ProtocolList = ProtocolServerBase.GetAllSubInstance();
                // set selected protocol
                try
                {
                    SelectedProtocol = ProtocolList.First(x => x.GetType() == Server.GetType());
                }
                catch (Exception)
                {
                    SelectedProtocol = ProtocolList.First();
                }
            }

            // decrypt pwd
            var s = Server;
            _dataService.DecryptToConnectLevel(ref s);
            NameSelections = _globalData.VmItemList.Select(x => x.Server.DisplayName).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
            TagSelections = _globalData.TagList.Select(x => x.Name).ToList();
        }

        public string Title { get; set; }


        private ProtocolServerBase _server = null;
        public ProtocolServerBase Server
        {
            get => _server;
            set => SetAndNotifyIfChanged(ref _server, value);
        }


        private ProtocolServerBase _selectedProtocol = null;
        public ProtocolServerBase SelectedProtocol
        {
            get => _selectedProtocol;
            set
            {
                if (IsBuckEdit)
                {
                    // bulk edit can not change protocol
                }
                if (value == _selectedProtocol) return;

                SetAndNotifyIfChanged(ref _selectedProtocol, value);
                if (_orgServer.GetType() == Server.GetType())
                    _orgServer.Update(Server);
                UpdateServerWhenProtocolChanged(SelectedProtocol.GetType());
                ReflectProtocolEditControl(SelectedProtocol.GetType());
            }
        }


        public List<ProtocolServerBase> ProtocolList { get; set; } = new List<ProtocolServerBase>();


        private ProtocolServerFormBase _protocolEditControl = null;
        public ProtocolServerFormBase ProtocolEditControl
        {
            get => _protocolEditControl;
            set => SetAndNotifyIfChanged(ref _protocolEditControl, value);
        }


        public List<string> NameSelections { get; set; }
        public List<string> TagSelections { get; set; }

        public TagsEditor TagsEditor { get; set; }


        private RelayCommand _cmdSave;
        public RelayCommand CmdSave
        {
            get
            {
                if (_cmdSave != null) return _cmdSave;
                _cmdSave = new RelayCommand((o) =>
                {
                    // bulk edit
                    if (IsBuckEdit == true)
                    {
                        // copy the same value properties
                        var properties = _orgServersCommonType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                        foreach (var property in properties)
                        {
                            if (property.SetMethod?.IsPublic == true
                                && property.SetMethod.IsAbstract == false
                                && property.Name != nameof(ProtocolServerBase.Id)
                                && property.Name != nameof(ProtocolServerBase.Tags))
                            {
                                var obj = property.GetValue(Server);
                                if (obj == null)
                                    continue;
                                else if (obj.ToString() == Server.Server_editor_different_options)
                                    continue;
                                else
                                    foreach (var server in _orgServers)
                                    {
                                        property.SetValue(server, obj);
                                    }
                            }
                        }


                        // merge tags
                        foreach (var server in _orgServers)
                        {
                            // process old tags, remove the not existed tags.
                            foreach (var tag in server.Tags.ToArray())
                            {
                                if (_commonTags.Contains(tag) == true)
                                {
                                    // remove tag if it is in common and not in Server.Tags
                                    if(Server.Tags.Contains(tag) == false)
                                        server.Tags.Remove(tag);
                                }
                                else
                                {
                                    // remove tag if it is in not common and Server_editor_different_options is not existed
                                    if(Server.Tags.Contains(Server.Server_editor_different_options) == false)
                                        server.Tags.Remove(tag);
                                }
                            }

                            // add new tags
                            foreach (var tag in Server.Tags.Where(tag => tag != Server.Server_editor_different_options))
                            {
                                server.Tags.Add(tag);
                            }

                            server.Tags = server.Tags.Distinct().ToList();
                        }

                        // save
                        _globalData.UpdateServer(_orgServers);
                    }
                    // edit
                    else if (Server.Id > 0)
                    {
                        _globalData.UpdateServer(Server);
                    }
                    // add
                    else
                    {
                        _globalData.AddServer(Server);
                    }
                    App.MainUi.Vm.AnimationPageEditor = null;
                }, o => (this.Server.DisplayName?.Trim() != "" && (_protocolEditControl?.CanSave() ?? false)));
                return _cmdSave;
            }
        }



        private RelayCommand _cmdCancel;
        public RelayCommand CmdCancel
        {
            get
            {
                if (_cmdCancel != null) return _cmdCancel;
                _cmdCancel = new RelayCommand((o) =>
                {
                    App.MainUi.Vm.AnimationPageEditor = null;
                });
                return _cmdCancel;
            }
        }




        private void UpdateServerWhenProtocolChanged(Type newProtocolType)
        {
            Debug.Assert(newProtocolType?.FullName != null);
            // change protocol
            var protocolServerBaseAssembly = typeof(ProtocolServerBase).Assembly;
            var server = (ProtocolServerBase)protocolServerBaseAssembly.CreateInstance(newProtocolType.FullName);
            // restore original server base info
            if (_orgServer.GetType() == server.GetType())
            {
                server.Update(_orgServer);
            }
            else if (_orgServer.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase)) && server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase)))
            {
                server.Update(_orgServer, typeof(ProtocolServerWithAddrPortUserPwdBase));
            }
            else if (_orgServer.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortBase)) && server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortBase)))
            {
                server.Update(_orgServer, typeof(ProtocolServerWithAddrPortBase));
            }
            else
            {
                server.Update(_orgServer, typeof(ProtocolServerBase));
            }


            // switch protocol and hold user name & pwd.
            if (server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase)) && Server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase)))
            {
                server.Update(Server, typeof(ProtocolServerWithAddrPortUserPwdBase));
            }
            else if (server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortBase)) && Server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortBase)))
            {
                server.Update(Server, typeof(ProtocolServerWithAddrPortBase));
            }
            // switch just hold base info
            else
            {
                server.Update(Server, typeof(ProtocolServerBase));
            }


            #region change default port and username
            if (server is ProtocolServerWithAddrPortBase newPort && Server is ProtocolServerWithAddrPortBase)
            {
                var oldPortDefault = (ProtocolServerWithAddrPortBase)protocolServerBaseAssembly.CreateInstance(Server.GetType().FullName);
                if (newPort.Port == oldPortDefault.Port)
                {
                    var newDefault = (ProtocolServerWithAddrPortBase)protocolServerBaseAssembly.CreateInstance(newProtocolType.FullName);
                    newPort.Port = newDefault.Port;
                }
            }
            if (server is ProtocolServerWithAddrPortUserPwdBase newUserName && Server is ProtocolServerWithAddrPortUserPwdBase)
            {
                var oldDefault = (ProtocolServerWithAddrPortUserPwdBase)protocolServerBaseAssembly.CreateInstance(Server.GetType().FullName);
                if (newUserName.UserName == oldDefault.UserName)
                {
                    var newDefault = (ProtocolServerWithAddrPortUserPwdBase)protocolServerBaseAssembly.CreateInstance(newProtocolType.FullName);
                    newUserName.UserName = newDefault.UserName;
                }
            }
            #endregion

            Server = server;
        }

        /// <summary>
        /// switch UI when Selected protocol changed
        /// keep the common field value between 2 protocols
        /// </summary>
        /// <param name="protocolType"></param>
        private void ReflectProtocolEditControl(Type protocolType)
        {
            Debug.Assert(protocolType?.FullName != null);

            try
            {
                ProtocolEditControl = null;
                if (protocolType == typeof(ProtocolServerRDP))
                {
                    ProtocolEditControl = new RdpForm(Server);
                }
                else if (protocolType == typeof(ProtocolServerRemoteApp))
                {
                    ProtocolEditControl = new RdpAppForm(Server);
                }
                else if (protocolType == typeof(ProtocolServerSSH))
                {
                    ProtocolEditControl = new SshForm(Server);
                }
                else if (protocolType == typeof(ProtocolServerTelnet))
                {
                    ProtocolEditControl = new TelnetForm(Server);
                }
                else if (protocolType == typeof(ProtocolServerFTP))
                {
                    ProtocolEditControl = new FTPForm(Server);
                }
                else if (protocolType == typeof(ProtocolServerSFTP))
                {
                    ProtocolEditControl = new SftpForm(Server);
                }
                else if (protocolType == typeof(ProtocolServerVNC))
                {
                    ProtocolEditControl = new VncForm(Server);
                }
                else if (protocolType == typeof(ProtocolServerWithAddrPortUserPwdBase))
                {
                    ProtocolEditControl = new BaseFormWithAddressPortUserPwd(Server);
                }
                else if (protocolType == typeof(ProtocolServerWithAddrPortBase))
                {
                    ProtocolEditControl = new BaseFormWithAddressPort(Server);
                }
                else if (protocolType == typeof(ProtocolServerApp))
                {
                    ProtocolEditControl = new AppForm(Server);
                }
                else
                    throw new NotImplementedException($"can not find from for '{protocolType.Name}' in {nameof(VmServerEditorPage)}");
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
                throw;
            }
        }
    }
}