namespace Smop.OdorDisplay;

/// <summary>
/// A subset of Windows ERROR_XXX codes that are used to return from <see cref="CommPort"/> methods
/// </summary>
public enum Error
{
    /// <summary>
    /// ERROR_SUCCESS, no errors
    /// </summary>
    Success = 0,

    /// <summary>
    /// ERROR_INVALID_FUNCTION, <see cref="Packets.Type.Ack"/> packet returned as error
    /// </summary>
    DeviceError = 0x01,

    /// <summary>
    /// ERROR_BAD_FORMAT
    /// </summary>
    //BadDataFormat = 0x0B,
    /// <summary>
    /// ERROR_INVALID_ACCESS
    /// </summary>
    //WrongDevice = 0x0C,

    /// <summary>
    /// ERROR_INVALID_DATA, something is wrong with the received data: it is not following the expected pattern
    /// </summary>
    InvalidData = 0x0D,

    /// <summary>
    /// ERROR_NOT_READY, the port is not open yet/already
    /// </summary>
    NotReady = 0x15,

    /// <summary>
    /// ERROR_CRC, the received and the calculated CRCs do not match
    /// </summary>
    CRC = 0x17,

    /// <summary>
    /// ERROR_GEN_FAILURE, reading the port resulted in an exception
    /// </summary>
    AccessFailed = 0x1F,

    /// <summary>
    /// ERROR_OPEN_FAILED, not succeeded to open a port
    /// </summary>
    /// 
    OpenFailed = 0x6E,

    /// <summary>
    /// WAIT_TIMEOUT, either the request packet was not sent or its response not received within <see cref="TimedRequest.WaitInterval"/> ms
    /// </summary>
    Timeout = 0x102,
}

/// <summary>
/// (error, reason) pair used to return from <see cref="CommPort"/> public methods
/// </summary>
public class Result
{
    public Error Error { get; init; }
    public string? Reason { get; init; }

    public override string ToString() => $"{Error} ({Reason})";
}
