using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace SMOP.Comm
{
    public class PID
    {
        public static PID Instance => _instance ??= new();

        public bool IsDebugging
        {
            get => _isDebugging;
            set
            {
                if (_isDebugging != value)
                {
                    _isDebugging = value;
                    if (_isDebugging)
                    {
                        _emulator = Emulator.PID.Instance;
                        _emulator.Source.Sample += COM_SampleAsync;
                    }
                    else if (_emulator != null)
                    {
                        _emulator.Source.Sample -= COM_SampleAsync;
                        _emulator = null;
                    }
                }
            }
        }

        public bool ArePIDsOn { get; private set; } = false;

        public double Value => _emulator?.Model.PID ?? _lastSample.SystemPID;

        public event EventHandler<DeviceSample>? Sample;

        /// <summary>
        /// In addition closing the port, we have to try to turn the lamp off (this will fail, if the PID SDK wasn't connected)
        /// </summary>
        public void Stop()
        {
            if (_com.IsOpen)
            {
                try
                {
                    EnableLamps(false);
                }
                catch { }

                try
                {
                    EnableSampling(0);
                }
                catch { }
            }
        }

        /// <summary>
        /// Read data from the port
        /// </summary>
        /// <param name="sample">Buffer to store data</param>
        /// <returns>Error code and description</returns>
        public Result GetSample(out DeviceSample sample)
        {
            if (!_com.IsOpen)
            {
                throw new Exception("Port is not opened");
            }

            Result result;
            try
            {
                result = ReadSample(out sample);
            }
            catch (Exception ex)
            {
                Stop();
                sample = new DeviceSample();
                return new Result()
                {
                    Error = !IsDebugging ? (Error)ex.HResult : Error.AccessFailed,
                    Reason = "PID IO error: " + ex.Message
                };
            }

            if (result.Error == Error.Success)
            {
                _lastSample = sample;
            }

            return result;
        }

        /// <summary>
        /// Enables/disabled auto-sampling
        /// </summary>
        /// <param name="interval">Interval in seconds</param>
        /// <returns>Error code and description</returns>
        public Result EnableSampling(double interval)
        {
            if (!_com.IsOpen)
            {
                return new Result { Error = Error.NotReady, Reason = "Port is not opened" };
                //throw new Exception("Port is not opened");
            }

            MessageResult? result;
            Error error;
            string? reason = null;

            ushort ms = (ushort)(100 * Math.Round(interval * 10));
            MessageSetSampling request = new(ms);

            if (!IsDebugging)
            {
                error = _com.Request(request, out result);
                if (_com.PortError != null)
                {
                    error = (Error)Marshal.GetLastWin32Error();
                }
            }
            else
            {
                error = _emulator!.Request(request, out result, out _);
            }

            HandleReply(request.ToString(), ref error, ref reason, result);

            return new Result()
            {
                Error = error,
                Reason = reason
            };
        }


        // Internal

        static PID? _instance;

        readonly CommPort _com = CommPort.Instance;

        Emulator.PID? _emulator;
        DeviceSample _lastSample = new();

        bool _isDebugging = false;

        /// <summary>
        /// Private constructor; use Instance property to get the instance.
        /// </summary>
        private PID()
        {
            _com.Opened += COM_Opened;
            _com.Sample += COM_SampleAsync;
            _com.Closed += COM_Closed;
        }

        /// <summary>
        /// Try to turn the PID lamp on
        /// </summary>
        /// <returns>Error code and description</returns>
        private Result Initialize()
        {
            Result result;
            try
            {
                result = EnableLamps(true);
            }
            catch (Exception ex)
            {
                Stop();
                result = new Result()
                {
                    Error = (Error)ex.HResult,
                    Reason = "PID lamp IO error: " + ex.Message
                };
            }

            return result;
        }

        /// <summary>
        /// Before measuring can start, the PID must be turned on.
        /// </summary>
        /// <param name="enable">State to set</param>
        /// <returns>Reading result</returns>
        private Result EnableLamps(bool enable)
        {
            string? reason = null;
            Error error;
            MessageResult? result;

            MessageSetPID request = new(enable ? DeviceState.On : DeviceState.Off);

            if (!IsDebugging)
            {
                error = _com.Request(request, out result);
                if (_com.PortError != null)
                {
                    error = (Error)Marshal.GetLastWin32Error();
                }
            }
            else
            {
                error = _emulator!.Request(request, out result, out MessageSample _);
            }

            HandleReply(request.ToString(), ref error, ref reason, result);

            ArePIDsOn = enable && error == Error.Success;

            return new Result()
            {
                Error = error,
                Reason = reason
            };
        }

        /// <summary>
        /// Reads PID sample.
        /// </summary>
        /// <param name="sample">sample</param>
        /// <returns>Reading result</returns>
        private Result ReadSample(out DeviceSample sample)
        {
            MessageGetSample request = new();

            Thread.Sleep(3);   // Guarantees sufficient spacing between commands; shouldn't be actually needed.

            Error error;
            string? reason = null;

            MessageResult? result;
            MessageSample? reply;

            if (!IsDebugging)
            {
                error = _com.Request(request, out result, out reply);
                if (_com.PortError != null)
                {
                    error = (Error)Marshal.GetLastWin32Error();
                }
            }
            else
            {
                error = _emulator!.Request(request, out result, out reply);
            }

            HandleReply(request.ToString(), ref error, ref reason, result, reply);

            if (error == Error.Success && reply != null)
            {
                sample = new DeviceSample(reply);
            }
            else
            {
                sample = new DeviceSample();
            }

            return new Result()
            {
                Error = error,
                Reason = reason
            };
        }

        private void HandleReply(string command, ref Error error, ref string? reason, MessageResult? result, MessageSample? reply = null)
        {
            if (error != Error.Success)
            {
                reason = $"Failed to send/receive '{command}'";
                //Stop();
            }
            else if (!string.IsNullOrEmpty(reason))
            {
                error = Error.AccessFailed;
                //Stop();
            }
            else if (result?.Type != PacketType.Ack)
            {
                error = Error.InvalidData;
                reason = $"Wrong RESULT response packet for '{command}'";
            }
            else if (result.Result != Packets.Result.OK)
            {
                error = (Error)((int)Error.DeviceError | (int)result.Result);
                reason = $"Got '{result.Result}' from the port for '{command}'";
            }
            else if (reply != null && reply.Type != PacketType.Sample)
            {
                error = Error.InvalidData;
                reason = $"Wrong MFC response packet for '{command}'";
            }
            else
            {
                reason = $"Command '{command}' sent successfully";
            }
        }

        private async void COM_SampleAsync(object? s, MessageSample? sample)
        {
            await System.Threading.Tasks.Task.Run(() =>
            {
                if (sample!= null)
                {
                    var pidSample = new DeviceSample(sample);
                    try
                    {
                        Sample?.Invoke(this, pidSample);
                    }
                    catch { }
                }
            });
        }

        private void COM_Opened(object? s, EventArgs e)
        {
            var result = Initialize();
            if (result.Error != Error.Success)
            {
                Packets.Result deviceError = (Packets.Result)((int)result.Error & ~(int)Error.DeviceError);
                Utils.MsgBox.Error("PID", Utils.L10n.T("DeviceError") + $"\n[{deviceError}] {result.Reason}");
            }
        }

        private void COM_Closed(object? s, EventArgs e) => _emulator?.Stop();
    }
}
