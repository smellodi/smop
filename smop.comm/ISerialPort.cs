namespace SMOP.Comm
{
    public interface ISerialPort
    {
        bool IsOpen { get; }

        void Open();
        void Write(byte[] buffer, int offset, int count);
        int Read(byte[] buffer, int offset, int count);
        void Close();
    }
}
