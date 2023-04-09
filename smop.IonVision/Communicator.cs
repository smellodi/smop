using System.Threading.Tasks;
using System.Text.Json;
using RestSharp;

namespace Smop.IonVision
{
    public class Communicator
    {
        public Communicator()
        {
            var host = $"http://{_settings.IP}/api";
            _client = new RestClient("https://jsonplaceholder.typicode.com/");
            _client.AddDefaultHeader("Content-Type", "application/json");
        }

        /// <summary>
        /// Retrieves system status
        /// </summary>
        /// <returns>System status</returns>
        public async Task<SystemStatus> GetSystemStatus()
        {
            var request = new RestRequest("/system/status");
            var response = await _client.ExecuteGetAsync(request);
            return response.To<SystemStatus>();
        }

        /// <summary>
        /// Retrieves user
        /// </summary>
        /// <returns>User name</returns>
        public async Task<User> GetUser()
        {
            var request = new RestRequest("currentUser");
            var response = await _client.ExecuteGetAsync(request);
            return response.To<User>();
        }

        /// <summary>
        /// Sets user
        /// </summary>
        /// <returns>Error message, if any</returns>
        public async Task<string?> SetUser()
        {
            var request = new RestRequest("currentUser");
            request.AddBody($"{{\"name\": \"${_settings.User}\"}}");
            var response = await _client.ExecutePutAsync(request);
            return response.IsSuccessStatusCode ? null : response.To<Error>().Message;
        }

        /// <summary>
        /// Retrieves a list of projects
        /// </summary>
        /// <returns>Project names</returns>
        public async Task<string[]?> ListProjects()
        {
            var request = new RestRequest("project");
            var response = await _client.ExecuteGetAsync(request);
            return !response.IsSuccessStatusCode ? null : response.To<string[]>();
        }

        /// <summary>
        /// Retrieves a list of parameters
        /// </summary>
        /// <returns>Parameters</returns>
        public async Task<Parameter[]?> ListParameters()
        {
            var request = new RestRequest("parameter");
            var response = await _client.ExecuteGetAsync(request);
            return !response.IsSuccessStatusCode ? null : response.To<Parameter[]>();
        }

        /// <summary>
        /// Sets the SMOP project as active
        /// </summary>
        /// <returns>Error message if the project was not set as active</returns>
        public async Task<string?> LoadProject()
        {
            var request = new RestRequest("currentProject");
            request.AddBody($"{{\"project\": \"${_settings.Project}\"}}");

            var response = await _client.ExecutePutAsync(request);
            bool isSuccess = response.IsSuccessStatusCode;

            if (isSuccess)
            {
                await Task.Delay(1500);
            }

            return isSuccess ? null : response.To<Error>().Message;
        }

        /// <summary>
        /// Sets the SMOP project parameter, and also preloads it immediately
        /// </summary>
        /// <returns>Error message if the project parameter was not set</returns>
        public async Task<string?> SetParameter()
        {
            var request = new RestRequest("currentParameter");
            request.AddBody($"{{\"parameter\": \"${_settings.Parameter}\"}}");

            var response = await _client.ExecutePutAsync(request);
            bool isSuccess = response.IsSuccessStatusCode;

            if (isSuccess)
            {
                request = new RestRequest("currentParameter/preload");
                await _client.ExecutePostAsync(request);
            }

            return isSuccess ? null : response.To<Error>().Message;
        }

        /// <summary>
        /// Starts a new scan
        /// </summary>
        /// <returns>Error message if the scan was not started</returns>
        public async Task<string?> StartScan()
        {
            var request = new RestRequest("currentScan");
            var response = await _client.ExecutePostAsync(request);
            return response.IsSuccessStatusCode ? null : response.To<Error>().Message;
        }

        /// <summary>
        /// Retrieves scan progress
        /// </summary>
        /// <returns>Scan progress</returns>
        public async Task<int?> GetScanProgress()
        {
            var request = new RestRequest("currentScan");
            var response = await _client.ExecuteGetAsync(request);
            return !response.IsSuccessStatusCode ? null : response.To<ScanProgress>().Progress;
        }

        /// <summary>
        /// Retrieves the latest scanning result
        /// </summary>
        /// <returns>Scanning result, if any</returns>
        internal async Task<ScanResult?> GetScanResult()
        {
            var request = new RestRequest("results/latest");
            var response = await _client.ExecuteGetAsync(request);
            return !response.IsSuccessStatusCode ? null : response.To<ScanResult>();
        }

        /// <summary>
        /// Retrieves all project scanning result
        /// </summary>
        /// <returns>Scanning results, if any</returns>
        internal async Task<string[]> GetProjectResults()
        {
            var request = new RestRequest($"project/{_settings.Project}/results");
            var response = await _client.ExecuteGetAsync(request);
            return !response.IsSuccessStatusCode ? new string[] { } : response.To<string[]>();
        }

        //  DEBUG

        public record class Post(int UserId, int Id, string Title, string Body);
        public async Task<Post[]> ListPosts()
        {
            var request = new RestRequest("posts");
            var response = await _client.ExecuteGetAsync(request);
            return response.To<Post[]>();
        }

        // Internal

        Settings _settings = Settings.Instance;
        RestClient _client;
    }

    internal static class RestResponseExt
    {
        public static T To<T>(this RestResponse response)
        {
            return JsonSerializer.Deserialize<T>(response.Content!, _serializerOptions)!;
        }

        static JsonSerializerOptions _serializerOptions = new() { PropertyNameCaseInsensitive = true };
    }
}
