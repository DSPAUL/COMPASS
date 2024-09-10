﻿using Autofac;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using COMPASS.Common.Interfaces;
using COMPASS.Common.Models.Enums;
using COMPASS.Common.Services;
using COMPASS.Common.ViewModels;
using COMPASS.Common.Views;
using COMPASS.Common.Views.Windows;

namespace COMPASS.Common;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // Line below is needed to remove Avalonia data validation.
        // Without this line you will get duplicate validations from both Avalonia and CT
        BindingPlugins.DataValidators.RemoveAt(0);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel()
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = new MainViewModel()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static IContainer? _container;

    public static IContainer Container
    {
        get
        {
            if (_container is null)
            {
                //init the container
                var builder = new ContainerBuilder();

                builder.RegisterType<WindowedNotificationService>().Keyed<INotificationService>(NotificationDisplayType.Windowed);
                builder.RegisterType<WindowedNotificationService>().Keyed<INotificationService>(NotificationDisplayType.Toast); //use windowed for everything for now

                _container = builder.Build();
            }
            return _container;
        }
        set => _container = value;
    }
}