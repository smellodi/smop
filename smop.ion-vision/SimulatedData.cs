using Smop.Common;
using Smop.IonVision.Defs;
using System;
using System.Linq;

namespace Smop.IonVision;

public static class SimulatedData
{
    const int DATA_ROWS = 60;//3
    const int DATA_COLS = ScopeParameters.DATA_SIZE;
    const float DATA_PP = 1000;
    const float DATA_PW = 200;
    const short DATA_SAMPLE_COUNT = 64;
    const float USV_START = 200;//420;
    const float USV_STOP = 800;//760;
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
                MakeArray((col, row) => USV_START + row * (USV_STOP - USV_START) / (DATA_ROWS - 1)),
                MakeArray((col, row) => UCV_START + col * (UCV_STOP - UCV_START) / (DATA_COLS - 1)),
                MakeArray((col, row) => VB_START),
                MakeArray((col, row) => DATA_PP),
                MakeArray((col, row) => DATA_PW),
                MakeArray((col, row) => DATA_SAMPLE_COUNT)
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
        Id: Guid.NewGuid().ToString(),
        Measurer: User.Name,
        StartTime: DateTime.Now.AddSeconds(-10).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
        FinishTime: DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
        Parameters: Parameter1.Id,
        Project: Project1.Name,
        Comments: new(),
        FormatVersion: 3,
        new SystemData(
            ErrorRegister: new ErrorRegister(false, false, false, false, false, false,
                false, false,
                true, false, false, false, false, false, false, false,
                true, false, false, false, false, false, false, false,
                false, false,
                false, false
            ),
            FetTemperature: new(23.8, 23.63, 23.97),
            Sample: new FlowDetector(
                Flow: new(2.11, 2.07, 2.12),
                Temperature: new(21.81, 21.70, 21.83),
                Pressure: new(1100, 1018.5, 1102),
                Humidity: new(20.12, 19.98, 21.03),
                PumpPWM: new(90, 90, 90)
            ),
            Sensor: new FlowDetector(
                Flow: new(2.11, 2.07, 2.12),
                Temperature: new(21.81, 21.70, 21.83),
                Pressure: new(1100, 1018.5, 1102),
                Humidity: new(20.12, 19.98, 21.03),
                PumpPWM: new(90, 90, 90)
            ),
            Ambient: new Detector(
                Temperature: new(21.81, 21.70, 21.83),
                Pressure: new(1100, 1018.5, 1102),
                Humidity: new(20.12, 19.98, 21.03)
            )
        ),
        new MeasurementData(
            DataValid: true,
            DataPoints: Usv.Steps * Ucv.Steps,
            IntensityTop: MakeArray(GetImitatedPixel),
            IntensityBottom: MakeArray((x, y) => 100f * x),
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

        var yStart = (config?.Usv.Min ?? USV_START) / 1000;
        var dy = ((config?.Usv.Max ?? USV_STOP) - (config?.Usv.Min ?? USV_START)) / rowCount / 1000;

        var result = new T[count];
        for (int row = 0; row < rowCount; row++)
            for (int col = 0; col < colCount; col++)
                result[row * colCount + col] = callback(
                    (float)col / (colCount - 1),
                    yStart + row * dy);  //(float)row / (rowCount - 0.5f));    // avoid row=max, as in simulation all signals vanish at this level.
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
        var dy = b * Math.Sqrt(vy) + k - y;

        var vx = (y - k) * (y - k) / b / b + 1;
        var dx = a * Math.Sqrt(vx) + h - x;

        var d2 = dx * dx + dy * dy;

        return (float)Math.Exp(-(d2 / s / s));
    }

    private static float Line(float x, float y, float a, float b, float s)
    {
        var vy = a * x + b;
        var dy = vy - y;

        var vx = (y - b) / a;
        var dx = vx - x;

        var d2 = dx * dx + dy * dy;

        return (float)Math.Exp(-(d2 / s / s));
    }

    /// <summary>
    /// Imitate real data
    /// </summary>
    /// <param name="x">0..1</param>
    /// <param name="y">0..1</param>
    /// <returns>Pixels value</returns>
    private static float GetImitatedPixel(float x, float y) =>
        new float[1 + OdorPrinter.MaxOdorCount]
        {
            // The water/moisture line
            (Simulation.DmsWaterGain - 0.5f * Simulation.DmsWaterGain * x) * Hyperbola(x, y, 0.4f, 0.2f, 0.05f),

            (Simulation.DmsGains[0] - Simulation.DmsGains[0] * (float)Math.Sqrt(y)) * Line(x, y, -7f, 1.75f, 0.6f),
            (Simulation.DmsGains[1] - Simulation.DmsGains[1] * (float)Math.Sqrt(y)) * Line(x, y, 8f, -2f, 0.5f),
            (Simulation.DmsGains[2] - Simulation.DmsGains[2] * y) * Hyperbola(x, y, HyperbolaParams1[0], HyperbolaParams1[1], HyperbolaParams1[2]),
            (Simulation.DmsGains[3] - Simulation.DmsGains[3] * y) * Hyperbola(x, y, HyperbolaParams2[0], HyperbolaParams2[1], HyperbolaParams2[2]),
            (Simulation.DmsGains[4] - Simulation.DmsGains[4] * y) * Hyperbola(x, y, HyperbolaParams3[0], HyperbolaParams3[1], HyperbolaParams3[2]),
        }.Sum();
}
