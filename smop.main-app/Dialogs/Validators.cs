using System;
using System.Globalization;
using System.Windows.Controls;

namespace Smop.MainApp.Dialogs;

public class RangeRule : ValidationRule
{
    public double Min { get; set; }
    public double Max { get; set; }

    public override ValidationResult Validate(object value, CultureInfo cultureInfo)
    {
        double valueNum = 0;
        string valueStr = (string)value;

        try
        {
            if (valueStr.Length > 0)
                valueNum = double.Parse(valueStr);
        }
        catch (Exception e)
        {
            return new ValidationResult(false, $"Illegal characters or {e.Message}");
        }

        if ((valueNum < Min) || (valueNum > Max))
        {
            return new ValidationResult(false,
              $"Please enter a number in the range: {Min}-{Max}.");
        }
        return ValidationResult.ValidResult;
    }
}