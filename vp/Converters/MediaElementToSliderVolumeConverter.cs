﻿using System;
using System.Globalization;
using System.Windows.Data;
using vp.Services.Settings;

namespace vp.Converters
{
    public class MediaElementToSliderVolumeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double v)
            {
                return v * ApplicationConstants.VolumeSliderMaxValue;
            }
            throw new ArgumentException();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double v)
            {
                return v / ApplicationConstants.VolumeSliderMaxValue;
            }
            throw new ArgumentException();
        }
    }
}
