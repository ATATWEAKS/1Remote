﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using PRM.Core.Base;
using PRM.Core.Protocol.RDP;
using PRM.ViewModel;

namespace PRM.View
{
    /// <summary>
    /// ServerEditorPage.xaml 的交互逻辑
    /// </summary>
    public partial class ServerEditorPage : UserControl
    {
        private readonly VmServerEditorPage _vmServerEditorPage;
        public ServerEditorPage(VmServerEditorPage vm)
        {
            Debug.Assert(vm?.Server != null);
            InitializeComponent();
            _vmServerEditorPage = vm;
            DataContext = vm;
            // edit mode
            if (vm.Server.Id > 0 && vm.Server.GetType() != typeof(NoneServer))
            {
                LogoSelector.SetImg(vm?.Server?.IconImg);
            }
            else
            // add mode
            {
                // TODO 研究子类之间的互相转换
                // TODO 随机选择LOGO
                vm.Server = new ServerRDP();
            }

            var serverType = _vmServerEditorPage.Server.GetType();
            if (serverType == typeof(ServerRDP))
            {
                ContentDetail.Content = new ServerRDPEditForm(_vmServerEditorPage.Server);
            }
            else
            {
                
            }
        }


        private void ButtonSave_OnClick(object sender, RoutedEventArgs e)
        {
            if (_vmServerEditorPage != null && _vmServerEditorPage.CmdSave.CanExecute(null))
                _vmServerEditorPage.CmdSave.Execute(null);
        }

        private void ImgLogo_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PopupLogoSelector.IsOpen = true;
        }

        private void ButtonLogoSave_OnClick(object sender, RoutedEventArgs e)
        {
            if (_vmServerEditorPage?.Server != null && _vmServerEditorPage.Server.GetType() != typeof(NoneServer))
            {
                _vmServerEditorPage.Server.IconImg = LogoSelector.Logo;
            }
            PopupLogoSelector.IsOpen = false;
        }

        private void ButtonLogoCancel_OnClick(object sender, RoutedEventArgs e)
        {
            PopupLogoSelector.IsOpen = false;
        }
    }
}
