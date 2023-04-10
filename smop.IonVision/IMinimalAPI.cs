using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;

namespace Smop.IonVision
{
    internal interface IMinimalAPI
    {
        public Task<API.Response<SystemStatus>> GetSystemStatus();
        public Task<API.Response<User>> GetUser();
        public Task<API.Response<Error>> SetUser(User user);
        public Task<API.Response<string[]>> GetProjects();
        public Task<API.Response<Parameter[]>> GetParameters();
        public Task<API.Response<Error>> SetProject(ProjectAsName project);
        public Task<API.Response<Error>> SetParameter(ParameterAsId parameter);
        public Task<API.Response<Error>> PreloadParameter();
        public Task<API.Response<Error>> StartScan();
        public Task<API.Response<ScanProgress>> GetScanProgress();
        public Task<API.Response<ScanResult>> GetLatestResult();
        public Task<API.Response<string[]>> GetProjectResults(ProjectAsName project);
    }
}
