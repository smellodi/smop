﻿using Smop.IonVision;

namespace Smop.ML;

internal record class ScanSetup(
    RangeStep Usv,
    RangeStep Ucv
);
internal record class ScanConditions(
    RangeAvg Flow,
    RangeAvg Temperature,
    RangeAvg Pressure,
    RangeAvg Humidity
);
internal record class ScanData(
    float[] Positive,
    float[] Negative
);

internal record class DmsMeasurement(
    ScanSetup Setup,
    ScanConditions Conditions,
    ScanData Data
) : Content(ML.Source.DMS)
{
    public static DmsMeasurement From(ScanResult scan, ParameterDefinition paramDefinition) => new(
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
