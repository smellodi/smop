using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SMOP.Utils
{
    /// <summary>
    /// Makes scroll-viewers scrollable by click-and-drag with mouse (or finger on a touchscreen)
    /// </summary>
    class MouseDragScroll
    {
        public static void InstallTo(ScrollViewer element) => new MouseDragScroll(element);

        // Internal

        ScrollViewer _element;
        Point _scrollMousePoint = new Point();
        double _offset = 0;

        private MouseDragScroll(ScrollViewer element)
        {
            _element = element;
            _element.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            _element.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
            _element.PreviewMouseMove += OnPreviewMouseMove;
        }

        private void OnPreviewMouseLeftButtonDown(object? sender, MouseButtonEventArgs e)
        {
            bool IsInput(object? el) => el is RadioButton || el is CheckBox || el is TextBox;

            if (IsInput(e.OriginalSource) || IsInput((e.OriginalSource as Border)?.TemplatedParent))
            {
                return;
            }

            _scrollMousePoint = e.GetPosition(_element);
            _offset = _element.VerticalOffset;
            _element.CaptureMouse();
        }

        private void OnPreviewMouseLeftButtonUp(object? sender, MouseButtonEventArgs e)
        {
            _element.ReleaseMouseCapture();
        }

        private void OnPreviewMouseMove(object? sender, MouseEventArgs e)
        {
            if (_element.IsMouseCaptured)
            {
                var d = _scrollMousePoint.Y - e.GetPosition(_element).Y;
                _element.ScrollToVerticalOffset(_offset + d);
            }
        }
    }
}
