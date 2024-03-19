using LinqStatistics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Smop.Common;

public class Plot
{
    public enum ComparisonOperation { None, Difference, BlandAltman }

    public double PointSize { get; set; } = 4;
    public Color PointColor { get; set; } = Application.Current?.FindResource("ColorDark") is Color color ? color : Color.FromRgb(0x80, 0x45, 0x15);
    public Color CurveColor { get; set; } = Application.Current?.FindResource("ColorLightDarker") is Color color ? color : Color.FromRgb(0xD4, 0x9A, 0x6A);
    public bool UseLogarithmicScaleInBlandAltman { get; set; } = true;

    /// <summary>
    /// Creates a plot and shows it in a separate window running in its own thread.
    /// </summary>
    /// <param name="rows">Number of rows</param>
    /// <param name="cols">Number of columns</param>
    /// <param name="values1">Vector of the DMS data (Intensity top or bottom)</param>
    /// <param name="values2">Second vector of the DMS data (Intensity top or bottom) used for comparison</param>
    /// <param name="compOp">Comparison operation, see <see cref="ComparisonOperation"/></param>
    /// <param name="theme">Coloring theme as a list of {level: Color} record where 
    /// levels are numbers greater starting from 0 and ending by 1 in ascending order
    public void Show(int rows, int cols,
        float[] values1, float[]? values2 = null,
        ComparisonOperation compOp = ComparisonOperation.Difference,
        KeyValuePair<double, Color>[]? theme = null)
    {
        var thread = new Thread(() =>
        {
            var plot = new Window()
            {
                Width = 1200,
                Height = 750
            };
            var canvas = new Canvas()
            {
                Background = Brushes.LightGray,
                Width = plot.Width,
                Height = plot.Height,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            plot.Content = canvas;
            plot.Loaded += (s, e) =>
            {
                Create(canvas, rows, cols, values1, values2, compOp, theme);
                plot.Title = (values2, compOp) switch
                {
                    (null, _) or (_, ComparisonOperation.None) => "Single scan",
                    (_, ComparisonOperation.BlandAltman) => "Bland-Altman",
                    (_, ComparisonOperation.Difference) => "Difference between two scans",
                    _ => throw new Exception($"Operation '{compOp}' is not supported")
                };
            };

            plot.Show();
            plot.Closed += (s, e) => Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);

            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    /// <summary>
    /// Creates a plot and draws it on the provided canvas.
    /// </summary>
    /// <param name="canvas">The canvas to draw the plot</param>
    /// <param name="rows">Number of rows</param>
    /// <param name="cols">Number of columns</param>
    /// <param name="values1">Vector of the DMS data (<see cref="MeasurementData.IntensityTop"/>)</param>
    /// <param name="values2">Second vector of the DMS data (<see cref="MeasurementData.IntensityTop"/>) used for comparison</param>
    /// <param name="compOp">Comparison operation, see <see cref="ComparisonOperation"/></param>
    /// <param name="theme">Coloring theme as a list of {level: <see cref="Color"/>} record 
    /// where levels are numbers starting from 0 and ending by 1 in ascending order
    public void Create(Canvas canvas, int rows, int cols,
        float[] values1,
        float[]? values2 = null,
        ComparisonOperation compOp = ComparisonOperation.Difference,
        KeyValuePair<double, Color>[]? theme = null)
    {
        canvas.Children.Clear();

        PlotColorTheme.CreateTheme(theme);

        var rc = new Rect();
        if (values2 is null)
        {
            if (rows == 1)
                rc = DrawCurve(canvas, cols, values1);
            else
                rc = DrawPlot(canvas, rows, cols, values1);
        }
        else if (values1.Length == values2.Length)
        {
            if (compOp == ComparisonOperation.BlandAltman)
            {
                rc = DrawBlandAltman(canvas, values1, values2);
            }
            else if (compOp == ComparisonOperation.Difference)
            {
                rc = DrawDiff(canvas, rows, cols, values1, values2);
            }
        }

        CreateAxis(canvas, rc);
    }

    // Internal

    private static void CreateAxis(Canvas canvas, Rect rc)
    {
        /*
        double width = canvas.ActualWidth;
        double height = canvas.ActualHeight;

        int offset = 2;
        int lbOffset = 25;

        var shadowEffect = new DropShadowEffect()
        {
            Color = Colors.White,
            ShadowDepth = 0,
        };

        var xMin = new Label() { Content = rc.Left.ToString("0.##"), Effect = shadowEffect };
        canvas.Children.Add(xMin);
        Canvas.SetLeft(xMin, offset + lbOffset);
        Canvas.SetTop(xMin, height - offset - 20);

        var xMax = new Label() { Content = rc.Right.ToString("0.##"), Effect = shadowEffect };
        canvas.Children.Add(xMax);
        Canvas.SetLeft(xMax, width - offset - 40 );
        Canvas.SetTop(xMax, height - offset - 20);

        var yMin = new Label() { Content = rc.Top.ToString("0.##"), Effect = shadowEffect };
        canvas.Children.Add(yMin);
        Canvas.SetLeft(yMin, offset);
        Canvas.SetTop(yMin, height - offset - lbOffset - 20);

        var yMax = new Label() { Content = rc.Bottom.ToString("0.##"), Effect = shadowEffect };
        canvas.Children.Add(yMax);
        Canvas.SetLeft(yMax, offset);
        Canvas.SetTop(yMax, offset);
        */

        /*
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
        }*/
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

    private static Rect DrawPlot(Canvas canvas, int rows, int cols, float[] values)
    {
        double width = canvas.ActualWidth;
        double height = canvas.ActualHeight;

        double colSize = width / cols;
        double rowSize = height / rows;

        double cellWidth = Math.Ceiling(colSize);
        double cellHeight = Math.Ceiling(rowSize);

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
                    Width = cellWidth,
                    Height = cellHeight,
                    Fill = new SolidColorBrush(PlotColorTheme.ValueToColor(value / range)),
                };
                canvas.Children.Add(pixel);
                Canvas.SetLeft(pixel, (int)(x * colSize));
                Canvas.SetTop(pixel, (int)(height - (y + 1) * rowSize));
            }
        }

        return new Rect(new Point(0, 0), new Point(cols, rows));
    }

    private static Rect DrawDiff(Canvas canvas, int rows, int cols, float[] values1, float[] values2)
    {
        float[] values = new float[values1.Length];
        for (int i = 0; i < values1.Length; i++)
            values[i] = values1[i] - values2[i];

        return DrawPlot(canvas, rows, cols, values);
    }

    private Rect DrawBlandAltman(Canvas canvas, float[] values1, float[] values2)
    {
        float[] valuesX = new float[values1.Length];
        float[] valuesY = new float[values1.Length];
        bool useLog = UseLogarithmicScaleInBlandAltman && values1.Min() > 0 && values2.Min() > 0;

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

    private Rect DrawCurve(Canvas canvas, int cols, float[] values)
    {
        double width = canvas.ActualWidth;
        double height = canvas.ActualHeight;

        int count = values.Length;

        var minValue = Math.Min(-10, values.Min());
        var maxValue = Math.Max(100, values.Max());
        var range = maxValue - minValue;

        double xp = 0;
        double yp = height - (values[0] - minValue) / range * height;
        var pathComp = new List<string>() { $"M{xp},{yp}" };

        var brush = new SolidColorBrush(PointColor);

        for (int i = 1; i < cols; i++)
        {
            var x = width * i / (count - 1);
            var y = height - (values[i] - minValue) / range * height;
            pathComp.Add($"L{x},{y}");

            var dot = new Ellipse()
            {
                Width = PointSize,
                Height = PointSize,
                Fill = brush
            };
            canvas.Children.Add(dot);
            Canvas.SetLeft(dot, x - PointSize / 2);
            Canvas.SetTop(dot, y - PointSize / 2);
        }

        var path = new Path()
        {
            Stroke = new SolidColorBrush(CurveColor),
            StrokeThickness = 1,
            Data = Geometry.Parse(string.Join(' ', pathComp)),
            VerticalAlignment = VerticalAlignment.Top
        };

        canvas.Children.Add(path);

        return new Rect(new Point(0, 0), new Point(cols, 1));
    }
}
