﻿using Smop.Common;
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
    public bool CanRate => _hasSniffed;
    public bool CanSubmit => IsInstruction || (_hasSniffed && _stage != Stage.SniffingMixture && HasRatings);
    public string StageInfo => $"Odor #{_controller?.MixtureId}";
    public string SubmitButtonText => _stage == Stage.Initial ? "Continue" : "Submit";

    public event EventHandler<Navigation>? Next;
    public event PropertyChangedEventHandler? PropertyChanged;

    public HumanTestRating()
    {
        InitializeComponent();

        DataContext = this;
        Name = "HumanTestsRating";

        ((App)Application.Current).AddCleanupAction(CleanUp);
    }

    public void Start(Settings settings)
    {
        try
        {
            _controller = new RatingController(settings);
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

    RatingController? _controller = null;

    Stage _stage = Stage.Initial;
    bool _hasSniffed = false;

    DispatchOnce? _delayedAction = null;

    private void CleanUp()
    {
        _delayedAction?.Close();
        _delayedAction = null;

        _controller?.Stop();
        _controller?.Dispose();
        _controller = null;
    }

    private void SetStage(Stage stage)
    {
        _stage = stage;

        if (_stage == Stage.WaitingMixture)
        {
            wtiWaiting.Text = "Wait";
            wtiWaiting.Start(Mixture.WaitingInterval);

            ClearRatingButtons();
            EnableRatingButtons(false);

            _hasSniffed = false;
        }
        else if (stage == Stage.Ready)
        {
            wtiWaiting.Text = "Ready to release the odor";
            wtiWaiting.Reset();
        }

        else if (stage == Stage.SniffingMixture)
        {
            wtiWaiting.Text = "Sniff the oder, then select matched descriptions";
            wtiWaiting.Start(Mixture.SniffingInterval);

            if (!_hasSniffed)
            {
                EnableRatingButtons(true);
            }

            _hasSniffed = true;
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsInstruction)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanReleaseOdor)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanRate)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanSubmit)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StageInfo)));
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

    private string[] GetRatings() =>
        grdRatingButtons.Children
            .OfType<ToggleButton>()
            .Where(tbn => tbn.IsChecked == true)
            .Select(tbn => (string)tbn.Content)
            .ToArray();

    private bool HasRatings =>
        grdRatingButtons.Children
            .OfType<ToggleButton>()
            .Where(tbn => tbn.IsChecked == true)
            .Count() > 0;

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
