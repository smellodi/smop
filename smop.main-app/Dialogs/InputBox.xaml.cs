using System.ComponentModel;
using System.Windows;

namespace Smop.MainApp.Dialogs;

public partial class InputBox : Window, INotifyPropertyChanged
{
    public enum InputType
    {
        String,
        Integer,
        Number
    }

    public string Value => txbInput.Text;
    public bool HasValue => txbInput.Text.Length > 0;

    public event PropertyChangedEventHandler? PropertyChanged;

    public static string? Show(string title, string message, string? value = null, InputType inputType = InputType.String)
    {
        string? CreateAndShow()
        {
            var box = new InputBox(title, message, value, inputType);
            DialogTools.SetCentralPosition(box);

            return box.ShowDialog() == true ? box.Value : null;
        }

        return DialogTools.ShowSafe(CreateAndShow);
    }

    // Internal

    readonly InputType _inputType;

    private InputBox(string title, string message, string? value, InputType inputType)
    {
        InitializeComponent();

        DialogTools.HideWindowButtons(this);

        _inputType = inputType;

        Title = title;
        txbMessage.Text = message;
        txbMessage.MaxWidth = 220 + message.Length * 2;
        txbInput.Text = value ?? "";
        txbInput.Focus();
    }

    // UI

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Storage.Instance.BindScaleToZoomLevel(sctScale);
    }

    private void Window_Unloaded(object sender, RoutedEventArgs e)
    {
        Storage.Instance.UnbindScaleToZoomLevel(sctScale);
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        var isValid = _inputType switch
        {
            InputType.Integer => int.TryParse(Value, out int _),
            InputType.Number => float.TryParse(Value, out float _),
            _ => true
        };

        if (!isValid)
        {
            txbInput.Focus();
        }
        else
        {
            DialogResult = true;
        }
    }

    private void Input_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasValue)));
    }
}
