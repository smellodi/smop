namespace Smop.IonVision;

public record class ErrorRegister(
    bool ambientPressureR1Under,
    bool ambientPressureR1Over,
    bool ambientHumidityR1Under,
    bool ambientHumidityR1Over,
    bool ambientTemperatureR1Under,
    bool ambientTemperatureR1Over,
    bool fetTemperatureR1Under,
    bool fetTemperatureR1Over,
    bool sampleFlowR1Under,
    bool sampleFlowR1Over,
    bool sampleTemperatureR1Under,
    bool sampleTemperatureR1Over,
    bool samplePressureR1Under,
    bool samplePressureR1Over,
    bool sampleHumidityR1Under,
    bool sampleHumidityR1Over,
    bool sensorFlowR1Under,
    bool sensorFlowR1Over,
    bool sensorTemperatureR1Under,
    bool sensorTemperatureR1Over,
    bool sensorPressureR1Under,
    bool sensorPressureR1Over,
    bool sensorHumidityR1Under,
    bool sensorHumidityR1Over,
    bool sampleHeaterTemperatureR1Under,
    bool sampleHeaterTemperatureR1Over,
    bool sensorHeaterTemperatureR1Under,
    bool sensorHeaterTemperatureR1Over
);
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
    ErrorRegister ErrorRegister,
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
    string Measurer,
    string StartTime,
    string FinishTime,
    string Parameters,
    string Project,
    Comments Comments,
    int FormatVersion,
    SystemData SystemData,
    MeasurementData MeasurementData
);
