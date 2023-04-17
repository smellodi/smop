namespace Smop.IonVision;

public static class SimulatedData
{
    const int DATA_ROWS = 3;
    const int DATA_COLS = 4;
    const int DATA_POINT_COUNT = DATA_ROWS * DATA_COLS;
    const int DATA_PP = 1000;
    const int DATA_PW = 200;
    const short DATA_SAMPLE_COUNT = 64;

    static readonly Settings _setting = new();

    public static User User = new(_setting.User);
    public static readonly Parameter Parameter = new(_setting.ParameterId, _setting.ParameterName);
    public static readonly Project Project = new(_setting.Project, new Parameter[] { Parameter });
    public static readonly ParameterDefinition ParameterDefinition = new(
        Parameter.Id,
        Parameter.Name,
        "2022-12-09T13:27:46.945Z",
        "Scan simulation",
        new SystemParameters(
            500000000,
            new SampleSensor(
                90,
                new RangeWithPID(0.2f, 2, new PID(0.7f, 1, 0.5f, 200, -200, 0.02f, 0.5f)),
                new Range(10, 45),
                new Range(900, 1200),
                new Range(0, 40),
                new RangeWithPID(0, 90, new PID(1, 0.1f, 0.1f, 2, -2, 0.1f, 35)),
                0
            ),
            new Ambient(new Range(0, 30), new Range(900, 1300), new Range(0, 40)),
            new Miscellaneous(new Range(0, 80)),
            new SampleSensor(
                90,
                new RangeWithPID(2, 6, new PID(1, 2, 1, 200, -200, 0.03f, 4.5f)),
                new Range(10, 45),
                new Range(800, 1200),
                new Range(0, 40),
                new RangeWithPID(0, 80, new PID(1, 0.1f, 0.1f, 2, -2, 0.1f, 35)),
                0
            )
        ),
        new MeasurementParameters(
            0,
            false,
            true,
            new Delays(100000000, 300, 10000000, 3000000, 5000000000, 200000000),
            new SteppingControl(
                new StepRange(200, 800, DATA_COLS),
                new StepRange(-3, 13, DATA_ROWS),
                new StepRange(-6, -6, 1),
                DATA_PP,
                DATA_PW,
                DATA_SAMPLE_COUNT,
                1
            ),
            new PointConfiguration(
                new float[DATA_POINT_COUNT] { 200, 200, 200, 400, 400, 400, 600, 600, 600, 800, 800, 800 },
                new float[DATA_POINT_COUNT] { -3, 5, 13, -3, 5, 13, -3, 5, 13, -3, 5, 13 },
                new float[DATA_POINT_COUNT] { -6, -6, -6, -6, -6, -6, -6, -6, -6, -6, -6, -6 },
                new float[DATA_POINT_COUNT] { DATA_PP, DATA_PP, DATA_PP, DATA_PP, DATA_PP, DATA_PP, DATA_PP, DATA_PP, DATA_PP, DATA_PP, DATA_PP, DATA_PP },
                new float[DATA_POINT_COUNT] { DATA_PW, DATA_PW, DATA_PW, DATA_PW, DATA_PW, DATA_PW, DATA_PW, DATA_PW, DATA_PW, DATA_PW, DATA_PW, DATA_PW },
                new short[DATA_POINT_COUNT] { DATA_SAMPLE_COUNT, DATA_SAMPLE_COUNT, DATA_SAMPLE_COUNT, DATA_SAMPLE_COUNT, DATA_SAMPLE_COUNT,
                    DATA_SAMPLE_COUNT, DATA_SAMPLE_COUNT, DATA_SAMPLE_COUNT, DATA_SAMPLE_COUNT, DATA_SAMPLE_COUNT, DATA_SAMPLE_COUNT, DATA_SAMPLE_COUNT }
            )
        ),
        6
    );

    public static readonly ScanResult ScanResult = new(
        "07d26c66-33e9-48fa-9877-4f64156d6b75",
        User.Name,
        "2023-04-05T14:38:58.349Z",
        "2023-04-05T14:40:10.288Z",
        Parameter.Id,
        Project.Name,
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
            new float[DATA_POINT_COUNT] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 },
            new float[DATA_POINT_COUNT] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 },
            ParameterDefinition.MeasurementParameters.PointConfiguration.Usv,
            ParameterDefinition.MeasurementParameters.PointConfiguration.Ucv,
            ParameterDefinition.MeasurementParameters.PointConfiguration.Vb,
            ParameterDefinition.MeasurementParameters.PointConfiguration.PP,
            ParameterDefinition.MeasurementParameters.PointConfiguration.PW,
            ParameterDefinition.MeasurementParameters.PointConfiguration.NForSampleAverages
        )
    );
}
