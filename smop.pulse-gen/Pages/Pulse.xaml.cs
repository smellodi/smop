using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Smop.PulseGen.Logging;
using Smop.PulseGen.Test;

namespace Smop.PulseGen.Pages;

public partial class Pulse : Page, IPage<bool>, ITest, INotifyPropertyChanged
{
	public class RequestSavingArgs : EventArgs
	{
		public SavingResult Result { get; set; }
		public RequestSavingArgs(SavingResult result)
		{
			Result = result;
		}
	}

    /// <summary>
    /// true: finished all trials, false: interrupted
    /// </summary>
    public event EventHandler<bool>? Next;
	public event PropertyChangedEventHandler? PropertyChanged;

	public Pulse()
	{
		InitializeComponent();

		DataContext = this;

        Application.Current.Exit += (s, e) => Interrupt();
    }

    public void Start()
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
		GC.SuppressFinalize(this);
    }

    // Internal

    private void Interrupt()
    {
    }

    private void ForceToFinish()
    {
    }


    // UI events

    private void Page_Loaded(object sender, RoutedEventArgs e)
	{
		Storage.Instance
			.BindScaleToZoomLevel(sctScale)
			.BindVisibilityToDebug(lblDebug);
	}

	private void Page_Unloaded(object sender, RoutedEventArgs e)
	{
		Storage.Instance
			.UnbindScaleToZoomLevel(sctScale)
			.UnbindVisibilityToDebug(lblDebug);
	}

    private void Page_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F2)
        {
            ForceToFinish();
        }
    }
    private void Interrupt_Click(object sender, RoutedEventArgs e)
	{
		Next?.Invoke(this, false);
	}
}
