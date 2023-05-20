﻿using COMPASS.Commands;
using COMPASS.Models;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows;

namespace COMPASS.ViewModels
{
    public class ProgressViewModel : ObservableObject
    {

        #region Singleton pattern
        private ProgressViewModel() { }

        private static ProgressViewModel _progressVM;
        public static ProgressViewModel GetInstance() => _progressVM ??= new ProgressViewModel();

        #endregion

        private ObservableCollection<LogEntry> _log = new();
        public ObservableCollection<LogEntry> Log
        {
            get => _log;
            set => SetProperty(ref _log, value);
        }


        private int _counter;
        public int Counter
        {
            get => _counter;
            private set
            {
                SetProperty(ref _counter, value);
                RaisePropertyChanged(nameof(Percentage));
                RaisePropertyChanged(nameof(FullText));
                RaisePropertyChanged(nameof(ImportInProgress));
            }
        }

        private int _totalAmount;
        public int TotalAmount
        {
            get => _totalAmount;
            set
            {
                SetProperty(ref _totalAmount, value);
                RaisePropertyChanged(nameof(Percentage));
                RaisePropertyChanged(nameof(FullText));
                RaisePropertyChanged(nameof(ImportInProgress));
            }
        }

        public float Percentage
        {
            get
            {
                if (TotalAmount == 0) return 100;
                return Counter * 100 / TotalAmount;
            }
        }

        private string _text;
        public string Text
        {
            get => _text;
            set
            {
                SetProperty(ref _text, value);
                RaisePropertyChanged(nameof(FullText));
            }
        }

        public string FullText => Cancelling ? $"Cancelling {Text}..." : $"{Text} [{Counter} / {TotalAmount}]";

        public bool ImportInProgress => TotalAmount > 0 && Counter < TotalAmount;

        private readonly Mutex _progressMutex = new();
        public void IncrementCounter()
        {
            _progressMutex.WaitOne();
            Counter++;
            _progressMutex.ReleaseMutex();
        }

        public void ResetCounter()
        {
            _progressMutex.WaitOne();
            Counter = 0;
            _progressMutex.ReleaseMutex();
        }

        public void AddLogEntry(LogEntry entry) =>
            Application.Current.Dispatcher.Invoke(() =>
            Log.Add(entry)
        );

        public bool Cancelling { get; set; } = false;
        public static CancellationTokenSource GlobalCancellationTokenSource { get; private set; } = new();
        public void ConfirmCancellation()
        {
            //Reset any progress
            Counter = 0;
            TotalAmount = 0;
            //create a new tokensource
            GlobalCancellationTokenSource = new();
            //force refresh the command so that it grabs the right cancel function
            _cancelTasksCommand = null;
            RaisePropertyChanged(nameof(CancelTasksCommand));
            Cancelling = false;
        }

        private ActionCommand _cancelTasksCommand;
        public ActionCommand CancelTasksCommand => _cancelTasksCommand ??= new(CancelBackgroundTask);
        public void CancelBackgroundTask()
        {
            GlobalCancellationTokenSource.Cancel();
            Cancelling = true;
            RaisePropertyChanged(nameof(FullText));
        }
    }
}
