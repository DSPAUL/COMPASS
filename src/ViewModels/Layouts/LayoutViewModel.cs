﻿using COMPASS.Models;
using COMPASS.ViewModels.Sources;
using GongSolutions.Wpf.DragDrop;
using System.IO;
using System.Linq;
using System.Windows;

namespace COMPASS.ViewModels
{
    public abstract class LayoutViewModel : ViewModelBase, IDropTarget
    {
        protected LayoutViewModel() : base() { }

        public enum Layout
        {
            List,
            Card,
            Tile,
            Home
        }

        // Should put this function seperate Factory class for propper factory pattern
        // but I don't see the point, seems a lot of boilerplate without real advantages
        public static LayoutViewModel GetLayout(Layout? layout = null)
        {
            layout ??= (Layout)Properties.Settings.Default.PreferedLayout;
            Properties.Settings.Default.PreferedLayout = (int)layout;
            LayoutViewModel newLayout = layout switch
            {
                Layout.Home => new HomeLayoutViewModel(),
                Layout.List => new ListLayoutViewModel(),
                Layout.Card => new CardLayoutViewModel(),
                Layout.Tile => new TileLayoutViewModel(),
                _ => null
            };
            return newLayout;
        }

        #region Properties

        public CodexViewModel CodexVM { get; init; } = new();

        //Selected File
        private Codex _selectedCodex;
        public Codex SelectedCodex
        {
            get => _selectedCodex;
            set => SetProperty(ref _selectedCodex, value);
        }

        //Set Type of view
        public Layout LayoutType { get; init; }
        #endregion

        public void DragOver(IDropInfo dropInfo)
        {
            if (dropInfo.Data is DataObject)
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
                dropInfo.Effects = DragDropEffects.Copy;
            }
            else
            {
                dropInfo.NotHandled = true;
            }

        }

        public void Drop(IDropInfo dropInfo)
        {
            if (dropInfo.Data is DataObject data)
            {
                var paths = data.GetFileDropList();

                var folders = paths.Cast<string>().ToList().Where(path => File.GetAttributes(path).HasFlag(FileAttributes.Directory));
                var files = paths.Cast<string>().ToList().Where(path => !File.GetAttributes(path).HasFlag(FileAttributes.Directory));


                if (folders.Any())
                {
                    FolderSourceViewModel fsvm = new()
                    {
                        FolderNames = folders.ToList(),
                        FileNames = files.ToList()
                    };
                    fsvm.ImportFolders();
                    return;
                }

                if (files.Any())
                {
                    FileSourceViewModel fsvm = new();
                    fsvm.ImportFiles(files.ToList(), true);
                    return;
                }
            }
        }
    }
}
