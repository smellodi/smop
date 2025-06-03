using System.Windows;

namespace Smop.ML.Search;

public partial class DebugDisplay : Window
{
    public DebugDisplay()
    {
        InitializeComponent();
    }

    public void WriteLine(string? msg = null)
    {
        Dispatcher.Invoke(() =>
        {
            if (msg != null)
            {
                System.Diagnostics.Debug.WriteLine("[ML.DE] " + msg);
                txbOutput.AppendText(msg);
                txbOutput.AppendText("\n");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine(string.Empty);
                txbOutput.AppendText("\n");
            }
        });
    }

    public void Write(string msg)
    {
        Dispatcher.Invoke(() =>
        {
            System.Diagnostics.Debug.Write("[ML.DE] " + msg);
            txbOutput.AppendText(msg);
        });
    }
}
