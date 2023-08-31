using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Smop.PulseGen.Dialogs;

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

    public static string? Show(string? title, string? message, string? value, InputType inputType = InputType.String)
    {
        if (Application.Current.Dispatcher.Thread != System.Threading.Thread.CurrentThread)
        {
            return Application.Current.Dispatcher.Invoke(() =>
            {
                var box = new InputBox(title, message, value, inputType)
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    ShowInTaskbar = true
                };

                return box.ShowDialog() == true ? box.Value : null;
            });
        }
        else
        {
            var box = new InputBox(title, message, value, inputType);
            if (!Application.Current.MainWindow.IsLoaded)
            {
                box.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                box.ShowInTaskbar = true;
            }
            else
            {
                box.Owner = Application.Current.MainWindow;
            }

            return box.ShowDialog() == true ? box.Value : null;
        }
    }

    // Internal

    const int GWL_STYLE = -16;
    const int WS_SYSMENU = 0x80000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    InputType _inputType;

    private InputBox(string? title, string? message, string? value, InputType inputType)
    {
        InitializeComponent();
        DataContext = this;

        _inputType = inputType;

        message ??= "MISSING THE MESSAGE TEXT";

        Title = title;
        txbMessage.Text = message;
        txbMessage.MaxWidth = 220 + message.Length * 2;
        txbInput.Text = value ?? "";
        txbInput.Focus();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        _ = SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) & ~WS_SYSMENU);
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
