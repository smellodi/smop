using System.Collections.Generic;
using System.Windows.Controls;

namespace Smop.MainApp.Controls;

public partial class ProgressPlot : UserControl
{
    public class MeasureModel
    {
        public int ID { get; set; }
        public double Value { get; set; }
    }

    public ProgressPlot()
    {
        InitializeComponent();

        _scatter = new ScottPlot.Plottable.ScatterPlot(new double[] { 0 }, new double[] { 0 })
        {
            Color = LINE_COLOR,
            MarkerSize = 2f
        };

        chart.Plot.Add(_scatter);

        chart.Plot.XAxis.SetSizeLimit(0, 0, 0);
        chart.Plot.XAxis.Line(false);
        chart.Plot.YAxis.SetSizeLimit(0, 0, 0);
        chart.Plot.YAxis.Line(false);

        chart.Plot.XAxis2.Hide();
        chart.Plot.YAxis2.Hide();
    }

    public void Reset()
    {
        _data.Clear();

        _scatter.Update(new double[] { 0 }, new double[] { 0 });

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

    static readonly System.Drawing.Color LINE_COLOR = System.Drawing.Color.FromArgb(16, 160, 255);

    readonly List<MeasureModel> _data = new();
    readonly ScottPlot.Plottable.ScatterPlot _scatter;
}
