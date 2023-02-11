﻿using COMPASS.ViewModels.Commands;
using static COMPASS.Models.Enums;


namespace COMPASS.ViewModels
{
    public class LeftDockViewModel : ViewModelBase
    {
        public LeftDockViewModel() : base()
        {
            TagsTabVM = new();
            FiltersTabVM = new();
        }

        public int SelectedTab
        {
            get => Properties.Settings.Default.SelectedTab;
            set
            {
                Properties.Settings.Default.SelectedTab = value;
                RaisePropertyChanged(nameof(SelectedTab));
                if (value > 0) Collapsed = false;
            }
        }

        private bool _collapsed = false;
        public bool Collapsed
        {
            get => _collapsed;
            set
            {
                SetProperty(ref _collapsed, value);
                if (value == true) SelectedTab = 0;
            }
        }

        #region Tags Tab
        private TagsTabViewModel _tagsTabVM;
        public TagsTabViewModel TagsTabVM
        {
            get => _tagsTabVM;
            set => SetProperty(ref _tagsTabVM, value);
        }
        #endregion

        #region Filters Tab

        private FiltersTabViewModel _filtersTabVM;
        public FiltersTabViewModel FiltersTabVM
        {
            get => _filtersTabVM;
            set => SetProperty(ref _filtersTabVM, value);
        }

        #endregion

        #region Add Books Tab
        private ImportViewModel _currentImportVM;
        public ImportViewModel CurrentImportViewModel
        {
            get => _currentImportVM;
            set => SetProperty(ref _currentImportVM, value);
        }

        private RelayCommand<Sources> _importFilesCommand;
        public RelayCommand<Sources> ImportFilesCommand => _importFilesCommand ??= new(ImportFiles);
        public void ImportFiles(Sources source) => CurrentImportViewModel = new ImportViewModel(source, MVM.CurrentCollection);
        #endregion

    }
}
