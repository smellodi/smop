using System;
using System.Collections.Generic;
using System.Threading;

namespace SMOP.Comm.Emulator
{
    public class RequestSampleArgs : EventArgs
    {
        public MessageSample? Sample { get; set; }
    }

    public class RequestMFCResultArgs : EventArgs
    {
        public MessageMFCResult? MFCResult { get; set; }
    }

    public class Source
    {
        public static Source Instance => _instance ??= new();

        /// <summary>
        /// The handler MUST be async!
        /// </summary>
        public event EventHandler<MessageSample?>? Sample;

        /// <summary>
        /// The handler MUST be async!
        /// </summary>
        public event EventHandler<MessageMFCResult?>? MFC;

        public event EventHandler<RequestSampleArgs>? RequestSample;
        public event EventHandler<RequestMFCResultArgs>? RequestMFCResult;

        public event EventHandler<string>? Debug;

        public int SamplingInterval { get; set; } = 0;

        public void Stop()
        {
            if (_samplingThread != null)
            {
                _samplingThread.Interrupt();
                _samplingThread = null;
            }
        }

        public Error ReadMessage(PacketType type, out Packet? msg)
        {
            var result = ReadMessages(new PacketType[] { type }, out Packet[] msgs);
            msg = msgs.Length > 0 ? msgs[0] : null;
            return result;
        }

        public Error ReadMessages(PacketType[] types, out Packet[] msgs)
        {
            //Debug?.Invoke(this, $"REQ {string.Join(',', types)}");

            List<Packet> replies = new();
            List<Request> requests = new();

            lock (_requests)
            {
                foreach (var type in types)
                {
                    Request req = new(type);
                    _requests.Enqueue(req);
                    requests.Add(req);
                }
            }

            foreach (var req in requests)
            {
                if (!req.WaitUntilUnlocked())
                {
                    req.IsValid = false;
                    Debug?.Invoke(this, $"REQ !TIMEOUT after {req.Duration} ms: {req.Type}");
                    msgs = replies.ToArray();
                    return Error.Timeout;
                }

                if (req.Message != null && req.IsValid)
                {
                    replies.Add(req.Message);
                }
            }

            msgs = replies.ToArray();
            return Error.Success;
        }

        // Internal

        static Source? _instance;

        const int INTERVAL = 10;

        readonly Queue<Request> _requests = new();

        Thread? _samplingThread = null;
        int _pause = 0;

        //Random _rnd = new((int)DateTime.Now.Ticks);

        private Source()
        {
            _samplingThread = new Thread(SampleAndRequestThread);
            _samplingThread.Start();
        }

        private void SampleAndRequestThread()
        {
            while (true)
            {
                try { Thread.Sleep(INTERVAL); }
                catch (ThreadInterruptedException) { break; }  // will exit the loop upon Interrupt is called

                //Debug?.Invoke(this, $"READ [cycle start]");

                Error error = Read(out Packet? message);

                lock (_requests)
                {
                    Request? req = null;
                    while (_requests.Count > 0 && !(req = _requests.Peek()).IsValid)
                    {
                        _requests.Dequeue();
                        Debug?.Invoke(this, $"REQ !CLEANED after {req.Duration} ms: {req.Type}");
                        req = null;
                    }

                    if (message != null)
                    {
                        if (req?.Type == message.Type)
                        {
                            _requests.Dequeue();

                            Debug?.Invoke(this, $"RCV [{req.Duration}ms]: {message}");
                            req.SetReply(message, error);
                        }
                        /*NEW
                        else if (message.Type == MessageType.Sample)
                        {
                            Debug?.Invoke(this, $"RCV [pop] {message}");
                            Sample?.Invoke(this, MessageSample.From(message));
                        }
                        else if (message.Type == MessageType.MFCResult)
                        {
                            Debug?.Invoke(this, $"RCV [pop] {message}");
                            MFC?.Invoke(this, MessageMFCResult.From(message));
                        }*/
                        else
                        {
                            Debug?.Invoke(this, $"RCV [pop] !UNEXPECTED {message}");
                        }
                    }
                    else
                    {
                        Debug?.Invoke(this, $"READ ! no message");
                    }
                }

                //Debug?.Invoke(this, $"READ [cycle end]");
            }
        }

        private Error Read(out Packet? message)
        {
            message = null;

            while (message == null)
            {
                try { Thread.Sleep(INTERVAL); }
                catch (ThreadInterruptedException) { break; }  // will exit the loop upon Interrupt is called

                Request? req = null;
                lock (_requests)
                {
                    if (_requests.Count > 0)
                    {
                        req = _requests.Peek();
                    }
                }

                bool intentionalSampleIntrusion = false; // req != null && _rnd.NextDouble() > 0.5;
                if ((SamplingInterval > 0 && (_pause += INTERVAL) > SamplingInterval) || intentionalSampleIntrusion)
                {
                    try { Thread.Sleep(INTERVAL); }
                    catch (ThreadInterruptedException) { break; }

                    RequestSampleArgs args = new RequestSampleArgs();
                    RequestSample?.Invoke(this, args);
                    message = args.Sample;
                    _pause = 0;
                    break;
                }

                if (req != null) // simulate reading from the port requested message
                {
                    try { Thread.Sleep(INTERVAL); }
                    catch (ThreadInterruptedException) { break; }

                    if (req.Type == PacketType.Ack)
                    {
                        message = new MessageResult(Packet.Result.OK);
                    }
                    /*NEW
                    else if (req.Type == MessageType.Sample)
                    {
                        RequestSampleArgs args = new();
                        RequestSample?.Invoke(this, args);
                        message = args.Sample;
                    }
                    else if (req.Type == MessageType.MFCResult)
                    {
                        RequestMFCResultArgs args = new();
                        RequestMFCResult?.Invoke(this, args);
                        message = args.MFCResult;
                    }*/
                    else
                    {
                        throw new Exception($"PID simulator does not support reading '{req.Type}' messages");
                    }
                }
            }

            return Error.Success;
        }
    }
}
