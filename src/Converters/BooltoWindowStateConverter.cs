﻿using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace COMPASS.Converters
{
    class WindowStatetoBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (WindowState)value == WindowState.Maximized;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((bool)value) return WindowState.Maximized;
            else return WindowState.Normal;
        }
    }
}