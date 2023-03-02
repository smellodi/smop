namespace SMOP.Comm
{
    /// <summary>
    /// A subset of Windows ERROR_XXX codes that are used to return from methods,
    /// except the last value
    /// </summary>
    public enum Error
    {
        /// <summary>
        /// ERROR_SUCCESS, No errors
        /// </summary>
        Success = 0,
        /// <summary>
        /// ERROR_INVALID_FUNCTION, Ack packet returned as error
        /// </summary>
        DeviceError = 0x01,
        /// <summary>
        /// ERROR_BAD_FORMAT
        /// </summary>
        BadDataFormat = 0x0B,
        /// <summary>
        /// ERROR_INVALID_ACCESS
        /// </summary>
        WrongDevice = 0x0C,
        /// <summary>
        /// ERROR_INVALID_DATA
        /// </summary>
        InvalidData = 0x0D,
        /// <summary>
        /// ERROR_NOT_READY, the port is not open yer/already
        /// </summary>
        NotReady = 0x15,
        /// <summary>
        /// ERROR_CRC
        /// </summary>
        CRC = 0x17,
        /// <summary>
        /// ERROR_GEN_FAILURE, ports were available, but access was not successful
        /// </summary>
        AccessFailed = 0x1F,
        /// <summary>
        /// ERROR_OPEN_FAILED, not succeeded to open a port
        /// </summary>
        OpenFailed = 0x6E,
        /// <summary>
        /// WAIT_TIMEOUT, the request packet was not sent in 
        /// </summary>
        Timeout = 0x102,
    }

    /// <summary>
    /// (error, reason) pair is used to return from <see cref="CommPort"/> public methods
    /// </summary>
    public class Result
    {
        public Error Error { get; init;  }
        public string? Reason { get; init; }

        public override string ToString()
        {
            return $"{Error} ({Reason})";
        }
    }
}
