using System.Collections.Generic;
using System.Windows.Controls;

namespace Smop.PulseGen.Controls;

public partial class LiveData : UserControl
{
    public class MeasureModel
    {
        public double Timestamp { get; set; }
        public double Value { get; set; }
    }

	public double Step
	{
		get => _step;
		set
		{
			if (value != _step)
			{
				_step = value;
				if (_data.Count > 0)
				{
					FillWith(_lastValue);
				}
			}
		}
	}

	public LiveData()
	{
		InitializeComponent();

		_scatter = new ScottPlot.Plottable.ScatterPlot(new double[] { 0 }, new double[] { 0 })
		{
			Color = LINE_COLOR,
			MarkerSize = 4f
		};

		chart.Plot.Add(_scatter);
		chart.Plot.XAxis.Color(AXIS_COLOR);
		chart.Plot.YAxis.Color(AXIS_COLOR);

		chart.Plot.XAxis.TickLabelStyle(fontSize: 10);
		chart.Plot.XAxis.SetSizeLimit(10, 20, 0);
		chart.Plot.XAxis.Line(false);
		chart.Plot.XAxis.Ticks(true, false, true);
		chart.Plot.YAxis.TickLabelStyle(fontSize: 10);
		chart.Plot.YAxis.SetSizeLimit(10, 30, 0);
		chart.Plot.YAxis.Line(false);

		chart.Plot.XAxis2.Hide();
		chart.Plot.YAxis2.Hide();
	}

	public void Empty()
	{
		_data.Clear();
		
		_scatter.Update(new double[] { 0 }, new double[] { 0 });
		
		chart.Plot.AxisAuto();
		chart.Render();
	}

	public void Reset(double step, double baseline = 0)
	{
		_data.Clear();

		Step = step;

		FillWith(baseline);

		Data2XY(out double[] x, out double[] y);

		_scatter.Update(x, y);

		chart.Plot.AxisAuto();
		chart.Render();
	}

	public void Add(double timestamp, double value)
	{
		var maxPointCount = ActualWidth / PIXELS_PER_POINT;

		if (_data.Count == 0)
		{
			FillWith(value);
		}
		else while (_data.Count > maxPointCount)
		{
			_data.RemoveAt(0);
		}

		if (!double.IsFinite(value))
		{
			value = 0;
		}

		_lastValue = value;
		_data.Add(new MeasureModel { Timestamp = timestamp, Value = value });

		Data2XY(out double[] x, out double[] y);

		_scatter.Update(x, y);
		chart.Plot.AxisAuto();
		chart.Render();
	}


    // Internal 

    static readonly System.Drawing.Color LINE_COLOR = System.Drawing.Color.FromArgb(16, 160, 255);
    static readonly System.Drawing.Color AXIS_COLOR = System.Drawing.Color.FromArgb(80, 80, 80);
    static readonly int PIXELS_PER_POINT = 4;

	readonly List<MeasureModel> _data = new();
	readonly ScottPlot.Plottable.ScatterPlot _scatter;

	double _step = 1;
	double _lastValue = 0;

	private void FillWith(double value)
	{
		var maxPointCount = ActualWidth / PIXELS_PER_POINT;
		var ts = Utils.Timestamp.Sec;

		if (!double.IsFinite(value))
		{
			value = 0;
		}

		while (_data.Count < maxPointCount)
		{
			_data.Add(new MeasureModel
			{
				Timestamp = ts + Step * (_data.Count - maxPointCount),
				Value = value,
			});
		}
	}

	private void Data2XY(out double[] x, out double[] y)
	{
		var xi = new List<double>();
		var yi = new List<double>();

		if (_data.Count > 0)
		{
			foreach (var i in _data)
			{
				xi.Add(i.Timestamp);
				yi.Add(i.Value);
			}
		}
		else
		{
			xi.Add(0);
			yi.Add(0);
		}

		x = xi.ToArray();
		y = yi.ToArray();
	}
}
