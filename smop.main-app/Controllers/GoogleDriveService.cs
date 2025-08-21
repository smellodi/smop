using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Smop.MainApp.Controllers;

internal class GoogleDriveService
{
    public static GoogleDriveService Instance { get; } = _instance ??= new GoogleDriveService();

    public Google.Apis.Drive.v3.Data.File[] Jsons => _files.FindAll(f => f.MimeType == "application/json").ToArray();
    public Google.Apis.Drive.v3.Data.File[] Texts => _files.FindAll(f => f.MimeType == "text/plain").ToArray();

    public bool IsReady => _service != null;

    public void Initialize()
    {
        UserCredential credential;

        if (!File.Exists(_appCredentialsFilename))
        {
            throw new ApplicationException($"Credentials file '{_appCredentialsFilename}' not found. Please create it with your Google Drive API credentials.");
        }

        using (var stream = new FileStream(_appCredentialsFilename, FileMode.Open, FileAccess.Read))
        {
            credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.FromStream(stream).Secrets,
                _scopes,
                "user",
                CancellationToken.None,
                new FileDataStore(_userTokenPath, true)).Result;
        }

        /* Alternative way but does not work
        GoogleCredential credential;
        using (var stream = new FileStream(_appKeyFilename, FileMode.Open, FileAccess.Read))
        {
            credential = GoogleCredential.FromStream(stream)
                .CreateScoped(_scopes)
                .CreateWithUser("app-659@smop-468812.iam.gserviceaccount.com");
        }*/

        // Create Drive API service
        _service = new DriveService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = _applicationName,
        });

        // Get or create the root folder if it doesn't exist
        _rootFolderId = GetRootFolder();
        if (string.IsNullOrEmpty(_rootFolderId))
            _rootFolderId = CreateRootFolder();
    }

    public async Task RetrieveFiles()
    {
        var files = await GetFiles();
        _files.AddRange(files);
    }

    public async Task<Google.Apis.Drive.v3.Data.File> Create(string remoteFilename, string content)
    {
        if (_service == null)
            return new();

        var fileMetadata = new Google.Apis.Drive.v3.Data.File()
        {
            Name = remoteFilename,
            Parents = new List<string> { _rootFolderId } // Upload to the root folder
        };

        var mimeType = "application/octet-stream";
        if (remoteFilename.EndsWith(".json"))
            mimeType = "application/json";
        else if (remoteFilename.EndsWith(".txt"))
            mimeType = "text/plain";

        FilesResource.CreateMediaUpload request;
        byte[] byteArray = Encoding.UTF8.GetBytes(content);
        using (var stream = new MemoryStream(byteArray))
        {
            request = _service.Files.Create(fileMetadata, stream, mimeType);
            request.Fields = "id";
            await request.UploadAsync();
        }

        _files.Clear();
        var files = await GetFiles();
        if (files != null)
            _files.AddRange(files);

        return request.ResponseBody;
    }

    public Google.Apis.Drive.v3.Data.File CreateSync(string remoteFilename, string content)
    {
        if (_service == null)
            return new();

        var fileMetadata = new Google.Apis.Drive.v3.Data.File()
        {
            Name = remoteFilename,
            Parents = new List<string> { _rootFolderId } // Upload to the root folder
        };

        var mimeType = "application/octet-stream";
        if (remoteFilename.EndsWith(".json"))
            mimeType = "application/json";
        else if (remoteFilename.EndsWith(".txt"))
            mimeType = "text/plain";

        FilesResource.CreateMediaUpload request;
        byte[] byteArray = Encoding.UTF8.GetBytes(content);
        using (var stream = new MemoryStream(byteArray))
        {
            request = _service.Files.Create(fileMetadata, stream, mimeType);
            request.Fields = "id";
            try
            {
                request.Upload();
            }
            catch { }
        }

        _files.Clear();
        var files = GetFilesSync();
        if (files != null)
            _files.AddRange(files);

        return request.ResponseBody;
    }

    /*public async Task<Google.Apis.Drive.v3.Data.File> Upload(string localFilename, string? remoteFilename = null)
    {
        if (string.IsNullOrEmpty(remoteFilename))
            remoteFilename = Path.GetFileName(localFilename);

        // Upload a file
        var fileMetadata = new Google.Apis.Drive.v3.Data.File()
        {
            Name = remoteFilename,
            Parents = new List<string> { _rootFolderId } // Upload to the root folder
        };

        var mimeType = "application/octet-stream";
        if (localFilename.EndsWith(".json"))
            mimeType = "application/json";
        else if (localFilename.EndsWith(".txt"))
            mimeType = "text/plain";

        FilesResource.CreateMediaUpload request;
        using (var stream = new FileStream(localFilename, FileMode.Open))
        {
            request = _service.Files.Create(fileMetadata, stream, mimeType);
            request.Fields = "id";
            await request.UploadAsync();
        }

        _files.Clear();
        var files = await GetFiles();
        if (files != null)
            _files.AddRange(files);

        return request.ResponseBody;
    }*/

    public async Task<IList<Google.Apis.Drive.v3.Data.File>> GetFiles(string? mimeType = null)
    {
        if (_service == null)
            return [];

        List<Google.Apis.Drive.v3.Data.File> result = [];
        var nextPageToken = string.Empty;
        var filter = $"'{_rootFolderId}' in parents"; // Filter files by the root folder
        if (!string.IsNullOrEmpty(mimeType))
        {
            filter += $" and mimeType = '{mimeType}'"; // Add mime type filter if specified
        }

        do
        {
            var request = _service.Files.List();
            request.Fields = "nextPageToken, files(id, name, mimeType, trashed)";
            request.Q = filter;
            var response = await request.ExecuteAsync();
            result.AddRange(response.Files);

            nextPageToken = response.NextPageToken;

        } while (!string.IsNullOrEmpty(nextPageToken));

        return result;
    }

    public IList<Google.Apis.Drive.v3.Data.File> GetFilesSync(string? mimeType = null)
    {
        if (_service == null)
            return [];

        List<Google.Apis.Drive.v3.Data.File> result = [];
        var nextPageToken = string.Empty;
        var filter = $"'{_rootFolderId}' in parents"; // Filter files by the root folder
        if (!string.IsNullOrEmpty(mimeType))
        {
            filter += $" and mimeType = '{mimeType}'"; // Add mime type filter if specified
        }

        do
        {
            var request = _service.Files.List();
            request.Fields = "nextPageToken, files(id, name, mimeType, trashed)";
            request.Q = filter;
            try
            {
                var response = request.Execute();
                result.AddRange(response.Files);
                nextPageToken = response.NextPageToken;
            }
            catch { break; }

        } while (!string.IsNullOrEmpty(nextPageToken));

        return result;
    }

    public async Task<string> ReadFile(string id)
    {
        if (_service == null)
            return string.Empty;

        var request = _service.Files.Get(id);
        request.Alt = DriveBaseServiceRequest<Google.Apis.Drive.v3.Data.File>.AltEnum.Media; // Get the file content
        using var stream = new MemoryStream();
        await request.DownloadAsync(stream);
        var bytes = stream.ToArray();
        return Encoding.UTF8.GetString(bytes);
    }

    public string ReadFileSync(string id)
    {
        if (_service == null)
            return string.Empty;

        var request = _service.Files.Get(id);
        request.Alt = DriveBaseServiceRequest<Google.Apis.Drive.v3.Data.File>.AltEnum.Media; // Get the file content
        using var stream = new MemoryStream();
        request.Download(stream);
        var bytes = stream.ToArray();
        return Encoding.UTF8.GetString(bytes);
    }

    public async Task DeleteFile(string id)
    {
        if (_service == null)
            return;

        var request = _service.Files.Delete(id);
        var response = await request.ExecuteAsync();
        Console.WriteLine($"File with ID '{id}' deleted successfully: {response}");
    }

    public void DeleteFileSync(string id)
    {
        if (_service == null)
            return;

        var request = _service.Files.Delete(id);
        var response = request.Execute();
        Console.WriteLine($"File with ID '{id}' deleted successfully: {response}");
    }

    // Internal

    static GoogleDriveService? _instance;

    readonly string[] _scopes = [DriveService.Scope.Drive]; // Allows uploading files
    readonly string _applicationName = "SMOP";
    readonly string _userTokenPath = "tokens";
    readonly string _appCredentialsFilename = "credentials.json";
    //readonly string _appKeyFilename = "key.json";

    readonly List<Google.Apis.Drive.v3.Data.File> _files = [];

    DriveService? _service;
    string _rootFolderId = string.Empty;

    private string GetRootFolder()
    {
        if (_service == null)
            return string.Empty;

        // Ensure the root folder exists
        var request = _service.Files.List();
        request.Q = "name = 'SMOP' and mimeType = 'application/vnd.google-apps.folder'";
        request.Fields = "files(id, name)";
        var response = request.Execute();
        return response.Files.Count == 0 ? "" : response.Files[0].Id;
    }

    private string CreateRootFolder()
    {
        if (_service == null)
            return string.Empty;

        var fileMetadata = new Google.Apis.Drive.v3.Data.File()
        {
            Name = "SMOP",
            MimeType = "application/vnd.google-apps.folder"
        };
        var request = _service.Files.Create(fileMetadata);
        request.Fields = "id";
        var file = request.Execute();
        return file.Id;
    }
}