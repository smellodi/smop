using System;
using System.Globalization;
using System.Linq;
using System.Windows.Controls;

namespace Smop.PulseGen.Utils;

public class Validation
{
	public static readonly NumberStyles INTEGER = NumberStyles.Integer | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite;
	public static readonly NumberStyles FLOAT = NumberStyles.Float;

	public enum ValueFormat
	{
		Integer = NumberStyles.Integer | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
		Float = NumberStyles.Float
	}

	public readonly TextBox Source;
	public readonly double Min;
	public readonly double Max;
	public readonly ValueFormat Format;

	public bool IsList => _listDelim != null;
	public bool AcceptsExpression => _expressionDelims != null;
	public string Value => _value;
	public double? AsNumber => IsValid ? double.Parse(_value) : null;

	public bool IsValid
	{
		get
		{
			if (_listDelim != null)
			{
				var chunks = Source.Text
					.Split(_listDelim ?? ' ')
					.Where(v => !string.IsNullOrWhiteSpace(v));
				if (chunks.Count() == 0)
				{
					_code = ValidityViolationCode.EmptyList;
				}
				else
				{
					chunks.All(chunk => IsValidValueOrExpression(chunk));
				}
			}
			else
			{
				IsValidValueOrExpression(Source.Text);
			}

			return _code == ValidityViolationCode.OK;
		}
	}

	public Validation(TextBox textbox, double min, double max, ValueFormat format, char? listDelim = null, char[]? expressionDelims = null)
	{
		Source = textbox;
		Min = min;
		Max = max;
		Format = format;
		_listDelim = listDelim;
		_expressionDelims = expressionDelims;

		_value = Source.Text;
	}

	public override string ToString()
	{
		return _code switch
		{
			ValidityViolationCode.EmptyList => L10n.T("EmptyList"),
			ValidityViolationCode.InvalidExpression => string.Format(L10n.T("ExpressionNotValid"), _value, _expressionDelims),
			ValidityViolationCode.InvalidFormat => string.Format(L10n.T("ValueFormatNotValid"), _value, Format),
			ValidityViolationCode.TooLarge => string.Format(L10n.T("ValueTooLarge"), _value, Max),
			ValidityViolationCode.TooSmall => string.Format(L10n.T("ValueTooSmall"), _value, Min),
			_ => "unknown error",
		};
	}

	public static bool Do(TextBox textbox, double min, double max, EventHandler<int> action, char? listDelim = null, char[]? expressionDelims = null)
	{
		return Do(textbox, min, max, ValueFormat.Integer, (object? s, double e) => action(s, (int)e), listDelim, expressionDelims);
	}

	public static bool Do(TextBox textbox, double min, double max, EventHandler<double> action, char? listDelim = null, char[]? expressionDelims = null)
	{
		return Do(textbox, min, max, ValueFormat.Float, (object? s, double e) => action(s, e), listDelim, expressionDelims);
	}

	// Internal

	enum ValidityViolationCode
	{
		OK,
		InvalidFormat,
		TooSmall,
		TooLarge,
		EmptyList,
		InvalidExpression
	}

	readonly char? _listDelim;
	readonly char[]? _expressionDelims;

	ValidityViolationCode _code = ValidityViolationCode.OK;
	string _value;

	private static bool Do(TextBox textbox, double min, double max, ValueFormat format, EventHandler<double> action, char? listDelim = null, char[]? expressionDelims = null)
	{
		var validation = new Validation(textbox, min, max, format, listDelim, expressionDelims);
		if (!validation.IsValid)
		{
			var msg = L10n.T("CorrectAndTryAgain");
			MsgBox.Error(
				L10n.T("OlfactoryTestTool") + " - " + L10n.T("Validator"),
				$"{validation}.\n{msg}");
			validation.Source.Focus();
			validation.Source.SelectAll();

			return false;
		}

		action(validation, validation.AsNumber ?? 0);
		return true;
	}

	private bool IsValidValueOrExpression(string value)
	{
		if (_expressionDelims != null)
		{
			var exprValues = value.Split(_expressionDelims);
			if (exprValues.Length > 1)
			{
				return exprValues.All(exprValue => IsValidValueOrExpression(exprValue));
			}
		}

		_value = value;

		// not expresion, or the expression is a simple value
		if (Format == ValueFormat.Float)
		{
			if (!double.TryParse(value, FLOAT, null, out double val))
			{
				_code = ValidityViolationCode.InvalidFormat;
			}
			else if (val < Min)
			{
				_code = ValidityViolationCode.TooSmall;
			}
			else if (val > Max)
			{
				_code = ValidityViolationCode.TooLarge;
			}
		}
		else if (Format == ValueFormat.Integer)
		{
			if (!int.TryParse(value, INTEGER, null, out int val))
			{
				_code = ValidityViolationCode.InvalidFormat;
			}
			else if (val < Min)
			{
				_code = ValidityViolationCode.TooSmall;
			}
			else if (val > Max)
			{
				_code = ValidityViolationCode.TooLarge;
			}
		}
		else
		{
			_code = ValidityViolationCode.InvalidFormat;
		}

		return _code == ValidityViolationCode.OK;
	}
}
