using IVDefs = Smop.IonVision.Defs;

namespace Smop.ML;

internal record class ScanSetup(
    IVDefs.RangeStep Usv,
    IVDefs.RangeStep Ucv
);
internal record class ScanConditions(
    IVDefs.RangeAvg Flow,
    IVDefs.RangeAvg Temperature,
    IVDefs.RangeAvg Pressure,
    IVDefs.RangeAvg Humidity
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
    public static DmsMeasurement From(IVDefs.ScanResult scan, IVDefs.ParameterDefinition paramDefinition) => new(
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

internal record class ScopeSetup(
    float Usv,
    IVDefs.RangeStep Ucv
);

internal record class DmsMeasurementScope(
    ScopeSetup Setup,
    ScanData Data
) : Content(ML.Source.DMS)
{
    public static DmsMeasurementScope From(IVDefs.ScopeResult scopeResult, IVDefs.ScopeParameters parameters) => new(
        new ScopeSetup(
            parameters.Usv,
            new IVDefs.RangeStep(parameters.UcvStart, parameters.UcvStop, scopeResult.IntensityTop.Length)
        ),
        new ScanData(
            scopeResult.IntensityTop,
            scopeResult.IntensityBottom
        )
    );
}
