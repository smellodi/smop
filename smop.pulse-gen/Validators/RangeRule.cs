using System;
using System.Globalization;
using System.Windows.Controls;

namespace Smop.PulseGen.Validators;

public class RangeRule : ValidationRule
{
    public float Min { get; set; }
    public float Max { get; set; }
    public bool IsInteger { get; set; } = false;
    public bool CanBeEmpty { get; set; } = false;

    public override ValidationResult Validate(object value, CultureInfo cultureInfo)
    {
        string? strValue = Convert.ToString(value);

        if (string.IsNullOrEmpty(strValue))
        {
            if (CanBeEmpty)
                return new ValidationResult(true, null);
            else
                return new ValidationResult(false, $"The value cannot be empty");
        }

        if (IsInteger)
        {
            if (!int.TryParse(strValue, out int number))
            {
                return new ValidationResult(false, $"The value must be an integer number");
            }
            if ((number < Min) || (number > Max))
            {
                return new ValidationResult(false, "The value must be in the range: " + Min + " - " + Max + ".");
            }
        }
        else
        {
            if (!float.TryParse(strValue, out float number))
            {
                return new ValidationResult(false, $"The value must be a number");
            }
            if ((number < Min) || (number > Max))
            {
                return new ValidationResult(false, "The value must be in the range: " + Min + " - " + Max + ".");
            }
        }

        return new ValidationResult(true, null);
    }
}