using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Smop.MainApp.Utils;

public class NumberToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value.GetType() == typeof(int))
        {
            return (int)value != 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        else if (value.GetType() == typeof(double))
        {
            return (double)value != 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        else return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var visibility = (Visibility)value;
        return visibility == Visibility.Visible ? 1.0 : 0.0;
    }
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isInversed = (bool?)parameter == true;
        return (bool)value ?
            (isInversed ? Visibility.Hidden : Visibility.Visible) :
            (isInversed ? Visibility.Visible : Visibility.Hidden);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isInversed = (bool?)parameter == true;
        var visibility = (Visibility)value;
        return isInversed ? visibility == Visibility.Hidden : visibility == Visibility.Visible;
    }
}

public class ObjectToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value != null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class ZoomToPercentageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value.GetType() == typeof(float) || value.GetType() == typeof(double))
        {
            double number = (double)value * 100;
            return $"{number:F0}%";
        }
        else
        {
            return "NaN";
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class NullableNumberToString : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null)
            return "";
        else 
            return $"{(double)value:F1}";
    }

    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string? s = (string?)value;
        if (string.IsNullOrEmpty(s))
            return null;
        else
            return double.TryParse(s, out double result) ? result : null;
    }
}

public class BoolInverse : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return !(bool)value;
    }

    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return !(bool)value;
    }
}
