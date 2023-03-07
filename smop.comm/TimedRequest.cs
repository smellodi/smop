using SMOP.Comm.Packets;
using System.Threading;

namespace SMOP.Comm
{
    /// <summary>
    /// The request used internally in <see cref="CommPort"/> to matcg responses with the corresponding requests
    /// </summary>
    internal class TimedRequest
    {
        public Type Type { get; }
        /// <summary>
        /// Stores the response recevied from the port
        /// </summary>
        public Response? Response { get; private set; } = null;

        public long Duration => (System.Diagnostics.Stopwatch.GetTimestamp() - _timestamp) / 10000;
        public bool IsValid => Duration < WAIT_INTERVAL;

        public TimedRequest(Type type)
        {
            Type = type;
        }

        /// <summary>
        /// Locks the method call and waits until <see cref="SetResponse(Response)"/> is called.
        /// </summary>
        /// <returns>False if the timeout reached</returns>
        public bool WaitUntilReceived()
        {
            bool result = _mutex.WaitOne(WAIT_INTERVAL);
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

        const int WAIT_INTERVAL = 500;

        readonly AutoResetEvent _mutex = new(false);
        readonly long _timestamp = System.Diagnostics.Stopwatch.GetTimestamp();
    }
}
