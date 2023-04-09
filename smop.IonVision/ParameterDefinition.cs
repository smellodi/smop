#pragma warning disable CS8618

namespace Smop.IonVision
{
    public record class PID(
        float P,
        float I,
        float D,
        float IMaxWindup,
        float IMinWindup,
        float Hysteresis,
        float Setpoint
    );
    public record class Range(float Min, float Max);
    public record class RangeWithPID(float Min, float Max, PID PID) : Range(Min, Max);
    public record class SampleSensor(
        float FlowOverride,
        RangeWithPID Flow,
        Range Temperature,
        Range Pressure,
        Range Humidity,
        RangeWithPID HeaterTemperature,
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
    public record class StepRange(
        int Min,
        int Max,
        int Steps 
    );
    public record class SteppingControl(
        StepRange Usv,
        StepRange Ucv,
        StepRange Vb,
        int PP,
        int PW,
        int NForSampleAverages,
        int Layers
    );
    public record class PointConfiguration(
        float[] Usv,
        float[] Ucv,
        int[] Vb,
        int[] PP,
        int[] PW,
        int[] NForSampleAverages
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
}
