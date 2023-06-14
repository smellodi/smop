using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Smop
{
    /// <summary>
    /// Cross-app storage, mainly used to share the app state
    /// </summary>
    public class Storage : INotifyPropertyChanged
    {
        public static Storage Instance => _instance ??= new();

        public event PropertyChangedEventHandler? PropertyChanged;

        // Variables

        public bool IsDebugging
        {
            get => _isDebugging;
            set
            {
                if (_isDebugging != value)
                {
                    _isDebugging = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDebugging)));
                }
            }
        }

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
            ZoomLevel = Utils.MathExt.Limit(_zoomLevel + ZOOM_STEP, ZOOM_MIN, ZOOM_MAX);
        }

        public void ZoomOut()
        {
            ZoomLevel = Utils.MathExt.Limit(_zoomLevel - ZOOM_STEP, ZOOM_MIN, ZOOM_MAX);
        }

        // Helpers

        public Storage BindVisibilityToDebug(DependencyObject obj)
        {
            var isDebuggingBinding = new Binding(nameof(IsDebugging))
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

        public Storage UnbindVisibilityToDebug(DependencyObject obj)
        {
            BindingOperations.ClearBinding(obj, UIElement.VisibilityProperty);
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

        bool _isDebugging = false;
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
}
