using System;
using System.Linq;

namespace Smop.IonVision;

public static class SimulatedData
{
    const int DATA_ROWS = 10;
    const int DATA_COLS = 200;
    const int DATA_POINT_COUNT = DATA_ROWS * DATA_COLS;
    const float DATA_PP = 1000;
    const float DATA_PW = 200;
    const short DATA_SAMPLE_COUNT = 64;
    const float USV_START = 200;
    const float USV_STOP = 800;
    const float UCV_START = -3;
    const float UCV_STOP = 13;
    const float VB_START = -6;
    const float VB_STOP = -6;

    public static User User = new();
    public static readonly Parameter Parameter1 = new("daa1c397-ebd0-4920-b405-5c6029d45fdd", "Fast scan");
    public static readonly Parameter Parameter2 = new("8036cca5-0677-475c-aa7f-1c9202d94b85", "Slow scan");
    public static readonly Project Project1 = new("Oleg fast scan", new Parameter[] { Parameter1, Parameter2 });
    public static readonly Project Project2 = new("Fake project", new Parameter[] { Parameter2 });
    public static readonly ParameterDefinition ParameterDefinition = new(
        Parameter1.Id,
        Parameter1.Name,
        "2022-12-09T13:27:46.945Z",
        "Scan simulation",
        new SystemParameters(
            500000000,
            new SampleSensor(
                90,
                new RangePID(0.2f, 2, new PID(0.7f, 1, 0.5f, 200, -200, 0.02f, 0.5f)),
                new Range(10, 45),
                new Range(900, 1200),
                new Range(0, 40),
                new RangePID(0, 90, new PID(1, 0.1f, 0.1f, 2, -2, 0.1f, 35)),
                0
            ),
            new Ambient(new Range(0, 30), new Range(900, 1300), new Range(0, 40)),
            new Miscellaneous(new Range(0, 80)),
            new SampleSensor(
                90,
                new RangePID(2, 6, new PID(1, 2, 1, 200, -200, 0.03f, 4.5f)),
                new Range(10, 45),
                new Range(800, 1200),
                new Range(0, 40),
                new RangePID(0, 80, new PID(1, 0.1f, 0.1f, 2, -2, 0.1f, 35)),
                0
            )
        ),
        new MeasurementParameters(
            0,
            false,
            true,
            new Delays(100000000, 300, 10000000, 3000000, 5000000000, 200000000),
            new SteppingControl(
                new RangeStep(USV_START, USV_STOP, DATA_ROWS),
                new RangeStep(UCV_START, UCV_STOP, DATA_COLS),
                new RangeStep(VB_START, VB_STOP, 1),
                DATA_PP,
                DATA_PW,
                DATA_SAMPLE_COUNT,
                1
            ),
            new PointConfiguration(
                MakeArray((row, col) => USV_START + row * (USV_STOP - USV_START) / (DATA_COLS - 1)),
                MakeArray((row, col) => UCV_START + col * (UCV_STOP - UCV_START) / (DATA_ROWS - 1)),
                MakeArray((row, col) => VB_START),
                MakeArray((row, col) => DATA_PP),
                MakeArray((row, col) => DATA_PW),
                MakeArray((row, col) => DATA_SAMPLE_COUNT)
            )
        ),
        6
    );

    public static readonly ParameterDefinition ParameterDefinition2 = ParameterDefinition with
    {
        Id = Parameter2.Id,
        Name = Parameter2.Name,
        Description = "Another fake param"
    };

    public static ScanResult ScanResult => new(
        Guid.NewGuid().ToString(),
        User.Name,
        DateTime.Now.AddSeconds(-10).ToString("yyyy-MM-ddTHH-mm-ss.fffZ"),
        DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss.fffZ"),
        Parameter1.Id,
        Project1.Name,
        new(),
        3,
        new SystemData(
            new ErrorRegister(false, false, false, false, false, false,
                false, false,
                true, false, false, false, false, false, false, false,
                true, false, false, false, false, false, false, false,
                false, false,
                false, false
            ),
            new(0, 17657, 0),
            new FlowDetector(
                new(0.11, 301.26, 0),
                new(21.81, 0, 21.75),
                new(1100, 21.78, 1018.5),
                new(20.12, 0.67, 17.84),
                new(90, 30, 90)
            ),
            new FlowDetector(
                new(3.92, 578.74, 3.29),
                new(21.68, 589.82, 21.6),
                new(1019.96, 21.68, 960.51),
                new(17.4, 2.16, 0.93),
                new(90, 1, 90)
            ),
            new Detector(
                new(26, 589.82, 25.94),
                new(1017.33, 25.99, 1017.12),
                new(17.34, 99.34, 17.24)
            )
        ),
        new MeasurementData(
            true,
            DATA_POINT_COUNT,
            MakeArray(GetImitatedPixel),
            MakeArray((row, col) => 100f * col),
            ParameterDefinition.MeasurementParameters.PointConfiguration.Usv,
            ParameterDefinition.MeasurementParameters.PointConfiguration.Ucv,
            ParameterDefinition.MeasurementParameters.PointConfiguration.Vb,
            ParameterDefinition.MeasurementParameters.PointConfiguration.PP,
            ParameterDefinition.MeasurementParameters.PointConfiguration.PW,
            ParameterDefinition.MeasurementParameters.PointConfiguration.NForSampleAverages
        )
    );

    public static ScopeParameters ScopeParameters = new(-3, 11, 400, -6, 1000, 220, 1024, 90, 90, 0, 0);

    public static ScopeResult ScopeResult => new(
        ScopeParameters.Usv,
        MakeArrayLine(0, (row, col) => UCV_START + col * (UCV_STOP - UCV_START) / (DATA_ROWS - 1)),
        MakeArrayLine((int)((ScopeParameters.Usv - USV_START) / (USV_STOP - USV_START) * DATA_ROWS), GetImitatedPixel),
        MakeArrayLine(0, (row, col) => 100f * col)
    );

    // Internal

    static float[] HyperbolaParams1 = new float[] { 0.4f, 0.5f, 0.1f };
    static float[] HyperbolaParams2 = new float[] { 0.4f, 0.55f, 0.07f };
    static float[] HyperbolaParams = HyperbolaParams1;

    private static T[] MakeArray<T>(Func<int,int,T> callback)
    {
        HyperbolaParams = new Random().NextDouble() < 0.5 ? HyperbolaParams1 : HyperbolaParams2;

        var result = new T[DATA_POINT_COUNT];
        for (int row = 0; row < DATA_ROWS; row++)
            for (int col = 0; col < DATA_COLS; col++)
                result[row * DATA_COLS + col] = callback(row, col);
        return result;
    }

    private static T[] MakeArrayLine<T>(int usvIndex, Func<int, int, T> callback)
    {
        HyperbolaParams = new Random().NextDouble() < 0.5 ? HyperbolaParams1 : HyperbolaParams2;
        usvIndex = Math.Max(usvIndex, 0);

        var result = new T[DATA_COLS];
        for (int col = 0; col < DATA_COLS; col++)
            result[col] = callback(usvIndex, col);
        return result;
    }

    private static float Hyperbola(float x, float y, float a, float b, float s, float h = -0.1f, float k = 0)
    {
        var vy = Math.Max(0, (x - h) * (x - h) / a / a - 1);
        var dy = Math.Abs(b * Math.Sqrt(vy) + k - y);

        var vx = (y - k) * (y - k) / b / b + 1;
        var dx = Math.Abs(a * Math.Sqrt(vx) + h - x);

        var d = Math.Sqrt(dx * dx + dy * dy);

        return (float)Math.Exp(-(d * d / s / s));
    }

    private static float Line(float x, float y, float a, float b, float s)
    {
        var vy = a * x + b;
        var dy = Math.Abs(vy - y);

        var vx = (y - b) / a;
        var dx = Math.Abs(vx - x);

        var d = Math.Sqrt(dx * dx + dy * dy);

        return (float)Math.Exp(-(d * d / s / s));
    }

    /// <summary>
    /// Imitate real data
    /// </summary>
    /// <param name="row">Row</param>
    /// <param name="col">Col</param>
    /// <returns>Pixels value</returns>
    private static float GetImitatedPixel(int row, int col)
    {
        float x = (float)col / (DATA_COLS - 1);
        float y = (float)row / (DATA_ROWS - 1);

        var lines = new float[]
        {
            // The strongests line
            (100f - 40f * x) * Hyperbola(x, y, 0.4f, 0.3f, 0.1f),
            // Another line
            (40f - 40f * y) * Hyperbola(x, y, HyperbolaParams[0], HyperbolaParams[1], HyperbolaParams[2]),
            // Wide line up
            (100f - 100f * (float)Math.Sqrt(y)) * Line(x, y, -7f, 1.75f, 0.6f),
            // Second wide line up
            (90f - 90f * (float)Math.Sqrt(y)) * Line(x, y, 8f, -2f, 0.5f),
        };

        return lines.Max();
    }
}
