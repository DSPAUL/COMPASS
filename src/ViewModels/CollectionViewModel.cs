﻿using COMPASS.Commands;
using COMPASS.Models;
using COMPASS.Properties;
using COMPASS.Services;
using COMPASS.Tools;
using COMPASS.ViewModels.Import;
using COMPASS.Windows;
using Ionic.Zip;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace COMPASS.ViewModels
{
    public class CollectionViewModel : ObservableObject
    {
        public CollectionViewModel(MainViewModel mainViewModel)
        {
            MainVM = mainViewModel;

            //Get all collections by folder name
            AllCodexCollections = new(Directory
                .GetDirectories(CodexCollection.CollectionsPath)
                .Select(Path.GetFileName)
                .Where(IsLegalCollectionName)
                .Select(dir => new CodexCollection(dir)));
        }

        #region Properties
        public MainViewModel MainVM { get; init; }

        private CodexCollection _currentCollection;
        public CodexCollection CurrentCollection
        {
            get => _currentCollection;
            set
            {
                if (value == null || value == _currentCollection)
                {
                    return;
                }
                //save prev collection before switching
                _currentCollection?.Save();
                SetProperty(ref _currentCollection, value);
            }
        }

        private ObservableCollection<CodexCollection> _allCodexCollections = new();
        public ObservableCollection<CodexCollection> AllCodexCollections
        {
            get => _allCodexCollections;
            init => SetProperty(ref _allCodexCollections, value);
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

        public bool IncludeFilesInExport { get; set; } = false;

        #endregion

        #region Methods and Commands
        public void LoadInitialCollection()
        {
            Directory.CreateDirectory(CodexCollection.CollectionsPath);

            while (CurrentCollection is null)
            {
                //in case of first boot, or if the existing ones couldn't load, create default collection
                if (AllCodexCollections.Count == 0)
                {
                    string name = "Default Collection";
                    CreateAndLoadCollection(name);
                }

                //in case startup collection no longer exists, pick first one that does exists
                else if (!AllCodexCollections.Any(collection => collection.DirectoryName == Settings.Default.StartupCollection))
                {
                    Logger.Warn($"The collection {Settings.Default.StartupCollection} could not be found.", new DirectoryNotFoundException());
                    var firstCollection = AllCodexCollections.First();
                    bool loaded = TryLoadCollection(firstCollection);
                    if (!loaded)
                    {
                        // if loading failed -> remove it from the pool and try again
                        AllCodexCollections.RemoveAt(0);
                    }
                }

                //otherwise, open startup collection
                else
                {
                    var startupCollection = AllCodexCollections.First(collection => collection.DirectoryName == Settings.Default.StartupCollection);
                    bool loaded = TryLoadCollection(startupCollection);
                    if (!loaded)
                    {
                        // if loading failed -> remove it from the pool and try again
                        AllCodexCollections.Remove(startupCollection);
                    }
                }
            }
        }

        public bool IsLegalCollectionName(string dirName)
        {
            bool legal =
                dirName.IndexOfAny(Path.GetInvalidPathChars()) < 0
                && dirName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
                && AllCodexCollections.All(collection => collection.DirectoryName != dirName)
                && !String.IsNullOrWhiteSpace(dirName)
                && dirName.Length < 100
                && (dirName.Length < 2 || dirName[..2] != "__"); //reserved for protected folders
            return legal;
        }

        /// <summary>
        /// Tries to load a collection and will set <see cref="CurrentCollection"/> if succesfull
        /// </summary>
        /// <param name="collection"></param>
        /// <returns>A boolean indiciating whether or not the load was a succes </returns>
        private bool TryLoadCollection(CodexCollection collection)
        {
            int success = collection.Load();
            if (success < 0)
            {
                string msg = success switch
                {
                    -1 => "The save file for the Tags seems to be corrupted and could not be read.",
                    -2 => "The save file with all items seems to be corrupted and could not be read.",
                    -3 => "Both the save file with tags and items seems to be corrupted and could not be read.",
                    _ => ""
                };
                _ = MessageBox.Show($"Could not load {collection.DirectoryName}. \n" + msg, "Failed to Load Collection", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            CurrentCollection = collection;
            return true;
        }

        public async Task AutoImport()
        {
            //Start Auto Imports
            ImportFolderViewModel folderImportVM = new(manuallyTriggered: false)
            {
                NonRecursiveDirectories = CurrentCollection.Info.AutoImportFolders.Flatten().Select(f => f.FullPath).ToList(),
            };
            await Task.Delay(TimeSpan.FromSeconds(2));
            await folderImportVM.Import();
        }

        public async Task Refresh()
        {
            CurrentCollection.Save();
            bool loaded = TryLoadCollection(CurrentCollection);

            if (!loaded)
            {
                //something must have gone wrong during save right before
                //highly unlikely, look at this later
                //TODO
                return;
            }

            MainVM?.CurrentLayout?.UpdateDoVirtualization();

            FilterVM = new(CurrentCollection.AllCodices);
            TagsVM = new(CurrentCollection, FilterVM);

            FilterVM.ReFilter(true);

            await AutoImport();
        }

        private ActionCommand _toggleCreateCollectionCommand;
        public ActionCommand ToggleCreateCollectionCommand => _toggleCreateCollectionCommand ??= new(ToggleCreateCollection);
        private void ToggleCreateCollection() => CreateCollectionVisibility = !CreateCollectionVisibility;

        private ActionCommand _toggleEditCollectionCommand;
        public ActionCommand ToggleEditCollectionCommand => _toggleEditCollectionCommand ??= new(ToggleEditCollection);
        private void ToggleEditCollection() => EditCollectionVisibility = !EditCollectionVisibility;

        // Create CodexCollection
        private ReturningRelayCommand<string, CodexCollection> _createCollectionCommand;
        public ReturningRelayCommand<string, CodexCollection> CreateCollectionCommand =>
            _createCollectionCommand ??= new(CreateAndLoadCollection, IsLegalCollectionName);
        public CodexCollection CreateAndLoadCollection(string dirName)
        {
            var newCollection = CreateCollection(dirName);

            bool success = TryLoadCollection(newCollection);

            if (success)
            {
                CurrentCollection = newCollection;
                CreateCollectionVisibility = false;
                return newCollection;
            }
            return null;
        }

        public CodexCollection CreateCollection(string dirName)
        {
            if (string.IsNullOrEmpty(dirName)) return null;
            CodexCollection newCollection = new(dirName);

            newCollection.CreateDirectories();

            AllCodexCollections.Add(newCollection);
            return newCollection;
        }

        // Rename Collection
        private RelayCommand<string> _editCollectionNameCommand;
        public RelayCommand<string> EditCollectionNameCommand => _editCollectionNameCommand ??= new(EditCollectionName, IsLegalCollectionName);
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

                const string messageSingle = "There is still one item in this collection, if you don't want to remove it from COMPASS, move it to another collection first. Are you sure you want to continue?";
                string messageMultiple = $"There are still {CurrentCollection.AllCodices.Count} items in this collection, if you don't want to remove these from COMPASS, move them to another collection first. Are you sure you want to continue?";

                string sMessageBoxText = CurrentCollection.AllCodices.Count == 1 ? messageSingle : messageMultiple;

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
            AllCodexCollections.Remove(toDelete);
            if (CurrentCollection == toDelete)
            {
                CurrentCollection = AllCodexCollections.FirstOrDefault();
            }

            //if Dir name of toDelete is empty, it will delete the entire collections folder
            if (String.IsNullOrEmpty(toDelete.DirectoryName)) return;
            if (Directory.Exists(toDelete.FullDataPath)) //does not exist if collection was never saved
            {
                Directory.Delete(toDelete.FullDataPath, true);
            }
        }

        //Export Collection
        private ActionCommand _exportCommand;
        public ActionCommand ExportCommand => _exportCommand ??= new(Export);
        public void Export()
        {
            //open wizard
            ExportCollectionViewModel exportCollectionVM = new(CurrentCollection);
            ExportCollectionWizard wizard = new(exportCollectionVM);
            wizard.Show();
        }

        private ActionCommand _exportTagsCommand;
        public ActionCommand ExportTagsCommand => _exportTagsCommand ??= new(ExportTags);
        public void ExportTags()
        {
            SaveFileDialog saveFileDialog = new()
            {
                Filter = $"COMPASS File (*{Constants.COMPASSFileExtension})|*{Constants.COMPASSFileExtension}",
                FileName = $"{CurrentCollection.DirectoryName}_Tags",
                DefaultExt = Constants.COMPASSFileExtension
            };

            if (saveFileDialog.ShowDialog() != true) return;

            //make sure to save first
            CurrentCollection.SaveTags();

            string targetPath = saveFileDialog.FileName;
            using ZipFile zip = new();
            zip.AddFile(CurrentCollection.TagsDataFilePath, "");

            //Export
            zip.Save(targetPath);
            Logger.Info($"Exported Tags from {CurrentCollection.DirectoryName} to {targetPath}");
        }

        //Import Collection
        private ActionCommand _importCommand;
        public ActionCommand ImportCommand => _importCommand ??= new(async () => await ImportCMPSSFileAsync());


        public async Task ImportCMPSSFileAsync(string path = null)
        {
            var collectionToImport = await IOService.OpenCPMSSFile(path);

            if (collectionToImport == null)
            {
                Logger.Warn("Failed to open file");
                return;
            }

            //open wizard
            ImportCollectionViewModel importCollectionVM = new(collectionToImport);
            ImportCollectionWizard wizard = new(importCollectionVM);
            wizard.Show();
        }

        //Merge Collection into another
        private RelayCommand<string> _mergeCollectionIntoCommand;
        public RelayCommand<string> MergeCollectionIntoCommand => _mergeCollectionIntoCommand ??= new(async s => await MergeIntoCollection(s));
        public async Task MergeIntoCollection(string collectionToMergeInto)
        {
            //Show some kind of are you sure?
            string message = $"You are about to merge '{CurrentCollection.DirectoryName}' into '{collectionToMergeInto}'. \n" +
                           $"This will copy all items, tags and preferences to the chosen collection. \n" +
                           $"Are you sure you want to continue?";
            var result = MessageBox.Show(message, "Confirm merge", MessageBoxButton.OKCancel);
            if (result != MessageBoxResult.OK) return;

            CodexCollection targetCollection = new(collectionToMergeInto);

            targetCollection.Load(MakeStartupCollection: false);
            await targetCollection.MergeWith(CurrentCollection);

            message = $"Successfully merged '{CurrentCollection.DirectoryName}' into '{collectionToMergeInto}'";
            MessageBox.Show(message, "Merge Success");
        }
        #endregion
    }
}
