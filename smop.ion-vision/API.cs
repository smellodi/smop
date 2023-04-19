using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using RestSharp;

namespace Smop.IonVision;

public class API : IMinimalAPI
{
    public class Response<T>
    {
        public T? Value { get; private set; }
        public string? Error { get; private set; }
        public bool Success => Value != null;
        public Response(T? value, string? error)
        {
            Value = value;
            Error = error;
        }
    }

    public API(string ip)
    {
        var host = $"http://{ip}/api";
        _client = new RestClient(host);
        _client.AddDefaultHeader("Content-Type", "application/json");
    }

    /// <summary>
    /// Retrieves scan progress
    /// </summary>
    /// <returns>Scan progress</returns>
    public async Task<Response<ScanProgress>> GetScanProgress()
    {
        var request = new RestRequest("currentScan");
        var response = await _client.GetAsync(request);
        return response.As<ScanProgress>();
    }

    /// <summary>
    /// Starts a new scan
    /// </summary>
    /// <returns>Confirmation message</returns>
    public async Task<Response<Confirm>> StartScan()
    {
        var request = new RestRequest("currentScan");
        try
        {
            var response = await _client.PostAsync(request);
            return response.As<Confirm>();
        }
        catch (Exception ex)
        {
            return new Response<Confirm>(null, ex.Message);
        }
    }

    /// <summary>
    /// Stops the ongoing scan
    /// </summary>
    /// <returns>Confirmations message</returns>
    public async Task<Response<Confirm>> StopScan()
    {
        var request = new RestRequest("currentScan");
        var response = await _client.DeleteAsync(request);
        return response.As<Confirm>();
    }

    /// <summary>
    /// Retrieves latest scan comments
    /// </summary>
    /// <returns>Comments</returns>
    public async Task<Response<Comments>> GetScanComments()
    {
        var request = new RestRequest("currentScan/comments");
        var response = await _client.GetAsync(request);
        return response.As<Comments>();
    }

    /// <summary>
    /// Sets latest scan comments
    /// </summary>
    /// <param name="comment">Comment</param>
    /// <returns>Conrimation</returns>
    public async Task<Response<Confirm>> SetScanComments(Comments comment)
    {
        var request = new RestRequest("currentScan/comments");
        request.AddBody(JsonSerializer.Serialize(comment, _serializationOptions));
        var response = await _client.PutAsync(request);
        return response.As<Confirm>();
    }

    /// <summary>
    /// Check if the device is in scope mode
    /// </summary>
    /// <returns>Scope status</returns>
    public async Task<Response<ScopeStatus>> CheckScopeMode()
    {
        var request = new RestRequest("scope");
        var response = await _client.GetAsync(request);
        return response.As<ScopeStatus>();
    }

    /// <summary>
    /// Set the device to the scope mode
    /// </summary>
    /// <returns>Confirmation message</returns>
    public async Task<Response<Confirm>> EnableScopeMode()
    {
        var request = new RestRequest("scope");
        var response = await _client.PutAsync(request);
        return response.As<Confirm>();
    }

    /// <summary>
    /// Move the device back to idle.
    /// </summary>
    /// <returns>Confirmation message</returns>
    public async Task<Response<Confirm>> DisableScopeMode()
    {
        var request = new RestRequest("scope");
        var response = await _client.DeleteAsync(request);
        return response.As<Confirm>();
    }

    /// <summary>
    /// Retrieves the latest scope result
    /// </summary>
    /// <returns>Scope result</returns>
    public async Task<Response<ScopeResult>> GetScopeResult()
    {
        var request = new RestRequest("scope/latestResult");
        var response = await _client.GetAsync(request);
        return response.As<ScopeResult>();
    }

    /// <summary>
    /// Retrieves the scope parameters
    /// </summary>
    /// <returns>Scope parameters</returns>
    public async Task<Response<ScopeParameters>> GetScopeParameters()
    {
        var request = new RestRequest("scope/parameters");
        var response = await _client.GetAsync(request);
        return response.As<ScopeParameters>();
    }

    /// <summary>
    /// Sets the scope parameters
    /// </summary>
    /// <param name="parameters">Scope parameters</param>
    /// <returns>Confirmation message</returns>
    public async Task<Response<Confirm>> SetScopeParameters(ScopeParameters parameters)
    {
        var request = new RestRequest("scope/parameters");
        request.AddBody(JsonSerializer.Serialize(parameters, _serializationOptions));
        var response = await _client.PutAsync(request);
        return response.As<Confirm>();
    }

    /// <summary>
    /// Retrieves user
    /// </summary>
    /// <returns>User name</returns>
    public async Task<Response<User>> GetUser()
    {
        var request = new RestRequest("currentUser");
        var response = await _client.GetAsync(request);
        return response.As<User>();
    }

    /// <summary>
    /// Sets user
    /// </summary>
    /// <param name="user">User name</param>
    /// <returns>Confirmation message</returns>
    public async Task<Response<Confirm>> SetUser(User user)
    {
        var request = new RestRequest("currentUser");
        request.AddBody(JsonSerializer.Serialize(user, _serializationOptions));
        var response = await _client.PutAsync(request);
        return response.As<Confirm>();
    }

    /// <summary>
    /// Retrieves current parameter
    /// </summary>
    /// <returns>Parameter</returns>
    public async Task<Response<ParameterAsNameAndId>> GetParameter()
    {
        var request = new RestRequest("currentParameter");
        var response = await _client.GetAsync(request);
        return response.As<ParameterAsNameAndId>();
    }

    /// <summary>
    /// Sets current parameter
    /// </summary>
    /// <param name="parameter">Parameter</param>
    /// <returns>Confirmation message</returns>
    public async Task<Response<Confirm>> SetParameter(ParameterAsId parameter)
    {
        var request = new RestRequest("currentParameter");
        request.AddBody(JsonSerializer.Serialize(parameter, _serializationOptions));
        var response = await _client.PutAsync(request);
        return response.As<Confirm>();
    }

    /// <summary>
    /// Proloads current parameter
    /// </summary>
    /// <returns>Confirmation message</returns>
    public async Task<Response<Confirm>> PreloadParameter()
    {
        var request = new RestRequest("currentParameter/preload");
        var response = await _client.PostAsync(request);
        return response.As<Confirm>();
    }

    /// <summary>
    /// Retrieves a list of parameters
    /// </summary>
    /// <returns>Parameters</returns>
    public async Task<Response<Parameter[]>> GetParameters()
    {
        var request = new RestRequest("parameter");
        var response = await _client.GetAsync(request);
        return response.As<Parameter[]>();
    }

    /// <summary>
    /// Creates a new parameter
    /// </summary>
    /// <param name="parameter">New parameter definition</param>
    /// <returns>Parameter as name</returns>
    public async Task<Response<ParameterAsId>> CreateParameter(ParameterDefinition parameter)
    {
        var request = new RestRequest("parameter");
        request.AddBody(JsonSerializer.Serialize(parameter));
        var response = await _client.PostAsync(request);
        return response.As<ParameterAsId>();
    }

    /// <summary>
    /// Retrieves the parameter definition
    /// </summary>
    /// <param name="parameter">Parameter</param>
    /// <returns>Parameter definition</returns>
    public async Task<Response<ParameterDefinition>> GetParameterDefinition(Parameter parameter)
    {
        var request = new RestRequest($"parameter/{parameter.Id}");
        var response = await _client.GetAsync(request);
        return response.As<ParameterDefinition>(true);
    }

    /// <summary>
    /// Creates a new parameter
    /// Valid only if no scan was performed with this parameter
    /// </summary>
    /// <param name="parameter">Parameter definition</param>
    /// <returns>Confirmation message</returns>
    public async Task<Response<Confirm>> UpdateParameterDefinition(ParameterDefinition parameter)
    {
        var request = new RestRequest($"parameter/{parameter.Id}");
        request.AddBody(JsonSerializer.Serialize(parameter));
        var response = await _client.PutAsync(request);
        return response.As<Confirm>();
    }

    /// <summary>
    /// Deletes the parameter
    /// </summary>
    /// <param name="parameter">Parameter</param>
    /// <returns>Confirmation message</returns>
    public async Task<Response<Confirm>> DeleteParameter(Parameter parameter)
    {
        var request = new RestRequest($"parameter/{parameter.Id}");
        var response = await _client.DeleteAsync(request);
        return response.As<Confirm>();
    }

    /// <summary>
    /// Retrieves the parameter metadata
    /// </summary>
    /// <param name="parameter">Parameter</param>
    /// <returns>Metadata</returns>
    public async Task<Response<ParameterMetadata>> GetParameterMedatada(Parameter parameter)
    {
        var request = new RestRequest($"parameter/{parameter.Id}/metadata");
        var response = await _client.GetAsync(request);
        return response.As<ParameterMetadata>(true);
    }

    /// <summary>
    /// Retrieves the parameter scan IDs
    /// </summary>
    /// <param name="parameter">Parameter</param>
    /// <returns>List of scan IDs</returns>
    public async Task<Response<string[]>> GetParameterResults(Parameter parameter)
    {
        var request = new RestRequest($"parameter/{parameter.Id}/results");
        var response = await _client.GetAsync(request);
        return response.As<string[]>();
    }

    /// <summary>
    /// Retrieves gases that can be detected with this parameter
    /// </summary>
    /// <param name="parameter">Parameter</param>
    /// <returns>List of gases</returns>
    public async Task<Response<Dictionary<string,List<string>>>> GetParameterGases(Parameter parameter)
    {
        var request = new RestRequest($"parameter/{parameter.Id}/gasDetection");
        var response = await _client.GetAsync(request);
        return response.As<Dictionary<string, List<string>>>();
    }

    /// <summary>
    /// Retrieves available parameter templates
    /// </summary>
    /// <param name="name">the name to search for</param>
    /// <param name="gasDetection">whether to search for templates with or without built-in gas detection</param>
    /// <returns>List of templates</returns>
    public async Task<Response<ParameterTemplate[]>> GetParameterTemplates(string? name = null, bool? gasDetection = null)
    {
        var request = new RestRequest($"parameterTemplate");
        if (name != null)
            request.AddQueryParameter("search", name);
        if (gasDetection != null)
            request.AddQueryParameter("gas_detection", gasDetection.ToString());
        var response = await _client.GetAsync(request);
        return response.As<ParameterTemplate[]>();
    }

    /// <summary>
    /// Retrieves the parameter template
    /// </summary>
    /// <param name="name">template name</param>
    /// <returns>Template as the parameter definition</returns>
    public async Task<Response<ParameterDefinition>> GetParameterTemplate(string name)
    {
        var request = new RestRequest($"parameterTemplate/{name}");
        var response = await _client.GetAsync(request);
        return response.As<ParameterDefinition>();
    }

    /// <summary>
    /// Retrieves the parameter template metadata
    /// </summary>
    /// <param name="name">template name</param>
    /// <returns>Template metadata</returns>
    public async Task<Response<ParameterTemplate>> GetParameterTemplateMetadata(string name)
    {
        var request = new RestRequest($"parameterTemplate/{name}/metadata");
        var response = await _client.GetAsync(request);
        return response.As<ParameterTemplate>();
    }

    /// <summary>
    /// Retrieves the project in use
    /// </summary>
    /// <returns>Project name</returns>
    public async Task<Response<ProjectAsName>> GetProject()
    {
        var request = new RestRequest("currentProject");
        var response = await _client.GetAsync(request);
        return response.As<ProjectAsName>();
    }

    /// <summary>
    /// Loads a project
    /// </summary>
    /// <param name="project">Project name</param>
    /// <returns>Confirmation message</returns>
    public async Task<Response<Confirm>> SetProject(ProjectAsName project)
    {
        var request = new RestRequest("currentProject");
        request.AddBody(JsonSerializer.Serialize(project, _serializationOptions));
        var response = await _client.PutAsync(request);
        return response.As<Confirm>();
    }

    /// <summary>
    /// Retrieves the list of projects
    /// </summary>
    /// <returns>List of projects</returns>
    public async Task<Response<string[]>> GetProjects()
    {
        var request = new RestRequest("project");
        var response = await _client.GetAsync(request);
        return response.As<string[]>();
    }

    /// <summary>
    /// Creates a new project
    /// </summary>
    /// <param name="project">Project</param>
    /// <returns>Confirmation message</returns>
    public async Task<Response<Confirm>> CreateProject(Project project)
    {
        var request = new RestRequest("project");
        request.AddBody(JsonSerializer.Serialize(project, _serializationOptions));
        var response = await _client.PostAsync(request);
        return response.As<Confirm>();
    }

    /// <summary>
    /// Gets the project definition
    /// </summary>
    /// <param name="name">Project</param>
    /// <returns>Project definition</returns>
    public async Task<Response<Project>> GetProjectDefinition(string name)
    {
        var request = new RestRequest($"project/{name}");
        var response = await _client.GetAsync(request);
        return response.As<Project>();
    }

    /// <summary>
    /// Updates the project
    /// </summary>
    /// <param name="name">Project name</param>
    /// <param name="project">New project</param>
    /// <returns>Confirmation message</returns>
    public async Task<Response<Confirm>> UpdateProject(string name, Project project)
    {
        var request = new RestRequest($"project/{name}");
        request.AddBody(JsonSerializer.Serialize(project, _serializationOptions));
        var response = await _client.PutAsync(request);
        return response.As<Confirm>();
    }

    /// <summary>
    /// Deleted the project
    /// </summary>
    /// <param name="name">Project name</param>
    /// <returns>Confirmation message</returns>
    public async Task<Response<Confirm>> DeleteProject(string name)
    {
        var request = new RestRequest($"project/{name}");
        var response = await _client.DeleteAsync(request);
        return response.As<Confirm>();
    }

    /// <summary>
    /// Gets the list of project scans
    /// </summary>
    /// <param name="name">Project name</param>
    /// <returns>List of scan IDs</returns>
    public async Task<Response<string[]>> GetProjectResults(string name)
    {
        var request = new RestRequest($"project/{name}/results");
        var response = await _client.GetAsync(request);
        return response.As<string[]>();
    }

    /// <summary>
    /// Get the sequence of parameter presets that is used when a measurement is done with the project.
    /// </summary>
    /// <param name="name">Project name</param>
    /// <returns>List of project parameters</returns>
    public async Task<Response<Parameter[]>> GetProjectSequence(string name)
    {
        var request = new RestRequest($"project/{name}/sequence");
        var response = await _client.GetAsync(request);
        return response.As<Parameter[]>();
    }

    /// <summary>
    /// Updates the list of project's parameters
    /// </summary>
    /// <param name="name">Project name</param>
    /// <param name="parameters">List of parameters</param>
    /// <returns>Confirmation message</returns>
    public async Task<Response<Confirm>> UpdateProjectParameters(string name, Parameter[] parameters)
    {
        var request = new RestRequest($"project/{name}/sequence");
        request.AddBody(JsonSerializer.Serialize(parameters, _serializationOptions));
        var response = await _client.PutAsync(request);
        return response.As<Confirm>();
    }


    /// <summary>
    /// Gets current sample flow adjustment values
    /// </summary>
    /// <returns>Flow adjustment values</returns>
    public async Task<Response<Flow>> GetSampleFlow()
    {
        var request = new RestRequest("controller/sample/flow");
        var response = await _client.GetAsync(request);
        return response.As<Flow>();
    }

    /// <summary>
    /// Sets current sample flow adjustment values
    /// </summary>
    /// <param name="values">Flow adjustment values</param>
    /// <returns>Confirmation message</returns>
    public async Task<Response<Confirm>> SetSampleFlow(Range values)
    {
        var request = new RestRequest("controller/sample/flow");
        request.AddBody(JsonSerializer.Serialize(values, _serializationOptions));
        var response = await _client.PutAsync(request);
        return response.As<Confirm>();
    }

    /// <summary>
    /// Gets current sample flow pid controller values.
    /// </summary>
    /// <returns>Flow pid controller values</returns>
    public async Task<Response<PID>> GetSampleFlowPID()
    {
        var request = new RestRequest("controller/sample/flow/pid");
        var response = await _client.GetAsync(request);
        return response.As<PID>();
    }

    /// <summary>
    /// Sets current sample flow pid controller values.
    /// </summary>
    /// <param name="values">Flow pid controller values</param>
    /// <returns>Confirmation message</returns>
    public async Task<Response<Confirm>> SetSampleFlowPID(PID values)
    {
        var request = new RestRequest("controller/sample/flow/pid");
        request.AddBody(JsonSerializer.Serialize(values, _serializationOptions));
        var response = await _client.PutAsync(request);
        return response.As<Confirm>();
    }

    /// <summary>
    /// Gets whether the sample flow pump is directly adjusted.
    /// </summary>
    /// <returns>Flow direct control values</returns>
    public async Task<Response<PumpDirectControl>> GetSampleFlowPumpControl()
    {
        var request = new RestRequest("controller/sample/flow/directControl");
        var response = await _client.GetAsync(request);
        return response.As<PumpDirectControl>();
    }

    /// <summary>
    /// Sets whether the sample flow pump is directly adjusted.
    /// </summary>
    /// <param name="values">Flow pid controller values</param>
    /// <returns>Confirmation message</returns>
    public async Task<Response<Confirm>> SetSampleFlowPumpControl(PumpDirectControl values)
    {
        var request = new RestRequest("controller/sample/flow/directControl");
        request.AddBody(JsonSerializer.Serialize(values, _serializationOptions));
        var response = await _client.PutAsync(request);
        return response.As<Confirm>();
    }

    /// <summary>
    /// Gets current sensor flow adjustment values
    /// </summary>
    /// <returns>Flow adjustment values</returns>
    public async Task<Response<Flow>> GetSensorFlow()
    {
        var request = new RestRequest("controller/sensor/flow");
        var response = await _client.GetAsync(request);
        return response.As<Flow>();
    }

    /// <summary>
    /// Sets current sensor flow adjustment values
    /// </summary>
    /// <param name="values">Flow adjustment values</param>
    /// <returns>Confirmation message</returns>
    public async Task<Response<Confirm>> SetSensorFlow(Range values)
    {
        var request = new RestRequest("controller/sensor/flow");
        request.AddBody(JsonSerializer.Serialize(values, _serializationOptions));
        var response = await _client.PutAsync(request);
        return response.As<Confirm>();
    }

    /// <summary>
    /// Gets current sensor flow pid controller values.
    /// </summary>
    /// <returns>Flow pid controller values</returns>
    public async Task<Response<PID>> GetSensorFlowPID()
    {
        var request = new RestRequest("controller/sensor/flow/pid");
        var response = await _client.GetAsync(request);
        return response.As<PID>();
    }

    /// <summary>
    /// Sets current sensor flow pid controller values.
    /// </summary>
    /// <param name="values">Flow pid controller values</param>
    /// <returns>Confirmation message</returns>
    public async Task<Response<Confirm>> SetSensorFlowPID(PID values)
    {
        var request = new RestRequest("controller/sensor/flow/pid");
        request.AddBody(JsonSerializer.Serialize(values, _serializationOptions));
        var response = await _client.PutAsync(request);
        return response.As<Confirm>();
    }

    /// <summary>
    /// Gets whether the sensor flow pump is directly adjusted.
    /// </summary>
    /// <returns>Flow direct control values</returns>
    public async Task<Response<PumpDirectControl>> GetSensorFlowPumpControl()
    {
        var request = new RestRequest("controller/sensor/flow/directControl");
        var response = await _client.GetAsync(request);
        return response.As<PumpDirectControl>();
    }

    /// <summary>
    /// Sets whether the sensor flow pump is directly adjusted.
    /// </summary>
    /// <param name="values">Flow pid controller values</param>
    /// <returns>Confirmation message</returns>
    public async Task<Response<Confirm>> SetSensorFlowPumpControl(PumpDirectControl values)
    {
        var request = new RestRequest("controller/sensor/flow/directControl");
        request.AddBody(JsonSerializer.Serialize(values, _serializationOptions));
        var response = await _client.PutAsync(request);
        return response.As<Confirm>();
    }

    /// <summary>
    /// Gets current sample gas heater temperature adjustment values.
    /// </summary>
    /// <returns>Gas heater temperature adjustment values</returns>
    public async Task<Response<Flow>> GetSampleHeaterTemp()
    {
        var request = new RestRequest("controller/sample/heaterTemperature");
        var response = await _client.GetAsync(request);
        return response.As<Flow>();
    }

    /// <summary>
    /// Sets current sample gas heater temperature adjustment values.
    /// </summary>
    /// <param name="values">Gas heater temperature adjustment values</param>
    /// <returns>Confirmation message</returns>
    public async Task<Response<Confirm>> SetSampleHeaterTemp(Range values)
    {
        var request = new RestRequest("controller/sample/heaterTemperature");
        request.AddBody(JsonSerializer.Serialize(values, _serializationOptions));
        var response = await _client.PutAsync(request);
        return response.As<Confirm>();
    }

    /// <summary>
    /// Gets current sample gas heater temperature pid controller values.
    /// </summary>
    /// <returns>Gas heater temperature pid controller values</returns>
    public async Task<Response<PID>> GetSampleHeaterTempPID()
    {
        var request = new RestRequest("controller/sample/heaterTemperature/pid");
        var response = await _client.GetAsync(request);
        return response.As<PID>();
    }

    /// <summary>
    /// Sets current sample gas heater temperature pid controller values.
    /// </summary>
    /// <param name="values">Gas heater temperature  pid controller values</param>
    /// <returns>Confirmation message</returns>
    public async Task<Response<Confirm>> SetSampleHeaterTempPID(PID values)
    {
        var request = new RestRequest("controller/sample/heaterTemperature/pid");
        request.AddBody(JsonSerializer.Serialize(values, _serializationOptions));
        var response = await _client.PutAsync(request);
        return response.As<Confirm>();
    }

    /// <summary>
    /// Gets whether the sample gas heater temperature  pump is directly adjusted.
    /// </summary>
    /// <returns>Gas heater temperature direct control values</returns>
    public async Task<Response<PumpDirectControl>> GetSampleHeaterTempPumpControl()
    {
        var request = new RestRequest("controller/sample/heaterTemperature/directControl");
        var response = await _client.GetAsync(request);
        return response.As<PumpDirectControl>();
    }

    /// <summary>
    /// Sets whether the sample gas heater temperature  pump is directly adjusted.
    /// </summary>
    /// <param name="values">Gas heater temperature controller values</param>
    /// <returns>Confirmation message</returns>
    public async Task<Response<Confirm>> SetSampleHeaterTempPumpControl(PumpDirectControl values)
    {
        var request = new RestRequest("controller/sample/heaterTemperature/directControl");
        request.AddBody(JsonSerializer.Serialize(values, _serializationOptions));
        var response = await _client.PutAsync(request);
        return response.As<Confirm>();
    }

    // Skipped:
    // * /controller/sample/temperature
    // * /controller/sensor/temperature
    // * /controller/ambient/temperature
    // * /controller/sample/pressure
    // * /controller/sensor/pressure
    // * /controller/ambient/pressure
    // * /controller/sample/humidity
    // * /controller/sensor/humidity
    // * /controller/ambient/humidity
    // * /controller/miscellaneous/fetTemperature


    /// <summary>
    /// Gets the list of project scans
    /// </summary>
    /// <param name="max_results">How many results to fetch at a time.May be limited if the results are too large.</param>
    /// <param name="page">If there are more results than maxResults, the results are divided into maxResults length pages. List results from this page.</param>
    /// <param name="search">Filter the results using this free form text search. It searches from the comments object, the name of the used parameter preset, the name of the user who performed the scan and the project used for the scan.</param>
    /// <param name="start_date">The earliest date from which the data is searched from.</param>
    /// <param name="date">The latest date to which the data is searched to.</param>
    /// <param name="sort_by">Sort the results by: date_asc or date_dsc</param>
    /// <param name="only_metadata">Get only limited metadata of the results instead of full result objects.</param>
    /// <returns>List of scan IDs</returns>
    public async Task<Response<SearchResult>> GetResults(
        int? maxResults = null,
        int? page = null,
        string? search = null,
        string? startDate = null,
	        string? date = null,
        string? sortBy = null,
        bool? onlyMetadata = null,
        string[]? ids = null)
    {
        var request = new RestRequest("results");
        if (maxResults != null) request.AddParameter("max_results", maxResults.ToString());
        if (page != null) request.AddParameter("page", page.ToString());
        if (search != null) request.AddParameter("search", search.ToString());
        if (startDate != null) request.AddParameter("start_date", startDate.ToString());
        if (date != null) request.AddParameter("date", date.ToString());
        if (sortBy != null) request.AddParameter("sort_by", sortBy.ToString());
        if (onlyMetadata != null) request.AddParameter("only_metadata", onlyMetadata.ToString());
        if (ids != null) request.AddParameter("ids", string.Join(',', ids));

        var response = await _client.GetAsync(request);
        return response.As<SearchResult>();
    }

    /// <summary>
    /// Deletes scans
    /// </summary>
    /// <param name="list">List of scan IDs</param>
    /// <returns>Confirmation message</returns>
    public async Task<Response<Confirm>> DeleteResults(ListOfIDs list)
    {
        var request = new RestRequest($"results");
        request.AddBody(JsonSerializer.Serialize(list, _serializationOptions));
        var response = await _client.DeleteAsync(request);
        return response.As<Confirm>();
    }

    /// <summary>
    /// Retrieves the latest scan
    /// </summary>
    /// <returns>Scan data</returns>
    public async Task<Response<ScanResult>> GetLatestResult()
    {
        var request = new RestRequest("results/latest");
        var response = await _client.GetAsync(request);
        return response.As<ScanResult>();
    }

    /// <summary>
    /// Retrieves gases of the latest scan
    /// </summary>
    /// <returns>Gases</returns>
    public async Task<Response<string[]>> GetLatestResultGases()
    {
        var request = new RestRequest("results/latest/gasDetection");
        var response = await _client.GetAsync(request);
        return response.As<string[]>();
    }

    /// <summary>
    /// Retrieves the scan
    /// </summary>
    /// <param name="id">Scan id</param>
    /// <returns>Scan data</returns>
    public async Task<Response<ScanResult>> GetResult(string id)
    {
        var request = new RestRequest($"results/id/{id}");
        var response = await _client.GetAsync(request);
        return response.As<ScanResult>();
    }

    /// <summary>
    /// Deletes the scan
    /// </summary>
    /// <param name="id">Scan id</param>
    /// <returns>Confirmation message</returns>
    public async Task<Response<Confirm>> DeleteResult(string id)
    {
        var request = new RestRequest($"results/id/{id}");
        var response = await _client.DeleteAsync(request);
        return response.As<Confirm>();
    }

    /// <summary>
    /// Copies the scan
    /// </summary>
    /// <param name="id">Scan id</param>
    /// <param name="props">Copying properties</param>
    /// <returns>Confirmation message</returns>
    public async Task<Response<Confirm>> CopyResult(string id, CopyResultProperties props)
    {
        var request = new RestRequest($"results/id/{id}/copy");
        request.AddBody(JsonSerializer.Serialize(props, _serializationOptions));
        var response = await _client.PostAsync(request);
        return response.As<Confirm>();
    }

    /// <summary>
    /// Retrieves the scan comments
    /// </summary>
    /// <param name="id">Scan id</param>
    /// <returns>Comments</returns>
    public async Task<Response<Comments>> GetResultComments(string id)
    {
        var request = new RestRequest($"results/id/{id}/comments");
        var response = await _client.GetAsync(request);
        return response.As<Comments>();
    }

    /// <summary>
    /// Sets the scan comments
    /// </summary>
    /// <param name="id">Scan id</param>
    /// <param name="comments">Comments</param>
    /// <returns>Confirmation message</returns>
    public async Task<Response<Confirm>> GetResultComments(string id, Comments comments)
    {
        var request = new RestRequest($"results/id/{id}/comments");
        request.AddBody(JsonSerializer.Serialize(comments, _serializationOptions));
        var response = await _client.PutAsync(request);
        return response.As<Confirm>();
    }

    /// <summary>
    /// Retrieves the scan gases
    /// </summary>
    /// <param name="id">Scan id</param>
    /// <returns>Gases</returns>
    public async Task<Response<string[]>> GetResultGases(string id)
    {
        var request = new RestRequest($"results/id/{id}/gasDetection");
        var response = await _client.GetAsync(request);
        return response.As<string[]>();
    }

    /// <summary>
    /// Retrieves the system status
    /// </summary>
    /// <returns>System status</returns>
    public async Task<Response<SystemStatus>> GetSystemStatus()
    {
        var request = new RestRequest("system/status");
        var response = await _client.GetAsync(request);
        return response.As<SystemStatus>();
    }

    /// <summary>
    /// Resets the gas filter usage counter.
    /// </summary>
    /// <returns>Confirmation message</returns>
    public async Task<Response<Confirm>> ResetGasFilter()
    {
        var request = new RestRequest("system/status/resetGasFilter");
        var response = await _client.PostAsync(request);
        return response.As<Confirm>();
    }

    /// <summary>
    /// Retrieves the calibration
    /// </summary>
    /// <returns>Calibration</returns>
    public async Task<Response<Calibration>> GetCalibration()
    {
        var request = new RestRequest("system/calibration");
        var response = await _client.GetAsync(request);
        return response.As<Calibration>();
    }

    /// <summary>
    /// Starts new calibration
    /// </summary>
    /// <returns>Confirmation message</returns>
    public async Task<Response<Confirm>> StartCalibration()
    {
        var request = new RestRequest("system/calibration");
        var response = await _client.PostAsync(request);
        return response.As<Confirm>();
    }

    /// <summary>
    /// Retrieves the storage devices
    /// </summary>
    /// <returns>List of devices</returns>
    public async Task<Response<Device[]>> GetDevices()
    {
        var request = new RestRequest("system/devices");
        var response = await _client.GetAsync(request);
        return response.As<Device[]>();
    }

    // Skipped:
    // GET /system/update
    // POST /system/update
    // GET /system/debug
    // GET /system/debug/logs
    // GET /system/errors
    // GET /system/errors/valueLimits
    // GET /system/reset
    // POST /system/reset
    // GET /system/licenses

    /// <summary>
    /// Retrieves the system clock
    /// </summary>
    /// <returns>Clock</returns>
    public async Task<Response<Clock>> GetClock()
    {
        var request = new RestRequest("settings/clock");
        var response = await _client.GetAsync(request);
        return response.As<Clock>();
    }

    /// <summary>
    /// Sets the system clock
    /// </summary>
    /// <param name="clock">clock</param>
    /// <returns>Confirmation message</returns>
    public async Task<Response<Confirm>> SetClock(Clock clock)
    {
        var request = new RestRequest("settings/clock");
        request.AddBody(JsonSerializer.Serialize(clock, _serializationOptions));
        var response = await _client.PutAsync(request);
        return response.As<Confirm>();
    }

    /// <summary>
    /// Get a list of timezones
    /// </summary>
    /// <returns>List of timezones</returns>
    public async Task<Response<Timezone[]>> GetClockTimezones()
    {
        var request = new RestRequest("settings/clock");
        var response = await _client.OptionsAsync(request);
        return response.As<Timezone[]>();
    }

    /// <summary>
    /// Get the keyboard
    /// </summary>
    /// <returns>Keyboard</returns>
    public async Task<Response<Keyboard>> GetKeyboard()
    {
        var request = new RestRequest("settings/keyboard");
        var response = await _client.GetAsync(request);
        return response.As<Keyboard>();
    }

    /// <summary>
    /// Sets a keyboard
    /// </summary>
    /// <param name="keyboard">keyboard</param>
    /// <returns>Confirmation message</returns>
    public async Task<Response<Confirm>> SetKeyboard(Keyboard keyboard)
    {
        var request = new RestRequest("settings/keyboard");
        request.AddBody(JsonSerializer.Serialize(keyboard, _serializationOptions));
        var response = await _client.PutAsync(request);
        return response.As<Confirm>();
    }

    /// <summary>
    /// Get the available keyboard layouts
    /// </summary>
    /// <returns>Layouts</returns>
    public async Task<Response<string[]>> GetAvailableKeyboardLayouts()
    {
        var request = new RestRequest("settings/keyboard/layout");
        var response = await _client.GetAsync(request);
        return response.As<string[]>();
    }

    /// <summary>
    /// Get the available keyboard layout variants
    /// </summary>
    /// <returns>Layouts</returns>
    public async Task<Response<KeyboardLayout>> GetAvailableKeyboardLayouts(string layout)
    {
        var request = new RestRequest($"settings/keyboard/layout/{layout}");
        var response = await _client.GetAsync(request);
        return response.As<KeyboardLayout>();
    }

    /// <summary>
    /// Get a list of external locations data is currently saved to
    /// </summary>
    /// <returns>List of locations</returns>
    public async Task<Response<string[]>> GetDataSaveLocations()
    {
        var request = new RestRequest("settings/dataSaveLocations");
        var response = await _client.GetAsync(request);
        return response.As<string[]>();
    }

    /// <summary>
    /// Set a list of external locations data will be saved to
    /// </summary>
    /// <param name="locations">List of locations</param>
    /// <returns>Confirmation message</returns>
    public async Task<Response<string[]>> GetDataSaveLocations(string[] locations)
    {
        var request = new RestRequest("settings/dataSaveLocations");
        request.AddBody(JsonSerializer.Serialize(locations, _serializationOptions));
        var response = await _client.PutAsync(request);
        return response.As<string[]>();
    }

    /// <summary>
    /// Get a list of available external locations to save data to
    /// </summary>
    /// <returns>List of available locations</returns>
    public async Task<Response<string[]>> GetAvailableDataSaveLocations()
    {
        var request = new RestRequest("settings/dataSaveLocations");
        var response = await _client.OptionsAsync(request);
        return response.As<string[]>();
    }

    // Skipping:
    // GET /graphColour/*

    // Skipping:
    // GET /backups/*

    /// <summary>
    /// Reboots the system
    /// </summary>
    /// <returns>Confirmation message</returns>
    public async Task<Response<Confirm>> Reboot()
    {
        var request = new RestRequest("reboot");
        var response = await _client.PostAsync(request);
        return response.As<Confirm>();
    }

    /// <summary>
    /// Shutdowns the system
    /// </summary>
    /// <returns>Confirmation message</returns>
    public async Task<Response<Confirm>> Shutdown(bool? force = null)
    {
        var query = new List<string>();
        if (force != null) query.Add($"force={force}");

        string queryStr = query.Count > 0 ? "?" + string.Join('&', query) : "";

        var request = new RestRequest($"shutdown{queryStr}");
        var response = await _client.PostAsync(request);
        return response.As<Confirm>();
    }

    // Skipping:
    // GET /olfactomics/*

    // Internal

    readonly RestClient _client;
    readonly JsonSerializerOptions _serializationOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}

internal static class RestResponseExtension
{
    public static API.Response<T> As<T>(this RestResponse response, bool preserveCase = false)
    {
        T? value = default;
        string? error = null;

        int maxDataLengthToPrint = 80;
        string data = response.Content!;
        if (data.Length > maxDataLengthToPrint)
        {
            data = data[..maxDataLengthToPrint] + "...";
        }
        
        Console.WriteLine($"[API] {(int)response.StatusCode} ({response.StatusDescription}), {data} ({response.ContentLength} bytes)");

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
        return new API.Response<T>(value, error);
    }

    static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
