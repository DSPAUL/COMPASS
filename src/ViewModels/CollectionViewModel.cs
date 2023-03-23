﻿using COMPASS.Commands;
using COMPASS.Models;
using COMPASS.Tools;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;

namespace COMPASS.ViewModels
{
    public class CollectionViewModel : ObservableObject
    {
        public CollectionViewModel()
        {
            LoadInitialCollection();
        }

        #region Properties

        private CodexCollection _currentCollection;
        public CodexCollection CurrentCollection
        {
            get => _currentCollection;
            set
            {
                if (_currentCollection != null)
                {
                    _currentCollection.SaveCodices();
                    _currentCollection.SaveTags();
                }

                if (value != null)
                {
                    int success = value.Load();
                    if (success < 0)
                    {
                        string msg = success switch
                        {
                            -1 => "The Tag data seems to be corrupted and could not be read.",
                            -2 => "The Codex data seems to be corrupted and could not be read.",
                            -3 => "Both the Tag and Codex data seems to be corrupted and could not be read.",
                            _ => ""
                        };
                        _ = MessageBox.Show($"Could not load {value.DirectoryName}. \n" + msg, "Fail to Load Collection", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    SetProperty(ref _currentCollection, value);
                    FilterVM = new(value.AllCodices);
                    TagsVM = new(this);
                }
            }
        }

        private ObservableCollection<CodexCollection> _allCodexCollections;
        public ObservableCollection<CodexCollection> AllCodexCollections
        {
            get => _allCodexCollections;
            set => SetProperty(ref _allCodexCollections, value);
        }

        //Needed for binding to context menu "Move to Collection"
        public ObservableCollection<string> CollectionDirectories => new(AllCodexCollections.Select(collection => collection.DirectoryName));

        private FilterViewModel _filterVM;
        public FilterViewModel FilterVM
        {
            get => _filterVM;
            private set => SetProperty(ref _filterVM, value);
        }

        private TagsViewModel _tagsVM;
        public TagsViewModel TagsVM
        {
            get => _tagsVM;
            set => SetProperty(ref _tagsVM, value);
        }

        //show edit Collection Stuff
        private bool _createCollectionVisibility = false;
        public bool CreateCollectionVisibility
        {
            get => _createCollectionVisibility;
            set => SetProperty(ref _createCollectionVisibility, value);
        }

        //show edit Collection Stuff
        private bool _editCollectionVisibility = false;
        public bool EditCollectionVisibility
        {
            get => _editCollectionVisibility;
            set => SetProperty(ref _editCollectionVisibility, value);
        }

        #endregion

        #region Methods and Commands
        private void LoadInitialCollection()
        {
            Directory.CreateDirectory(CodexCollection.CollectionsPath);

            //Get all collections by folder name
            AllCodexCollections = new(Directory
                .GetDirectories(CodexCollection.CollectionsPath)
                .Select(Path.GetFileName)
                .Select(dir => new CodexCollection(dir)));

            while (CurrentCollection is null)
            {
                //in case of first boot or all saves are corrupted, create default collection
                if (AllCodexCollections.Count == 0)
                {
                    // if default collection already exists but is corrupted
                    // keep generating new collection names until a new one is found
                    bool created = false;
                    int i = 0;
                    string name = "Default Collection";
                    while (!created && i < 10) //only try 10 times as safefail if there is a bug to prevent infinite loop
                    {
                        if (!Path.Exists(Path.Combine(CodexCollection.CollectionsPath, name)))
                        {
                            CreateCollection(name);
                            created = true;
                            return; //not needed but extra safety because while loops are dangerous
                        }
                        else
                        {
                            name = $"Default Collection {i}";
                            i++;
                        }
                    }
                }

                //in case startup collection no longer exists, pick first one that does exists
                else if (!AllCodexCollections.Any(collection => collection.DirectoryName == Properties.Settings.Default.StartupCollection))
                {
                    Logger.Warn($"The collection {Properties.Settings.Default.StartupCollection} could not be found.", new DirectoryNotFoundException());
                    CurrentCollection = AllCodexCollections.First();
                    if (CurrentCollection is null)
                    {
                        // if it is null -> loading failed -> remove it from the pool and try again
                        AllCodexCollections.RemoveAt(0);
                    }
                }

                //otherwise, open startup collection
                else
                {
                    CurrentCollection = AllCodexCollections.First(collection => collection.DirectoryName == Properties.Settings.Default.StartupCollection);
                    if (CurrentCollection is null)
                    {
                        // if it is null -> loading failed -> remove it from the pool and try again
                        AllCodexCollections.Remove(AllCodexCollections.First(collection => collection.DirectoryName == Properties.Settings.Default.StartupCollection));
                    }
                }
            }
        }

        private bool isLegalCollectionName(string dirName)
        {
            bool legal =
                dirName.IndexOfAny(Path.GetInvalidPathChars()) < 0
                && dirName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
                && !AllCodexCollections.Any(collection => collection.DirectoryName == dirName)
                && !String.IsNullOrWhiteSpace(dirName)
                && dirName.Length < 100;
            return legal;
        }

        public void Refresh() => CurrentCollection = CurrentCollection;

        private ActionCommand _toggleCreateCollectionCommand;
        public ActionCommand ToggleCreateCollectionCommand => _toggleCreateCollectionCommand ??= new(ToggleCreateCollection);
        private void ToggleCreateCollection() => CreateCollectionVisibility = !CreateCollectionVisibility;

        private ActionCommand _toggleEditCollectionCommand;
        public ActionCommand ToggleEditCollectionCommand => _toggleEditCollectionCommand ??= new(ToggleEditCollection);
        private void ToggleEditCollection() => EditCollectionVisibility = !EditCollectionVisibility;

        // Create CodexCollection
        private RelayCommand<string> _createCollectionCommand;
        public RelayCommand<string> CreateCollectionCommand => _createCollectionCommand ??= new(CreateCollection, isLegalCollectionName);
        public void CreateCollection(string dirName)
        {
            if (string.IsNullOrEmpty(dirName)) return;

            CodexCollection newCollection = new(dirName);
            Directory.CreateDirectory(CodexCollection.CollectionsPath + dirName + @"\CoverArt");
            Directory.CreateDirectory(CodexCollection.CollectionsPath + dirName + @"\Thumbnails");
            AllCodexCollections.Add(newCollection);
            CurrentCollection = newCollection;

            CreateCollectionVisibility = false;
        }

        // Rename Collection
        private RelayCommand<string> _editCollectionNameCommand;
        public RelayCommand<string> EditCollectionNameCommand => _editCollectionNameCommand ??= new(EditCollectionName, isLegalCollectionName);
        public void EditCollectionName(string newName)
        {
            CurrentCollection.RenameCollection(newName);
            EditCollectionVisibility = false;
        }

        // Delete Collection
        private ActionCommand _deleteCollectionCommand;
        public ActionCommand DeleteCollectionCommand => _deleteCollectionCommand ??= new(RaiseDeleteCollectionWarning);
        public void RaiseDeleteCollectionWarning()
        {
            if (CurrentCollection.AllCodices.Count > 0)
            {
                //MessageBox "Are you Sure?"
                string sCaption = "Are you Sure?";

                string MessageSingle = "There is still one file in this collection, if you don't want to remove these from COMPASS, move them to another collection first. Are you sure you want to continue?";
                string MessageMultiple = $"There are still {CurrentCollection.AllCodices.Count} files in this collection, if you don't want to remove these from COMPASS, move them to another collection first. Are you sure you want to continue?";

                string sMessageBoxText = CurrentCollection.AllCodices.Count == 1 ? MessageSingle : MessageMultiple;

                MessageBoxButton btnMessageBox = MessageBoxButton.YesNo;
                MessageBoxImage imgMessageBox = MessageBoxImage.Warning;

                MessageBoxResult rsltMessageBox = MessageBox.Show(sMessageBoxText, sCaption, btnMessageBox, imgMessageBox);

                if (rsltMessageBox == MessageBoxResult.Yes)
                {
                    DeleteCollection(CurrentCollection);
                }
            }
            else
            {
                DeleteCollection(CurrentCollection);
            }
        }
        public void DeleteCollection(CodexCollection toDelete)
        {
            AllCodexCollections.Remove(CurrentCollection);
            CurrentCollection = AllCodexCollections.FirstOrDefault();

            //if Dir name of toDelete is empty, it will delete the entire collections folder
            if (String.IsNullOrEmpty(toDelete.DirectoryName)) return;
            Directory.Delete(CodexCollection.CollectionsPath + toDelete.DirectoryName, true);
        }
        #endregion
    }
}
