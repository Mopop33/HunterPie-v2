﻿using System.Windows;
using HunterPie.Core.Domain.Dialog;
using System.ComponentModel;
using HunterPie.UI.Overlay;
using System;
using HunterPie.Core.Logger;
using HunterPie.Internal;
using HunterPie.UI.Overlay.Widgets.Damage.View;
using System.Windows.Input;
using HunterPie.UI.Overlay.Widgets.Monster.Views;
using HunterPie.Core.Client;
using HunterPie.UI.Overlay.Widgets.Monster.ViewModels;
using HunterPie.UI.Overlay.Widgets.Damage.ViewModel;
using HunterPie.Internal.Tray;
using System.Diagnostics;
using HunterPie.UI.Overlay.Widgets.Abnormality.View;
using HunterPie.UI.Overlay.Widgets.Abnormality.ViewModel;
using HunterPie.Core.Client.Configuration.Overlay;
using HunterPie.UI.Overlay.Widgets.Wirebug.Views;
using HunterPie.UI.Overlay.Widgets.Wirebug.ViewModel;
using System.Windows.Media.Animation;
using HunterPie.UI.Overlay.Widgets.Activities.View;
using HunterPie.UI.Overlay.Widgets.Activities.ViewModel;
using HunterPie.UI.Overlay.Widgets.Chat.Views;
using HunterPie.UI.Overlay.Widgets.Chat.ViewModels;
using HunterPie.Features.Debug;

namespace HunterPie
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        public MainWindow()
        {
            Log.Info("Initializing HunterPie GUI");

            Timeline.DesiredFrameRateProperty.OverrideMetadata(typeof(Timeline), new FrameworkPropertyMetadata { DefaultValue = 60 });
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!ClientConfig.Config.Client.EnableSeamlessShutdown)
            {
                NativeDialogResult result = DialogManager.Info(
                    "Confirmation", 
                    "Are you sure you want to exit HunterPie?", 
                    NativeDialogButtons.Accept | NativeDialogButtons.Cancel
                );

                if (result != NativeDialogResult.Accept)
                {
                    e.Cancel = true;
                    return;
                }
            }

            base.OnClosing(e);
        }

        private void OnInitialized(object sender, EventArgs e)
        {
            InitializerManager.InitializeGUI();
            
            InitializeDebugWidgets();
                       
            SetupTrayIcon();
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyboardDevice.IsKeyDown(Key.LeftCtrl) && e.KeyboardDevice.IsKeyDown(Key.R))
                App.Restart();
        }

        private void InitializeDebugWidgets() => DebugWidgets.MockIfNeeded();

        private void SetupTrayIcon()
        {
            TrayService.AddDoubleClickHandler(OnTrayShowClick);

            TrayService.AddItem("Show")
                .Click += OnTrayShowClick;

            TrayService.AddItem("Close")
                .Click += OnTrayCloseClick;
        }

        private void OnTrayShowClick(object sender, EventArgs e)
        {
            Show();
            WindowState = WindowState.Normal;
            Focus();
        }

        private void OnTrayCloseClick(object sender, EventArgs e) => Close();

        private void OnStartGameClick(object sender, EventArgs e) => Steam.RunGameBy(ClientConfig.Config.Client.DefaultGameType);
    }
}
