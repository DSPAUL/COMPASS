﻿using COMPASS.Models;
using COMPASS.Tools;
using COMPASS.ViewModels.Commands;
using GongSolutions.Wpf.DragDrop;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml;

namespace COMPASS.ViewModels
{
    public class SettingsViewModel : BaseViewModel
    {
        public SettingsViewModel()
        {
            //find name of current release-notes
            var version = Assembly.GetEntryAssembly().GetName().Version.ToString()[..5];
            if (File.Exists($"release-notes-{version}.md"))
            {
                ReleaseNotes = File.ReadAllText($"release-notes-{version}.md");
            }

            if (File.Exists(Constants.PreferencesFilePath)) LoadPreferences();
            else Logger.log.Warn($"{Constants.PreferencesFilePath} does not exist.");
        }

        #region Load and Save Settings
        public static XmlWriterSettings XmlWriteSettings { get; private set; } = new() { Indent = true };
        private static SerializablePreferences AllPreferences = new();
        public void SavePreferences()
        {
            //Save OpenFilePriority
            AllPreferences.OpenFilePriorityIDs = OpenFilePriority.Select(pf => pf.ID).ToList();

            using var writer = XmlWriter.Create(Constants.PreferencesFilePath, XmlWriteSettings);
            System.Xml.Serialization.XmlSerializer serializer = new(typeof(SerializablePreferences));
            serializer.Serialize(writer, AllPreferences);
        }

        public void LoadPreferences()
        {
            using (var Reader = new StreamReader(Constants.PreferencesFilePath))
            {
                System.Xml.Serialization.XmlSerializer serializer = new(typeof(SerializablePreferences));
                AllPreferences = serializer.Deserialize(Reader) as SerializablePreferences;
                Reader.Close();
            }
            //put openFilePriority in right order
            OpenFilePriority = new ObservableCollection<PreferableFunction<Codex>>(OpenFileFunctions.OrderBy(pf => AllPreferences.OpenFilePriorityIDs.IndexOf(pf.ID)));
        }
        #endregion

        #region General Tab

        #region File Source Preference
        //list with possible functions to open a file
        private readonly List<PreferableFunction<Codex>> OpenFileFunctions = new()
            {
                new PreferableFunction<Codex>("Local File", FileBaseViewModel.OpenFileLocally,0),
                new PreferableFunction<Codex>("Web Version", FileBaseViewModel.OpenFileOnline,1)
            };
        //same ordered version of the list
        private ObservableCollection<PreferableFunction<Codex>> _openFilePriority;
        public ObservableCollection<PreferableFunction<Codex>> OpenFilePriority
        {
            get { return _openFilePriority; }
            set { SetProperty(ref _openFilePriority, value); }
        }
        #endregion

        #region Fix Renamed Folder
        private int _amountRenamed = 0;
        public int AmountRenamed
        {
            get { return _amountRenamed; }
            set 
            { 
                SetProperty(ref _amountRenamed, value);
                RaisePropertyChanged(nameof(RenameCompleteMessage));
            }
        }

        public string RenameCompleteMessage
        {
            get { return $"Renamed Path Reference in {AmountRenamed} Codices"; }
        }

        private RelayCommand<object[]> _renameFolderRefCommand;
        public RelayCommand<object[]> RenameFolderRefCommand => _renameFolderRefCommand ??= new(RenameFolderReferences);

        private void RenameFolderReferences(object[] args)
        {
            RenameFolderReferences((string)args[0],(string)args[1]);
        }
        private void RenameFolderReferences(string oldpath, string newpath)
        {
            AmountRenamed = 0;
            foreach(Codex c in MVM.CurrentCollection.AllFiles)
            {
                if (c.Path != null)
                {
                    if (c.Path.Contains(oldpath)){
                        AmountRenamed++;
                        c.Path = c.Path.Replace(oldpath, newpath);
                    }
                }
                
            }
        }
        #endregion

        public void RegenAllThumbnails() { 
            foreach(Codex codex in MVM.CurrentCollection.AllFiles)
            {
                //codex.Thumbnail = codex.CoverArt.Replace("CoverArt", "Thumbnails");
                CoverFetcher.CreateThumbnail(codex);
            }
        }
        #endregion

        #region What's New Tab
        private string _releaseNotes;
        public string ReleaseNotes
        {
            get { return _releaseNotes; }
            set { SetProperty(ref _releaseNotes, value); }
        }
        #endregion
    }
}
