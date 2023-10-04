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
                var canvas = new Canvas()
                {
                    Background = Brushes.Gray
                };

                plot.Content = canvas;
                plot.Loaded += (s, e) =>
                {

                    Rect rc = new Rect();
                    if (values2 is null)
                        rc = DrawPlot(canvas, cols, rows, values1);
                    else if (values1.Length == values2.Length)
                        rc = DrawBlandAltman(canvas, values1, values2);

                    CreateAxis(canvas, rc);
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

        private static void CreateAxis(Canvas canvas, Rect rc)
        {
            int offset = 2;
            int lbOffset = 25;
            double width = canvas.ActualWidth;
            double height = canvas.ActualHeight;

            var xMin = new Label() { Content = rc.Left.ToString("F2") };
            canvas.Children.Add(xMin);
            Canvas.SetLeft(xMin, offset + lbOffset);
            Canvas.SetTop(xMin, height - offset - 20);

            var xMax = new Label() { Content = rc.Right.ToString("F2") };
            canvas.Children.Add(xMax);
            Canvas.SetLeft(xMax, width - offset - 40 );
            Canvas.SetTop(xMax, height - offset - 20);

            var yMin = new Label() { Content = rc.Top.ToString("F2") };
            canvas.Children.Add(yMin);
            Canvas.SetLeft(yMin, offset);
            Canvas.SetTop(yMin, height - offset - lbOffset - 20);

            var yMax = new Label() { Content = rc.Bottom.ToString("F2") };
            canvas.Children.Add(yMax);
            Canvas.SetLeft(yMax, offset);
            Canvas.SetTop(yMax, offset);

            var xAxe = new Line()
            {
                X1 = 0,
                Y1 = height * rc.Bottom / rc.Height,
                X2 = width,
                Y2 = height * rc.Bottom / rc.Height,
                Stroke = Brushes.Black,
                StrokeThickness = 2
            };
            canvas.Children.Add(xAxe);

            var yAxe = new Line()
            {
                X1 = width * -rc.Left / rc.Width,
                Y1 = 0,
                X2 = width * -rc.Left / rc.Width,
                Y2 = height,
                Stroke = Brushes.Black,
                StrokeThickness = 2
            };
            canvas.Children.Add(yAxe);
        }

        private static Rect DrawPlot(Canvas canvas, int cols, int rows, float[] values)
        {
            double colSize = canvas.ActualWidth / cols;
            double rowSize = canvas.ActualHeight / rows;

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

            return new Rect(new Point(0, 0), new Point(cols, rows));
        }

        private static Rect DrawDiff(Canvas canvas, int cols, int rows, float[] values1, float[] values2)
        {
            double colSize = canvas.ActualWidth / cols;
            double rowSize = canvas.ActualHeight / rows;

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
                    Canvas.SetLeft(pixel, col * cols);
                    Canvas.SetTop(pixel, row * rows);
                }
            }

            return new Rect(new Point(0, 0), new Point(colSize, rowSize));
        }

        private static Rect DrawBlandAltman(Canvas canvas, float[] values1, float[] values2)
        {
            float[] valuesX = new float[values1.Length];
            float[] valuesY = new float[values1.Length];
            for (int i = 0; i < values1.Length; i++)
            {
                valuesX[i] = (values1[i] + values2[i]) / 2;
                valuesY[i] = values1[i] - values2[i];
            }

            var minValueX = valuesX.Min();
            var maxValueX = valuesX.Max();
            var valueRangeX = maxValueX - minValueX;

            var minValueY = valuesY.Min();
            var maxValueY = valuesY.Max();
            var valueRangeY = maxValueY - minValueY;

            for (int i = 0; i < values1.Length; i++)
            {
                var x = (valuesX[i] - minValueX) * canvas.ActualWidth / valueRangeX;
                var y = (valuesY[i] - minValueY) * canvas.ActualHeight / valueRangeY;
                var dot = new Ellipse()
                {
                    Width = 3,
                    Height = 3,
                    Fill = Brushes.Blue
                };
                canvas.Children.Add(dot);
                Canvas.SetLeft(dot, x);
                Canvas.SetTop(dot, y);
            }

            return new Rect(new Point(minValueX, minValueY), new Point(maxValueX, maxValueY));
        }
    }
}
