using RestSharp;
using Smop.Common;
using Smop.IonVision.Defs;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

using Param = Smop.IonVision.Defs.Parameter;
using MinMax = Smop.IonVision.Defs.Range;

namespace Smop.IonVision;

public class Response<T>
{
    public T? Value { get; private set; }
    public string? Error { get; private set; }
    public bool Success => Value != null;
    /// <summary>
    /// For the future: this field may contain name of the function for which this response was generated.
    /// </summary>
    public string? Action { get; set; } = null;
    public Response(T? value, string? error)
    {
        Value = value;
        Error = error;
    }
}

internal class API : IMinimalAPI
{
    public string Version => "1.5";

    public API(string ip)
    {
        var host = $"http://{ip}/api";
        _client = new RestClient(host, config =>
        {
            config.Timeout = TimeSpan.FromSeconds(5);
            config.ThrowOnAnyError = false;
        });
        _client.AddDefaultHeader("Content-Type", "application/json");
    }

    public void Dispose()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }

    #region SCAN

    /// <summary>Retrieves scan progress</summary>
    /// <returns>Scan progress</returns>
    public Task<Response<ScanProgress>> GetScanProgress() => Get<ScanProgress>("currentScan");

    /// <summary>Starts a new scan</summary>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> StartScan() => Create("currentScan");

    /// <summary>Stops the ongoing scan</summary>
    /// <returns>Confirmations message</returns>
    public Task<Response<Confirm>> StopScan() => Remove("currentScan");

    /// <summary>Retrieves latest scan comments</summary>
    /// <returns>Comments</returns>
    public Task<Response<object>> GetScanComments() => Get<object>("currentScan/comments");

    /// <summary>Sets latest scan comments</summary>
    /// <param name="comment">Comment</param>
    /// <returns>Conrimation</returns>
    public Task<Response<Confirm>> SetScanComments(object comment) => Set("currentScan/comments", comment);

    #endregion

    #region SCOPE

    /// <summary>Check if the device is in scope mode</summary>
    /// <returns>Scope status</returns>
    public Task<Response<ScopeStatus>> CheckScopeMode() => Get<ScopeStatus>("scope");

    /// <summary>Set the device to the scope mode</summary>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> EnableScopeMode() => Create("scope");

    /// <summary>Move the device back to idle</summary>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> DisableScopeMode() => Remove("scope");

    /// <summary>Retrieves the latest scope result</summary>
    /// <returns>Scope result</returns>
    public Task<Response<ScopeResult>> GetScopeResult() => Get<ScopeResult>("scope/latestResult");

    /// <summary>Retrieves the scope parameters</summary>
    /// <returns>Scope parameters</returns>
    public Task<Response<ScopeParameters>> GetScopeParameters() => Get<ScopeParameters>("scope/parameters");

    /// <summary>Sets the scope parameters</summary>
    /// <param name="parameters">Scope parameters</param>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> SetScopeParameters(ScopeParameters parameters) => Set("scope/parameters", parameters);

    #endregion

    #region USER

    /// <summary>Retrieves the user</summary>
    /// <returns>User name</returns>
    public Task<Response<User>> GetUser() => Get<User>("currentUser");

    /// <summary>Sets a user</summary>
    /// <param name="user">User name</param>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> SetUser(User user) => Set("currentUser", user);

    #endregion

    #region PARAMETER

    /// <summary>Retrieves current parameter</summary>
    /// <returns>Parameter</returns>
    public Task<Response<ParameterAsNameAndId>> GetParameter() => Get<ParameterAsNameAndId>("currentParameter");

    /// <summary>Sets current parameter</summary>
    /// <param name="parameter">Parameter</param>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> SetParameter(ParameterAsId parameter) => Set("currentParameter", parameter);

    /// <summary>Proloads current parameter</summary>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> PreloadParameter() => Create("currentParameter/preload");

    /// <summary>Retrieves a list of parameters</summary>
    /// <returns>Parameters</returns>
    public Task<Response<Param[]>> GetParameters() => Get<Param[]>("parameter");

    /// <summary>Creates a new parameter</summary>
    /// <param name="parameter">New parameter definition</param>
    /// <returns>Parameter as name</returns>
    public Task<Response<ParameterAsId>> CreateParameter(ParameterDefinition parameter) => Create<ParameterDefinition, ParameterAsId>("parameter", parameter, true);

    /// <summary>Retrieves the parameter definition</summary>
    /// <param name="parameter">Parameter</param>
    /// <returns>Parameter definition</returns>
    public Task<Response<ParameterDefinition>> GetParameterDefinition(Param parameter) => Get<ParameterDefinition>($"parameter/{parameter.Id}", true);

    /// <summary>Creates a new parameter (valid only if no scan was performed with this parameter)</summary>
    /// <param name="parameter">Parameter definition</param>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> UpdateParameterDefinition(ParameterDefinition parameter) => Set($"parameter/{parameter.Id}", parameter, true);

    /// <summary>Deletes the parameter</summary>
    /// <param name="parameter">Parameter</param>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> DeleteParameter(Param parameter) => Remove($"parameter/{parameter.Id}");

    /// <summary>Retrieves the parameter metadata</summary>
    /// <param name="parameter">Parameter</param>
    /// <returns>Metadata</returns>
    public Task<Response<ParameterMetadata>> GetParameterMedatada(Param parameter) => Get<ParameterMetadata>($"parameter/{parameter.Id}/metadata", true);

    /// <summary>Retrieves the parameter scan IDs</summary>
    /// <param name="parameter">Parameter</param>
    /// <returns>List of scan IDs</returns>
    public Task<Response<string[]>> GetParameterResults(Param parameter) => Get<string[]>($"parameter/{parameter.Id}/results");

    /// <summary>Retrieves gases that can be detected with this parameter</summary>
    /// <param name="parameter">Parameter</param>
    /// <returns>List of gases</returns>
    public Task<Response<Dictionary<string, string[]>>> GetParameterGases(Param parameter) => Get<Dictionary<string, string[]>>($"parameter/{parameter.Id}/gasDetection");

    /// <summary>Retrieves available parameter templates</summary>
    /// <param name="name">the name to search for</param>
    /// <param name="gasDetection">whether to search for templates with or without built-in gas detection</param>
    /// <returns>List of templates</returns>
    public Task<Response<ParameterTemplate[]>> GetParameterTemplates(string? name = null, bool? gasDetection = null) =>
        Get<ParameterTemplate[]>("parameterTemplate" + ToParams(
            ("search", name),
            ("gas_detection", gasDetection)
        ));

    /// <summary>Retrieves the parameter template</summary>
    /// <param name="name">template name</param>
    /// <returns>Template as the parameter definition</returns>
    public Task<Response<ParameterDefinition>> GetParameterTemplate(string name) => Get<ParameterDefinition>($"parameterTemplate/{name}");

    /// <summary>Retrieves the parameter template metadata</summary>
    /// <param name="name">template name</param>
    /// <returns>Template metadata</returns>
    public Task<Response<ParameterTemplate>> GetParameterTemplateMetadata(string name) => Get<ParameterTemplate>($"parameterTemplate/{name}/metadata");

    #endregion

    #region PROJECT

    /// <summary>Retrieves the project in use</summary>
    /// <returns>Project name</returns>
    public Task<Response<ProjectAsName>> GetProject() => Get<ProjectAsName>("currentProject");

    /// <summary>Loads a project</summary>
    /// <param name="project">Project name</param>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> SetProject(ProjectAsName project) => Set("currentProject", project);

    /// <summary>Retrieves the list of projects</summary>
    /// <returns>List of projects</returns>
    public Task<Response<string[]>> GetProjects() => Get<string[]>("project");

    /// <summary>Creates a new project</summary>
    /// <param name="project">Project</param>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> CreateProject(Project project) => Create("project", project);

    /// <summary>Gets the project definition</summary>
    /// <param name="name">Project</param>
    /// <returns>Project definition</returns>
    public Task<Response<Project>> GetProjectDefinition(string name) => Get<Project>($"project/{name}");

    /// <summary>Updates the project</summary>
    /// <param name="name">Project name</param>
    /// <param name="project">New project</param>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> UpdateProject(string name, Project project) => Set($"project/{name}", project);

    /// <summary>Deletes the project</summary>
    /// <param name="name">Project name</param>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> DeleteProject(string name) => Remove($"project/{name}");

    /// <summary>Gets the list of project scans</summary>
    /// <param name="name">Project name</param>
    /// <returns>List of scan IDs</returns>
    public Task<Response<string[]>> GetProjectResults(string name) => Get<string[]>($"project/{name}/results");

    /// <summary>Get the list of project parameters.
    /// NOTE! Not working, use <see cref="GetProjectDefinition(string)"/> instead.</summary>
    /// <param name="name">Project name</param>
    /// <returns>List of project parameters</returns>
    public Task<Response<Param[]>> GetProjectParameters(string name) => Get<Param[]>($"project/{name}/sequence");

    /// <summary>Sets the project list of parameters</summary>
    /// <param name="name">Project name</param>
    /// <param name="parameters">List of parameters</param>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> SetProjectParameters(string name, Param[] parameters) => Set($"project/{name}/sequence", parameters);

    #endregion

    #region CONTROLLER

    /// <summary>Gets current sample flow adjustment values</summary>
    /// <returns>Flow adjustment values</returns>
    public Task<Response<RangeValue>> GetSampleFlow() => Get<RangeValue>("controller/sample/flow");

    /// <summary>Sets current sample flow adjustment values</summary>
    /// <param name="values">Flow adjustment values</param>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> SetSampleFlow(MinMax values) => Set("controller/sample/flow", values);

    /// <summary>Gets current sample flow pid controller values</summary>
    /// <returns>Flow pid controller values</returns>
    public Task<Response<PID>> GetSampleFlowPID() => Get<PID>("controller/sample/flow/pid");

    /// <summary>Sets current sample flow pid controller values</summary>
    /// <param name="values">Flow pid controller values</param>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> SetSampleFlowPID(PID values) => Set("controller/sample/flow/pid", values);

    /// <summary>Gets whether the sample flow pump is directly adjusted</summary>
    /// <returns>Flow direct control values</returns>
    public Task<Response<PumpDirectControl>> GetSampleFlowPumpControl() => Get<PumpDirectControl>("controller/sample/flow/directControl");

    /// <summary>Sets whether the sample flow pump is directly adjusted</summary>
    /// <param name="values">Flow pid controller values</param>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> SetSampleFlowPumpControl(PumpDirectControl values) => Set("controller/sample/flow/directControl", values);

    /// <summary>Gets current sensor flow adjustment values</summary>
    /// <returns>Flow adjustment values</returns>
    public Task<Response<RangeValue>> GetSensorFlow() => Get<RangeValue>("controller/sensor/flow");

    /// <summary>Sets current sensor flow adjustment values</summary>
    /// <param name="values">Flow adjustment values</param>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> SetSensorFlow(MinMax values) => Set("controller/sensor/flow", values);

    /// <summary>Gets current sensor flow pid controller values</summary>
    /// <returns>Flow pid controller values</returns>
    public Task<Response<PID>> GetSensorFlowPID() => Get<PID>("controller/sensor/flow/pid");

    /// <summary>Sets current sensor flow pid controller values</summary>
    /// <param name="values">Flow pid controller values</param>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> SetSensorFlowPID(PID values) => Set("controller/sensor/flow/pid", values);

    /// <summary>Gets whether the sensor flow pump is directly adjusted</summary>
    /// <returns>Flow direct control values</returns>
    public Task<Response<PumpDirectControl>> GetSensorFlowPumpControl() => Get<PumpDirectControl>("controller/sensor/flow/directControl");

    /// <summary>Sets whether the sensor flow pump is directly adjusted</summary>
    /// <param name="values">Flow pid controller values</param>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> SetSensorFlowPumpControl(PumpDirectControl values) => Set("controller/sensor/flow/directControl", values);

    /// <summary>Gets current sample gas heater temperature adjustment values</summary>
    /// <returns>Gas heater temperature adjustment values</returns>
    public Task<Response<RangeValue>> GetSampleHeaterTemp() => Get<RangeValue>("controller/sample/heaterTemperature");

    /// <summary>Sets current sample gas heater temperature adjustment values</summary>
    /// <param name="values">Gas heater temperature adjustment values</param>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> SetSampleHeaterTemp(MinMax values) => Set("controller/sample/heaterTemperature", values);

    /// <summary>Gets current sample gas heater temperature pid controller values</summary>
    /// <returns>Gas heater temperature pid controller values</returns>
    public Task<Response<PID>> GetSampleHeaterTempPID() => Get<PID>("controller/sample/heaterTemperature/pid");

    /// <summary>Sets current sample gas heater temperature pid controller values</summary>
    /// <param name="values">Gas heater temperature  pid controller values</param>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> SetSampleHeaterTempPID(PID values) => Set("controller/sample/heaterTemperature/pid", values);

    /// <summary>Gets whether the sample gas heater temperature  pump is directly adjusted</summary>
    /// <returns>Gas heater temperature direct control values</returns>
    public Task<Response<PumpDirectControl>> GetSampleHeaterTempPumpControl() => Get<PumpDirectControl>("controller/sample/heaterTemperature/directControl");

    /// <summary>Sets whether the sample gas heater temperature  pump is directly adjusted</summary>
    /// <param name="values">Gas heater temperature controller values</param>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> SetSampleHeaterTempPumpControl(PumpDirectControl values) => Set("controller/sample/heaterTemperature/directControl", values);

    /// <summary>Gets the sample temperature</summary>
    /// <returns>Temperature</returns>
    public Task<Response<RangeValue>> GetSampleTemperature() => Get<RangeValue>("controller/sample/temperature");

    /// <summary>Sets the sample temperature</summary>
    /// <param name="values">Temperature</param>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> SetSampleTemperature(MinMax values) => Set("controller/sample/temperature", values);

    /// <summary>Gets the sensor temperature</summary>
    /// <returns>Temperature</returns>
    public Task<Response<RangeValue>> GetSensorTemperature() => Get<RangeValue>("controller/sensor/temperature");

    /// <summary>Sets the sensor temperature</summary>
    /// <param name="values">Temperature</param>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> SetSensorTemperature(MinMax values) => Set("controller/sensor/temperature", values);

    /// <summary>Gets the ambient temperature</summary>
    /// <returns>Temperature</returns>
    public Task<Response<RangeValue>> GetAmbientTemperature() => Get<RangeValue>("controller/ambient/temperature");

    /// <summary>Sets the ambient temperature</summary>
    /// <param name="values">Temperature</param>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> SetAmbientTemperature(MinMax values) => Set("controller/senambientsor/temperature", values);

    /// <summary>Gets the sample pressure</summary>
    /// <returns>Pressure</returns>
    public Task<Response<RangeValue>> GetSamplePressure() => Get<RangeValue>("controller/sample/pressure");

    /// <summary>Sets the sample pressure</summary>
    /// <param name="values">Pressure</param>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> SetSamplePressure(MinMax values) => Set("controller/sample/pressure", values);

    /// <summary>Gets the sensor pressure</summary>
    /// <returns>Pressure</returns>
    public Task<Response<RangeValue>> GetSensorPressure() => Get<RangeValue>("controller/sensor/pressure");

    /// <summary>Sets the sensor pressure</summary>
    /// <param name="values">Pressure</param>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> SetSensorPressure(MinMax values) => Set("controller/sensor/pressure", values);

    /// <summary>Gets the ambient pressure</summary>
    /// <returns>Pressure</returns>
    public Task<Response<RangeValue>> GetAmbientPressure() => Get<RangeValue>("controller/ambient/pressure");

    /// <summary>Sets the ambient pressure</summary>
    /// <param name="values">Pressure</param>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> SetAmbientPressure(MinMax values) => Set("controller/senambientsor/pressure", values);

    /// <summary>Gets the sample humidity</summary>
    /// <returns>Humidity</returns>
    public Task<Response<RangeValue>> GetSampleHumidity() => Get<RangeValue>("controller/sample/humidity");

    /// <summary>Sets the sample humidity</summary>
    /// <param name="values">Humidity</param>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> SetSampleHumidity(MinMax values) => Set("controller/sample/humidity", values);

    /// <summary>Gets the sensor humidity</summary>
    /// <returns>Humidity</returns>
    public Task<Response<RangeValue>> GetSensorHumidity() => Get<RangeValue>("controller/sensor/humidity");

    /// <summary>Sets the sensor humidity</summary>
    /// <param name="values">Humidity</param>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> SetSensorHumidity(MinMax values) => Set("controller/sensor/humidity", values);

    /// <summary>Gets the ambient humidity</summary>
    /// <returns>Humidity</returns>
    public Task<Response<RangeValue>> GetAmbientHumidity() => Get<RangeValue>("controller/ambient/humidity");

    /// <summary>Sets the ambient humidity</summary>
    /// <param name="values">Humidity</param>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> SetAmbientHumidity(MinMax values) => Set("controller/senambientsor/humidity", values);

    /// <summary>Gets FET temperature</summary>
    /// <returns>FET temperature</returns>
    public Task<Response<RangeValue>> GetFETTemperature() => Get<RangeValue>("controller/miscellaneous/fetTemperature");

    /// <summary>Sets FET temperature</summary>
    /// <param name="values">FET temperature</param>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> SetFETTemperature(MinMax values) => Set("controller/miscellaneous/fetTemperature", values);

    #endregion

    #region RESULTS

    /// <summary>Gets the list of project scans</summary>
    /// <param name="maxResults">How many results to fetch at a time.May be limited if the results are too large.</param>
    /// <param name="page">If there are more results than maxResults, the results are divided into maxResults length pages. List results from this page.</param>
    /// <param name="search">Filter the results using this free form text search. It searches from the comments object, the name of the used parameter preset, the name of the user who performed the scan and the project used for the scan.</param>
    /// <param name="startDate">The earliest date from which the data is searched from.</param>
    /// <param name="date">The latest date to which the data is searched to.</param>
    /// <param name="sortBy">Sort the results by: date_asc or date_dsc</param>
    /// <param name="onlyMetadata">Get only limited metadata of the results instead of full result objects.</param>
    /// <param name="ids">List of result IDs to query</param>
    /// <returns>List of scan IDs</returns>
    public Task<Response<SearchResult>> GetResults(
        int? maxResults = null,
        int? page = null,
        string? search = null,
        string? startDate = null,
        string? date = null,
        string? sortBy = null,
        bool? onlyMetadata = null,
        string[]? ids = null) => Get<SearchResult>("result" + ToParams(
            ("max_results", maxResults),
            ("page", page),
            ("search", search),
            ("start_date", startDate),
            ("date", date),
            ("sortBy", sortBy),
            ("onlyMetadata", onlyMetadata),
            ("ids", ids)
        ));

    /// <summary>Deletes scans</summary>
    /// <param name="list">List of scan IDs</param>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> DeleteResults(ListOfIDs list) => Remove($"results", list);

    /// <summary>Retrieves the latest scan</summary>
    /// <returns>Scan data</returns>
    public Task<Response<ScanResult>> GetLatestResult() => Get<ScanResult>("results/latest");

    /// <summary>Retrieves gases of the latest scan</summary>
    /// <returns>Gases</returns>
    public Task<Response<string[]>> GetLatestResultGases() => Get<string[]>("results/latest/gasDetection");

    /// <summary>Retrieves the scan</summary>
    /// <param name="id">Scan id</param>
    /// <returns>Scan data</returns>
    public Task<Response<ScanResult>> GetResult(string id) => Get<ScanResult>($"results/id/{id}");

    /// <summary>Deletes the scan</summary>
    /// <param name="id">Scan id</param>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> DeleteResult(string id) => Remove($"results/id/{id}");

    /// <summary>Copies the scan</summary>
    /// <param name="id">Scan id</param>
    /// <param name="props">Copying properties</param>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> CopyResult(string id, CopyResultProperties props) => Create($"results/id/{id}/copy", props);

    /// <summary>Retrieves the scan comments</summary>
    /// <param name="id">Scan id</param>
    /// <returns>Comments</returns>
    public Task<Response<object>> GetResultComments(string id) => Get<object>($"results/id/{id}/comments");

    /// <summary>Sets the scan comments</summary>
    /// <param name="id">Scan id</param>
    /// <param name="comments">Comments</param>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> SetResultComments(string id, object comments) => Set($"results/id/{id}/comments", comments);

    /// <summary>Retrieves the scan gases</summary>
    /// <param name="id">Scan id</param>
    /// <returns>Gases</returns>
    public Task<Response<string[]>> GetResultGases(string id) => Get<string[]>($"results/id/{id}/gasDetection");

    #endregion

    #region SYSTEM

    /// <summary> Retrieves the system status</summary>
    /// <returns>System status</returns>
    public Task<Response<SystemStatus>> GetSystemStatus() => Get<SystemStatus>("system/status");

    /// <summary>Resets the gas filter usage counter</summary>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> ResetGasFilter() => Create("system/status/resetGasFilter");

    /// <summary>Retrieves the calibration</summary>
    /// <returns>Calibration</returns>
    public Task<Response<Calibration>> GetCalibration() => Get<Calibration>("system/calibration");

    /// <summary>Starts new calibration</summary>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> StartCalibration() => Create("system/calibration");

    /// <summary>Retrieves the storage devices</summary>
    /// <returns>List of devices</returns>
    public Task<Response<Device[]>> GetDevices() => Get<Device[]>("system/devices");

    /// <summary>Retrieves the storage devices</summary>
    /// <returns>List of devices</returns>
    public Task<Response<SystemInfo>> GetSystemInfo() => Get<SystemInfo>("system/update/info");

    // Skipped:
    // GET /system/update
    // POST /system/update
    // GET /system/debug
    // GET /system/debug/logs

    /// <summary>Retrieves a list last errors</summary>
    /// <returns>List of errors</returns>
    public Task<Response<string[]>> GetErrors() => Get<string[]>("system/errors");

    /// <summary>Retrieves a list of limit errors</summary>
    /// <returns>List of limit errors</returns>
    public Task<Response<ErrorRegister>> GetLimitErrors() => Get<ErrorRegister>("system/errors/valueLimits");

    // Skipped:
    // GET /system/reset
    // POST /system/reset
    // GET /system/licenses

    #endregion

    #region SETTINGS

    /// <summary>Retrieves the system clock</summary>
    /// <returns>Clock</returns>
    public Task<Response<Clock>> GetClock() => Get<Clock>("settings/clock");

    /// <summary>Sets the system clock</summary>
    /// <param name="clock">clock</param>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> SetClock(ClockToSet clock) => Set("settings/clock", clock);

    /// <summary>Get a list of timezones</summary>
    /// <returns>List of timezones</returns>
    public Task<Response<Timezone[]>> GetClockTimezones() => Get<Timezone[]>("settings/clock");

    /// <summary>Get the keyboard</summary>
    /// <returns>Keyboard</returns>
    public Task<Response<KeyboardAndModel>> GetKeyboard() => Get<KeyboardAndModel>("settings/keyboard");

    /// <summary>Sets a keyboard</summary>
    /// <param name="keyboard">keyboard</param>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> SetKeyboard(Keyboard keyboard) => Set("settings/keyboard", keyboard);

    /// <summary>Get the available keyboard layouts</summary>
    /// <returns>Layouts</returns>
    public Task<Response<string[]>> GetAvailableKeyboardLayouts() => Get<string[]>("settings/keyboard/layout");

    /// <summary>Get the available keyboard layout variants</summary>
    /// <returns>Layouts</returns>
    public Task<Response<KeyboardLayout>> GetAvailableKeyboardLayouts(string layout) => Get<KeyboardLayout>($"settings/keyboard/layout/{layout}");

    /// <summary>Get a list of external locations data is currently saved to</summary>
    /// <returns>List of locations</returns>
    public Task<Response<string[]>> GetDataSaveLocations() => Get<string[]>("settings/dataSaveLocations");

    /// <summary>Set a list of external locations data will be saved to</summary>
    /// <param name="locations">List of locations</param>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> SetDataSaveLocations(string[] locations) => Set("settings/dataSaveLocations", locations);

    /// <summary>Get a list of available external locations to save data to</summary>
    /// <returns>List of available locations</returns>
    public Task<Response<string[]>> GetAvailableDataSaveLocations() => Get<string[]>("settings/dataSaveLocations");

    #endregion

    // Skipping:
    // * /graphColour/*

    // Skipping:
    // * /backups/*

    #region OTHER

    /// <summary>Reboots the system</summary>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> Reboot() => Create("reboot");

    /// <summary>Shutdowns the system</summary>
    /// <returns>Confirmation message</returns>
    public Task<Response<Confirm>> Shutdown(bool? force = null) => Create("shutdown" + ToParams(("force", force)));

    #endregion

    // Skipping:
    // GET /olfactomics/*

    // Internal

    readonly RestClient _client;
    readonly JsonSerializerOptions _serializationOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private async Task<Response<T>> Get<T>(string path, bool preserveCase = false) where T : class
    {
        var request = new RestRequest(path);
        try
        {
            var response = await _client.GetAsync(request);
            return response.As<T>(preserveCase);
        }
        catch (Exception ex)
        {
            return new Response<T>(null, ex.Message);
        }
    }

    private async Task<Response<Confirm>> Set<T>(string path, T? param = null, bool preserveCase = false) where T : class
    {
        var request = new RestRequest(path);
        if (param != null)
        {
            request.AddBody(JsonSerializer.Serialize(param, preserveCase ? null : _serializationOptions));
        }
        try
        {
            var response = await _client.PutAsync(request);
            return response.As<Confirm>();
        }
        catch (Exception ex)
        {
            return new Response<Confirm>(null, ex.Message);
        }
    }

    private async Task<Response<U>> Create<T, U>(string path, T? param, bool preserveParamCase = false, bool preserveResponseCase = false)
        where T : class
        where U : class
    {
        var request = new RestRequest(path);
        if (param != null)
        {
            request.AddBody(JsonSerializer.Serialize(param, preserveParamCase ? null : _serializationOptions));
        }
        try
        {
            var response = await _client.PostAsync(request);
            return response.As<U>(preserveResponseCase);
        }
        catch (Exception ex)
        {
            return new Response<U>(null, ex.Message);
        }
    }
    private Task<Response<Confirm>> Create<T>(string path, T? param = null, bool preserveCase = false) where T : class =>
        Create<T, Confirm>(path, param, preserveCase, false);
    private Task<Response<Confirm>> Create(string path) => Create<object>(path);

    private async Task<Response<Confirm>> Remove<T>(string path, T? param = null, bool preserveCase = false) where T : class
    {
        var request = new RestRequest(path);
        if (param != null)
        {
            request.AddBody(JsonSerializer.Serialize(param, preserveCase ? null : _serializationOptions));
        }
        try
        {
            var response = await _client.DeleteAsync(request);
            return response.As<Confirm>();
        }
        catch (Exception ex)
        {
            return new Response<Confirm>(null, ex.Message);
        }
    }

    private Task<Response<Confirm>> Remove(string path) => Remove<object>(path);

    private static string ToParams(params (string, object?)[] list)
    {
        List<string> result = new();
        foreach (var item in list)
        {
            if (item.Item2 != null)
            {
                if (item.Item2 is Array array)
                {
                    result.Add($"{item.Item1}={string.Join(',', array)}");
                }
                else
                {
                    result.Add($"{item.Item1}={item.Item2}");
                }
            }
        }
        return (result.Count > 0 ? "?" : "") + string.Join("&", result);
    }
}

internal static class RestResponseExtension
{
    public static Response<T> As<T>(this RestResponse response, bool preserveCase = false)
    {
        T? value = default;
        string? error = null;

        int maxDataLengthToPrint = 80;
        string data = response.Content!;
        if (data.Length > maxDataLengthToPrint)
        {
            data = data[..maxDataLengthToPrint] + "...";
        }

        ScreenLogger.Print($"[IvAPI] {(int)response.StatusCode} ({response.StatusDescription}), {data} ({response.ContentLength} bytes)");

        if (response.IsSuccessful)
        {
            var serializerOptions = preserveCase ? new JsonSerializerOptions() : _serializerOptions;
            value = JsonSerializer.Deserialize<T>(response.Content!, serializerOptions)!;
        }
        else
        {
            var err = JsonSerializer.Deserialize<Err>(response.Content!, _serializerOptions)!;
            if (err.Errors != null)
            {
                error = string.Join('\n', err.Errors);
            }
            else if (err.Message != null)
            {
                error = err.Message;
            }
            else
            {
                error = $"Unrecognized error structure: {response.Content}";
            }
        }
        return new Response<T>(value, error);
    }

    static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
