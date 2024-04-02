using ScottPlot;
using System;
using System.Windows.Controls;

namespace Smop.MainApp.Controls;

public partial class SearchSpace2D : UserControl
{
    /// <summary>
    /// RMSE above this threshold is shown as the possibly smallest dot.
    /// RMSE below this threshold starts growing in size.
    /// </summary>
    public float RmseThreshold { get; set; } = 10;
    public float BubbleMinRadius { get; set; } = 1.5f;
    public float BubbleMaxRadius { get; set; } = 5f;

    public SearchSpace2D()
    {
        InitializeComponent();

        ScottPlot.Version.ShouldBe(4, 1, 71);

        chart.Plot.Frameless(false);

        var color = (System.Windows.Media.Color)FindResource("ColorLight");
        chart.Plot.Style(
            figureBackground: System.Drawing.Color.Gray,
            dataBackground: System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B));
        chart.Plot.SetAxisLimits(-5, 55, -5, 55);
        chart.Plot.Margins(x: .1, y: .1);
        chart.Plot.ManualDataArea(new PixelPadding(1, 1, 1, 1));
        chart.Plot.XAxis.LockLimits(true);
        chart.Plot.YAxis.LockLimits(true);

        _bubbles = chart.Plot.AddBubblePlot();

        chart.Plot.XAxis.IsVisible = false;
        chart.Plot.YAxis.IsVisible = false;

        chart.Plot.XAxis2.Hide();
        chart.Plot.YAxis2.Hide();
    }

    public void Reset()
    {
        _bubbles.Clear();

        chart.Plot.AxisAuto();
        chart.Render();
    }

    public void Add(float rmse, float[] flows, System.Drawing.Color? color = null)
    {
        if (flows.Length < 2)
            return;

        // Limitation: only two odors
        Add(rmse, flows[0], flows[1], color);
    }


    // Internal 

    static readonly System.Drawing.Color DOT_COLOR = System.Drawing.Color.FromArgb(16, 160, 255);

    readonly ScottPlot.Plottable.BubblePlot _bubbles;

    private void Add(float rmse, float flow1, float flow2, System.Drawing.Color? color = null)
    {
        var weight = Math.Pow(rmse < RmseThreshold ? (RmseThreshold - rmse) / RmseThreshold : 0, 1.5);
        var radius = rmse >= RmseThreshold ? BubbleMinRadius :
            BubbleMinRadius + (BubbleMaxRadius - BubbleMinRadius) * weight;

        var dotColor = color ?? DOT_COLOR;
        _bubbles.Add(flow1, flow2, radius, dotColor, 0, System.Drawing.Color.Transparent);

        chart.Plot.AxisAuto();
        chart.Render();
    }
}
