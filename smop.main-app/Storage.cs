using Smop.MainApp.Pages;
using Smop.MainApp.Utils.Extensions;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Smop.MainApp;

[Flags]
public enum SimulationTarget
{
    Nothing = 0,
    OdorDisplay = 1,
    SmellInspector = 2,
    IonVision = 4,
    ML = 8,
    All = 15
}

public enum SetupType { Undefined, PulseGenerator, OdorReproduction }

/// <summary>
/// Cross-app storage, mainly used to share the app state
/// </summary>
public class Storage : INotifyPropertyChanged
{
    public static Storage Instance => _instance ??= new();

    public event PropertyChangedEventHandler? PropertyChanged;

    // Variables

    public SetupType SetupType
    {
        get => _setupType;
        set
        {
            _setupType = value;
            _setupPage = _setupType switch
            {
                SetupType.OdorReproduction => Navigation.OdorReproductionSetup,
                SetupType.PulseGenerator => Navigation.PulseGeneratorSetup,
                _ => throw new Exception($"Setup type {_setupType} is not supported")
            };
        }
    }

    public Navigation SetupPage
    {
        get => _setupPage;
        set
        {
            _setupPage = value;
            _setupType = _setupPage switch
            {
                Navigation.OdorReproductionSetup => SetupType.OdorReproduction,
                Navigation.PulseGeneratorSetup => SetupType.PulseGenerator,
                _ => throw new Exception($"Setup type {_setupPage} is not supported")
            };
        }
    }

    public SimulationTarget Simulating { get; private set; } = SimulationTarget.Nothing;

    public bool HasSimulatingComponents => Simulating != SimulationTarget.Nothing;

    public double ZoomLevel
    {
        get => _zoomLevel;
        set
        {
            if (_zoomLevel != value)
            {
                _zoomLevel = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ZoomLevel)));
                Save();
            }
        }
    }

    // Actions

    public void ZoomIn()
    {
        ZoomLevel = MathExt.Limit(_zoomLevel + ZOOM_STEP, ZOOM_MIN, ZOOM_MAX);
    }

    public void ZoomOut()
    {
        ZoomLevel = MathExt.Limit(_zoomLevel - ZOOM_STEP, ZOOM_MIN, ZOOM_MAX);
    }

    public void AddSimulatingTarget(SimulationTarget target)
    {
        if (!Simulating.HasFlag(target))
        {
            Simulating |= target;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasSimulatingComponents)));
        }
    }

    // Helpers

    public Storage BindVisibilityToDebug(DependencyObject obj)
    {
        var isDebuggingBinding = new Binding(nameof(HasSimulatingComponents))
        {
            Source = this,
            Converter = new BooleanToVisibilityConverter()
        };

        BindingOperations.SetBinding(obj, UIElement.VisibilityProperty, isDebuggingBinding);

        return this;
    }

    public Storage BindScaleToZoomLevel(DependencyObject obj)
    {
        var zoomLevelBinding = new Binding(nameof(ZoomLevel))
        {
            Source = this
        };

        BindingOperations.SetBinding(obj, ScaleTransform.ScaleXProperty, zoomLevelBinding);
        BindingOperations.SetBinding(obj, ScaleTransform.ScaleYProperty, zoomLevelBinding);

        return this;
    }

    public Storage BindContentToZoomLevel(DependencyObject obj)
    {
        var zoomValueBinding = new Binding(nameof(ZoomLevel))
        {
            Source = this,
            Converter = new Utils.ZoomToPercentageConverter()
        };

        BindingOperations.SetBinding(obj, ContentControl.ContentProperty, zoomValueBinding);

        return this;
    }

    public Storage UnbindVisibilityToDebug(DependencyObject obj)
    {
        BindingOperations.ClearBinding(obj, UIElement.VisibilityProperty);
        return this;
    }

    public Storage UnbindContentToZoomLevel(DependencyObject obj)
    {
        BindingOperations.ClearBinding(obj, ContentControl.ContentProperty);
        return this;
    }

    public Storage UnbindScaleToZoomLevel(DependencyObject obj)
    {
        BindingOperations.ClearBinding(obj, ScaleTransform.ScaleXProperty);
        BindingOperations.ClearBinding(obj, ScaleTransform.ScaleYProperty);

        return this;
    }


    // Internal

    static Storage? _instance;

    const double ZOOM_MIN = 0.8;
    const double ZOOM_MAX = 3.0;
    const double ZOOM_STEP = 0.1;

    SetupType _setupType = SetupType.Undefined;
    Navigation _setupPage = Navigation.Exit;
    double _zoomLevel;

    private Storage()
    {
        var settings = Properties.Settings.Default;

        _zoomLevel = settings.App_ZoomLevel;
    }

    private void Save()
    {
        var settings = Properties.Settings.Default;
        settings.App_ZoomLevel = _zoomLevel;
        settings.Save();
    }
}
