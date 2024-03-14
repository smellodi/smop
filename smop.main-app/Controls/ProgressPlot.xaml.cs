using ScottPlot;
using System.Collections.Generic;
using System.Windows.Controls;

namespace Smop.MainApp.Controls;

public partial class ProgressPlot : UserControl
{
    public ProgressPlot()
    {
        InitializeComponent();

        var color = (System.Windows.Media.Color)FindResource("ColorLight");
        chart.Plot.Style(
            figureBackground: System.Drawing.Color.Gray,
            dataBackground: System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B));
        chart.Plot.Margins(x: .1, y: .1);
        chart.Plot.ManualDataArea(new PixelPadding(1, 1, 1, 1));

        _scatter = new ScottPlot.Plottable.ScatterPlot([0], [0])
        {
            Color = LINE_COLOR,
            MarkerSize = 2f
        };

        chart.Plot.Add(_scatter);
        chart.Plot.Grid(false);
        chart.Plot.Frameless(false);

        chart.Plot.XAxis.SetSizeLimit(0, 0, 0);
        chart.Plot.XAxis.Line(false);
        chart.Plot.XAxis.Ticks(false);
        chart.Plot.YAxis.SetSizeLimit(0, 0, 0);
        chart.Plot.YAxis.Line(false);
        chart.Plot.YAxis.Ticks(false);

        chart.Plot.XAxis2.Hide();
        chart.Plot.YAxis2.Hide();
    }

    public void Reset()
    {
        _data.Clear();

        _scatter.Update([0], [0]);

        chart.Plot.AxisAuto();
        chart.Render();
    }

    public void Add(double value)
    {
        _data.Add(new MeasureModel { ID = _data.Count, Value = value });

        var x = new List<double>();
        var y = new List<double>();

        foreach (var point in _data)
        {
            x.Add(point.ID);
            y.Add(point.Value);
        }

        _scatter.Update(x.ToArray(), y.ToArray());

        chart.Plot.AxisAuto();
        chart.Render();
    }


    // Internal 

    private class MeasureModel
    {
        public int ID { get; set; }
        public double Value { get; set; }
    }

    static readonly System.Drawing.Color LINE_COLOR = System.Drawing.Color.FromArgb(16, 160, 255);

    readonly List<MeasureModel> _data = new();
    readonly ScottPlot.Plottable.ScatterPlot _scatter;
}
