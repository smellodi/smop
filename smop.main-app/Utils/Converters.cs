using Smop.MainApp.Controllers;
using Smop.MainApp.Controllers.HumanTests;
using System;
using System.Globalization;
using System.Text.Json;
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
        bool isInverted = (bool?)parameter == true;
        return (bool)value ?
            (isInverted ? Visibility.Hidden : Visibility.Visible) :
            (isInverted ? Visibility.Visible : Visibility.Hidden);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isInverted = (bool?)parameter == true;
        var visibility = (Visibility)value;
        return isInverted ? visibility == Visibility.Hidden : visibility == Visibility.Visible;
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

public class TrialStageToButtonBrush : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        TrialStage trialStage = (TrialStage)value;
        int odorID = int.Parse(parameter?.ToString() ?? "0");
        return 
            trialStage.Stage == Stage.Question ? Brushes.Clickable :
                trialStage.MixtureId < odorID ? Brushes.Inactive :
                    trialStage.MixtureId > odorID ? Brushes.Done :
                        trialStage.Stage switch
                        {
                            Stage.WaitingMixture => Brushes.Inactive,
                            Stage.SniffingMixture => Brushes.Active,
                            _ => Brushes.Done,
                        };
    }

    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => new TrialStage(Stage.Initial, 0);
}

public class TrialStageToBoxBrush : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        TrialStage trialStage = (TrialStage)value;
        int odorID = int.Parse(parameter?.ToString() ?? "0");
        return
            trialStage.MixtureId < odorID ? Brushes.Inactive :
                trialStage.MixtureId > odorID ? Brushes.Done :
                    trialStage.Stage switch
                    {
                        Stage.WaitingMixture => Brushes.Inactive,
                        Stage.SniffingMixture => Brushes.Active,
                        _ => Brushes.Done,
                    };
    }

    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => new TrialStage(Stage.Initial, 0);
}

public class ItemToDmsInfo : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        IonVision.Defs.ScanResult? dms = null;
        if (value is Dialogs.DmsSaveDialog.DmsData dmsData)
            return dmsData.Data.Info;
        
        return string.Empty;
    }

    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
