namespace Smop.ML;

internal record class ScanSetup(
    IonVision.Param.RangeStep Usv,
    IonVision.Param.RangeStep Ucv
);
internal record class ScanConditions(
    IonVision.Scan.RangeAvg Flow,
    IonVision.Scan.RangeAvg Temperature,
    IonVision.Scan.RangeAvg Pressure,
    IonVision.Scan.RangeAvg Humidity
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
    public static DmsMeasurement From(IonVision.Scan.ScanResult scan, IonVision.Param.ParameterDefinition paramDefinition) => new(
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
    IonVision.Param.RangeStep Ucv
);

internal record class DmsMeasurementScope(
    ScopeSetup Setup,
    ScanData Data
) : Content(ML.Source.DMS)
{
    public static DmsMeasurementScope From(IonVision.Defs.ScopeResult scopeResult, IonVision.Defs.ScopeParameters parameters) => new(
        new ScopeSetup(
            parameters.Usv,
            new IonVision.Param.RangeStep(parameters.UcvStart, parameters.UcvStop, scopeResult.IntensityTop.Length)
        ),
        new ScanData(
            scopeResult.IntensityTop,
            scopeResult.IntensityBottom
        )
    );
}
