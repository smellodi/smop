#pragma warning disable CS8618

namespace Smop.IonVision;

public record class PID(
    float P,
    float I,
    float D,
    float IMaxWindup,
    float IMinWindup,
    float Hysteresis,
    float Setpoint
);
public record class RangePID(
    float Min,
    float Max,
    PID PID
) : Range(Min, Max);
public record class SampleSensor(
    float FlowOverride,
    RangePID Flow,
    Range Temperature,
    Range Pressure,
    Range Humidity,
    RangePID HeaterTemperature,
    float HeaterTemperatureOverride
);
public record class Ambient(
    Range Temperature,
    Range Pressure,
    Range Humidity
);
public record class Miscellaneous(
    Range FetTemperature
);
public record class SystemParameters(
    long PIDControllersSamplingTime,
    SampleSensor Sample,
    Ambient Ambient,
    Miscellaneous Miscellaneous,
    SampleSensor Sensor
);
public record class Delays(
    long StartDelay,
    long PulseToADC,
    long UsvToADC,
    long UcvToADC,
    long VbToADC,
    long ColdStartDelay
);
public record class RangeStep(
    float Min,
    float Max,
    float Steps 
) : Range(Min, Max);
public record class SteppingControl(
    RangeStep Usv,
    RangeStep Ucv,
    RangeStep Vb,
    int PP,
    int PW,
    int NForSampleAverages,
    int Layers
);
public record class PointConfiguration(
    float[] Usv,
    float[] Ucv,
    float[] Vb,
    float[] PP,
    float[] PW,
    short[] NForSampleAverages
);
public record class MeasurementParameters(
    float VbSwitchingTime,
    bool ExternalTrigger,
    bool UseSteppingControl,
    Delays Delays,
    SteppingControl SteppingControl,
    PointConfiguration PointConfiguration
);
public record class ParameterDefinition(
    string Id,
    string Name,
    string DateEdited,
    string Description,
    SystemParameters SystemParameters,
    MeasurementParameters MeasurementParameters,
    int FormatVersion
) : Parameter(Id, Name);
