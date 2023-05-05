using Smop.OdorDisplay.Packets;
using System.Threading;

namespace Smop.OdorDisplay;

/// <summary>
/// The request used internally in <see cref="CommPort"/> to match responses with the corresponding requests
/// </summary>
internal class TimedRequest
{
    public static int WaitInterval => 500;

    public Type Type { get; }

    /// <summary>
    /// Stores the response packet received from the port
    /// </summary>
    public Response? Response { get; private set; } = null;

    public long Duration => (System.Diagnostics.Stopwatch.GetTimestamp() - _timestamp) / 10000;
    public bool IsValid => Duration < WaitInterval;

    public TimedRequest(Type type)
    {
        Type = type;
    }

    /// <summary>
    /// Waits until <see cref="SetResponse(Response)"/> is called.
    /// </summary>
    /// <returns>False if the timeout reached</returns>
    public bool WaitUntilReceived()
    {
        bool result = _mutex.WaitOne(WaitInterval);
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

    readonly AutoResetEvent _mutex = new(false);
    readonly long _timestamp = System.Diagnostics.Stopwatch.GetTimestamp();
}
