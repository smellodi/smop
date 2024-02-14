using System;
using System.Threading.Tasks;
using static Smop.IonVision.API;

namespace Smop.IonVision;

internal interface IMinimalAPI : IDisposable
{
    string Version { get; }
    Task<Response<SystemStatus>> GetSystemStatus();
    Task<Response<SystemInfo>> GetSystemInfo();
    Task<Response<User>> GetUser();
    Task<Response<Confirm>> SetUser(User user);
    Task<Response<string[]>> GetProjects();
    Task<Response<Parameter[]>> GetParameters();
    Task<Response<ProjectAsName>> GetProject();
    Task<Response<Project>> GetProjectDefinition(string project);
    Task<Response<Confirm>> SetProject(ProjectAsName project);
    Task<Response<ParameterDefinition>> GetParameterDefinition(Parameter parameter);
    Task<Response<ParameterAsNameAndId>> GetParameter();
    Task<Response<Confirm>> SetParameter(ParameterAsId parameter);
    Task<Response<Confirm>> PreloadParameter();
    Task<Response<Confirm>> StartScan();
    Task<Response<ScanProgress>> GetScanProgress();
    Task<Response<Confirm>> SetScanComments(object comment);
    Task<Response<ScanResult>> GetLatestResult();
    Task<Response<string[]>> GetProjectResults(string project);
    Task<Response<Clock>> GetClock();
    Task<Response<Confirm>> SetClock(ClockToSet clock);
    Task<Response<ScopeStatus>> CheckScopeMode();
    Task<Response<Confirm>> EnableScopeMode();
    Task<Response<Confirm>> DisableScopeMode();
    Task<Response<ScopeResult>> GetScopeResult();
    Task<Response<ScopeParameters>> GetScopeParameters();
    Task<Response<Confirm>> SetScopeParameters(ScopeParameters parameters);
}
