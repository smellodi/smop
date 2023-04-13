namespace Smop.OdorDisplay.Packets;

/// <summary>
/// Request result present in <see cref="Type.Ack"/> packet
/// </summary>
public enum Result : byte
{
    /// <summary>
    /// No error, the request was executed without problems
    /// </summary>
    OK = 0,
    /// <summary>
    /// Request parameter has an invalid value
    /// </summary>
    InvalidValue = 0xF6,
    /// <summary>
    /// Resource is not available
    /// </summary>
    NotAvailable = 0xF5,
    /// <summary>
    /// The device has no enough memory for the operation
    /// </summary>
    OutOfMemory = 0xF4,
    /// <summary>
    /// Requested operation is not possible in the current operating mode
    /// </summary>
    InvalidMode = 0xF3,
    /// <summary>
    /// Operation timed out
    /// </summary>
    Timeout = 0xF2,
    /// <summary>
    /// Data is not available
    /// </summary>
    NoData = 0xF1,
    /// <summary>
    /// Unknown or unsupported packet type
    /// </summary>
    UnknownPacket = 0xF0,
    /// <summary>
    /// Invalid amount of data
    /// </summary>
    InvalidLength = 0xEF,
    /// <summary>
    /// Invalid index or device number 
    /// </summary>
    InvalidDeviceID = 0xEE,
    /// <summary>
    /// Device is busy and can’t perform the requested operation
    /// </summary>
    Busy = 0xED,
    /// <summary>
    /// Non-specific error
    /// </summary>
    Error = 0x80,
}

public class Ack : Response
{
    public Result Result { get; }
    public static Ack? From(Response response)
    {
        if (response?.Type != Type.Ack || response?.Payload.Length != 1)
        {
            return null;
        }

        return new Ack(response.Payload);
    }
    public Ack(byte[] payload) : base(Type.Ack, payload) { Result = (Result)payload[0]; }
    public override string ToString() => $"{_type} [{Result}]";

    // Internal

    internal Ack(Result result) : base(Type.Ack, new byte[] { (byte)result }) { Result = result; }
}
