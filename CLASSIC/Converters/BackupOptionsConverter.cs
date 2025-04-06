using System;
using System.Globalization;
using Avalonia.Data.Converters;
using CLASSIC.ViewModels;

namespace CLASSIC.Converters;

public class BackupOptionsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is BackupOptions options && parameter is string type)
        {
            return new BackupOptions { Type = type };
        }

        return null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}