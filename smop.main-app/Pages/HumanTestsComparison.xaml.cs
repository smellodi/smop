using Smop.MainApp.Controllers.HumanTests;
using Smop.MainApp.Utils.Extensions;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Smop.MainApp.Pages;

public partial class HumanTestComparison : Page, IPage<Navigation>, IDisposable, INotifyPropertyChanged
{
    public bool IsInstruction => _stage == Stage.Initial;
    public bool IsQuestion => _stage == Stage.Question;
    public bool IsUserControlledPause => _stage == Stage.UserControlledPause;
    public bool IsTimedPause => _stage == Stage.TimedPause;
    public Brush Mixture1Color => _controller?.MixtureID == 1 ? 
        (_stage == Stage.SniffingMixture ? Controllers.HumanTests.Brushes.Active : 
            (_stage == Stage.Question ? Controllers.HumanTests.Brushes.Done : Controllers.HumanTests.Brushes.Inactive)) :
        Controllers.HumanTests.Brushes.Done;
    public Brush Mixture2Color => _controller?.MixtureID == 2 ?
        (_stage == Stage.SniffingMixture ? Controllers.HumanTests.Brushes.Active :
            (_stage == Stage.Question ? Controllers.HumanTests.Brushes.Done : Controllers.HumanTests.Brushes.Inactive)) :
        (_controller?.MixtureID == 1 ? Controllers.HumanTests.Brushes.Inactive : Controllers.HumanTests.Brushes.Done);
    public string StageInfo => $"Block {_controller?.BlockID}, Comparison {_controller?.ComparisonID}";
    public string? InstructionText => _stage switch
    {
        Stage.WaitingMixture => Strings?.Wait,
        Stage.SniffingMixture => Strings?.Sniff,
        Stage.UserControlledPause => Strings?.ContinueWhenReady,
        _ => null
    };

    public Settings? Settings { get; private set; }

    public IUiStrings? Strings { get; private set; }

    public event EventHandler<Navigation>? Next;
    public event PropertyChangedEventHandler? PropertyChanged;

    public HumanTestComparison()
    {
        InitializeComponent();

        Name = "HumanTestsComparison";

        wtiWaiting.TimeUpdated += (s, e) =>
        {
            if (IsUserControlledPause)
            {
                Dispatcher.Invoke(() => lblWaitingTime.Content = Math.Max(0, e.Remaining).ToTime(wtiWaiting.WaitingTime));
            }
            else
            {
                Dispatcher.Invoke(() => lblWaitingTime.Content = "");
            }
        };

        ((App)Application.Current).AddCleanupAction(CleanUp);
    }

    public void Start(Settings settings)
    {
        Settings = settings;

        Strings = UiStrings.Get(settings.Language);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Strings)));

        try
        {
            _controller = new ComparisonController(settings);
            _controller.StageChanged += (s, e) => Dispatcher.Invoke(() => SetStage(e.Stage));
        }
        catch
        {
            MessageBox.Show("Failed to initialize the test. Make sure the following odors are loaded:\nCyclohexanone, Limonene, Cytronellyl acetate",
                "Human test", MessageBoxButton.OK, MessageBoxImage.Error);
            Next?.Invoke(this, Navigation.Setup);
        }
    }

    public void Dispose()
    {
        CleanUp();
        GC.SuppressFinalize(this);
    }


    // Internal

    ComparisonController? _controller = null;

    Stage _stage = Stage.Initial;

    private void CleanUp()
    {
        _controller?.Stop();
        _controller?.Dispose();
        _controller = null;
    }

    private void SetStage(Stage stage)
    {
        _stage = stage;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StageInfo)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsInstruction)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsQuestion)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsUserControlledPause)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsTimedPause)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Mixture1Color)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Mixture2Color)));

        wtiWaiting.Text = InstructionText ?? "";

        if (stage == Stage.WaitingMixture)
        {
            wtiWaiting.Start(Settings?.WaitingInterval ?? 0);
        }
        else if (stage == Stage.SniffingMixture)
        {
            wtiWaiting.Start(Settings?.SniffingInterval ?? 0);
        }
        else if (stage == Stage.Question)
        {
            wtiWaiting.Reset();
        }
        else if (stage == Stage.UserControlledPause)
        {
            wtiWaiting.Start(ComparisonController.PAUSE_BETWEEN_BLOCKS);
        }
        else if (stage == Stage.Finished)
        {
            CleanUp();
            Next?.Invoke(this, Navigation.Test);
        }
    }

    // UI events

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        Storage.Instance
            .BindScaleToZoomLevel(sctScale)
            .BindContentToZoomLevel(lblZoom)
            .BindVisibilityToDebug(lblDebug);

        if (Focusable)
        {
            Focus();
        }

        SetStage(Stage.Initial);
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        Storage.Instance
            .UnbindScaleToZoomLevel(sctScale)
            .UnbindContentToZoomLevel(lblZoom)
            .UnbindVisibilityToDebug(lblDebug);
    }

    private void Page_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F2)
        {
            _controller?.ForceToFinish();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StageInfo)));
        }
        else if (e.Key == Key.Escape)
        {
            CleanUp();
            Next?.Invoke(this, Navigation.Setup);
        }
    }

    private void Submit_Click(object sender, RoutedEventArgs e)
    {
        _controller?.Start();
    }

    private void AnswerButton_Click(object sender, RoutedEventArgs e)
    {
        bool areSame = (string?)((Button)sender)?.Tag == "True";
        _controller?.SetAnswer(areSame);
    }

    private void NextBlock_Click(object sender, RoutedEventArgs e)
    {
        _controller?.Continue();
    }
}
