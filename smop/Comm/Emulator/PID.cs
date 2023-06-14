using System;

namespace Smop.OdorDisplay.Emulator
{
    internal class PID
    {
        public static PID Instance => _instance ??= new();

        public OlfactoryDeviceModel Model => _model;

        public Source Source => _source;

        public event EventHandler<string>? Debug;

        public Error Request(Packet query, out MessageResult? result, out MessageSample? sample)
        {
            //result = new MessageResult(Message.Result.OK);
            Debug?.Invoke(this, $"SND {query}");

            result = null;
            sample = null;

            if (query is MessageNoop)
            {
                // do nothing
            }
            else if (query is MessageSetPID)
            {
                // do nothing
            }
            else if (query is MessageSetSampling setSampling)
            {
                _source.SamplingInterval = setSampling.Interval;
            }
            else if (query is MessageGetSample)
            {
                // sample = CreateSample();
            }
            else
            {
                throw new Exception($"PID simulator does not support reading '{query.Type}' messages");
            }

            Error error;
            if (query is MessageGetSample)
            {
                error = _source.ReadMessages(new PacketType[] {
                    PacketType.Ack,
                    PacketType.Sample
                }, out Packets[] msgs);
                result = msgs.Length > 0 ? msgs[0] as MessageResult : null;
                sample = msgs.Length > 1 ? msgs[1] as MessageSample : null;
            }
            else
            {
                error = _source.ReadMessage(PacketType.Ack, out Packets? msg1);
                result = msg1 as MessageResult;
            }

            return error;
        }

        public void Stop()
        {
            _source.Stop();
        }


        // Internal

        static PID? _instance;

        readonly MFC _mfcEmul = MFC.Instance;

        readonly Random _rnd = new((int)DateTime.Now.Ticks);
        readonly OlfactoryDeviceModel _model = new();

        readonly Source _source = Source.Instance;

        private PID()
        {
            _source.RequestSample += Source_RequestSample;
        }

        private MessageSample? CreateSample()
        {
            var time = new BtoD((uint)Utils.Timestamp.Ms);
            var pid0 = new BtoD(_model.PID / 1000 + e(0.0003));
            var pid1 = new BtoD(0.02f + e(0.0004));
            var ther0 = new BtoD(100_000f + 2000 * (float)Math.Sin(Utils.Timestamp.Ms % 5000 * 0.072 * Math.PI / 180f) + e(3.0));  // 5s is the breathing cycle
            var ther1 = new BtoD(float.PositiveInfinity);
            var ic0 = new BtoD(29.3 * 0.01f + e(0.0005));
            var ic1 = new BtoD(0.794579f + 50 * 0.03003766f + e(0.003));
            var ic2 = new BtoD(0f);
            var aux0 = new BtoD(0f);
            var aux1 = new BtoD(0f);
            var aux2 = new BtoD(0f);
            byte odorValve = (byte)(_mfcEmul.OdorDirection.HasFlag(OdorDisplay.MFC.OdorFlowsTo.System) ? 1 : 0);
            byte userValve = (byte)(_mfcEmul.OdorDirection.HasFlag(OdorDisplay.MFC.OdorFlowsTo.User) ? 1 : 0);
            byte pumpRelay = (byte)(_mfcEmul.IsPumpOn ? 1 : 0);

            return MessageSample.From(new Packet(new byte[52] {
                    (byte)PacketType.Sample, 49,
                    time.B0, time.B1, time.B2, time.B3,
                    pid0.B0, pid0.B1, pid0.B2, pid0.B3,
                    pid1.B0, pid1.B1, pid1.B2, pid1.B3,
                    ther0.B0, ther0.B1, ther0.B2, ther0.B3,
                    ther1.B0, ther1.B1, ther1.B2, ther1.B3,
                    ic0.B0, ic0.B1, ic0.B2, ic0.B3,
                    ic1.B0, ic1.B1, ic1.B2, ic1.B3,
                    ic2.B0, ic2.B1, ic2.B2, ic2.B3,
                    aux0.B0, aux0.B1, aux0.B2, aux0.B3,
                    aux1.B0, aux1.B1, aux1.B2, aux1.B3,
                    aux2.B0, aux2.B1, aux2.B2, aux2.B3,
                    odorValve, userValve, 0, 0, pumpRelay,

                    (byte)~((byte)PacketType.Sample + 49 +
                    time.B0 + time.B1 + time.B2 + time.B3 +
                    pid0.B0 + pid0.B1 + pid0.B2 + pid0.B3+
                    pid1.B0 + pid1.B1 + pid1.B2 + pid1.B3+
                    ther0.B0 + ther0.B1 + ther0.B2 + ther0.B3+
                    ther1.B0 + ther1.B1 + ther1.B2 + ther1.B3+
                    ic0.B0 + ic0.B1 + ic0.B2 + ic0.B3+
                    ic1.B0 + ic1.B1 + ic1.B2 + ic1.B3+
                    ic2.B0 + ic2.B1 + ic2.B2 + ic2.B3+
                    aux0.B0 + aux0.B1 + aux0.B2 + aux0.B3+
                    aux1.B0 + aux1.B1 + aux1.B2 + aux1.B3+
                    aux2.B0 + aux2.B1 + aux2.B2 + aux2.B3+
                    odorValve + userValve + 0 + 0 + pumpRelay)
                }));
        }

        private void Source_RequestSample(object? s, RequestSampleArgs e) => 
            e.Sample = CreateSample();

        // emulates measurement inaccuracy
        private double e(double amplitude) => (_rnd.NextDouble() - 0.5) * 2 * amplitude;
        private int e(int amplitude) => _rnd.Next(-amplitude, amplitude);
    }
}
