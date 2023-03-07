using System.IO.Ports;
using System.Threading;

namespace SMOP.Comm
{
    /// <summary>
    /// This class only translates all method calls to an instance of <see cref="SerialPort"/>
    /// This class is needed only because using <see cref="ISerialPort"/> interface allows 
    /// testing the <see cref="SMOP.Comm"/> module without opening a real serial port
    /// (we use <see cref="SerialPortEmulator"/> for this purpose).
    /// </summary>
    public class SerialPortCOM : ISerialPort
    {
        public bool IsOpen => _port.IsOpen;

        public event SerialErrorReceivedEventHandler? ErrorReceived;

        public SerialPortCOM(string name)
        {
            _port = new SerialPort(name)
            {
                StopBits = PORT_STOP_BITS,
                Parity = PORT_PARITY,
                BaudRate = PORT_SPEED,
                DataBits = PORT_DATA_BITS,
                DtrEnable = true,
                RtsEnable = true,
                DiscardNull = false,
                WriteTimeout = PORT_WRITE_TIMEOUT,
                NewLine = "\r"
            };
            _port.ErrorReceived += ErrorReceived;
        }

        public void Open()
        {
            _port.Open();

            Thread.Sleep(150);  // according to the documentation

            try { _port.BaseStream.Flush(); }
            catch { }  // do nothing
        }

        public void Close() => _port.Close();

        public int Read(byte[] buffer, int offset, int count) => _port.Read(buffer, offset, count);

        public void Write(byte[] buffer, int offset, int count) => _port.Write(buffer, offset, count);

        // Internal

        const int PORT_SPEED = 230400;
        const Parity PORT_PARITY = Parity.None;
        const StopBits PORT_STOP_BITS = StopBits.One;
        const int PORT_DATA_BITS = 8;
        const int PORT_WRITE_TIMEOUT = 300;     // only for writing.. reading should be able to hand until it returns with some data 

        readonly SerialPort _port;
    }
}
