using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Test.IonVision
{
    internal class DataPlot
    {
        public static void Show(int cols, int rows, float[] values1, float[]? values2 = null)
        {
            var thread = new Thread(() => {
                var plot = new Window()
                {
                    Title = "DMS data plot",
                    Width = 1200,
                    Height = 750,
                };
                plot.Loaded += (s, e) =>
                {
                    var canvas = new Canvas()
                    {
                        Background = Brushes.Gray
                    };

                    plot.Content = canvas;
                    if (values2 is null)
                        DrawPlot(canvas, plot.ActualWidth, plot.ActualHeight, cols, rows, values1);
                    else if (values1.Length == values2.Length)
                        DrawDiff(canvas, plot.ActualWidth, plot.ActualHeight, cols, rows, values1, values2);
                };

                plot.Show();
                plot.Closed += (s, e) => Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);

                Dispatcher.Run();
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
        }

        // Internal

        private static void DrawPlot(Canvas canvas, double width, double height, int cols, int rows, float[] values)
        {
            double colSize = width / cols;
            double rowSize = height / rows;

            var minValue = values.Min();
            var maxValue = values.Max();
            var valueRange = maxValue - minValue;

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    var value = values[row * cols + col];
                    var pixel = new Rectangle()
                    {
                        Width = colSize,
                        Height = rowSize,
                        Fill = new SolidColorBrush(Color.FromRgb(
                            (byte)Math.Min(0xff, 0xff * (2 * value / valueRange)),
                            (byte)(0xff * (value / valueRange)),
                            0xff
                        ))
                    };
                    canvas.Children.Add(pixel);
                    Canvas.SetLeft(pixel, col * colSize);
                    Canvas.SetTop(pixel, row * rowSize);
                }
            }
        }

        private static void DrawDiff(Canvas canvas, double width, double height, int cols, int rows, float[] values1, float[] values2)
        {
            double colSize = width / cols;
            double rowSize = height / rows;

            float[] values = new float[values1.Length];
            for (int i = 0; i < values1.Length; i++)
                values[i] = values1[i] - values2[i];

            var minValue = values.Min();
            var maxValue = values.Max();
            var valueRange = maxValue - minValue;

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    var value = values[row * cols + col] - minValue;
                    var pixel = new Rectangle()
                    {
                        Width = colSize,
                        Height = rowSize,
                        Fill = new SolidColorBrush(Color.FromRgb(
                            (byte)Math.Min(0xff, 0xff * (2 * value / valueRange)),
                            (byte)(0xff * (value / valueRange)),
                            0xff
                        ))
                    };
                    canvas.Children.Add(pixel);
                    Canvas.SetLeft(pixel, col * colSize);
                    Canvas.SetTop(pixel, row * rowSize);
                }
            }
        }
    }
}
