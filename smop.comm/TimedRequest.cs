using SMOP.Comm.Packets;
using System.Threading;

namespace SMOP.Comm
{
    public class TimedRequest
    {
        public Type Type { get; }
        public Response? Response { get; private set; } = null;

        public long Duration { get; private set; } = 0;
        public bool IsValid => Duration < WAIT_INTERVAL;

        public TimedRequest(Type type)
        {
            Type = type;
        }

        /// <summary>
        /// Locks the method call and waits until <see cref="SetResponse(Response)"/> is called
        /// </summary>
        /// <returns>False if the timeout reached</returns>
        public bool WaitUntilReceived()
        {
            _timestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            bool result = _mutex.WaitOne(WAIT_INTERVAL);
            Duration = (System.Diagnostics.Stopwatch.GetTimestamp() - _timestamp) / 10000;
            return result;
        }

        /// <summary>
        /// Sets the response
        /// </summary>
        /// <param name="res">Response</param>
        public void SetResponse(Response res)
        {
            Response = res;
            _mutex.Set();
        }

        // Internal

        const int WAIT_INTERVAL = 1000;

        readonly AutoResetEvent _mutex = new(false);
        long _timestamp = 0;
    }
}
