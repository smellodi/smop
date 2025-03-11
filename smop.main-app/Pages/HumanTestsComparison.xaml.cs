using Smop.MainApp.Controllers.HumanTests;
using Smop.MainApp.Controls;
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
    public Brush Mixture1Color => _controller?.MixtureID == 1 ? 
        (_stage == Stage.SniffingMixture ? ODOR_BRUSH_SNIFFING : 
            (_stage == Stage.Question ? ODOR_BRUSH_READY : ODOR_BRUSH_INACTIVE)) :
        ODOR_BRUSH_READY;
    public Brush Mixture2Color => _controller?.MixtureID == 2 ?
        (_stage == Stage.SniffingMixture ? ODOR_BRUSH_SNIFFING :
            (_stage == Stage.Question ? ODOR_BRUSH_READY : ODOR_BRUSH_INACTIVE)) :
        (_controller?.MixtureID == 1 ? ODOR_BRUSH_INACTIVE : ODOR_BRUSH_READY);
    public string StageInfo => $"Block {_controller?.BlockID}, Comparison {_controller?.ComparisonID}";

    public Settings? Settings { get; private set; }

    public event EventHandler<Navigation>? Next;
    public event PropertyChangedEventHandler? PropertyChanged;

    public HumanTestComparison()
    {
        InitializeComponent();

        DataContext = this;
        Name = "HumanTestsComparison";

        ((App)Application.Current).AddCleanupAction(CleanUp);
    }

    public void Start(Settings settings)
    {
        Settings = settings;

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

    readonly Brush ODOR_BRUSH_INACTIVE = Brushes.LightGray;
    readonly Brush ODOR_BRUSH_SNIFFING = new SolidColorBrush(Color.FromRgb(0, 0xA0, 0));
    readonly Brush ODOR_BRUSH_READY = new SolidColorBrush(Color.FromRgb(0, 0x40, 0));

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
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Mixture1Color)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Mixture2Color)));

        if (stage == Stage.WaitingMixture)
        {
            wtiWaiting.Text = "Wait";
            wtiWaiting.Start(Mixture.WaitingInterval);
        }
        else if (stage == Stage.SniffingMixture)
        {
            wtiWaiting.Text = "Sniff";
            wtiWaiting.Start(Mixture.SniffingInterval);
        }
        else if (stage == Stage.Question)
        {
            wtiWaiting.Text = " ";
            wtiWaiting.Reset();
            lblWaitingTime.Content = " ";
        }
        else if (stage == Stage.Finished)
        {
            CleanUp();
            if (Settings?.IsPracticingProcedure == true)
            {
                Next?.Invoke(this, Navigation.Finished);
            }
            else
            {
                Next?.Invoke(this, Navigation.Test);
            }
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

    private void Waiting_TimeUpdated(object sender, WaitingInstruction.TimeUpdatedEventArgs e)
    {
        /*
        double remainingTime = wtiWaiting.WaitingTime - e.Duration;
        if (remainingTime > 0)
            lblWaitingTime.Content = (wtiWaiting.WaitingTime - e.Duration).ToTime(wtiWaiting.WaitingTime);
        else
            lblWaitingTime.Content = " ";
        */
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
}
