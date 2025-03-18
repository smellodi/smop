using Smop.MainApp.Controllers.HumanTests;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Smop.MainApp.Pages;

public partial class HumanTestRating : Page, IPage<Navigation>, IDisposable, INotifyPropertyChanged
{
    public bool IsInstruction => _stage == Stage.Initial;
    public bool CanReleaseOdor => _stage == Stage.Ready;
    public bool CanRate { get; private set; } = false;
    public bool CanSubmit => IsInstruction || (CanRate && _stage != Stage.SniffingMixture && HasRatings);
    public string StageInfo => $"Odor #{_controller?.MixtureId}";
    public string InstructionText => _stage switch
    {
        Stage.WaitingMixture => "Wait",
        Stage.Ready => CanRate ? "Select the matching descriptions" : "Ready to release the odor",
        Stage.SniffingMixture => "Sniff",
        _ => ""
    };
    public string SubmitButtonText => _stage == Stage.Initial ? "Continue" : "Submit";

    public event EventHandler<Navigation>? Next;
    public event PropertyChangedEventHandler? PropertyChanged;

    public HumanTestRating()
    {
        InitializeComponent();

        Name = "HumanTestsRating";

        ((App)Application.Current).AddCleanupAction(CleanUp);
    }

    public void Start(Settings settings)
    {
        try
        {
            CreateRatingControls(RatingWords.Get(settings.Language));

            _controller = new RatingController(settings);
            _controller.StageChanged += (s, e) => Dispatcher.Invoke(() => SetStage(e.Stage));
        }
        catch (Exception ex)
        {
            MessageBox.Show("Failed to initialize the test. Make sure the following odors are loaded:\nCyclohexanone, Limonene, Cytronellyl acetate" +
                $"\n\nInternal info: {ex.Message}",
                "Human Tests", MessageBoxButton.OK, MessageBoxImage.Error);
            Next?.Invoke(this, Navigation.Setup);
        }
    }

    public void Dispose()
    {
        CleanUp();
        GC.SuppressFinalize(this);
    }


    // Internal

    const int RatingButtonsPerRow = 4;

    RatingController? _controller = null;

    Stage _stage = Stage.Initial;

    private void CreateRatingControls(string[] words)
    {
        int row = 0;
        int col = 0;

        var style = (Style)FindResource("Rating");

        foreach (var word in words)
        {
            var control = new ToggleButton()
            {
                Style = style,
                Content = word,
            };
            control.Click += RatingButton_Click;

            if (col == RatingButtonsPerRow)
            {
                row += 1;
                col = Math.Max(0, RatingButtonsPerRow - (words.Length - row * RatingButtonsPerRow)) / 2;
            }

            Grid.SetRow(control, row);
            Grid.SetColumn(control, col);

            col += 1;

            grdRatingButtons.Children.Add(control);
        }
    }

    private void CleanUp()
    {
        _controller?.Stop();
        _controller?.Dispose();
        _controller = null;
    }

    private void SetStage(Stage stage)
    {
        _stage = stage;

        wtiWaiting.Text = InstructionText;

        if (_stage == Stage.WaitingMixture)
        {
            wtiWaiting.Start(_controller?.Settings.WaitingInterval ?? 0);

            ClearRatingButtons();
            EnableRatingButtons(false);

            CanRate = false;
        }
        else if (stage == Stage.Ready)
        {
            wtiWaiting.Reset();
        }

        else if (stage == Stage.SniffingMixture)
        {
            wtiWaiting.Start(_controller?.Settings.SniffingInterval ?? 0);

            if (!CanRate)
            {
                EnableRatingButtons(true);
            }

            CanRate = true;
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsInstruction)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanReleaseOdor)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanRate)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanSubmit)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StageInfo)));
        //PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InstructionText)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SubmitButtonText)));
    }

    private void ClearRatingButtons()
    {
        foreach (ToggleButton btn in grdRatingButtons.Children.OfType<ToggleButton>())
        {
            btn.IsChecked = false;
        }
    }

    private void EnableRatingButtons(bool areEnabled)
    {
        foreach (ToggleButton btn in grdRatingButtons.Children.OfType<ToggleButton>())
        {
            btn.IsEnabled = areEnabled;
        }
    }

    private string[] GetRatings() => grdRatingButtons.Children
            .OfType<ToggleButton>()
            .Where(tbn => tbn.IsChecked == true)
            .Select(tbn => (string)tbn.Content)
            .ToArray();

    private bool HasRatings => grdRatingButtons.Children
            .OfType<ToggleButton>()
            .Where(tbn => tbn.IsChecked == true)
            .Any();

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
        if (_stage == Stage.Initial)
        {
            _controller?.Start();
        }
        else
        {
            _controller?.SetAnswers(GetRatings());
            
            if (_controller?.Continue() != true)
            {
                CleanUp();
                Next?.Invoke(this, Navigation.Finished);
            }
        }
    }

    private void ReleaseOdor_Click(object sender, RoutedEventArgs e)
    {
        _controller?.ReleaseOdor();
    }

    private void RatingButton_Click(object sender, RoutedEventArgs e)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanSubmit)));
    }
}
