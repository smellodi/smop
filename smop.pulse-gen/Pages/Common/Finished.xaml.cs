using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Smop.PulseGen.Logging;

namespace Smop.PulseGen.Pages;

public partial class Finished : Page, IPage<bool>, INotifyPropertyChanged
{
	public class RequestSavingArgs : EventArgs
	{
		public SavingResult Result { get; set; }
		public RequestSavingArgs(SavingResult result)
		{
			Result = result;
		}
	}

	public event EventHandler<bool>? Next;       // true: exit, false: return to the fornt page
	public event EventHandler<RequestSavingArgs>? RequestSaving;
	public event PropertyChangedEventHandler? PropertyChanged;

	public string TestName
	{
		get => _testName;
		set
		{
			_testName = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TestName)));
		}
	}

	public Finished()
	{
		InitializeComponent();

		DataContext = this;
	}

	public void DisableSaving()
	{
		btnSaveData.IsEnabled = false;
	}

	// Internal

	string _testName = "";

	private bool HasDecisionAboutData()
	{
		if (!FlowLogger.Instance.HasAnyRecord)
		{
			return true;
		}

		RequestSavingArgs args = new(SavingResult.Cancel);
		RequestSaving?.Invoke(this, args);

		return args.Result != SavingResult.Cancel;
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

	private void Page_GotFocus(object sender, RoutedEventArgs e)
	{
		btnSaveData.IsEnabled = true;
	}

	private void SaveData_Click(object sender, RoutedEventArgs e)
	{
		RequestSaving?.Invoke(this, new(SavingResult.Cancel));
	}

	private void Return_Click(object sender, RoutedEventArgs e)
	{
		if (HasDecisionAboutData())
		{
			Next?.Invoke(this, false);
		}
	}

	private void Exit_Click(object sender, RoutedEventArgs e)
	{
		if (HasDecisionAboutData())
		{
			Next?.Invoke(this, true);
		}
	}
}
