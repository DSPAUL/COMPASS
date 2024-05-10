﻿using Autofac;
using Avalonia.Threading;
using COMPASS.Common.Interfaces;
using COMPASS.Common.Models;
using COMPASS.Common.Models.Enums;
using COMPASS.Common.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.ExceptionServices;

namespace COMPASS.Common.Tools
{
    public static class Logger
    {
        public static void Init()
        {
            log4net.GlobalContext.Properties["CompassDataPath"] = EnvironmentVarsService.CompassDataPath;
            log4net.Config.XmlConfigurator.Configure(new FileInfo("log4net.config"));
            FileLog = log4net.LogManager.GetLogger(nameof(Logger));
            //if (Application.Current is not null)
            //{
            //    Application.Current.DispatcherUnhandledException += LogUnhandledException;
            //}
            Info($"Launching Compass v{Reflection.Version}");
        }

        // Log To file
        public static log4net.ILog? FileLog { get; private set; }

        // Log to Log Tab
        public static ObservableCollection<LogEntry> ActivityLog { get; } = new ObservableCollection<LogEntry>();

        public static void Info(string message) =>
            Dispatcher.UIThread.Invoke(()
                => ActivityLog.Add(new(Severity.Info, message)));

        public static void Debug(string message) => FileLog?.Debug(message);


        public static void Warn(string message, Exception? ex = null)
        {
            Dispatcher.UIThread.Invoke(() => ActivityLog.Add(new(Severity.Warning, message)));
            if (ex is null)
                if (ex is null)
                {
                    FileLog?.Warn(message);
                }
                else
                {
                    FileLog?.Warn(message, ex);
                }
        }

        public static void Error(string message, Exception ex)
        {
            Dispatcher.UIThread.Invoke(() => ActivityLog.Add(new(Severity.Error, message)));
            FileLog?.Error(message, ex);
            FileLog?.Error(message, ex);
        }

        public static void LogUnhandledException(object sender, Exception e)
        {
            FileLog?.Fatal(e.ToString(), e);

            //prompt user to submit logs and open an issue
            string message = $"Its seems COMPASS has run into a critical error ({e.Message}).\n" +
                $"You can help improve COMPASS by opening an issue on {Constants.RepoURL} with the error message. \n" +
                $"Please include the log file located at {EnvironmentVarsService.CompassDataPath}\\logs.";
            Notification crashNotification = new($"COMPASS ran into a critical error.", message, Severity.Error);
            App.Container.ResolveKeyed<INotificationService>(NotificationDisplayType.Windowed).Show(crashNotification);

            Environment.Exit(1);
        }

        public static void LogUnhandledException(object? sender, FirstChanceExceptionEventArgs e)
            => FileLog?.Fatal(e.Exception.ToString(), e.Exception);
    }
}
