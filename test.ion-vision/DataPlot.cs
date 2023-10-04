using LinqStatistics;
using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Test.IonVision;

internal static class DataPlot
{
    public enum ComparisonOperation { None, Difference, BlandAltman }
    public static ComparisonOperation OperationWith2Sets { get; set; } = ComparisonOperation.BlandAltman;
    public static bool LogInBlandAltman { get; set; } = true;
    public static void Show(int cols, int rows, float[] values1, float[]? values2 = null)
    {
        var thread = new Thread(() => {
            var plot = new Window()
            {
                Width = 1200,
                Height = 750
            };
            var canvas = new Canvas()
            {
                Background = Brushes.LightGray
            };

            plot.Content = canvas;
            plot.Loaded += (s, e) =>
            {

                Rect rc = new Rect();
                if (values2 is null)
                {
                    rc = DrawPlot(canvas, cols, rows, values1);
                    plot.Title = "Data plot";
                }
                else if (values1.Length == values2.Length)
                {
                    if (OperationWith2Sets == ComparisonOperation.BlandAltman)
                    {
                        rc = DrawBlandAltman(canvas, values1, values2);
                        plot.Title = "Bland-Altman plot";
                    }
                    else if (OperationWith2Sets == ComparisonOperation.Difference)
                    {
                        rc = DrawDiff(canvas, cols, rows, values1, values2);
                        plot.Title = "Data difference plot";
                    }
                }

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

        var shadowEffect = new DropShadowEffect()
        {
            Color = Colors.White,
            ShadowDepth = 0,
        };

        var xMin = new Label() { Content = rc.Left.ToString("F2"), Effect = shadowEffect };
        canvas.Children.Add(xMin);
        Canvas.SetLeft(xMin, offset + lbOffset);
        Canvas.SetTop(xMin, height - offset - 20);

        var xMax = new Label() { Content = rc.Right.ToString("F2"), Effect = shadowEffect };
        canvas.Children.Add(xMax);
        Canvas.SetLeft(xMax, width - offset - 40 );
        Canvas.SetTop(xMax, height - offset - 20);

        var yMin = new Label() { Content = rc.Top.ToString("F2"), Effect = shadowEffect };
        canvas.Children.Add(yMin);
        Canvas.SetLeft(yMin, offset);
        Canvas.SetTop(yMin, height - offset - lbOffset - 20);

        var yMax = new Label() { Content = rc.Bottom.ToString("F2"), Effect = shadowEffect };
        canvas.Children.Add(yMax);
        Canvas.SetLeft(yMax, offset);
        Canvas.SetTop(yMax, offset);

        if (rc.Height != 0)
        {
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
    }

    private static void CreateStdLinesY(Canvas canvas, float[] values)
    {
        double width = canvas.ActualWidth;
        double height = canvas.ActualHeight;

        var minValue = values.Min();
        var maxValue = values.Max();
        var range = maxValue - minValue;

        var mean = values.Average();
        var std = values.StandardDeviation();

        var upperStd = new Line()
        {
            X1 = 0,
            Y1 = height * ((mean - 1.96 * std - minValue) / range),
            X2 = width,
            Y2 = height * ((mean - 1.96 * std - minValue) / range),
            Stroke = Brushes.Red,
            StrokeDashArray = new() { 5, 5 }
        };
        canvas.Children.Add(upperStd);

        var lowerStd = new Line()
        {
            X1 = 0,
            Y1 = height * ((mean + 1.96 * std - minValue) / range),
            X2 = width,
            Y2 = height * ((mean + 1.96 * std - minValue) / range),
            Stroke = Brushes.Red,
            StrokeDashArray = new() { 5, 5 }
        };
        canvas.Children.Add(lowerStd);
    }

    private static Rect DrawPlot(Canvas canvas, int cols, int rows, float[] values)
    {
        double width = canvas.ActualWidth;
        double height = canvas.ActualHeight;

        double colSize = width / cols;
        double rowSize = height / rows;

        var minValue = values.Min();
        var maxValue = values.Max();
        var range = maxValue - minValue;

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                var value = values[y * cols + x] - minValue;
                var pixel = new Rectangle()
                {
                    Width = colSize,
                    Height = rowSize,
                    Fill = new SolidColorBrush(ValueToColor(value / range))
                };
                canvas.Children.Add(pixel);
                Canvas.SetLeft(pixel, x * colSize);
                Canvas.SetTop(pixel, height - (y + 1) * rowSize);
            }
        }

        return new Rect(new Point(0, 0), new Point(cols, rows));
    }

    private static Rect DrawDiff(Canvas canvas, int cols, int rows, float[] values1, float[] values2)
    {
        double width = canvas.ActualWidth;
        double height = canvas.ActualHeight;

        double colSize = width / cols;
        double rowSize = height / rows;

        float[] values = new float[values1.Length];
        for (int i = 0; i < values1.Length; i++)
            values[i] = values1[i] - values2[i];

        var minValue = values.Min();
        var maxValue = values.Max();
        var range = maxValue - minValue;

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                var value = values[y * cols + x] - minValue;
                var pixel = new Rectangle()
                {
                    Width = colSize,
                    Height = rowSize,
                    Fill = new SolidColorBrush(ValueToColor(value))
                };
                canvas.Children.Add(pixel);
                Canvas.SetLeft(pixel, x * colSize);
                Canvas.SetTop(pixel, height - (y + 1) * rowSize);
            }
        }

        return new Rect(new Point(0, 0), new Point(colSize, rowSize));
    }

    private static Rect DrawBlandAltman(Canvas canvas, float[] values1, float[] values2)
    {
        float[] valuesX = new float[values1.Length];
        float[] valuesY = new float[values1.Length];
        bool useLog = LogInBlandAltman && values1.Min() > 0 && values2.Min() > 0;

        for (int i = 0; i < values1.Length; i++)
        {
            var val1 = useLog ? Math.Log2(values1[i]) : values1[i];
            var val2 = useLog ? Math.Log2(values2[i]) : values2[i];
            valuesX[i] = (float)((val1 + val2) / 2);
            valuesY[i] = (float)(val1 - val2);
        }

        var minValueX = valuesX.Min();
        var maxValueX = valuesX.Max();
        var rangeX = maxValueX - minValueX;

        var minValueY = valuesY.Min();
        var maxValueY = valuesY.Max();
        var rangeY = maxValueY - minValueY;

        for (int i = 0; i < values1.Length; i++)
        {
            var x = (valuesX[i] - minValueX) * canvas.ActualWidth / rangeX;
            var y = (valuesY[i] - minValueY) * canvas.ActualHeight / rangeY;
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

        CreateStdLinesY(canvas, valuesY);

        return new Rect(new Point(minValueX, minValueY), new Point(maxValueX, maxValueY));
    }

    const int COLOR_LEVEL_COUNT = 5;

    /// <summary>
    /// Creates a color from value
    /// </summary>
    /// <param name="value">0..1</param>
    /// <returns>Color</returns>
    private static Color ValueToColor(double value)
    {
        byte r = (byte)Math.Min(0xff, value switch
        {
            < 0.2 => 0,
            < 0.4 => 0,
            < 0.6 => 0xff * (value - 0.4) * COLOR_LEVEL_COUNT,
            < 0.8 => 0xff,
            _ => 0xff
        });
        byte g = (byte)Math.Min(0xff, value switch
        {
            < 0.2 => 0xff * value * COLOR_LEVEL_COUNT,
            < 0.4 => 0xff,
            < 0.6 => 0xff,
            < 0.8 => 0xff * (0.8 - value) * COLOR_LEVEL_COUNT,
            _ => 0xff * (value - 0.8) * COLOR_LEVEL_COUNT
        });
        byte b = (byte)Math.Min(0xff, value switch
        {
            < 0.2 => 0xff,
            < 0.4 => 0xff * (0.4 - value) * COLOR_LEVEL_COUNT,
            < 0.6 => 0,
            < 0.8 => 0,
            _ => 0xff * (value - 0.8) * COLOR_LEVEL_COUNT
        });
        return Color.FromRgb(r, g, b);
    }
}
