using Smop.IonVision.Defs;
using Smop.IonVision.Defs;
using System;
using System.Linq;

namespace Smop.IonVision;

public static class SimulatedData
{
    const int DATA_ROWS = 10;
    const int DATA_COLS = 200;
    const float DATA_PP = 1000;
    const float DATA_PW = 200;
    const short DATA_SAMPLE_COUNT = 64;
    const float USV_START = 200;
    const float USV_STOP = 800;
    const float UCV_START = -3;
    const float UCV_STOP = 13;
    const float VB_START = -6;
    const float VB_STOP = -6;

    public static User User { get; set; } = new();
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
                new Defs.Range(10, 45),
                new Defs.Range(900, 1200),
                new Defs.Range(0, 40),
                new RangePID(0, 90, new PID(1, 0.1f, 0.1f, 2, -2, 0.1f, 35)),
                0
            ),
            new Ambient(new Defs.Range(0, 30), new Defs.Range(900, 1300), new Defs.Range(0, 40)),
            new Miscellaneous(new Defs.Range(0, 80)),
            new SampleSensor(
                90,
                new RangePID(2, 6, new PID(1, 2, 1, 200, -200, 0.03f, 4.5f)),
                new Defs.Range(10, 45),
                new Defs.Range(800, 1200),
                new Defs.Range(0, 40),
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
                MakeArray((row, col) => USV_START + row * (USV_STOP - USV_START) / (DATA_ROWS - 1)),
                MakeArray((row, col) => UCV_START + col * (UCV_STOP - UCV_START) / (DATA_COLS - 1)),
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
            Usv.Steps * Ucv.Steps,
            MakeArray(GetImitatedPixel),
            MakeArray((x, y) => 100f * x),
            ParameterDefinition.MeasurementParameters.PointConfiguration.Usv,
            ParameterDefinition.MeasurementParameters.PointConfiguration.Ucv,
            ParameterDefinition.MeasurementParameters.PointConfiguration.Vb,
            ParameterDefinition.MeasurementParameters.PointConfiguration.PP,
            ParameterDefinition.MeasurementParameters.PointConfiguration.PW,
            ParameterDefinition.MeasurementParameters.PointConfiguration.NForSampleAverages
        )
    );

    public static ScopeParameters ScopeParameters { get; set; } = new(-3, 11, 400, -6, 1000, 220, 1024, 90, 90, 0, 0);

    public static ScopeResult ScopeResult => new(
        ScopeParameters.Usv,
        MakeArrayLine(0, (x, y) => ScopeParameters.UcvStart + x * (ScopeParameters.UcvStop - ScopeParameters.UcvStart)),
        MakeArrayLine((ScopeParameters.Usv - Usv.Min) / (Usv.Max - Usv.Min), GetImitatedPixel),
        MakeArrayLine(0, (x, y) => 100f * x)
    );

    public record class SimulatedLineGains(float Water, float[] Components);

    public static SimulatedLineGains LineGains { get; set; } = new SimulatedLineGains(100, new float[Common.OdorPrinter.MaxOdorCount] { 100, 90, 40, 0, 0 });

    // Internal

    static RangeStep Usv => ParameterDefinition.MeasurementParameters.SteppingControl.Usv; // shortcut
    static RangeStep Ucv => ParameterDefinition.MeasurementParameters.SteppingControl.Ucv; // shortcut

    static readonly float[] HyperbolaParams1 = new float[] { 0.4f, 0.45f, 0.11f };
    static readonly float[] HyperbolaParams2 = new float[] { 0.4f, 0.55f, 0.07f };
    static readonly float[] HyperbolaParams3 = new float[] { 0.4f, 0.65f, 0.05f };

    private static T[] MakeArray<T>(Func<float, float, T> callback)
    {
        //HyperbolaParams = new Random().NextDouble() < 0.5 ? HyperbolaParams1 : HyperbolaParams2;

        var config = ParameterDefinition?.MeasurementParameters.SteppingControl;
        var rowCount = config?.Usv.Steps ?? DATA_ROWS;
        var colCount = config?.Ucv.Steps ?? DATA_COLS;
        var count = rowCount * colCount;

        var result = new T[count];
        for (int row = 0; row < rowCount; row++)
            for (int col = 0; col < colCount; col++)
                result[row * colCount + col] = callback(
                    (float)col / (colCount - 1),
                    (float)row / (rowCount - 1));
        return result;
    }

    private static T[] MakeArrayLine<T>(float usvRatio, Func<float, float, T> callback)
    {
        //HyperbolaParams = new Random().NextDouble() < 0.5 ? HyperbolaParams1 : HyperbolaParams2;
        usvRatio = Math.Max(usvRatio, 0);

        var config = ParameterDefinition?.MeasurementParameters.SteppingControl;
        var colCount = config?.Ucv.Steps ?? DATA_COLS;

        var result = new T[colCount];
        for (int col = 0; col < colCount; col++)
            result[col] = callback((float)col / (colCount - 1), usvRatio);
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
    /// <param name="x">0..1</param>
    /// <param name="y">0..1</param>
    /// <returns>Pixels value</returns>
    private static float GetImitatedPixel(float x, float y) =>
        new float[1 + Common.OdorPrinter.MaxOdorCount]
        {
            // The water/moisture line
            (LineGains.Water - 0.5f * LineGains.Water * x) * Hyperbola(x, y, 0.4f, 0.3f, 0.1f),

            (LineGains.Components[0] - LineGains.Components[0] * (float)Math.Sqrt(y)) * Line(x, y, -7f, 1.75f, 0.6f),
            (LineGains.Components[1] - LineGains.Components[1] * (float)Math.Sqrt(y)) * Line(x, y, 8f, -2f, 0.5f),
            (LineGains.Components[2] - LineGains.Components[2] * y) * Hyperbola(x, y, HyperbolaParams1[0], HyperbolaParams1[1], HyperbolaParams1[2]),
            (LineGains.Components[3] - LineGains.Components[3] * y) * Hyperbola(x, y, HyperbolaParams2[0], HyperbolaParams2[1], HyperbolaParams2[2]),
            (LineGains.Components[4] - LineGains.Components[4] * y) * Hyperbola(x, y, HyperbolaParams3[0], HyperbolaParams3[1], HyperbolaParams3[2]),
        }.Max();
}
