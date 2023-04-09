using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using RestSharp;

namespace Smop.IonVision
{
    internal class API
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
        public async Task<Response<Error>> StartScan()
        {
            var request = new RestRequest("currentScan");
            var response = await _client.PostAsync(request);
            return response.As<Error>();
        }

        /// <summary>
        /// Stops the ongoing scan
        /// </summary>
        /// <returns>Confirmations message</returns>
        public async Task<Response<Error>> StopScan()
        {
            var request = new RestRequest("currentScan");
            var response = await _client.DeleteAsync(request);
            return response.As<Error>();
        }

        // Skipped:
        // GET /currentScan/comments
        // PUT /currentScan/comments
        // GET /scope
        // POST /scope
        // DELETE /scope
        // GET /scope/latestResult
        // GET /scope/parameters
        // PUT /scope/parameters

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
        public async Task<Response<Error>> SetUser(User user)
        {
            var request = new RestRequest("currentUser");
            request.AddBody(JsonSerializer.Serialize(user, _serializationOptions));
            var response = await _client.PutAsync(request);
            return response.As<Error>();
        }

        /// <summary>
        /// Retrieves current parameter
        /// </summary>
        /// <returns>Parameter</returns>
        public async Task<Response<Parameter>> GetParameter()
        {
            var request = new RestRequest("currentParameter");
            var response = await _client.GetAsync(request);
            return response.As<Parameter>();
        }

        /// <summary>
        /// Sets current parameter
        /// </summary>
        /// <param name="parameter">Parameter</param>
        /// <returns>Confirmation message</returns>
        public async Task<Response<Error>> SetParameter(ParameterAsName parameter)
        {
            var request = new RestRequest("currentParameter");
            request.AddBody(JsonSerializer.Serialize(parameter, _serializationOptions));
            var response = await _client.PutAsync(request);
            return response.As<Error>();
        }

        /// <summary>
        /// Proloads current parameter
        /// </summary>
        /// <returns>Confirmation message</returns>
        public async Task<Response<Error>> PreloadParameter()
        {
            var request = new RestRequest("currentParameter/preload");
            var response = await _client.PostAsync(request);
            return response.As<Error>();
        }

        /// <summary>
        /// Retrieves a list of parameters
        /// </summary>
        /// <returns>Parameters</returns>
        public async Task<Response<Parameter[]>> Parameters()
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
        public async Task<Response<ParameterAsName>> CreateParameter(ParameterDefinition parameter)
        {
            var request = new RestRequest("parameter");
            request.AddBody(JsonSerializer.Serialize(parameter));
            var response = await _client.PostAsync(request);
            return response.As<ParameterAsName>();
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
        public async Task<Response<Error>> UpdateParameterDefinition(ParameterDefinition parameter)
        {
            var request = new RestRequest($"parameter/{parameter.Id}");
            request.AddBody(JsonSerializer.Serialize(parameter));
            var response = await _client.PutAsync(request);
            return response.As<Error>();
        }

        /// <summary>
        /// Deletes the parameter
        /// </summary>
        /// <param name="parameter">Parameter</param>
        /// <returns>Confirmation message</returns>
        public async Task<Response<Error>> DeleteParameter(Parameter parameter)
        {
            var request = new RestRequest($"parameter/{parameter.Id}");
            var response = await _client.DeleteAsync(request);
            return response.As<Error>();
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

        // Skipped:
        // GET /parameterTemplate
        // GET /parameterTemplate/{unique_name}
        // GET /parameterTemplate/{unique_name}/metadata

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
        public async Task<Response<Error>> LoadProject(ProjectAsName project)
        {
            var request = new RestRequest("currentProject");
            request.AddBody(JsonSerializer.Serialize(project, _serializationOptions));
            var response = await _client.PutAsync(request);
            return response.As<Error>();
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
        public async Task<Response<Error>> CreateProject(Project project)
        {
            var request = new RestRequest("project");
            var response = await _client.PostAsync(request);
            return response.As<Error>();
        }

        /// <summary>
        /// Gets the project definition
        /// </summary>
        /// <param name="project">Project</param>
        /// <returns>Project definition</returns>
        public async Task<Response<Project>> GetProjectDefinition(ProjectAsName project)
        {
            var request = new RestRequest($"project/{project.Project}");
            var response = await _client.GetAsync(request);
            return response.As<Project>();
        }

        /// <summary>
        /// Updates the list of project's parameters
        /// </summary>
        /// <param name="project">Project</param>
        /// <returns>Confirmation message</returns>
        public async Task<Response<Error>> UpdateProjecParameters(ProjectAsName project)
        {
            var request = new RestRequest($"project/{project.Project}");
            request.AddBody(JsonSerializer.Serialize(project, _serializationOptions));
            var response = await _client.PutAsync(request);
            return response.As<Error>();
        }

        /// <summary>
        /// Deleted the project
        /// </summary>
        /// <param name="project">Project</param>
        /// <returns>Confirmation message</returns>
        public async Task<Response<Error>> DeleteProject(ProjectAsName project)
        {
            var request = new RestRequest($"project/{project.Project}");
            var response = await _client.DeleteAsync(request);
            return response.As<Error>();
        }

        /// <summary>
        /// Gets the list of project scans
        /// </summary>
        /// <param name="project">Project</param>
        /// <returns>List of scan IDs</returns>
        public async Task<Response<string[]>> GetProjectResults(ProjectAsName project)
        {
            var request = new RestRequest($"project/{project.Project}/results");
            var response = await _client.GetAsync(request);
            return response.As<string[]>();
        }

        // Skipped:
        // GET /project/{name}/sequence
        // PUT /project/{name}/sequence

        // Skipped:
        // * /controller/*


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
            var query = new List<string>();
            if (maxResults != null) query.Add($"max_results={maxResults}");
            if (page != null) query.Add($"page={page}");
            if (search != null) query.Add($"search={search}");
            if (startDate != null) query.Add($"start_date={startDate}");
            if (date != null) query.Add($"date={date}");
            if (sortBy != null) query.Add($"sort_by={sortBy}");
            if (onlyMetadata != null) query.Add($"only_metadata={onlyMetadata}");
            if (ids != null) query.Add($"ids={string.Join(',', ids)}");

            string queryStr = query.Count > 0 ? "?" + string.Join('&', query) : "";


            var request = new RestRequest($"results{queryStr}");
            var response = await _client.GetAsync(request);
            return response.As<SearchResult>();
        }

        /// <summary>
        /// Deletes scans
        /// </summary>
        /// <param name="list">List of scan IDs</param>
        /// <returns>Confirmation message</returns>
        public async Task<Response<Error>> DeleteResults(ListOfIDs list)
        {
            var request = new RestRequest($"results");
            request.AddBody(JsonSerializer.Serialize(list, _serializationOptions));
            var response = await _client.DeleteAsync(request);
            return response.As<Error>();
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
        public async Task<Response<ScanResult>> GetLatestResult(string id)
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
        public async Task<Response<Error>> DeleteResult(string id)
        {
            var request = new RestRequest($"results/id/{id}");
            var response = await _client.DeleteAsync(request);
            return response.As<Error>();
        }

        // Skipped:
        // GET /results/id/{id}/copy
        // GET /results/id/{id}/comments
        // PUT /results/id/{id}/comments

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
        public async Task<Response<Error>> ResetGasFilter()
        {
            var request = new RestRequest("system/status/resetGasFilter");
            var response = await _client.PostAsync(request);
            return response.As<Error>();
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
        public async Task<Response<Error>> StartCalibration()
        {
            var request = new RestRequest("system/calibration");
            var response = await _client.PostAsync(request);
            return response.As<Error>();
        }

        // Skipped:
        // GET /system/devices
        // GET /system/update
        // POST /system/update
        // GET /system/debug
        // GET /system/debug/logs
        // GET /system/errors
        // GET /system/errors/valueLimits
        // GET /system/reset
        // POST /system/reset
        // GET /system/licenses

        // Skipping:
        // GET /settings/*

        // Skipping:
        // GET /graphColour/*

        // Skipping:
        // GET /backups/*

        /// <summary>
        /// Reboots the system
        /// </summary>
        /// <returns>Confirmation message</returns>
        public async Task<Response<Error>> Reboot()
        {
            var request = new RestRequest("reboot");
            var response = await _client.PostAsync(request);
            return response.As<Error>();
        }

        /// <summary>
        /// Shutdowns the system
        /// </summary>
        /// <returns>Confirmation message</returns>
        public async Task<Response<Error>> Shutdown(bool? force = null)
        {
            var query = new List<string>();
            if (force != null) query.Add($"force={force}");

            string queryStr = query.Count > 0 ? "?" + string.Join('&', query) : "";

            var request = new RestRequest($"shutdown{queryStr}");
            var response = await _client.PostAsync(request);
            return response.As<Error>();
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

            if (response.IsSuccessful)
            {
                var serializerOptions = preserveCase ? new JsonSerializerOptions() : _serializerOptions;
                value = JsonSerializer.Deserialize<T>(response.Content!, serializerOptions)!;
            }
            else
            {
                error = JsonSerializer.Deserialize<Error>(response.Content!, _serializerOptions)!.Message;
            }
            return new API.Response<T>(value, error);
        }

        static readonly JsonSerializerOptions _serializerOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };
    }
}
