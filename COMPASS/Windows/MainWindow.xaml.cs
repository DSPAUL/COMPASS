﻿using System;
using Microsoft.Win32;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.ComponentModel;
using COMPASS.ViewModels;

namespace COMPASS
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            //set Itemsources for databinding
            MainViewModel = new MainViewModel("DnD");
            DataContext = MainViewModel;

            MaxHeight = SystemParameters.MaximizedPrimaryScreenHeight;
        }

        private MainViewModel MainViewModel;

        // is true if we hold left mouse button on windows tilebar
        private bool DragWindow = false;

        //Deselects when you click away
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            MainGrid.Focus();
        }

        //removes tag from filter list when clicked
        private void ActiveTag_Click(object sender, RoutedEventArgs e)
        {
            if((Tag)CurrentTagList.SelectedItem != null) MainViewModel.FilterHandler.RemoveTagFilter((Tag)CurrentTagList.SelectedItem);
        }

        //import files
        private void ImportBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                AddExtension = false,
                Multiselect = true   
            };
            if (openFileDialog.ShowDialog() == true)
            {
                foreach(string path in openFileDialog.FileNames)
                {
                    if(MainViewModel.CurrentData.AllFiles.All(p => p.Path != path))
                    {
                    MyFile pdf = new MyFile(MainViewModel) { Path = path, Title = System.IO.Path.GetFileNameWithoutExtension(path)};
                        MainViewModel.CurrentData.AllFiles.Add(pdf);
                        CoverArtGenerator.ConvertPDF(pdf, MainViewModel.CurrentData.Folder);
                    }
                }
                MainViewModel.Reset();
            }         
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            MainViewModel.CurrentData.SaveFilesToFile();
            MainViewModel.CurrentData.SaveTagsToFile();
        }

        #region Windows Tile Bar Buttons
                private void MinimizeWindow(object sender, RoutedEventArgs e)
                {
                    App.Current.MainWindow.WindowState = WindowState.Minimized;
                }
                private void WindowsBar_MouseDown(object sender, MouseButtonEventArgs e)
                {
                    if (e.ClickCount == 2)
                    {
                        WindowState = WindowState.Maximized;
                        DragWindow = false;
                    }

                    else
                    {
                        DragMove();
                        if (WindowState == WindowState.Maximized) DragWindow = WindowState == WindowState.Maximized;
                    }
                }
                private void OnMouseMove(object sender, MouseEventArgs e)
                {
                    if (DragWindow)
                    {
                        DragWindow = false;

                        var point = e.MouseDevice.GetPosition(this);

                        Left = point.X - (RestoreBounds.Width * 0.5);
                        Top = point.Y - 20;

                        WindowState = WindowState.Normal;

                        try
                        {
                            DragMove();
                        }

                        catch (InvalidOperationException)
                        {
                            WindowState = WindowState.Maximized;
                        }
                   
                    }
                }
                private void CloseButton_Click(object sender, RoutedEventArgs e)
                {
                    this.Close();
                }
        #endregion

    }

}


