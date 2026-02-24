using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WormholeAutomationUI.Converters;

public class EnumEqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
        {
            return Visibility.Collapsed;
        }

        if (value.GetType().IsEnum && parameter.GetType().IsEnum)
        {
            return Equals(value, parameter) ? Visibility.Visible : Visibility.Collapsed;
        }

        var equals = string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
        return equals ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
