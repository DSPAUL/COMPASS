﻿using COMPASS.Tools;
using COMPASS.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Xml;

namespace COMPASS.Models
{
    public class CodexCollection : ObservableObject
    {
        public CodexCollection(string collectionDirectory)
        {
            DirectoryName = collectionDirectory;
        }

        public static string CollectionsPath => Path.Combine(SettingsViewModel.CompassDataPath, "Collections");
        public string FullDataPath => Path.Combine(CollectionsPath, DirectoryName);
        public string CodicesDataFilePath => Path.Combine(FullDataPath, "CodexInfo.xml");
        public string TagsDataFilePath => Path.Combine(FullDataPath, "Tags.xml");
        public string CollectionInfoFilePath => Path.Combine(FullDataPath, "CollectionInfo.xml");

        #region Properties
        private string _directoryName;
        public string DirectoryName
        {
            get => _directoryName;
            set => SetProperty(ref _directoryName, value);
        }

        public List<Tag> AllTags { get; private set; } = new();
        public List<Tag> RootTags { get; set; }
        public ObservableCollection<Codex> AllCodices { get; private set; } = new();

        public CollectionInfo Info { get; private set; } = new();

        #endregion

        #region Load Data From File

        /// <summary>
        /// Loads the collection and unless hidden, sets it as the new default to load on startup
        /// </summary>
        /// <returns>int that gives status: 0 for success, -1 for failed tags, -2 for failed codices, -4 for failed info, or combination of those</returns>
        public int Load(bool hidden = false)
        {
            int result = 0;
            bool loadedTags = LoadTags();
            bool loadedCodices = LoadCodices();
            bool loadedInfo = LoadInfo();
            if (!loadedTags) { result -= 1; }
            if (!loadedCodices) { result -= 2; }
            if (!loadedInfo) { result -= 4; }
            if (!hidden)
            {
                Properties.Settings.Default.StartupCollection = DirectoryName;
                Logger.Info($"Loaded {DirectoryName}");
            }
            return result;
        }

        //Loads the RootTags from a file and constructs the AllTags list from it
        public bool LoadTags()
        {
            if (File.Exists(TagsDataFilePath))
            {
                //loading root tags          
                using (var reader = new StreamReader(TagsDataFilePath))
                {
                    System.Xml.Serialization.XmlSerializer serializer = new(typeof(List<Tag>));
                    try
                    {
                        RootTags = serializer.Deserialize(reader) as List<Tag>;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Could not load {TagsDataFilePath}.", ex);
                        return false;
                    }
                }

                //Constructing AllTags and pass it to all the tags
                AllTags = Utils.FlattenTree(RootTags).ToList();
                foreach (Tag t in AllTags) t.AllTags = AllTags;
            }
            else
            {
                RootTags = new();
            }
            return true;
        }

        //Loads AllCodices list from Files
        public bool LoadCodices()
        {
            if (File.Exists(CodicesDataFilePath))
            {
                using (var reader = new StreamReader(CodicesDataFilePath))
                {
                    System.Xml.Serialization.XmlSerializer serializer = new(typeof(ObservableCollection<Codex>));
                    try
                    {
                        AllCodices = serializer.Deserialize(reader) as ObservableCollection<Codex>;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Could not load {CodicesDataFilePath}", ex);
                        return false;
                    }
                }

                //AllCodices.CollectionChanged += (e, v) => SaveCodices();

                Debug.Assert(AllCodices != null, nameof(AllCodices) + " != null");
                foreach (Codex c in AllCodices)
                {
                    //reconstruct tags from ID's
                    c.Tags = new(AllTags.Where(t => c.TagIDs.Contains(t.ID)));

                    //double check image location, redundant but got fucked in an update
                    c.SetImagePaths(this);
                }
            }
            else
            {
                AllCodices = new();
            }
            return true;
        }

        public bool LoadInfo()
        {
            if (File.Exists(CollectionInfoFilePath))
            {
                using var reader = new StreamReader(CollectionInfoFilePath);
                System.Xml.Serialization.XmlSerializer serializer = new(typeof(CollectionInfo));
                try
                {
                    Info = serializer.Deserialize(reader) as CollectionInfo;
                    Info.CompleteLoading(this);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Could not load {CollectionInfoFilePath}", ex);
                    return false;
                }
            }
            else
            {
                Info = new();
            }
            return true;
        }
        #endregion

        #region Save Data To XML File

        public void Save()
        {
            SaveTags();
            SaveCodices();
            SaveInfo();
            Properties.Settings.Default.Save();
            Logger.Info($"Saved {DirectoryName}");
        }

        public void SaveTags()
        {
            try
            {
                using var writer = XmlWriter.Create(TagsDataFilePath, SettingsViewModel.XmlWriteSettings);
                System.Xml.Serialization.XmlSerializer serializer = new(typeof(List<Tag>));
                serializer.Serialize(writer, RootTags);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to Save Tags to {TagsDataFilePath}", ex);
            }
        }

        public void SaveCodices()
        {
            //Copy id's of tags into list for serialisation
            foreach (Codex codex in AllCodices)
            {
                codex.TagIDs = codex.Tags.Select(t => t.ID).ToList();
            }

            try
            {
                using var writer = XmlWriter.Create(CodicesDataFilePath, SettingsViewModel.XmlWriteSettings);
                System.Xml.Serialization.XmlSerializer serializer = new(typeof(ObservableCollection<Codex>));
                serializer.Serialize(writer, AllCodices);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to Save Codex Info to {CodicesDataFilePath}", ex);
            }
        }

        public void SaveInfo()
        {
            Info.PrepareSave();
            try
            {
                using var writer = XmlWriter.Create(CollectionInfoFilePath, SettingsViewModel.XmlWriteSettings);
                System.Xml.Serialization.XmlSerializer serializer = new(typeof(CollectionInfo));
                serializer.Serialize(writer, Info);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to Save Tags to {TagsDataFilePath}", ex);
            }
        }

        #endregion    

        public void DeleteCodex(Codex toDelete)
        {
            //Delete file from all lists
            AllCodices.Remove(toDelete);

            //Delete CoverArt & Thumbnail
            File.Delete(toDelete.CoverArt);
            File.Delete(toDelete.Thumbnail);
            Logger.Info($"Deleted {toDelete.Title} from {DirectoryName}");
        }

        public void DeleteCodices(IList<Codex> toDelete)
        {
            int count = toDelete.Count;
            string message = $"You are about to delete {count} file{(count > 1 ? @"s" : @"")}. " +
                           $"This cannot be undone. " +
                           $"Are you sure you want to continue?";
            var result = MessageBox.Show(message, "Delete", MessageBoxButton.OKCancel);
            if (result == MessageBoxResult.OK)
            {
                foreach (Codex toDel in toDelete)
                {
                    DeleteCodex(toDel);
                }
            }
            SaveCodices();
        }

        public void BanishCodices(IList<Codex> toBanish)
        {
            if (toBanish is null) return;
            IEnumerable<string> toBanishPaths = toBanish.Select(codex => codex.Path);
            IEnumerable<string> toBanishURLs = toBanish.Select(codex => codex.SourceURL);
            IEnumerable<string> toBanishStrings = toBanishPaths
                .Concat(toBanishURLs)
                .Where(s => !String.IsNullOrWhiteSpace(s))
                .ToHashSet();

            Info.BanishedPaths.AddRange(toBanishStrings);
        }

        public void DeleteTag(Tag toDelete)
        {
            //Recursive loop to delete all children
            if (toDelete.Children.Count > 0)
            {
                DeleteTag(toDelete.Children[0]);
                DeleteTag(toDelete);
            }
            AllTags.Remove(toDelete);
            //remove from parent items list
            if (toDelete.Parent is null)
            {
                RootTags.Remove(toDelete);
            }
            else
            {
                toDelete.Parent.Children.Remove(toDelete);
            }

            SaveTags();
        }

        public void RenameCollection(string newCollectionName)
        {
            foreach (Codex codex in AllCodices)
            {
                //Replace folder names in image paths
                codex.SetImagePaths(newCollectionName);
            }
            try
            {
                Directory.Move(Path.Combine(CollectionsPath, DirectoryName), Path.Combine(CollectionsPath, newCollectionName));
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to move data files from {DirectoryName} to {newCollectionName}", ex);
            }

            DirectoryName = newCollectionName;
            Logger.Info($"Renamed  {DirectoryName} to {newCollectionName}");
        }
    }
}
