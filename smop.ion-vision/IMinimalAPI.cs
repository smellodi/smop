using System.Threading.Tasks;
using static Smop.IonVision.API;

namespace Smop.IonVision;

internal interface IMinimalAPI
{
    Task<Response<SystemStatus>> GetSystemStatus();
    Task<Response<User>> GetUser();
    Task<Response<Confirm>> SetUser(User user);
    Task<Response<string[]>> GetProjects();
    Task<Response<Parameter[]>> GetParameters();
    Task<Response<ProjectAsName>> GetProject();
    Task<Response<Confirm>> SetProject(ProjectAsName project);
    Task<Response<ParameterDefinition>> GetParameterDefinition(Parameter parameter);
    Task<Response<ParameterAsNameAndId>> GetParameter();
    Task<Response<Confirm>> SetParameter(ParameterAsId parameter);
    Task<Response<Confirm>> PreloadParameter();
    Task<Response<Confirm>> StartScan();
    Task<Response<ScanProgress>> GetScanProgress();
    Task<Response<Confirm>> SetScanComments(Comment comment);
    Task<Response<ScanResult>> GetLatestResult();
    Task<Response<string[]>> GetProjectResults(ProjectAsName project);
    Task<Response<Clock>> GetSettingsClock();
    Task<Response<Confirm>> SetSettingsClock(Clock clock);
}
