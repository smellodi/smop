namespace Smop.IonVision.Scan;

public interface IScan { }

public record class RangeAvg(
    double Avg,
    double Min,
    double Max
);
public record class Detector(
    RangeAvg Temperature,
    RangeAvg Pressure,
    RangeAvg Humidity
);
public record class FlowDetector(
    RangeAvg Flow,
    RangeAvg Temperature,
    RangeAvg Pressure,
    RangeAvg Humidity,
    RangeAvg PumpPWM
) : Detector(Temperature, Pressure, Humidity);
public record class SystemData(
    Defs.ErrorRegister ErrorRegister,
    RangeAvg FetTemperature,
    FlowDetector Sample,
    FlowDetector Sensor,
    Detector Ambient
);
public record class MeasurementData(
    bool DataValid,
    int DataPoints,
    float[] IntensityTop,
    float[] IntensityBottom,
    float[] Usv,
    float[] Ucv,
    float[] Vb,
    float[] PP,
    float[] PW,
    short[] NForSampleAverages
);
public record class ScanResult(
    string Id,
    string? Measurer,
    string StartTime,
    string FinishTime,
    string Parameters,
    string Project,
    object Comments,
    int FormatVersion,
    SystemData SystemData,
    MeasurementData MeasurementData
) : IScan;
