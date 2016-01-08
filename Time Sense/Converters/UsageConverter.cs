﻿using System;
using Stuff;
using Windows.UI.Xaml.Data;

namespace Time_Sense
{
    public class UsageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            int usage = int.Parse(value.ToString());
            return utilities.FormatData(usage);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return null;
        }    
    }
}