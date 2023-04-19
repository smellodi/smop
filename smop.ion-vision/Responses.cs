#pragma warning disable CS8618
using System.Text.Json.Serialization;

namespace Smop.IonVision;

public record class Err(string? Message, string[]? Errors);
public record class Confirm(string Message = "OK");

public record class User(string Name);

public record class ParameterAsId(string Parameter);
public record class Parameter(string Id, string Name);
public record class ParameterAsNameAndId(Parameter Parameter);
public record class ParameterMetadata(
    string Id,
    string Name,
    string DateEdited,
    string Description
);

public record class ProjectAsName(string Project);
public record class Project(
    string Name,
    Parameter[] Parameters
);

public record class ScanProgress(int Progress, object Information);

public record class GasFilter(string LastChanged, int CurrentUses, int MaxUses);
public record class SystemStorage(long Used, long Total);
public record class SystemStatus(
    GasFilter GasFilter,
    string Address,
    int ConnectedUsers,
    int DeviceType,
    bool FailsafeMode,
    SystemStorage SystemStorage
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

public record class Comments() {
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? Text { get; set; }
}


public record class Timezone(
    int Offset,
    string timezone
);

public record class Clock(
  string UtcTime,
  object Timezone,
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
    string Name
);

public record class Keyboard(
    string Layout,
    string Variant,
    string Model
);

public record class KeyboardLayout(
    string Variants
);