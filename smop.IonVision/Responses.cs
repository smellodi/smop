#pragma warning disable CS8618

using System.Windows;

namespace Smop.IonVision
{
    public record class User(string Name);
    public record class ParameterAsName(string Parameter);
    public record class Parameter(string Id, string Name);
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

    internal record class ScanProgress(int Progress, object Information);
    internal record class Error(string Message);
}
