﻿using COMPASS.Commands;
using COMPASS.Models;
using COMPASS.Properties;
using COMPASS.Tools;
using FuzzySharp;
using GongSolutions.Wpf.DragDrop;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace COMPASS.ViewModels
{
    public class CollectionViewModel : ViewModelBase, IDropTarget
    {
        public CollectionViewModel(ObservableCollection<Codex> allCodices)
        {
            _allCodices = allCodices;

            // Load sorting from settings
            InitSortingProperties();

            IncludedCodices = new(_allCodices);
            ExcludedCodices = new();

            IncludedFilters.CollectionChanged += (e, v) => UpdateIncludedCodices();
            ExcludedFilters.CollectionChanged += (e, v) => UpdateExcludedCodices();

            _allCodices.CollectionChanged += (e, v) => SubscribeToCodexProperties();
            SubscribeToCodexProperties();

            ApplyFilters();
        }

        #region Properties
        private ObservableCollection<Codex> _allCodices;
        private readonly int _itemsShown = 15;
        public int ItemsShown => Math.Min(_itemsShown, FilteredCodices.Count);

        public ObservableCollection<Filter> IncludedFilters { get; set; } = new();
        public ObservableCollection<Filter> ExcludedFilters { get; set; } = new();
        private HashSet<Codex> IncludedCodices { get; set; }
        private HashSet<Codex> ExcludedCodices { get; set; }

        private ObservableCollection<Codex> _filteredCodices;
        public ObservableCollection<Codex> FilteredCodices
        {
            get => _filteredCodices;
            set => SetProperty(ref _filteredCodices, value);
        }

        public ObservableCollection<Codex> Favorites => new(FilteredCodices.Where(c => c.Favorite));
        public List<Codex> RecentCodices => FilteredCodices.OrderByDescending(c => c.LastOpened).ToList().GetRange(0, ItemsShown);
        public List<Codex> MostOpenedCodices => FilteredCodices.OrderByDescending(c => c.OpenedCount).ToList().GetRange(0, ItemsShown);
        public List<Codex> RecentlyAddedCodices => FilteredCodices.OrderByDescending(c => c.DateAdded).ToList().GetRange(0, ItemsShown);

        private string _searchTerm = "";
        public string SearchTerm
        {
            get => _searchTerm;
            set => SetProperty(ref _searchTerm, value);
        }

        public ListSortDirection SortDirection
        {
            get => (ListSortDirection)Settings.Default[nameof(SortDirection)];
            set
            {
                Settings.Default[nameof(SortDirection)] = (int)value;
                Settings.Default.Save();
                ApplySorting();
                RaisePropertyChanged(nameof(SortDirection));
            }
        }

        public string SortProperty
        {
            get => (string)Settings.Default[nameof(SortProperty)];
            set
            {
                Settings.Default[nameof(SortProperty)] = value;
                Settings.Default.Save();
                ApplySorting();
                RaisePropertyChanged(nameof(SortProperty));
            }
        }

        public Dictionary<string, string> SortOptions { get; } = new()
        {
            //("Display name","Property Name")
            { "Title", "SortingTitle" },
            { "Author", "AuthorsAsString" },
            { "Publisher", "Publisher" },
            { "User Rating", "Rating" },
            { "Date - Released", "ReleaseDate" },
            { "Date - Last Opened", "LastOpened"},
            { "Date - Added", "DateAdded" },
            { "Page Count", "PageCount" },
            { "Times opened", "OpenedCount" }
        };
        #endregion

        #region Methods and Commands
        private void SubscribeToCodexProperties()
        {
            //cause derived lists to update when codex gets updated
            foreach (Codex c in _allCodices)
            {
                c.PropertyChanged += (e, v) => RaisePropertyChanged(nameof(Favorites));
                c.PropertyChanged += (e, v) => RaisePropertyChanged(nameof(RecentCodices));
                c.PropertyChanged += (e, v) => RaisePropertyChanged(nameof(MostOpenedCodices));
            }
        }
        private void InitSortingProperties()
        {
            //double check on typos by checking if all property names exist in codex class
            var PossibleSortProptertyNames = typeof(Codex).GetProperties().Select(p => p.Name).ToList();
            if (SortOptions.Select(pair => pair.Value).Except(PossibleSortProptertyNames).Any())
            {
                MessageBox.Show("One of the sort property paths does not exist");
                Logger.log.Error("One of the sort property paths does not exist");
            }
        }

        //------------- Adding, Removing, ect ------------//
        public void AddFilter(Filter filter, bool include = true)
        {
            ObservableCollection<Filter> Target = include ? IncludedFilters : ExcludedFilters;
            ObservableCollection<Filter> Other = !include ? IncludedFilters : ExcludedFilters;
            //if Filter is unique, remove previous instance of that Filter before adding
            if (filter.Unique)
            {
                Target.Remove(
                    Target.SingleOrDefault(f => f.Type == filter.Type));
            }
            //only add if not yet in Included Tags
            if (!Target.Contains(filter))
            {
                Target.Add(filter);
                Other.Remove(filter);
            }
        }

        private RelayCommand<Filter> _removeFilterCommand;
        public RelayCommand<Filter> RemoveFilterCommand => _removeFilterCommand ??= new(RemoveFilter);
        public void RemoveFilter(Filter filter)
        {
            IncludedFilters.Remove(filter);
            ExcludedFilters.Remove(filter);
        }
        public void RemoveFilterType(Filter.FilterType filterType)
        {
            IncludedFilters.RemoveAll(filter => filter.Type == filterType);
            ExcludedFilters.RemoveAll(filter => filter.Type == filterType);
        }

        private ActionCommand _clearFiltersCommand;
        public ActionCommand ClearFiltersCommand => _clearFiltersCommand ??= new(ClearFilters);
        public void ClearFilters()
        {
            SearchTerm = "";
            IncludedFilters.Clear();
            ExcludedFilters.Clear();
        }


        //------------- Filter Logic ------------//
        private void UpdateIncludedCodices()
        {
            IncludedCodices = new(_allCodices);
            foreach (Filter.FilterType filterType in Enum.GetValues(typeof(Filter.FilterType)))
            {
                // Included codices must match filters of all types so IntersectWith()
                IncludedCodices.IntersectWith(GetFilteredCodicesByType(IncludedFilters, filterType, true));
            }
            ApplyFilters();
        }
        private void UpdateExcludedCodices()
        {
            ExcludedCodices = new();
            foreach (Filter.FilterType filterType in Enum.GetValues(typeof(Filter.FilterType)))
            {
                // Codex is excluded as soon as it matches any excluded filter so UnionWith()
                ExcludedCodices.UnionWith(GetFilteredCodicesByType(ExcludedFilters, filterType, false));
            }
            ApplyFilters();
        }

        /// <summary>
        /// Get list of Codices that match filters of one filter type
        /// </summary>
        /// <param name="Filters"></param>
        /// <param name="filtertype"></param>
        /// <param name="include"> Determines whether returned codices should be included or excluded </param>
        /// <returns></returns>
        private IEnumerable<Codex> GetFilteredCodicesByType(IEnumerable<Filter> Filters, Filter.FilterType filtertype, bool include)
        {
            List<Filter> RelevantFilters = new(Filters.Where(filter => filter.Type == filtertype));

            if (RelevantFilters.Count == 0) return include ? _allCodices : Enumerable.Empty<Codex>();

            return filtertype switch
            {
                Filter.FilterType.Search => GetFilteredCodicesBySearch(RelevantFilters.First()),
                Filter.FilterType.Tag => GetFilteredCodicesByTags(RelevantFilters, include),
                _ => _allCodices.Where(codex => RelevantFilters.Any(filter => filter.Method(codex)))
            };
        }

        private HashSet<Codex> GetFilteredCodicesBySearch(Filter searchFilter)
        {
            string searchterm = (string)searchFilter.FilterValue;

            if (String.IsNullOrEmpty(searchterm)) return new(_allCodices);

            HashSet<Codex> IncludedCodicesBySearch = new();
            //include acronyms
            IncludedCodicesBySearch.UnionWith(_allCodices
                .Where(f => Fuzz.TokenInitialismRatio(f.Title.ToLowerInvariant(), SearchTerm) > 80));
            //include string fragments
            IncludedCodicesBySearch.UnionWith(_allCodices
                .Where(f => f.Title.Contains(SearchTerm, StringComparison.InvariantCultureIgnoreCase)));
            //include spelling errors
            //include acronyms
            IncludedCodicesBySearch.UnionWith(_allCodices
                .Where(f => Fuzz.PartialRatio(f.Title.ToLowerInvariant(), SearchTerm) > 80));

            return IncludedCodicesBySearch;
        }

        private HashSet<Codex> GetFilteredCodicesByTags(IEnumerable<Filter> filters, bool include)
            => include ? GetIncludedCodicesByTags(filters) : GetExcludedCodicesByTags(filters);
        private HashSet<Codex> GetIncludedCodicesByTags(IEnumerable<Filter> filters)
        {
            HashSet<Codex> IncludedCodices = new(_allCodices);

            List<Tag> IncludedTags = filters
                .Select(filter => (Tag)filter.FilterValue)
                .ToList();

            if (IncludedTags.Count > 0)
            {
                HashSet<Tag> IncludedGroups = IncludedTags.Select(tag => tag.GetGroup()).ToHashSet();

                //List of codices that match filters in one group
                HashSet<Codex> SingleGroupFilteredCodices;

                // Go over every group, tags within same group have OR relation, groups have AND relation
                foreach (Tag Group in IncludedGroups)
                {
                    // Make list with all included tags in that group, including childeren
                    List<Tag> SingleGroupTags = Utils.FlattenTree(IncludedTags.Where(tag => tag.GetGroup() == Group)).ToList();
                    // Add parents of those tags, must come AFTER chileren, otherwise childeren of parents are included which is wrong
                    for (int i = 0; i < SingleGroupTags.Count; i++)
                    {
                        Tag parentTag = SingleGroupTags[i].Parent;
                        if (parentTag != null && !parentTag.IsGroup && !SingleGroupTags.Contains(parentTag)) SingleGroupTags.Add(parentTag);
                    }

                    SingleGroupFilteredCodices = new(_allCodices.Where(codex => SingleGroupTags.Intersect(codex.Tags).Any()));

                    IncludedCodices = IncludedCodices.Intersect(SingleGroupFilteredCodices).ToHashSet();
                }
            }
            return IncludedCodices;
        }
        private HashSet<Codex> GetExcludedCodicesByTags(IEnumerable<Filter> filters)
        {
            HashSet<Codex> ExcludedCodices = new();

            var ExcludedTags = filters.Select(filter => (Tag)filter.FilterValue).ToList();

            if (ExcludedTags.Count() > 0)
            {
                // If parent is excluded, so should all the childeren
                ExcludedTags = Utils.FlattenTree(ExcludedTags).ToList();
                ExcludedCodices = new(_allCodices.Where(f => ExcludedTags.Intersect(f.Tags).Any()));
            }

            return ExcludedCodices;
        }
        //------------------------------------//

        private void ApplySorting()
        {
            var sortDescr = CollectionViewSource.GetDefaultView(FilteredCodices).SortDescriptions;
            sortDescr.Clear();
            if (string.IsNullOrEmpty(SortProperty)) return;
            sortDescr.Add(new SortDescription(SortProperty, SortDirection));
        }
        private void ApplyFilters()
        {
            FilteredCodices = new(_allCodices
                .Intersect(IncludedCodices)
                .Except(ExcludedCodices));

            //Also apply filtering to these lists
            RaisePropertyChanged(nameof(Favorites));
            RaisePropertyChanged(nameof(RecentCodices));
            RaisePropertyChanged(nameof(MostOpenedCodices));
            RaisePropertyChanged(nameof(RecentlyAddedCodices));

            FilteredCodices.CollectionChanged += (e, v) => ApplySorting();
            ApplySorting();
        }

        public void ReFilter()
        {
            UpdateIncludedCodices();
            UpdateExcludedCodices();
        }
        public void RemoveCodex(Codex c)
        {
            ExcludedCodices.Remove(c);
            FilteredCodices.Remove(c);
        }

        #endregion

        #region Drag Drop Handlers
        //Drop on Treeview Behaviour
        void IDropTarget.DragOver(IDropInfo dropInfo)
        {
            //Tree to Filter Box
            switch (dropInfo.Data)
            {
                //Move From Treeview
                case TreeViewNode DraggedTVN:
                    if (!DraggedTVN.Tag.IsGroup)
                    {
                        dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
                        dropInfo.Effects = DragDropEffects.Copy;
                    }
                    break;
                //Move Filter included/excluded
                case Filter:
                //Do filter specific stuff here if needed
                //Move Tag between included/excluded
                case Tag:
                    dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
                    dropInfo.Effects = DragDropEffects.Move;
                    break;
            }
        }

        void IDropTarget.Drop(IDropInfo dropInfo)
        {
            //Included filter Listbox has extra empty collection to tell them apart
            bool ToIncluded = ((CompositeCollection)dropInfo.TargetCollection).Count > 1;

            switch (dropInfo.Data)
            {
                //Tree to Filter Box
                case TreeViewNode DraggedTVN:
                    AddFilter(new(Filter.FilterType.Tag, DraggedTVN.Tag), ToIncluded);
                    break;
                //Between include and exlude
                case Filter DraggedFilter:
                    AddFilter(DraggedFilter, ToIncluded);
                    break;
                case Tag DraggedTag:
                    AddFilter(new(Filter.FilterType.Tag, DraggedTag), ToIncluded);
                    break;
            }
        }
        #endregion
    }
}
