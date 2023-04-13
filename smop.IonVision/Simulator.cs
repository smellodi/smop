using System.Diagnostics;
using System.Timers;
using System.Threading.Tasks;
using static Smop.IonVision.API;

namespace Smop.IonVision;

internal class Simulator : IMinimalAPI
{
    public Simulator()
    {
        _timer.AutoReset = false;
        _timer.Interval = SCAN_DURATION;
        _timer.Elapsed += (s, e) =>
        {
            _stopwatch.Stop();
            _latestResult = SimulatedData.ScanResult;
        };
    }

    public async Task<Response<SystemStatus>> GetSystemStatus()
    {
        return await Task.FromResult(new Response<SystemStatus>(new SystemStatus(
            new GasFilter("never", 1, 1000),
            "simulator", 1, 1, true,
            new SystemStorage(1_000_000_000, 2_000_000_000)
        ), null));
    }
    public async Task<Response<User>> GetUser() => 
        await Task.FromResult(new Response<User>(SimulatedData.User, null));

    public async Task<Response<Confirm>> SetUser(User user)
    {
        SimulatedData.User = user;
        return await Task.FromResult(new Response<Confirm>(new Confirm(), null));
    }

    public async Task<Response<string[]>> GetProjects() => 
        await Task.FromResult(new Response<string[]>(new string[] { SimulatedData.Project.Name }, null));

    public async Task<Response<Parameter[]>> GetParameters() =>
        await Task.FromResult(new Response<Parameter[]>(new Parameter[] { SimulatedData.Parameter }, null));

    public async Task<Response<ProjectAsName>> GetProject() =>
        await Task.FromResult(new Response<ProjectAsName>(
            _currentProject == null ? null : new ProjectAsName(_currentProject.Name),
            _currentProject != null ? null : "No project is set as current"
        ));

    public async Task<Response<Confirm>> SetProject(ProjectAsName project)
    {
        if (project.Project != SimulatedData.Project.Name)
        {
            return await Task.FromResult(new Response<Confirm>(null, $"Project '{project.Project}' doesn't exist"));
        }

        _currentProject = SimulatedData.Project;
        return await Task.FromResult(new Response<Confirm>(new Confirm(), null));
    }

    public async Task<Response<ParameterDefinition>> GetParameterDefinition(Parameter parameter) =>
        await Task.FromResult(new Response<ParameterDefinition>(
            _currentParameter == null ? null : SimulatedData.ParameterDefinition,
            _currentParameter != null ? null : "No parameter is set as current"));

    public async Task<Response<ParameterAsNameAndId>> GetParameter() =>
        await Task.FromResult(new Response<ParameterAsNameAndId>(
            _currentParameter == null ? null : new ParameterAsNameAndId(_currentParameter),
            _currentParameter != null ? null : "No parameter is set as current"
        ));

    public async Task<Response<Confirm>> SetParameter(ParameterAsId parameter)
    {
        if (parameter.Parameter != SimulatedData.Parameter.Id)
        {
            return await Task.FromResult(new Response<Confirm>(null, $"Parameter '{parameter.Parameter}' doesn't exist"));
        }

        _currentParameter = SimulatedData.Parameter;
        return await Task.FromResult(new Response<Confirm>(new Confirm(), null));
    }

    public async Task<Response<Confirm>> PreloadParameter()
    {
        if (_currentParameter == null)
        {
            return await Task.FromResult(new Response<Confirm>(null, "No parameter is set as current"));
        }
        return await Task.FromResult(new Response<Confirm>(new Confirm(), null));
    }

    public async Task<Response<Confirm>> StartScan()
    {
        if (_currentProject == null)
        {
            return await Task.FromResult(new Response<Confirm>(null, "No project is set as current"));
        }
        if (_currentParameter == null)
        {
            return await Task.FromResult(new Response<Confirm>(null, "No parameter set as current"));
        }
        if (_stopwatch.IsRunning)
        {
            return await Task.FromResult(new Response<Confirm>(null, "Scan is in progress"));
        }

        _stopwatch.Reset();
        _stopwatch.Start();
        _timer.Start();

        return await Task.FromResult(new Response<Confirm>(new Confirm(), null));
    }

    public async Task<Response<ScanProgress>> GetScanProgress()
    {
        if (!_stopwatch.IsRunning)
        {
            return await Task.FromResult(new Response<ScanProgress>(null, "Scan is not running now"));
        }

        int progress = (int)(_stopwatch.Elapsed.TotalMilliseconds / SCAN_DURATION * 100);
        return await Task.FromResult(new Response<ScanProgress>(new ScanProgress(progress, new()), null));
    }

    public async Task<Response<ScanResult>> GetLatestResult()
    {
        if (_latestResult == null)
        {
            return await Task.FromResult(new Response<ScanResult>(null, "Scan was not yet performed"));
        }

        return await Task.FromResult(new Response<ScanResult>(_latestResult, null));
    }

    public async Task<Response<string[]>> GetProjectResults(ProjectAsName project)
    {
        if (project.Project != SimulatedData.Project.Name)
        {
            return await Task.FromResult(new Response<string[]>(null, $"Project '{project.Project}' doesn;t exist"));
        }
        if (_latestResult == null)
        {
            return await Task.FromResult(new Response<string[]>(null, "The project has no scan yet"));
        }

        return await Task.FromResult(new Response<string[]>(new string[] { _latestResult.Id }, null));
    }

    // Internal

    const double SCAN_DURATION = 10000;

    readonly Stopwatch _stopwatch = new();
    readonly Timer _timer = new();

    Project? _currentProject = null;
    Parameter? _currentParameter = null;
    ScanResult? _latestResult = null;
}
