﻿using System.Text.Json.Serialization;

namespace Smop.IonVision.Defs;

public record class Err(
    string? Message,
    string[]? Errors
);
public record class Confirm(
    string Message = "OK"
);

public record class User(
    string? Name = null
);

public record class ParameterAsId(
    string Parameter
);
public record class Parameter(
    string Id,
    string Name
);
public record class ParameterAsNameAndId(
    Parameter Parameter
);
public record class ParameterMetadata(
    string Id,
    string Name,
    string DateEdited,
    string Description
);

public record class ProjectAsName(
    string Project
);
public record class Project(
    string Name,
    Parameter[] Parameters
);

public record class ScanProgress(
    int Progress,
    object Information
);

public record class GasFilter(
    string LastChanged,
    int CurrentUses,
    int MaxUses
);
public record class SystemStorage(
    long Used,
    long Total
);
public record class SystemStatus(
    string Address,
    int ConnectedUsers,
    int DeviceType,
    bool FailsafeMode,
    SystemStorage SystemStorage,
    GasFilter GasFilter
);
public record class SystemVersion(
    string Id,
    string Version,
    string Source,
    string Changelog
);
public record class SystemInfo(
    string CurrentVersion,
    string? CurrentRtmVersion,
    SystemVersion[] AvailableVersions
);

public record class SearchResultMeta(
    int Page,
    int MaxResults,
    int TotalResults
);
public record class SearchResult(
    SearchResultMeta Meta,
    ScanResult[] Results
);

public record class ListOfIDs(
    string[] Ids
);

public record class Calibration(
    string LastConducted
);

// Could be modified 
public record class SimpleComment()
{
    // Nulls not to be added
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? Text { get; set; }
}


public record class Timezone(
    float Offset,
#pragma warning disable IDE1006 // Naming Styles
    string timezone
#pragma warning restore IDE1006 // Naming Styles
);

public record class Clock(
  string UtcTime,
  Timezone Timezone,
  bool UpdateFromInternet
);

public record class ClockToSet(
  string UtcTime,
  string Timezone,
  bool UpdateFromInternet
);

public record class ParameterTemplate(
    string Id,
    string Name,
    string Description,
    string[] Keywords,
    bool GasDetection
);

public record class CopyResultProperties(
    string DestinationType,
    string Destination,
    bool CopyParameter
);

public record class Device(
    string Location,
    string Type,
    string? Name = null
);

public record class Keyboard(
    string Layout,
    string Variant
);

public record class KeyboardAndModel(
    string Layout,
    string Variant,
    string Model
) : Keyboard(Layout, Variant);

public record class KeyboardLayout(
    string Variants
);

public record class ScopeStatus(
    float Progress
);
public record class ScopeResult(
    float Usv,
    float[] Ucv,
    float[] IntensityTop,
    float[] IntensityBottom
) : Common.IMeasurement;
public record class ScopeParameters(
    float UcvStart,
    float UcvStop,
    float Usv,
    float Vb,
    float Pp,
    float Pw,
    int SampleAverages,
    int SampleFlowControl,
    int SensorFlowControl,
    float SampleHeaterTemperatureControl,
    float SensorHeaterTemperatureControl
)
{
    public const int DATA_SIZE = 238;
};

public record class Range(
    float Min,
    float Max
);
public record class RangeValue(
    float Min,
    float Max,
    float CurrentValue
) : Range(Min, Max);

public record class PumpDirectControl(
    bool Enabled,
    int DutyCycle
);

public record class PID(
    float P,
    float I,
    float D,
    float IMaxWindup,
    float IMinWindup,
    float Hysteresis,
    float Setpoint
);

public record class ErrorRegister(
    bool AmbientPressureR1Under,
    bool AmbientPressureR1Over,
    bool AmbientHumidityR1Under,
    bool AmbientHumidityR1Over,
    bool AmbientTemperatureR1Under,
    bool AmbientTemperatureR1Over,
    bool FetTemperatureR1Under,
    bool FetTemperatureR1Over,
    bool SampleFlowR1Under,
    bool SampleFlowR1Over,
    bool SampleTemperatureR1Under,
    bool SampleTemperatureR1Over,
    bool SamplePressureR1Under,
    bool SamplePressureR1Over,
    bool SampleHumidityR1Under,
    bool SampleHumidityR1Over,
    bool SensorFlowR1Under,
    bool SensorFlowR1Over,
    bool SensorTemperatureR1Under,
    bool SensorTemperatureR1Over,
    bool SensorPressureR1Under,
    bool SensorPressureR1Over,
    bool SensorHumidityR1Under,
    bool SensorHumidityR1Over,
    bool SampleHeaterTemperatureR1Under,
    bool SampleHeaterTemperatureR1Over,
    bool SensorHeaterTemperatureR1Under,
    bool SensorHeaterTemperatureR1Over
);
