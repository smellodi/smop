using Smop.Common;
using System;
using System.Threading.Tasks;

namespace Smop.IonVision;

public class Communicator : IDisposable
{
    public string SupportedVersion => _api.Version;

    public event EventHandler<int>? ScanProgress;
    public event EventHandler? ScanFinished;

    public Settings Settings => _settings;

    public Communicator(string? settingsFilename = null, bool isSimulator = false)
    {
        _settings = settingsFilename == null ? new() : new(settingsFilename);
        _api = isSimulator ? new Simulator() : new API(_settings.IP);
        _events = new(isSimulator ? "127.0.0.1" : _settings.IP);
        _events.ScanProgressChanged += (s, e) => ScanProgress?.Invoke(this, e.Data.Progress);
        _events.ScanFinished += (s, e) => ScanProgress?.Invoke(this, 100);
        _events.ScanResultsProcessed += (s, e) => ScanFinished?.Invoke(this, EventArgs.Empty);
        _events.ProjectSetupFinished += (s, e) => _projectIsReady = true;
        _events.ParameterSetupFinished += (s, e) => _parameterIsReady = true;
        _events.ParameterPreloadFinished += (s, e) => _parameterWasPreloaded = true;
    }

    public void Dispose()
    {
        _events.Dispose();
        _api.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Retrieves system status</summary>
    /// <returns>System status</returns>
    public Task<API.Response<SystemStatus>> GetSystemStatus() => _api.GetSystemStatus();

    /// <summary>Retrieves system info</summary>
    /// <returns>System info</returns>
    public Task<API.Response<SystemInfo>> GetSystemInfo() => _api.GetSystemInfo();

    /// <summary>Retrieves user</summary>
    /// <returns>User name</returns>
    public Task<API.Response<User>> GetUser() => _api.GetUser();

    /// <summary>Sets user</summary>
    /// <returns>Error message, if any</returns>
    public Task<API.Response<Confirm>> SetUser() => _api.SetUser(new User(_settings.User));

    /// <summary>Retrieves a list of project names</summary>
    /// <returns>Project names</returns>
    public Task<API.Response<string[]>> GetProjects() => _api.GetProjects();

    /// <summary>Retrieves a list of projects</summary>
    /// <param name="name">Project name</param>
    /// <returns>Project definitions</returns>
    public Task<API.Response<Project>> GetProjectDefinition(string name) => _api.GetProjectDefinition(name);

    /// <summary>Retrieves a list of parameters</summary>
    /// <returns>Parameters</returns>
    public Task<API.Response<Parameter[]>> GetParameters() => _api.GetParameters();

    /// <summary>Retrieves the current project</summary>
    /// <returns>Project</returns>
    public Task<API.Response<ProjectAsName>> GetProject() => _api.GetProject();

    /// <summary>Sets the SMOP project as active and waiting until it is loaded</summary>
    /// <returns>Error message if the project was not set as active</returns>
    public async Task<API.Response<Confirm>> SetProjectAndWait()
    {
        _projectIsReady = false;
        var response = await _api.SetProject(new ProjectAsName(_settings.Project));
        if (response.Success)
        {
            while (!_projectIsReady)
            {
                ScreenLogger.Print("[IvComm] Waiting for the project to be loaded");
                await Task.Delay(200);
            }
        }
        return response;
    }

    /// <summary>Retrieves the current parameter definition</summary>
    /// <returns>Parameter definition</returns>
    public Task<API.Response<ParameterDefinition>> GetParameterDefinition() =>
        _api.GetParameterDefinition(new Parameter(_settings.ParameterId, _settings.ParameterName));


    /// <summary>Retrieves the current parameter</summary>
    /// <returns>Parameter</returns>
    public Task<API.Response<ParameterAsNameAndId>> GetParameter() => _api.GetParameter();

    /// <summary>Sets the SMOP project parameter and preloads it immediately</summary>
    /// <returns>Error message if the project parameter was not set</returns>
    public async Task<API.Response<Confirm>> SetParameterAndPreload()
    {
        _parameterIsReady = false;
        _parameterWasPreloaded = false;

        var response = await _api.SetParameter(new ParameterAsId(_settings.ParameterId));
        if (response.Success)
        {
            await Task.Delay(300);
            while (!_parameterIsReady)
            {
                ScreenLogger.Print("[IvComm] Waiting for the parameter to be set");
                await Task.Delay(200);
            }

            await _api.PreloadParameter();
            while (!_parameterWasPreloaded)
            {
                ScreenLogger.Print("[IvComm] Waiting for the parameter to be preloaded");
                await Task.Delay(200);
            }
        }
        return response;
    }

    /// <summary>Starts a new scan</summary>
    /// <returns>Error message if the scan was not started</returns>
    public Task<API.Response<Confirm>> StartScan() => _api.StartScan();

    /// <summary>Retrieves scan progress</summary>
    /// <returns>Scan progress</returns>
    public Task<API.Response<ScanProgress>> GetScanProgress() => _api.GetScanProgress();

    /// <summary>Sets a marker for the latest scan result</summary>
    /// <param name="comment">Comment to set</param>
    /// <returns>Error message, if any</returns>
    public Task<API.Response<Confirm>> SetScanResultComment(object comment) => _api.SetScanComments(comment);

    /// <summary>Retrieves the latest scanning result</summary>
    /// <returns>Scanning result, if any</returns>
    public Task<API.Response<ScanResult>> GetScanResult() => _api.GetLatestResult();

    /// <summary>Retrieves all project scanning result</summary>
    /// <returns>Array of scanning results</returns>
    public Task<API.Response<string[]>> GetProjectResults() => _api.GetProjectResults(_settings.Project);

    /// <summary>Retrieves the system clock</summary>
    /// <returns>Clock</returns>
    public Task<API.Response<Clock>> GetClock() => _api.GetClock();

    /// <summary>Sets the system clock to the clock of the machine running this code</summary>
    /// <returns>Confirmation message</returns>
    public Task<API.Response<Confirm>> SetClock() => _api.SetClock(new ClockToSet(
            DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            "Europe/Helsinki",
            false
        ));

    // Helpers

    /// <summary>
    /// Call this function after <see cref="StartScan"/> was called, it will return after the progress reaches 100%
    /// </summary>
    /// <param name="progressCallback">Callback to receive the progress value</param>
    public async Task WaitScanToFinish(Action<int> progressCallback)
    {
        bool isScanFinished = false;

        void FinalizeScan(object? s, EventArgs _) => isScanFinished = true;
        void UpdateProgress(object? s, int value) => progressCallback(value);

        ScanProgress += UpdateProgress;
        ScanFinished += FinalizeScan;

        while (!isScanFinished)
            await Task.Delay(100);

        ScanProgress -= UpdateProgress;
        ScanFinished -= FinalizeScan;
    }

    // Internal

    readonly Settings _settings;
    readonly IMinimalAPI _api;
    readonly EventSink _events;

    bool _projectIsReady = false;
    bool _parameterIsReady = false;
    bool _parameterWasPreloaded = false;
}
