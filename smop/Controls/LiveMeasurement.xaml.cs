using LiveCharts;
using LiveCharts.Configurations;
using LiveCharts.Wpf;
using System.Windows.Controls;
using System.Windows.Media;

// check the manual on https://lvcharts.net/

namespace SMOP.Controls
{
    public partial class LiveMeasurement : UserControl
    {
        public static readonly Brush BRUSH_NEUTRAL = Brushes.DeepSkyBlue;
        public static readonly Brush BRUSH_WARNING = Brushes.Red;
        public static readonly Brush BRUSH_OK = Brushes.Green;

        public static Brush OdorColor(Comm.MFC.OdorFlowsTo odorDirection)
        {
            return odorDirection switch
            {
                Comm.MFC.OdorFlowsTo.SystemAndUser => BRUSH_OK,
                Comm.MFC.OdorFlowsTo.SystemAndWaste => BRUSH_NEUTRAL,
                _ => BRUSH_WARNING
            };
        }

        public class MeasureModel
        {
            public double Timestamp { get; set; }
            public double Value { get; set; }
            public Brush? Brush { get; set; }
        }

        public SeriesCollection SeriesCollection { get; set; }

        public LiveMeasurement()
        {
            InitializeComponent();

            chart.DataTooltip = null;

            var mapper = Mappers.Xy<MeasureModel>()
                .X(v => v.Timestamp)
                .Y(v => v.Value)
                .Stroke(v => v.Brush)
                /*.Fill(v => v.Brush)*/;

            SeriesCollection = new(mapper);
            SeriesCollection.Add(new LineSeries
                {
                    Title = "",
                    Values = new ChartValues<MeasureModel>(),
                    //PointGeometry = null,
                    PointGeometrySize = 2,    // colored point causes lagging
                    StrokeThickness = 2,
                    LineSmoothness = 0,
                    Fill = Brushes.Transparent,
                }
            );

            DataContext = this;
        }

        public void Reset(Brush brush, double baseline = 0)
        {
            var values = SeriesCollection[0].Values;
            values.Clear();

            var count = ActualWidth / PIXELS_PER_POINT;
            var ts = Utils.Timestamp.Sec;

            while (values.Count < count)
            {
                values.Add(new MeasureModel {
                    Timestamp = ts + values.Count - count,
                    Value = baseline,
                    Brush = brush
                });
            }
        }

        public void Add(double timestamp, double value, Brush? brush = null)
        {
            var values = SeriesCollection[0].Values;
            while (values.Count > ActualWidth / PIXELS_PER_POINT)
            {
                values.RemoveAt(0);
            }

            values.Add(new MeasureModel { Timestamp = timestamp, Value = value, Brush = brush ?? Brushes.DeepSkyBlue });
        }

        // Internal 

        const int PIXELS_PER_POINT = 4;
    }
}
