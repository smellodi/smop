using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Test.IonVision
{
    internal class DataPlot
    {
        public static void Show(int cols, int rows, float[] values)
        {
            Thread thread = new Thread(() => {
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
                    Draw(canvas, plot.ActualWidth, plot.ActualHeight, cols, rows, values);
                };

                var app = new Application();
                app.Run(plot);
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
        }

        private static void Draw(Canvas canvas, double width, double height, int cols, int rows, float[] values)
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
    }
}
