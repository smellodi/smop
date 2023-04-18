using System;
using System.Threading.Tasks;

namespace Smop.IonVision;

public class Communicator
{
    public const int PROJECT_LOADING_DURATION = 2000;

    public bool IsOnline { get; private set; } = false;

    public Settings Settings => _settings;

    public Communicator(string? settingsFilename = null, bool isSimulator = false)
    {
        _settings = settingsFilename == null ? new() : new(settingsFilename);
        _api = isSimulator ? new Simulator() : new API(_settings.IP);
    }

    public async Task<bool> CheckConnection()
    {
        try
        {
            await _api.GetSystemStatus();
            IsOnline = true;
        }
        catch (System.Net.Http.HttpRequestException)
        {
            System.Diagnostics.Debug.WriteLine("The device is offline");
        }

        return IsOnline;
    }

    /// <summary>
    /// Retrieves system status
    /// </summary>
    /// <returns>System status</returns>
    public async Task<API.Response<SystemStatus>> GetSystemStatus() => await _api.GetSystemStatus();

    /// <summary>
    /// Retrieves user
    /// </summary>
    /// <returns>User name</returns>
    public async Task<API.Response<User>> GetUser() => await _api.GetUser();

    /// <summary>
    /// Sets user
    /// </summary>
    /// <returns>Error message, if any</returns>
    public async Task<API.Response<Confirm>> SetUser() => await _api.SetUser(new User(_settings.User));

    /// <summary>
    /// Retrieves a list of project names
    /// </summary>
    /// <returns>Project names</returns>
    public async Task<API.Response<string[]>> GetProjects() => await _api.GetProjects();

    /// <summary>
    /// Retrieves a list of projects
    /// </summary>
    /// <param name="project">Project name</param>
    /// <returns>Project definitions</returns>
    public async Task<API.Response<Project>> GetProjectDefinition(ProjectAsName project) => await _api.GetProjectDefinition(project);

    /// <summary>
    /// Retrieves a list of parameters
    /// </summary>
    /// <returns>Parameters</returns>
    public async Task<API.Response<Parameter[]>> GetParameters() => await _api.GetParameters();

    /// <summary>
    /// Retrieves the current project
    /// </summary>
    /// <returns>Project</returns>
    public async Task<API.Response<ProjectAsName>> GetProject() => await _api.GetProject();

    /// <summary>
    /// Sets the SMOP project as active
    /// </summary>
    /// <returns>Error message if the project was not set as active</returns>
    public async Task<API.Response<Confirm>> SetProject()
    {
        var response = await _api.SetProject(new ProjectAsName(_settings.Project));
        if (response.Success)
        {
            await Task.Delay(PROJECT_LOADING_DURATION);
        }
        return response;
    }

    /// <summary>
    /// Retrieves the current parameter definition
    /// </summary>
    /// <returns>Parameter definition</returns>
    public async Task<API.Response<ParameterDefinition>> GetParameterDefinition() => 
        await _api.GetParameterDefinition(new Parameter(_settings.ParameterId, _settings.ParameterName));


    /// <summary>
    /// Retrieves the current parameter
    /// </summary>
    /// <returns>Parameter</returns>
    public async Task<API.Response<ParameterAsNameAndId>> GetParameter() => await _api.GetParameter();

    /// <summary>
    /// Sets the SMOP project parameter, and also preloads it immediately
    /// </summary>
    /// <returns>Error message if the project parameter was not set</returns>
    public async Task<API.Response<Confirm>> SetParameter()
    {
        var response = await _api.SetParameter(new ParameterAsId(_settings.ParameterId));
        if (response.Success)
        {
            await Task.Delay(300);
            await _api.PreloadParameter();
        }
        return response;
    }

    /// <summary>
    /// Starts a new scan
    /// </summary>
    /// <returns>Error message if the scan was not started</returns>
    public async Task<API.Response<Confirm>> StartScan() => await _api.StartScan();

    /// <summary>
    /// Retrieves scan progress
    /// </summary>
    /// <returns>Scan progress</returns>
    public async Task<API.Response<ScanProgress>> GetScanProgress() => await _api.GetScanProgress();

    /// <summary>
    /// Sets a marker for the latest scan result
    /// </summary>
    /// <param name="comment">Comment to set</param>
    /// <returns>Error message, if any</returns>
    public async Task<API.Response<Confirm>> SetScanResultComment(Comment comment) => await _api.SetScanComments(comment);

    /// <summary>
    /// Retrieves the latest scanning result
    /// </summary>
    /// <returns>Scanning result, if any</returns>
    public async Task<API.Response<ScanResult>> GetScanResult() => await _api.GetLatestResult();

    /// <summary>
    /// Retrieves all project scanning result
    /// </summary>
    /// <returns>Array of scanning results</returns>
    public async Task<API.Response<string[]>> GetProjectResults() => await _api.GetProjectResults(new ProjectAsName(_settings.Project));

    /// <summary>
    /// Retrieves the system clock
    /// </summary>
    /// <returns>Clock</returns>
    public async Task<API.Response<Clock>> GetSettingsClock() => await _api.GetSettingsClock();

    /// <summary>
    /// Sets the system clock
    /// </summary>
    /// <returns>Clock</returns>
    public async Task<API.Response<Confirm>> SetSettingsClock() => await _api.SetSettingsClock(new Clock(
            DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            "Europe/Helsinki",
            false
        ));

    // Internal

    readonly Settings _settings;
    readonly IMinimalAPI _api;
}
