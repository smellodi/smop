using Smop.IonVision;

namespace Smop.ML;

internal record class ScanSetup(
    StepRange Usv,
    StepRange Ucv
);
internal record class ScanConditions(
    RangeProps Flow,
    RangeProps Temperature,
    RangeProps Pressure,
    RangeProps Humidity
);
internal record class ScanData(
    float[] Positive,
    float[] Negative
);

internal record class Measurement(
    ScanSetup Setup,
    ScanConditions Conditions,
    ScanData Data
)
{
    public static Measurement From(ScanResult scan, ParameterDefinition paramDefinition) =>
        new Measurement(
            new ScanSetup(
                paramDefinition.MeasurementParameters.SteppingControl.Usv,
                paramDefinition.MeasurementParameters.SteppingControl.Ucv
            ),
            new ScanConditions(
                scan.SystemData.Sample.Flow,
                scan.SystemData.Sample.Temperature,
                scan.SystemData.Sample.Pressure,
                scan.SystemData.Sample.Humidity
            ),
            new ScanData(
                scan.MeasurementData.IntensityTop,
                scan.MeasurementData.IntensityBottom
            )
        );
}
