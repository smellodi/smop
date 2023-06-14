using System;
using System.Collections.Generic;

namespace Smop.OdorDisplay.Emulator
{
    internal class MFC
    {
        public static MFC Instance => _instance ??= new();

        public event EventHandler<string>? Debug;


        public double FreshAirFlowRate { get; private set; } = 1.0;
        public double OdorFlowRate { get; private set; } = 0.02;

        public OdorDisplay.MFC.OdorFlowsTo OdorDirection => (_deviceOutputs[DeviceOutputID.OdorValve].State, _deviceOutputs[DeviceOutputID.UserValve].State) switch
        {
            (DeviceState.On, DeviceState.Off) => OdorDisplay.MFC.OdorFlowsTo.SystemAndWaste,
            (DeviceState.Off, DeviceState.On) => OdorDisplay.MFC.OdorFlowsTo.WasteAndUser,
            (DeviceState.On, DeviceState.On) => OdorDisplay.MFC.OdorFlowsTo.SystemAndUser,
            _ => OdorDisplay.MFC.OdorFlowsTo.Waste
        };

        public bool IsPumpOn => _deviceOutputs[DeviceOutputID.PumpRelay].State == DeviceState.On;

        public Error Request(Packet query, out MessageResult? result, out MessageMFCResult? mfcResult)
        {
            //result = new MessageResult(Message.Result.OK);
            Debug?.Invoke(this, $"SND {query}");

            result = null;
            mfcResult = null;

            if (query is MessageNoop)
            {
                // do nothing
            }
            else if (query is MessageSetOutput setOutput)
            {
                foreach (DeviceOutput output in setOutput.DeviceOutputs)
                {
                    _deviceOutputs[output.ID] = output;
                }
            }
            else if (query is MessageSendMFC sendMFC_)
            {
                Write(sendMFC_.Cmd);
                _channelToRead = sendMFC_.Channel;
                /*
                if (sendMFC.SecondResponse != MessageType.None)
                {
                    string reply = Read(sendMFC.Channel);
                    mfcSample = new MessageMFCResult(reply);
                }*/
            }
            else
            {
                throw new Exception($"PID simulator does not support reading '{query.Type}' messages");
            }

            Error error;

            if (query is MessageSendMFC sendMFC && sendMFC.SecondResponse != PacketType.None)
            {
                error = _source.ReadMessages(new PacketType[] {
                    PacketType.Ack,
                    PacketType.MFCResult
                }, out Packets[] msgs);
                result = msgs.Length > 0 ? msgs[0] as MessageResult : null;
                mfcResult = msgs.Length > 1 ? msgs[1] as MessageMFCResult : null;
            }
            else
            {
                error = _source.ReadMessage(PacketType.Ack, out Packets? msg);
                result = msg as MessageResult;
            }

            return error;
        }

        public Error Request(Packet query, out MessageResult? result, out MessageSample? pidSample)
        {
            return PID.Instance.Request(query, out result, out pidSample);
        }
         
        // Internal

        static MFC? _instance;

        readonly Random _rnd = new((int)DateTime.Now.Ticks);

        readonly Source _source = Source.Instance;

        readonly Dictionary<DeviceOutputID, DeviceOutput> _deviceOutputs = new()
        {
            { DeviceOutputID.OdorValve, new DeviceOutput(DeviceOutputID.OdorValve, DeviceState.Off, 0) },
            { DeviceOutputID.UserValve, new DeviceOutput(DeviceOutputID.UserValve, DeviceState.Off, 0) },
            { DeviceOutputID.Reserved1, new DeviceOutput(DeviceOutputID.Reserved1, DeviceState.Off, 0) },
            { DeviceOutputID.NoiseValve, new DeviceOutput(DeviceOutputID.NoiseValve, DeviceState.Off, 0) },
            { DeviceOutputID.PumpRelay, new DeviceOutput(DeviceOutputID.PumpRelay, DeviceState.On, 0) },
        };

        const int _pressureA = 1200;
        const int _pressureB = 1800;
        const double _volFlowA = .05;
        const double _volFlowB = .05;

        char _channelToRead = ' ';

        private MFC()
        {
            _source.RequestMFCResult += Source_RequestMFCResult;
        }

        // emulates measurement inaccuracy
        private double e(double amplitude) => (_rnd.NextDouble() - 0.5) * 2 * amplitude;
        private int e(int amplitude) => _rnd.Next(-amplitude, amplitude);

        private void Write(string input)
        {
            /* Uncomment this if error emulation is desired
            if (rnd.NextDouble() < 0.0002)
            {
                throw new Exception("Simulating writing fault");
            }*/

            string[] cmds = input.Split(OdorDisplay.MFC.DATA_END);
            foreach (string cmd in cmds)
            {
                if (cmd.Length > 4)
                {
                    ExecuteCommand(cmd);
                }
            }
        }

        private string Read(char channel)
        {
            int pressure = channel == 'A' ? _pressureA : _pressureB;
            double massFlow = channel == 'A' ? FreshAirFlowRate : OdorFlowRate;
            double volFlow = channel == 'A' ? _volFlowA : _volFlowB;

            return string.Join(' ',
                channel.ToString(),                         // channel
                (pressure + e(15)).ToString(),              // Absolute pressure
                (24.74 + e(0.3)).ToString("F2"),            // Temp
                (volFlow + e(0.05)).ToString("F5"),         // Volumentric flow
                (massFlow + e(0.05)).ToString("F5"),        // Standart (Mass) Flow
                "+50.000",                                  // Setpoint
                "Air"                                       // Gas
            );
        }

        private void ExecuteCommand(string cmd)
        {
            var channel = (OdorDisplay.MFC.Channel)Enum.Parse(typeof(OdorDisplay.MFC.Channel), cmd[0].ToString(), true);
            string cmdID = cmd[1].ToString();
            if (cmdID == OdorDisplay.MFC.CMD_SET)
            {
                var value = double.Parse(cmd[2..]);
                switch (channel)
                {
                    case OdorDisplay.MFC.Channel.A:
                        FreshAirFlowRate = value;
                        break;
                    case OdorDisplay.MFC.Channel.B:
                        OdorFlowRate = value;
                        break;
                    default: break;
                }
            }
        }

        private void Source_RequestMFCResult(object? s, RequestMFCResultArgs e) =>
            e.MFCResult = new MessageMFCResult(Read(_channelToRead));
    }
}
