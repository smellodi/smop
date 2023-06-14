using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Smop.OdorDisplay
{
    public struct MFCChannel
    {
        public double Pressure { get; set; }
        public double Temperature { get; set; }
        public double VolumeFlow { get; set; }
        public double MassFlow { get; set; }
        public double Setpoint { get; set; }
        public string Gas { get; set; }

        public string ToString(char separator) => string.Join(separator, new string[] {
            MassFlow.ToString("F4"),
            Pressure.ToString("F1"),
            Temperature.ToString("F2"),
        });

        public static string[] Header => new string[] {
            "M ml/m",
            "Pr PSIA",
            "Temp C",
        };
    }

    /// <summary>
    /// Argument to be used in events fired in response to a command send to the device
    /// </summary>
    public class CommandResultArgs : EventArgs
    {
        public readonly string Command;
        public readonly string Value;
        public readonly Result Result;
        public CommandResultArgs(string command, string value, Result result)
        {
            Command = command;
            Value = value;
            Result = result;
        }
    }


    public class MFC
    {
        public static MFC Instance => _instance ??= new();

        public enum Channel
        {
            A = 'a',
            B = 'b',
        }
        
        /// <summary>
        /// These are actualy two bits to control two output valves bound to the MFC 'z' channel
        /// The lower bit controls the Valve #2: 0 - to waste, 1 - to user
        /// The higher bit controls the Valve #1: 0 - to waste, 1 - to system
        /// </summary>
        [Flags]
        public enum OdorFlowsTo
        { 
            Waste = 0,
            User = 1,
            System = 10,
            WasteAndUser = Waste | User,
            SystemAndWaste = System | Waste,
            SystemAndUser = System | User,
        }

        public event EventHandler<CommandResultArgs>? CommandResult;
        public event EventHandler? ParamsChanged;

        public bool IsDebugging
        {
            get => _isDebugging;
            set
            {
                _isDebugging = value;
                if (_isDebugging)
                {
                    _emulator = Emulator.MFC.Instance;
                }
            }
        }

        public string Name => "MFC";

        public const double ODOR_MAX_SPEED = 90.0;  // ml/min
        public const double ODOR_MIN_SPEED = 0.0;   // ml/min

        public static readonly string CMD_SET = "s";
        public static readonly string CMD_TARE_FLOW = "v";

        public const char DATA_END = '\r';


        // Control

        /// <summary>
        /// Fresh air flow speed, in liters per minute
        /// </summary>
        public double FreshAirSpeed
        {
            get => _freshAir;
            set
            {
                var val = value.ToString("F1").Replace(',', '.');
                (Result result, _) = SendCommand(FRESH_AIR_CHANNEL, CMD_SET, val);
                if (result.Error == Error.Success)
                {
                    _freshAir = value;
                    _logger.ClearAirFlow = _freshAir;
                    ParamsChanged?.Invoke(this, new EventArgs());
                }
                CommandResult?.Invoke(this, new CommandResultArgs(FRESH_AIR_CHANNEL + CMD_SET, val, result));
            }
        }

        /// <summary>
        /// Odor flow speed, in milliliters per minute
        /// </summary>
        public double OdorSpeed
        {
            get => _odor;
            set
            {
                var val = value.ToString("F1").Replace(',', '.');
                (Result result, _) = SendCommand(ODOR_CHANNEL, CMD_SET, val);
                if (result.Error == Error.Success)
                {
                    _odor = value;
                    _logger.OdorFlow = _odor;
                    ParamsChanged?.Invoke(this, new EventArgs());
                }
                CommandResult?.Invoke(this, new CommandResultArgs(ODOR_CHANNEL + CMD_SET, val, result));
            }
        }

        public DeviceState Pump
        {
            get => _pumpState;
            set
            {
                List<DeviceOutput> deviceOutputs = new()
                {
                    new DeviceOutput(DeviceOutputID.PumpRelay, value, 0)
                };

                var result = SendValveRequest(deviceOutputs.ToArray());

                if (result.Error == Error.Success)
                {
                    _pumpState = value;
                    ParamsChanged?.Invoke(this, new EventArgs());
                }
            }
        }

        /// <summary>
        /// Gets/sets the state of both valve. When the state is set to "on", the duration is equal to 0 to keep this state until the next change.
        /// </summary>
        public OdorFlowsTo OdorDirection
        {
            get => _odorDirection;
            set => SetGasValves(
                value.HasFlag(OdorFlowsTo.System) ? DeviceState.On : DeviceState.Off,
                value.HasFlag(OdorFlowsTo.User) ? DeviceState.On : DeviceState.Off,
                0);
        }

        /// <summary>
        /// Here, we check available channels (A and B).
        /// </summary>
        /// <returns>Error code and description</returns>
        public Result ReadState()
        {
            int channelCount = 0;
            Error error;

            try
            {
                if ((error = ReadChannelValues(Channel.A, out MFCChannel? freshAir).Error) == Error.Success)
                {
                    channelCount++;
                    _freshAir = Math.Round(freshAir?.MassFlow ?? 0, 1);
                }
                if ((error = ReadChannelValues(Channel.B, out MFCChannel? odor).Error) == Error.Success)
                {
                    channelCount++;
                    _odor = Math.Round(odor?.MassFlow ?? 0, 1);
                }
                if ((error = ReadValveValues(out bool isValve1Opened, out bool isValve2Opened).Error) == Error.Success)
                {
                    _odorDirection = OdorFlowsTo.Waste
                        | (isValve1Opened ? OdorFlowsTo.System : OdorFlowsTo.Waste)
                        | (isValve2Opened ? OdorFlowsTo.User : OdorFlowsTo.Waste);
                }
            }
            catch (Exception ex)
            {
                Stop();
                return new Result()
                {
                    Error = !_isDebugging ? (Error)ex.HResult : Error.AccessFailed,
                    Reason = "IO error: " + ex.Message
                };
            }

            if (channelCount == 0)
            {
                Stop();
                return new Result()
                {
                    Error = Error.NotReady,
                    Reason = "Both MF controllers are unavailable"
                };
            }

            return new Result()
            {
                Error = Error.Success,
                Reason = "The device state read successfully"
            };
        }

        public void Stop()
        {
            if (_com.IsOpen)
            {
                OdorSpeed = ODOR_MIN_SPEED;
                OdorDirection = OdorFlowsTo.Waste;
                Pump = DeviceState.Off;
            }
        }

        /// <summary>
        /// Opens/closes a valve
        /// </summary>
        /// <param name="valve">Valve to be affected</param>
        /// <param name="state">New state</param>
        /// <param name="duration">State duration in seconds</param>
        /// <returns>Request result</returns>
        public Result SetValve(DeviceOutputID valve, DeviceState state, double duration)
        {
            var ms = (int)(1000 * duration);

            List<DeviceOutput> deviceOutputs = new()
            {
                new DeviceOutput(valve, state, ms)
            };

            var result = SendValveRequest(deviceOutputs.ToArray());

            bool isGasValve = valve == DeviceOutputID.OdorValve || valve == DeviceOutputID.UserValve;
            if (result.Error == Error.Success && isGasValve)
            {
                _odorDirection = valve switch
                {
                    DeviceOutputID.OdorValve => 
                        (state == DeviceState.On ? OdorFlowsTo.System : OdorFlowsTo.Waste) |
                        (_odorDirection.HasFlag(OdorFlowsTo.User) ? OdorFlowsTo.User : OdorFlowsTo.Waste),
                    DeviceOutputID.UserValve =>
                        (state == DeviceState.On ? OdorFlowsTo.User : OdorFlowsTo.Waste) |
                        (_odorDirection.HasFlag(OdorFlowsTo.System) ? OdorFlowsTo.System : OdorFlowsTo.Waste),
                    _ => OdorFlowsTo.Waste
                };
                ParamsChanged?.Invoke(this, new EventArgs());
            }

            return result;
        }

        /// <summary>
        /// Controls both gas-flow valves at once
        /// </summary>
        /// <param name="odorValveState">Odor valve state</param>
        /// <param name="userValveState">User valve state</param>
        /// <param name="duration">Pulse duration in seconds. Set 0 to keep the state until next change</param>
        /// <returns>Result of the request</returns>
        public Result SetGasValves(DeviceState odorValveState, DeviceState userValveState, double duration = 0)
        {
            var ms = (int)(1000 * duration);

            List<DeviceOutput> deviceOutputs = new()
            {
                new DeviceOutput(DeviceOutputID.OdorValve, odorValveState, ms),
                new DeviceOutput(DeviceOutputID.UserValve, userValveState, ms)
            };

            var result = SendValveRequest(deviceOutputs.ToArray());

            if (result.Error == Error.Success)
            {
                OdorFlowsTo newOdorDirection = OdorFlowsTo.Waste;
                newOdorDirection |= odorValveState == DeviceState.On ? OdorFlowsTo.System : OdorFlowsTo.Waste;
                newOdorDirection |= userValveState == DeviceState.On ? OdorFlowsTo.User : OdorFlowsTo.Waste;

                _odorDirection = newOdorDirection;
                ParamsChanged?.Invoke(this, new EventArgs());
            }

            return result;
        }


        // Internal

        static MFC? _instance;

        double _freshAir = 5.0;
        double _odor = 4.0;
        OdorFlowsTo _odorDirection = OdorFlowsTo.Waste;
        DeviceState _pumpState = DeviceState.Off;

        bool _isDebugging = false;

        readonly CommPort _com = CommPort.Instance;
        readonly SyncLogger _logger = SyncLogger.Instance;

        Emulator.MFC? _emulator;

        // constants

        const Channel FRESH_AIR_CHANNEL = Channel.A;
        const Channel ODOR_CHANNEL = Channel.B;

        /// <summary>
        /// Private constructor, use Instance property to get the instance.
        /// </summary>
        private MFC()
        {
            var settings = Properties.Settings.Default;
            _freshAir = settings.MFC_FreshAir;
        }

        /// <summary>
        /// Read the mass flow rate and the temperature of the specified MFC.
        /// The MFC responses are like:
        /// A +01006 +047.74 -0.00003 -00.002 +50.000    Air
        /// </summary>
        /// <param name="channel">MFC channel, either 'A' of 'B'</param>
        /// <param name="sample">Data sample</param>
        /// <returns>Error code and reason</returns>
        private Result ReadChannelValues(Channel channel, out MFCChannel? sample)
        {
            sample = null;

            Error error;
            string? reason = null;

            var mfcAddr = channel.ToString()[0];
            MessageResult? result;
            MessageMFCResult? reply;

            var request = new MessageSendMFC($"{mfcAddr}{DATA_END}");

            if (!_isDebugging)
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

            HandleReply(request.ToString(), ref error, ref reason, result, reply, PacketType.MFCResult);

            if (error == Error.Success && reply != null)
            {
                return HandleMFCSample(reply.Result, mfcAddr, out sample);
            }

            return new Result()
            {
                Error = error,
                Reason = reason
            };
        }

        private Result ReadValveValues(out bool isValve1Opened, out bool isValve2Opened)
        {
            Error error;
            string? reason = null;

            MessageResult? result;
            MessageSample? reply;

            MessageGetSample request = new();

            if (!_isDebugging)
            {
                error = _com.Request(request, out result, out reply);

                if (_com.PortError != null)
                {
                    error = (Error)Marshal.GetLastWin32Error();
                    reason = $"COM error on sending '{request}' to the port";
                }
            }
            else
            {
                error = _emulator!.Request(request, out result, out reply);
            }

            HandleReply(request.ToString(), ref error, ref reason, result, reply, PacketType.Sample);

            isValve1Opened = error == Error.Success && reply?.OdorValve == DeviceState.On;
            isValve2Opened = error == Error.Success && reply?.UserValve == DeviceState.On;

            return new Result()
            {
                Error = error,
                Reason = reason?.Replace(DATA_END.ToString(), @"\r") ?? string.Empty,
            };

        }

        private Result SendValveRequest(DeviceOutput[] deviceOutputs)
        {
            Error error;
            MessageResult? result;
            string? reason = null;

            MessageSetOutput request = new(deviceOutputs);
            if (!_isDebugging)
            {
                error = _com.Request(request, out result);
                if (_com.PortError != null)
                {
                    error = (Error)Marshal.GetLastWin32Error();
                    reason = $"COM error on setting valves '{request}'";
                }
            }
            else
            {
                error = _emulator!.Request(request, out result, out MessageMFCResult _);
            }

            HandleReply(request.ToString(), ref error, ref reason, result);

            var r = new Result()
            {
                Error = error,
                Reason = reason
            };

            CommandResult?.Invoke(this, new CommandResultArgs("VALVES", request.ToString(), r));

            return r;
        }

        /// <summary>
        /// Sends a specific command to the port.
        /// All parameters are simply concatenated in the order they appear
        /// </summary>
        /// <param name="channel">channel to send to</param>
        /// <param name="cmd">command to send</param>
        /// <param name="value">value to send</param>
        /// <returns>Error type and description, and potentially the reply (or null)</returns>
        private (Result, MessageMFCResult?) SendCommand(Channel channel, string cmd, string value = "")
        {
            return SendCommands(new string[] { char.ToLower((char)channel) + cmd + value });
        }

        /// <summary>
        /// Sends several commands to the port at once
        /// </summary>
        /// <param name="commands">commands</param>
        /// <returns>Error type and description, and potentially the reply (or null)</returns>
        private (Result, MessageMFCResult?) SendCommands(string[] commands)
        {
            if (!_com.IsOpen)
            {
                throw new Exception("Port is nor opened");
            }

            Error error;
            string? reason = null;

            MessageResult? result;
            MessageMFCResult? reply;

            var command = string.Join(DATA_END, commands) + DATA_END;

            // expect reply only from any MFC command
            PacketType expectedReplyType = PacketType.MFCResult;

            MessageSendMFC request = new(command);

            if (!_isDebugging)
            {
                error = _com.Request(request, out result, out reply);
                if (_com.PortError != null)
                {
                    error = (Error)Marshal.GetLastWin32Error();
                    reason = $"COM error on sending '{command}' to the port";
                }
            }
            else
            {
                error = _emulator!.Request(request, out result, out reply);
            }

            HandleReply(request.ToString(), ref error, ref reason, result, reply, expectedReplyType);

            return (new Result()
            {
                Error = error,
                Reason = reason?.Replace(DATA_END.ToString(), @"\r") ?? string.Empty,
            }, reply);
        }

        private void HandleReply(string command, ref Error error, ref string? reason, MessageResult? result, Packet? reply = null, PacketType extectedReplyType = PacketType.None)
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
            else if (extectedReplyType != PacketType.None && reply?.Type != extectedReplyType)
            {
                error = Error.InvalidData;
                reason = $"Wrong MFC response packet for '{command}'";
            }
            else
            {
                reason = $"Command '{command}' sent successfully";
            }
        }

        private Result HandleMFCSample(string response, char mfcAddr, out MFCChannel? buffer)
        {
            buffer = null;
            Error error = Error.Success;
            string reason = "OK";

            string[] values = new string(response.Replace(',', '.')).Split(' ');
            if (values.Length < 7)
            {
                error = Error.BadDataFormat;
                reason = "Invalid MFC response format";
            }
            else if (values[0][0] != mfcAddr)
            {
                error = Error.WrongDevice;
                reason = "Wrong MFC device";
            }
            else
            {
                try
                {
                    buffer = new MFCChannel()
                    {
                        Pressure = double.Parse(values[1]),
                        Temperature = double.Parse(values[2]),
                        VolumeFlow = double.Parse(values[3]),
                        MassFlow = double.Parse(values[4]),
                        Setpoint = double.Parse(values[5]),
                        Gas = values[6]
                    };
                }
                catch
                {
                    error = Error.InvalidData;
                    reason = "Invalid MFC response data";
                }
            }

            return new Result()
            {
                Error = error,
                Reason = reason
            };
        }
    }
}
