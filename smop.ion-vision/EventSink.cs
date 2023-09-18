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
public class EventSink : IDisposable
{
    /// <summary>
    /// Arguments of an event without a payload (message "body" field is empty)
    /// </summary>
    public class TimedEventArgs : EventArgs
    {
        public long Timestamp { get; init; }
        public TimedEventArgs(long timestamp) => Timestamp = timestamp;
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
        public TimedEventArgs(long timestamp, T data) : base(timestamp)
        {
            Timestamp = timestamp;
            Data = data;
        }
    }

    #region Message body payloads

    public record class ProcessProgress(int Progress);
    public record class ScanUserName(string? UserName);
    public record class ScopeData(float Usv, float[] Ucv, float[] IntensityTop, float[] IntensityBottom);
    public record class ScopeParameters(
        float UcvStart, float UcvStop, float Usv, float Vb,
        float PP, float PW,
        float SampleAverages,
        float SampleFlowControl,
        float SensorFlowControl,
        float SampleHeaterTemperatureControl,
        float SensorHeaterTemperatureControl);
    public record class CurrentParameter(Parameter NewParameter);
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
    public record class ControllersStatus(
        Status Status,
        SensorSample Sample,
        SensorSample Sensor,
        Detector Ambient);
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
    public event EventHandler<TimedEventArgs<KeyValuePair<string, string>[]>>? ScanCommentsChanged;
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
    public event EventHandler<TimedEventArgs<ScopeData>>? ScopeDataReceived;
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
    public event EventHandler<TimedEventArgs<Parameter>>? ParameterEdited;
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
    public event EventHandler<TimedEventArgs<Error>>? ErrorCode;
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

    private record class Message(string Type, long Time, object? Body);

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
        System.Diagnostics.Debug.WriteLine("[WS-C] Disconnected");
        DispatchOnce.Do(3, CreateWebSocketClient);
    }

    private void OnConnected(ReconnectionInfo info)
    {
        System.Diagnostics.Debug.WriteLine("[WS-C] Connected");
    }

    private void OnMessage(ResponseMessage msg)
    {
        Message? message = msg.Text != null ? JsonSerializer.Deserialize<Message>(msg.Text, _jsonSerializerOptions) : null;
        if (message == null)
        {
            return;
        }

        try
        {
            if (message.Type == "scan.started")
                ScanStarted?.Invoke(this, new TimedEventArgs(message.Time));
            else if (message.Type == "scan.stopped")
                ScanStopped?.Invoke(this, new TimedEventArgs(message.Time));
            else if (message.Type == "scan.finished")
                ScanStopped?.Invoke(this, new TimedEventArgs(message.Time));
            else if (message.Type == "scan.resultsProcessed")
                ScanResultsProcessed?.Invoke(this, new TimedEventArgs(message.Time));
            else if (message.Type == "scan.progress")
            {
                var data = JsonSerializer.Deserialize<ProcessProgress>(JsonSerializer.Serialize(message.Body), _jsonSerializerOptions);
                ScanProgressChanged?.Invoke(this, new TimedEventArgs<ProcessProgress>(message.Time, data!));
            }
            else if (message.Type == "scan.usernameChanged")
            {
                var data = JsonSerializer.Deserialize<ScanUserName>(JsonSerializer.Serialize(message.Body), _jsonSerializerOptions);
                ScanUserNameChanged?.Invoke(this, new TimedEventArgs<ScanUserName>(message.Time, data!));
            }
            else if (message.Type == "scan.commentsChanged")
            {
                var data = message.Body!.GetType().GetProperties().Select(prop =>
                    new KeyValuePair<string, string>(prop.Name, (string)prop.GetValue(message.Body, null)!)).ToArray();
                ScanCommentsChanged?.Invoke(this, new TimedEventArgs<KeyValuePair<string, string>[]>(message.Time, data));
            }
            else if (message.Type == "scope.started")
                ScopeStarted?.Invoke(this, new TimedEventArgs(message.Time));
            else if (message.Type == "scope.stopped")
                ScopeStopped?.Invoke(this, new TimedEventArgs(message.Time));
            else if (message.Type == "scope.data")
            {
                var data = JsonSerializer.Deserialize<ScopeData>(JsonSerializer.Serialize(message.Body), _jsonSerializerOptions);
                ScopeDataReceived?.Invoke(this, new TimedEventArgs<ScopeData>(message.Time, data!));
            }
            else if (message.Type == "scope.parametersChanged")
            {
                var data = JsonSerializer.Deserialize<ScopeParameters>(JsonSerializer.Serialize(message.Body), _jsonSerializerOptions);
                ScopeParametersChanged?.Invoke(this, new TimedEventArgs<ScopeParameters>(message.Time, data!));
            }
            else if (message.Type == "parameter.currentChanged")
            {
                var data = JsonSerializer.Deserialize<CurrentParameter>(JsonSerializer.Serialize(message.Body), _jsonSerializerOptions);
                CurrentParameterChanged?.Invoke(this, new TimedEventArgs<CurrentParameter>(message.Time, data!));
            }
            else if (message.Type == "parameter.setupCurrentStarted")
                ParameterSetupStarted?.Invoke(this, new TimedEventArgs(message.Time));
            else if (message.Type == "parameter.setupCurrentFinished")
                ParameterSetupFinished?.Invoke(this, new TimedEventArgs(message.Time));
            else if (message.Type == "parameter.preloadStarted")
                ParameterPreloadStarted?.Invoke(this, new TimedEventArgs(message.Time));
            else if (message.Type == "parameter.preloadFinished")
                ParameterPreloadFinished?.Invoke(this, new TimedEventArgs(message.Time));
            else if (message.Type == "parameter.edited")
            {
                var data = JsonSerializer.Deserialize<Parameter>(JsonSerializer.Serialize(message.Body), _jsonSerializerOptions);
                ParameterEdited?.Invoke(this, new TimedEventArgs<Parameter>(message.Time, data!));
            }
            else if (message.Type == "parameter.listChanged")
                ParameterListChanged?.Invoke(this, new TimedEventArgs(message.Time));
            else if (message.Type == "project.currentChanged")
            {
                var data = JsonSerializer.Deserialize<CurrentProject>(JsonSerializer.Serialize(message.Body), _jsonSerializerOptions);
                CurrentProjectChanged?.Invoke(this, new TimedEventArgs<CurrentProject>(message.Time, data!));
            }
            else if (message.Type == "project.setupCurrentStarted")
                ProjectSetupStarted?.Invoke(this, new TimedEventArgs(message.Time));
            else if (message.Type == "project.setupCurrentFinished")
                ProjectSetupFinished?.Invoke(this, new TimedEventArgs(message.Time));
            else if (message.Type == "project.edited")
            {
                var data = JsonSerializer.Deserialize<ProjectEdit>(JsonSerializer.Serialize(message.Body), _jsonSerializerOptions);
                ProjectEdited?.Invoke(this, new TimedEventArgs<ProjectEdit>(message.Time, data!));
            }
            else if (message.Type == "project.listChanged")
                ProjectListChanged?.Invoke(this, new TimedEventArgs(message.Time));
            else if (message.Type == "controllers.status")
            {
                var data = JsonSerializer.Deserialize<ControllersStatus>(JsonSerializer.Serialize(message.Body), _jsonSerializerOptions);
                ControllersStatusTimer?.Invoke(this, new TimedEventArgs<ControllersStatus>(message.Time, data!));
            }
            else if (message.Type == "device.standbyButtonPressed")
                StandbyButtonPressed?.Invoke(this, new TimedEventArgs(message.Time));
            else if (message.Type == "device.shutdown")
                StandbyButtonPressed?.Invoke(this, new TimedEventArgs(message.Time));
            else if (message.Type == "message.error")
            {
                var data = JsonSerializer.Deserialize<Error>(JsonSerializer.Serialize(message.Body), _jsonSerializerOptions);
                ErrorCode?.Invoke(this, new TimedEventArgs<Error>(message.Time, data!));
            }
            else if (message.Type == "message.limitError")
            {
                var data = JsonSerializer.Deserialize<ErrorLimits>(JsonSerializer.Serialize(message.Body), _jsonSerializerOptions);
                ErrorLimitsViolated?.Invoke(this, new TimedEventArgs<ErrorLimits>(message.Time, data!));
            }
            else if (message.Type == "message.timeChanged")
            {
                var data = JsonSerializer.Deserialize<Timestamp>(JsonSerializer.Serialize(message.Body), _jsonSerializerOptions);
                TimeChanged?.Invoke(this, new TimedEventArgs<Timestamp>(message.Time, data!));
            }
            else if (message.Type == "backup.started")
                BackupStarted?.Invoke(this, new TimedEventArgs(message.Time));
            else if (message.Type == "backup.finished")
                BackupFinished?.Invoke(this, new TimedEventArgs(message.Time));
            else if (message.Type == "backup.progress")
            {
                var data = JsonSerializer.Deserialize<ProcessProgress>(JsonSerializer.Serialize(message.Body), _jsonSerializerOptions);
                BackupProgress?.Invoke(this, new TimedEventArgs<ProcessProgress>(message.Time, data!));
            }
            else if (message.Type == "restore.started")
                RestoreStarted?.Invoke(this, new TimedEventArgs(message.Time));
            else if (message.Type == "restore.finished")
                RestoreFinished?.Invoke(this, new TimedEventArgs(message.Time));
            else if (message.Type == "restore.progress")
            {
                var data = JsonSerializer.Deserialize<ProcessProgress>(JsonSerializer.Serialize(message.Body), _jsonSerializerOptions);
                RestoreProgress?.Invoke(this, new TimedEventArgs<ProcessProgress>(message.Time, data!));
            }
            else if (message.Type == "reset.started")
                RestoreStarted?.Invoke(this, new TimedEventArgs(message.Time));
            else if (message.Type == "reset.finished")
                RestoreFinished?.Invoke(this, new TimedEventArgs(message.Time));
            else if (message.Type == "update.started")
                UpdateStarted?.Invoke(this, new TimedEventArgs(message.Time));
            else if (message.Type == "update.finished")
                UpdateFinished?.Invoke(this, new TimedEventArgs(message.Time));
            else if (message.Type == "update.progress")
            {
                var data = JsonSerializer.Deserialize<ProcessProgress>(JsonSerializer.Serialize(message.Body), _jsonSerializerOptions);
                UpdateProgress?.Invoke(this, new TimedEventArgs<ProcessProgress>(message.Time, data!));
            }
            else if (message.Type == "cloud.sessionStart")
                CloudSessionStarted?.Invoke(this, new TimedEventArgs(message.Time));
            else if (message.Type == "cloud.sessionEnd")
                CloudSessionFinished?.Invoke(this, new TimedEventArgs(message.Time));
            else if (message.Type == "cloud.connectionError")
                CloudConnectionError?.Invoke(this, new TimedEventArgs(message.Time));
            else if (message.Type == "cloud.connectionRestored")
                CloudConnectionRestored?.Invoke(this, new TimedEventArgs(message.Time));
        }
        catch (TaskCanceledException)
        {
            // ignore this error
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("! Exception in EventReported !");
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }
}