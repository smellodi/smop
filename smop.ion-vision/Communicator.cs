using Smop.Common;
using Smop.IonVision.Defs;
using System;
using System.Threading.Tasks;

namespace Smop.IonVision;

public class Communicator : IDisposable
{
    public string SupportedVersion => _api.Version;

    public event EventHandler<int>? ScanProgress;
    public event EventHandler? ScanFinished;
    public event EventHandler<ScopeResult>? ScopeResult;

    public Settings Settings { get; }

    public Communicator(string? settingsFilename = null, bool isSimulator = false, string? ip = null)
    {
        Settings = settingsFilename == null ? new() : new(settingsFilename);
        if (ip != null)
        {
            Settings.IP = ip;
        }

        _api = isSimulator ? new Simulator() : new API(Settings.IP);
        _events = new(isSimulator ? "127.0.0.1" : Settings.IP);
        _events.ScanProgressChanged += (s, e) => ScanProgress?.Invoke(this, e.Data.Progress);
        _events.ScanFinished += (s, e) => ScanProgress?.Invoke(this, 100);
        _events.ScanResultsProcessed += (s, e) => ScanFinished?.Invoke(this, EventArgs.Empty);
        _events.ProjectSetupFinished += (s, e) => _projectIsReady = true;
        _events.ParameterSetupFinished += (s, e) => _parameterIsReady = true;
        _events.ParameterPreloadFinished += (s, e) => _parameterWasPreloaded = true;
        _events.ScopeDataReceived += (s, e) => ScopeResult?.Invoke(this, e.Data);
    }

    public void Dispose()
    {
        _events.Dispose();
        _api.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Retrieves system status</summary>
    /// <returns>System status</returns>
    public Task<Response<SystemStatus>> GetSystemStatus() => _api.GetSystemStatus();

    /// <summary>Retrieves system info</summary>
    /// <returns>System info</returns>
    public Task<Response<SystemInfo>> GetSystemInfo() => _api.GetSystemInfo();

    /// <summary>Retrieves user</summary>
    /// <returns>User name</returns>
    public Task<Response<User>> GetUser() => _api.GetUser();

    /// <summary>Sets user</summary>
    /// <returns>Error message, if any</returns>
    public Task<Response<Confirm>> SetUser() => _api.SetUser(new User(Settings.User));

    /// <summary>Retrieves a list of project names</summary>
    /// <returns>Project names</returns>
    public Task<Response<string[]>> GetProjects() => _api.GetProjects();

    /// <summary>Retrieves a list of projects</summary>
    /// <param name="name">Project name</param>
    /// <returns>Project definitions</returns>
    public Task<Response<Project>> GetProjectDefinition(string name) => _api.GetProjectDefinition(name);

    /// <summary>Retrieves a list of parameters</summary>
    /// <returns>Parameters</returns>
    public Task<Response<Parameter[]>> GetParameters() => _api.GetParameters();

    /// <summary>Retrieves the current project</summary>
    /// <returns>Project</returns>
    public Task<Response<ProjectAsName>> GetProject() => _api.GetProject();

    /// <summary>Sets the SMOP project as active</summary>
    /// <returns>Error message if the project was not set as active</returns>
    public async Task<Response<Confirm>> SetProject(ProjectAsName project) => await _api.SetProject(project);

    /// <summary>Sets the SMOP project as active and waiting until it is loaded</summary>
    /// <returns>Error message if the project was not set as active</returns>
    public async Task<Response<Confirm>> SetProjectAndWait()
    {
        _projectIsReady = false;
        var response = await _api.SetProject(new ProjectAsName(Settings.Project));
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
    public Task<Response<Defs.ParameterDefinition>> GetParameterDefinition() =>
        _api.GetParameterDefinition(new Parameter(Settings.ParameterId, Settings.ParameterName));


    /// <summary>Retrieves the current parameter</summary>
    /// <returns>Parameter</returns>
    public Task<Response<ParameterAsNameAndId>> GetParameter() => _api.GetParameter();

    /// <summary>Sets the SMOP project parameter and preloads it immediately</summary>
    /// <returns>Error message if the project parameter was not set</returns>
    public async Task<Response<Confirm>> SetParameterAndPreload()
    {
        _parameterIsReady = false;
        _parameterWasPreloaded = false;

        var response = await _api.SetParameter(new ParameterAsId(Settings.ParameterId));
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
    public Task<Response<Confirm>> StartScan() => _api.StartScan();

    /// <summary>Retrieves scan progress</summary>
    /// <returns>Scan progress</returns>
    public Task<Response<ScanProgress>> GetScanProgress() => _api.GetScanProgress();

    /// <summary>Sets a comment for the next-to-be-performed scan</summary>
    /// <param name="comment">Comment to set</param>
    /// <returns>Error message, if any</returns>
    public Task<Response<Confirm>> SetScanResultComment(object comment) => _api.SetScanComments(comment);

    /// <summary>Retrieves the latest scanning result</summary>
    /// <returns>Scanning result, if any</returns>
    public Task<Response<ScanResult>> GetScanResult() => _api.GetLatestResult();

    /// <summary>Retrieves all project scanning result</summary>
    /// <returns>Array of scanning results</returns>
    public Task<Response<string[]>> GetProjectResults() => _api.GetProjectResults(Settings.Project);

    /// <summary>Retrieves the system clock</summary>
    /// <returns>Clock</returns>
    public Task<Response<Clock>> GetClock() => _api.GetClock();

    /// <summary>Sets the system clock to the clock of the machine running this code</summary>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> SetClock() => _api.SetClock(new ClockToSet(
            DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            "Europe/Helsinki",
            false
        ));

    /// <summary>Check if the device is in scope mode</summary>
    /// <returns>Scope status</returns>
    public Task<Response<ScopeStatus>> CheckScopeMode() => _api.CheckScopeMode();

    /// <summary>Set the device to the scope mode</summary>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> EnableScopeMode() => _api.EnableScopeMode();

    /// <summary>Move the device back to idle</summary>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> DisableScopeMode() => _api.DisableScopeMode();

    /// <summary>Retrieves the latest scope result</summary>
    /// <returns>Scope result</returns>
    public Task<Response<ScopeResult>> GetScopeResult() => _api.GetScopeResult();

    /// <summary>Retrieves the scope parameters</summary>
    /// <returns>Scope parameters</returns>
    public Task<Response<ScopeParameters>> GetScopeParameters() => _api.GetScopeParameters();

    /// <summary>Sets the scope parameters</summary>
    /// <param name="parameters">Scope parameters</param>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> SetScopeParameters(ScopeParameters parameters) => _api.SetScopeParameters(parameters);

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

    /// <summary>
    /// Call this function after <see cref="EnableScopeMode"/> was called, it will return after the progress reaches 100%
    /// </summary>
    /// <param name="progressCallback">Callback to receive the progress value</param>
    public async Task<ScopeResult> WaitScopeScanToFinish(Action<int> progressCallback)
    {
        ScopeResult? result = null;

        void FinalizeScan(object? s, ScopeResult scan) => result = scan;
        void UpdateProgress(object? s, int value) => progressCallback(value);

        ScanProgress += UpdateProgress;
        ScopeResult += FinalizeScan;

        while (result == null)
            await Task.Delay(100);

        ScanProgress -= UpdateProgress;
        ScopeResult -= FinalizeScan;

        return result;
    }

    readonly IMinimalAPI _api;
    readonly EventSink _events;

    bool _projectIsReady = false;
    bool _parameterIsReady = false;
    bool _parameterWasPreloaded = false;
}
