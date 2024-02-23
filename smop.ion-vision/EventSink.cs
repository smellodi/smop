using Smop.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Websocket.Client;

namespace Smop.IonVision;

/// <summary>
/// Implements WebSocket API
/// </summary>
internal class EventSink : IDisposable
{
    #region Event args

    public record class Message(string Type, long Time, object Body)
    {
        static readonly JsonSerializerOptions _jso = new() { PropertyNameCaseInsensitive = true };
        public T As<T>() => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(Body), _jso)!;
    }

    /// <summary>
    /// Arguments of an event without a payload (message "body" field is empty)
    /// </summary>
    public class TimedEventArgs : EventArgs
    {
        public string Type { get; init; }
        public long Timestamp { get; init; }
        public TimedEventArgs(Message msg)
        {
            Type = msg.Type;
            Timestamp = msg.Time;
        }
    }

    /// <summary>
    /// Arguments of an event with a payload in the message "body" field,
    /// exposed in this class as "Data"
    /// </summary>
    /// <typeparam name="T">Payload (body) type</typeparam>
    public class TimedEventArgs<T> : TimedEventArgs
    {
        /// <summary>
        /// Body / payload
        /// </summary>
        public T Data { get; init; }
        public TimedEventArgs(Message msg) : base(msg)
        {
            Data = msg.As<T>();
        }
    }

    /// <summary>
    /// Arguments of an event with a payload in the message "body" field
    /// as an array of key-value pairs,
    /// exposed in this class as "Data"
    /// </summary>
    /// <typeparam name="T">Type of values</typeparam>
    public class TimedArrayEventArgs<T> : TimedEventArgs
    {
        /// <summary>
        /// Body / payload
        /// </summary>
        public KeyValuePair<string, T>[] Data { get; init; }
        public TimedArrayEventArgs(Message msg) : base(msg)
        {
            Data = msg.Body.GetType().GetProperties()
                .Select(prop => new KeyValuePair<string, T>(prop.Name, (T)prop.GetValue(msg.Body, null)!))
                .ToArray();
        }
    }

    #endregion

    #region Message body payloads

    public record class ProcessProgress(int Progress);
    public record class ScanUserName(string? UserName);
    //public record class ScopeData(float Usv, float[] Ucv, float[] IntensityTop, float[] IntensityBottom);
    public record class ScopeParameters(
        float UcvStart, float UcvStop, float Usv, float Vb,
        float PP, float PW,
        float SampleAverages,
        float SampleFlowControl,
        float SensorFlowControl,
        float SampleHeaterTemperatureControl,
        float SensorHeaterTemperatureControl);
    public record class CurrentParameter(Defs.Parameter NewParameter);
    public record class CurrentProject(string NewProject);
    public record class ProjectEdit(string NewProject);

    public record class Status(
            bool RtmReady,
            bool MeasurementRunning,
            bool MeasurementReady,
            bool PowerOn,
            bool HvwmPowerOn,
            bool DemisecOn,
            bool XappiPowerOn,
            bool XappiOn,
            bool SampleHeaterOn,
            bool SamplePumpOn,
            bool SensorHeaterOn,
            bool SensorPumpOn
    );
    public record class SensorSample(float Temperature, float HeaterTemperature, float Pressure, float Flow, float Humidity);
    public record class Ambient(float Temperature, float Pressure, float Humidity);
    public record class ControllersStatus(
        Status Status,
        SensorSample Sample,
        SensorSample Sensor,
        Ambient Ambient);
    public record class Error(string Code);
    public record class ErrorLimits(
        bool AmbientPressureUnderMin,
        bool AmbientPressureOverMax,
        bool AmbientHumidityUnderMin,
        bool AmbientHumidityOverMax,
        bool AmbientTemperatureUnderMin,
        bool AmbientTemperatureOverMax,
        bool FetTemperatureUnderMin,
        bool FetTemperatureOverMax,
        bool SampleFlowUnderMin,
        bool SampleFlowOverMax,
        bool SampleTemperatureUnderMin,
        bool SampleTemperatureOverMax,
        bool SamplePressureUnderMin,
        bool SamplePressureOverMax,
        bool SampleHumidityUnderMin,
        bool SampleHumidityOverMax,
        bool CirculatingFlowUnderMin,
        bool CirculatingFlowOverMax,
        bool CirculatingTemperatureUnderMin,
        bool CirculatingTemperatureOverMax,
        bool CirculatingPressureUnderMin,
        bool CirculatingPressureOverMax,
        bool CirculatingHumidityUnderMin,
        bool CirculatingHumidityOverMax,
        bool SampleHeaterTemperatureUnderMin,
        bool SampleHeaterTemperatureOverMax,
        bool CirculatingHeaterTemperatureUnderMin,
        bool CirculatingHeaterTemperatureOverMax,
        bool AmbientTemperatureOverSafety,
        bool AmbientTemperatureOverDanger,
        bool FetTemperatureOverSafety,
        bool FetTemperatureOverDanger,
        bool CirculatingHeaterTemperatureOverSafety,
        bool CirculatingHeaterTemperatureOverDanger,
        bool SampleHeaterTemperatureOverSafety,
        bool SampleHeaterTemperatureOverDanger,
        bool SampleTemperatureOverSafety,
        bool SampleTemperatureOverDanger,
        bool CirculatingTemperatureOverSafety,
        bool CirculatingTemperatureOverDanger);
    public record class Timestamp(long Time);

    #endregion

    #region Events

    /// <summary>
    /// A new scan has started.
    /// </summary>
    public event EventHandler<TimedEventArgs>? ScanStarted;
    /// <summary>
    /// A scan has been stopped without finishing. No result data will be saved.
    /// </summary>
    public event EventHandler<TimedEventArgs>? ScanStopped;
    /// <summary>
    /// A scan has been finished successfully. The results of the scan are still being processed and are not yet available.
    /// </summary>
    public event EventHandler<TimedEventArgs>? ScanFinished;
    /// <summary>
    /// The results of the previously finished scan have been processed to the device storage.
    /// </summary>
    public event EventHandler<TimedEventArgs>? ScanResultsProcessed;
    /// <summary>
    /// The progress of an ongoing scan.
    /// </summary>
    public event EventHandler<TimedEventArgs<ProcessProgress>>? ScanProgressChanged;
    /// <summary>
    /// The local device username has changed.
    /// </summary>
    public event EventHandler<TimedEventArgs<ScanUserName>>? ScanUserNameChanged;
    /// <summary>
    /// The comments object of an ongoing or next scan has changed. The new comments object is passed as the body of this message.
    /// </summary>
    public event EventHandler<TimedArrayEventArgs<string>>? ScanCommentsChanged;
    /// <summary>
    /// The device has moved to scope mode. Scope mode will now continue until stopped from the HTTP API.
    /// </summary>
    public event EventHandler<TimedEventArgs>? ScopeStarted;
    /// <summary>
    /// The scope mode has been stopped and device is idle again.
    /// </summary>
    public event EventHandler<TimedEventArgs>? ScopeStopped;
    /// <summary>
    /// A scope scan has finished. This message includes the scan results. A new scope scan will be started automatically and scope mode will continue until manually stopped.
    /// </summary>
    public event EventHandler<TimedEventArgs<Defs.ScopeResult>>? ScopeDataReceived;
    /// <summary>
    /// The scope parameters have changed. The new parameters are included in the message.
    /// </summary>
    public event EventHandler<TimedEventArgs<ScopeParameters>>? ScopeParametersChanged;
    /// <summary>
    /// The current scan parameters preset has been changed.
    /// </summary>
    public event EventHandler<TimedEventArgs<CurrentParameter>>? CurrentParameterChanged;
    /// <summary>
    /// The current parameter preset is being set up on the RTM. A new current parameter preset or project can't be set during this. The operation can take a few seconds.
    /// </summary>
    public event EventHandler<TimedEventArgs>? ParameterSetupStarted;
    /// <summary>
    /// The current parameter preset has been set up to the RTM. The current parameter preset or project can be changed again.
    /// </summary>
    public event EventHandler<TimedEventArgs>? ParameterSetupFinished;
    /// <summary>
    /// The current parameter preset is being preloaded. A new current parameter preset or project can't be set during this. The operation takes a second.
    /// </summary>
    public event EventHandler<TimedEventArgs>? ParameterPreloadStarted;
    /// <summary>
    /// The current parameter preset has been preloaded. The current parameter preset or project can be changed again.
    /// </summary>
    public event EventHandler<TimedEventArgs>? ParameterPreloadFinished;
    /// <summary>
    /// A parameter preset on the device has been edited. The edited version can be fetched through the HTTP API.
    /// </summary>
    public event EventHandler<TimedEventArgs<Defs.Parameter>>? ParameterEdited;
    /// <summary>
    /// The list of parameters available on the device has changed. The new list must be fetched through the HTTP API as it can be long.
    /// </summary>
    public event EventHandler<TimedEventArgs>? ParameterListChanged;
    /// <summary>
    /// The current project has been changed.
    /// </summary>
    public event EventHandler<TimedEventArgs<CurrentProject>>? CurrentProjectChanged;
    /// <summary>
    /// The current project is being set up on the RTM. A new current project or parameter preset can't be set during this. The operation can take up to few minutes depending on the size of the project.
    /// </summary>
    public event EventHandler<TimedEventArgs>? ProjectSetupStarted;
    /// <summary>
    /// The current project has been set up to the RTM. The current project or parameter preset can be changed again.
    /// </summary>
    public event EventHandler<TimedEventArgs>? ProjectSetupFinished;
    /// <summary>
    /// A project on the device has been edited. The edited, new version can be fetched through the HTTP API.
    /// </summary>
    public event EventHandler<TimedEventArgs<ProjectEdit>>? ProjectEdited;
    /// <summary>
    /// The list of projects available on the device has changed. The new list must be fetched through the HTTP API as it can be long.
    /// </summary>
    public event EventHandler<TimedEventArgs>? ProjectListChanged;
    /// <summary>
    /// A message giving the current status of some hardware controllers in the system. This message is sent automatically periodically.
    /// </summary>
    public event EventHandler<TimedEventArgs<ControllersStatus>>? ControllersStatusTimer;
    /// <summary>
    /// The standby button at the front panel of the device has been pressed shortly. This is mainly used to show a "Do you want to power off the device?" dialog in the user interface.
    /// </summary>
    public event EventHandler<TimedEventArgs>? StandbyButtonPressed;
    /// <summary>
    /// The device is powering off once this message is received. The device APIs will no be usable shortly after this message.
    /// </summary>
    public event EventHandler<TimedEventArgs>? Shutdown;
    /// <summary>
    /// A error or warning message from the back-end.
    /// </summary>
    public event EventHandler<TimedEventArgs<Error>>? ErrorReceived;
    /// <summary>
    /// An user set or safety limit has been crossed. The message always contains every possible limit error and whether they are off (false) or on (true).
    /// </summary>
    public event EventHandler<TimedEventArgs<ErrorLimits>>? ErrorLimitsViolated;
    /// <summary>
    /// The system time of the device has changed. This also affects the WebSocket message timestamps. The message timestamp and the time listed in the body are the same.
    /// </summary>
    public event EventHandler<TimedEventArgs<Timestamp>>? TimeChanged;
    /// <summary>
    /// A process to back up the device has started. Some device features like scanning are not available during this.
    /// </summary>
    public event EventHandler<TimedEventArgs>? BackupStarted;
    /// <summary>
    /// A process to back up the device has finished successfully.
    /// </summary>
    public event EventHandler<TimedEventArgs>? BackupFinished;
    /// <summary>
    /// A progress update for an ongoing backup process.
    /// </summary>
    public event EventHandler<TimedEventArgs<ProcessProgress>>? BackupProgress;
    /// <summary>
    /// A process to restore the settings and storage of the device from a backup has started. Some device features like scanning are not available during this.
    /// </summary>
    public event EventHandler<TimedEventArgs>? RestoreStarted;
    /// <summary>
    /// A process to restore the device has finished successfully.
    /// </summary>
    public event EventHandler<TimedEventArgs>? RestoreFinished;
    /// <summary>
    /// A progress update for an ongoing system restore process.
    /// </summary>
    public event EventHandler<TimedEventArgs<ProcessProgress>>? RestoreProgress;
    /// <summary>
    /// A process to reset the storage of the device has started. Some device features like scanning are not available during this.
    /// </summary>
    public event EventHandler<TimedEventArgs>? ResetStarted;
    /// <summary>
    /// A process to reset the storage of the device has finished successfully.
    /// </summary>
    public event EventHandler<TimedEventArgs>? ResetFinished;
    /// <summary>
    /// A process to update the system software of the device has started. Some device features like scanning are not available during this.
    /// </summary>
    public event EventHandler<TimedEventArgs>? UpdateStarted;
    /// <summary>
    /// A process to update the system software of the device has finished successfully.
    /// </summary>
    public event EventHandler<TimedEventArgs>? UpdateFinished;
    /// <summary>
    /// A progress update for an ongoing system software update process.
    /// </summary>
    public event EventHandler<TimedEventArgs<ProcessProgress>>? UpdateProgress;
    /// <summary>
    /// A new Olfactomics cloud session has been established. All cloud functionality should be enabled after this message.
    /// </summary>
    public event EventHandler<TimedEventArgs>? CloudSessionStarted;
    /// <summary>
    /// The active Olfactomics cloud session has ended. The cloud functionality of the device will be disabled after this until a new session is started.
    /// </summary>
    public event EventHandler<TimedEventArgs>? CloudSessionFinished;
    /// <summary>
    /// There has been an error in the Olfactomics cloud connection. Cloud functionality might not work until a cloud.connectionRestored is sent.
    /// </summary>
    public event EventHandler<TimedEventArgs>? CloudConnectionError;
    /// <summary>
    /// Connection to Olfactomics cloud has been restored after an error.
    /// </summary>
    public event EventHandler<TimedEventArgs>? CloudConnectionRestored;

    #endregion

    /// <summary>
    /// Constructor. Immediately connects to the IonVision WebSocket server
    /// </summary>
    /// <param name="ip">IonVision IP address</param>
    public EventSink(string ip)
    {
        _ip = ip;
        CreateWebSocketClient();
    }

    public void Dispose()
    {
        _client?.Dispose();
        GC.SuppressFinalize(this);
    }


    // Internal

    const int MAX_CHARS_TO_PRINT = 700;

    private readonly JsonSerializerOptions _jsonSerializerOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly string _ip;

    private WebsocketClient? _client;

    private void CreateWebSocketClient()
    {
        var url = new Uri($"ws://{_ip}/socket");

        _client?.Dispose();
        _client = new WebsocketClient(url)
        {
            IsReconnectionEnabled = false
        };

        _client.DisconnectionHappened.Subscribe(OnDisconnected);
        _client.ReconnectionHappened.Subscribe(OnConnected);
        _client.MessageReceived.Subscribe(OnMessage);

        _client.Start();
    }

    private void OnDisconnected(DisconnectionInfo info)
    {
        ScreenLogger.Print("[IvWsClient] Disconnected");
        DispatchOnce.Do(3, CreateWebSocketClient);
    }

    private void OnConnected(ReconnectionInfo info)
    {
        ScreenLogger.Print("[IvWsClient] Connected");
    }

    private void OnMessage(ResponseMessage msg)
    {
        Message? message = msg.Text != null ? JsonSerializer.Deserialize<Message>(msg.Text, _jsonSerializerOptions) : null;
        if (message == null)
        {
            return;
        }

        if (message.Type != "controllers.status")
        {
            var text = msg.ToString();
            text = text.Length < MAX_CHARS_TO_PRINT ? text : $"{text[..MAX_CHARS_TO_PRINT]}...\nand {text.Length - MAX_CHARS_TO_PRINT} chars more.";
            ScreenLogger.Print("[IvWsC] " + text);
        }

        try
        {
            if (message.Type == "scan.started")
                ScanStarted?.Invoke(this, new TimedEventArgs(message));
            else if (message.Type == "scan.stopped")
                ScanStopped?.Invoke(this, new TimedEventArgs(message));
            else if (message.Type == "scan.finished")
                ScanFinished?.Invoke(this, new TimedEventArgs(message));
            else if (message.Type == "scan.resultsProcessed")
                ScanResultsProcessed?.Invoke(this, new TimedEventArgs(message));
            else if (message.Type == "scan.progress")
                ScanProgressChanged?.Invoke(this, new TimedEventArgs<ProcessProgress>(message));
            else if (message.Type == "scan.usernameChanged")
                ScanUserNameChanged?.Invoke(this, new TimedEventArgs<ScanUserName>(message));
            else if (message.Type == "scan.commentsChanged")
                ScanCommentsChanged?.Invoke(this, new TimedArrayEventArgs<string>(message));
            else if (message.Type == "scope.started")
                ScopeStarted?.Invoke(this, new TimedEventArgs(message));
            else if (message.Type == "scope.stopped")
                ScopeStopped?.Invoke(this, new TimedEventArgs(message));
            else if (message.Type == "scope.data")
                ScopeDataReceived?.Invoke(this, new TimedEventArgs<Defs.ScopeResult>(message));
            else if (message.Type == "scope.parametersChanged")
                ScopeParametersChanged?.Invoke(this, new TimedEventArgs<ScopeParameters>(message));
            else if (message.Type == "parameter.currentChanged")
                CurrentParameterChanged?.Invoke(this, new TimedEventArgs<CurrentParameter>(message));
            else if (message.Type == "parameter.setupCurrentStarted")
                ParameterSetupStarted?.Invoke(this, new TimedEventArgs(message));
            else if (message.Type == "parameter.setupCurrentFinished")
                ParameterSetupFinished?.Invoke(this, new TimedEventArgs(message));
            else if (message.Type == "parameter.preloadStarted")
                ParameterPreloadStarted?.Invoke(this, new TimedEventArgs(message));
            else if (message.Type == "parameter.preloadFinished")
                ParameterPreloadFinished?.Invoke(this, new TimedEventArgs(message));
            else if (message.Type == "parameter.edited")
                ParameterEdited?.Invoke(this, new TimedEventArgs<Defs.Parameter>(message));
            else if (message.Type == "parameter.listChanged")
                ParameterListChanged?.Invoke(this, new TimedEventArgs(message));
            else if (message.Type == "project.currentChanged")
                CurrentProjectChanged?.Invoke(this, new TimedEventArgs<CurrentProject>(message));
            else if (message.Type == "project.setupCurrentStarted")
                ProjectSetupStarted?.Invoke(this, new TimedEventArgs(message));
            else if (message.Type == "project.setupCurrentFinished")
                ProjectSetupFinished?.Invoke(this, new TimedEventArgs(message));
            else if (message.Type == "project.edited")
                ProjectEdited?.Invoke(this, new TimedEventArgs<ProjectEdit>(message));
            else if (message.Type == "project.listChanged")
                ProjectListChanged?.Invoke(this, new TimedEventArgs(message));
            else if (message.Type == "controllers.status")
                ControllersStatusTimer?.Invoke(this, new TimedEventArgs<ControllersStatus>(message));
            else if (message.Type == "device.standbyButtonPressed")
                StandbyButtonPressed?.Invoke(this, new TimedEventArgs(message));
            else if (message.Type == "device.shutdown")
                Shutdown?.Invoke(this, new TimedEventArgs(message));
            else if (message.Type == "message.error")
                ErrorReceived?.Invoke(this, new TimedEventArgs<Error>(message));
            else if (message.Type == "message.limitError")
                ErrorLimitsViolated?.Invoke(this, new TimedEventArgs<ErrorLimits>(message));
            else if (message.Type == "message.timeChanged")
                TimeChanged?.Invoke(this, new TimedEventArgs<Timestamp>(message));
            else if (message.Type == "backup.started")
                BackupStarted?.Invoke(this, new TimedEventArgs(message));
            else if (message.Type == "backup.finished")
                BackupFinished?.Invoke(this, new TimedEventArgs(message));
            else if (message.Type == "backup.progress")
                BackupProgress?.Invoke(this, new TimedEventArgs<ProcessProgress>(message));
            else if (message.Type == "restore.started")
                RestoreStarted?.Invoke(this, new TimedEventArgs(message));
            else if (message.Type == "restore.finished")
                RestoreFinished?.Invoke(this, new TimedEventArgs(message));
            else if (message.Type == "restore.progress")
                RestoreProgress?.Invoke(this, new TimedEventArgs<ProcessProgress>(message));
            else if (message.Type == "reset.started")
                ResetStarted?.Invoke(this, new TimedEventArgs(message));
            else if (message.Type == "reset.finished")
                ResetFinished?.Invoke(this, new TimedEventArgs(message));
            else if (message.Type == "update.started")
                UpdateStarted?.Invoke(this, new TimedEventArgs(message));
            else if (message.Type == "update.finished")
                UpdateFinished?.Invoke(this, new TimedEventArgs(message));
            else if (message.Type == "update.progress")
                UpdateProgress?.Invoke(this, new TimedEventArgs<ProcessProgress>(message));
            else if (message.Type == "cloud.sessionStart")
                CloudSessionStarted?.Invoke(this, new TimedEventArgs(message));
            else if (message.Type == "cloud.sessionEnd")
                CloudSessionFinished?.Invoke(this, new TimedEventArgs(message));
            else if (message.Type == "cloud.connectionError")
                CloudConnectionError?.Invoke(this, new TimedEventArgs(message));
            else if (message.Type == "cloud.connectionRestored")
                CloudConnectionRestored?.Invoke(this, new TimedEventArgs(message));
        }
        catch (TaskCanceledException)
        {
            // ignore this error
        }
        catch (Exception ex)
        {
            ScreenLogger.Print("[IvWsC] Exception in EventReported");
            ScreenLogger.Print($"[IvWsC] {ex.Message}");
        }
    }

    //private T As<T>(object body) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(body), _jsonSerializerOptions)!;
}