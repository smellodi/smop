using System.Diagnostics;
using System.Runtime.InteropServices;

namespace tests
{
    internal static class Program
    {
        public enum Error { Success, AccessFailed, Timeout, CRC }
        public class Message
        {
            public bool IsValidCRC => CalcChecksum() == _data[_data.Length - 1];
            public Message(byte[] data)
            {
                _data = data;
            }

            readonly byte[] _data;

            byte CalcChecksum()
            {
                long result = 1;
                for (int i = 0; i < _data.Length - 1; i++)
                {
                    result += _data[i];
                }

                return (byte)~result;
            }
        }

        const int MAX_BUFFER_LENGTH = 990;
        const byte PREAMBLE_BYTE = 0xCC;
        const int PREAMBLE_LENGTH = 3;
        const int PAYLOAD_LENGTH_OFFSET = 6;
        const int MIN_PACKAGE_LENGTH = 9; // the packet minimum length: PREAMBLE_LENGTH + 1B type + 1B from + 1B to + 2B payload length + 2B CRC

        static Error? PortError = null;

        private static Error Read(out Message? message, DataPackage package)
        {
            byte[] buffer = new byte[MAX_BUFFER_LENGTH];

            int bytesRemaining = MIN_PACKAGE_LENGTH;
            int packageStartOffset = 0;
            int bufferOffset = 0;
            bool isLengthKnown = false;
            int payloadLength = 0;

            // Try to receive bytes; wait more data in POLL_PERIOD pieces;
            while (bytesRemaining > 0)
            {
                int readCount;
                try
                {
                    readCount = package.GetNextBytes(buffer, bufferOffset, bytesRemaining);
                    if (readCount == 0)
                        break;
                }
                catch
                {
                    message = null;
                    return Error.AccessFailed;
                }

                if (PortError != null)           // return immediately (with error) if port read fails.
                {
                    message = null;
                    return (Error)Marshal.GetLastWin32Error();
                }

                int packageBytesRead = bufferOffset + readCount - packageStartOffset;
                if (!isLengthKnown && packageBytesRead >= MIN_PACKAGE_LENGTH - 1)  // After we have all bytes received from the port except payload and CRC , we try to 
                {
                    // ensure this is a valid start of the package
                    while (packageStartOffset < bufferOffset + readCount - PREAMBLE_LENGTH)
                    {
                        if (buffer[packageStartOffset] == PREAMBLE_BYTE &&
                            buffer[packageStartOffset + 1] == PREAMBLE_BYTE &&
                            buffer[packageStartOffset + 2] == PREAMBLE_BYTE)
                        {
                            break;
                        }

                        bytesRemaining++;
                        packageStartOffset++;
                    }

                    packageBytesRead = bufferOffset + readCount - packageStartOffset;
                    if (packageBytesRead >= MIN_PACKAGE_LENGTH - 1)
                    {
                        // payload is little-endian WORD
                        payloadLength =                             // get the length of the payload, so we known 
                            buffer[packageStartOffset + PAYLOAD_LENGTH_OFFSET] +
                            (buffer[packageStartOffset + PAYLOAD_LENGTH_OFFSET + 1] << 8);
                        bytesRemaining += payloadLength;                   // how many bytes (including checksum) we need to read more
                        isLengthKnown = true;
                    }
                }

                bytesRemaining -= readCount;
                bufferOffset += readCount;
            }

            if (bytesRemaining > 0)
            {
                message = null;
                return Error.Timeout;
            }

            packageStartOffset += 3;
            int packageEndOffset = packageStartOffset + 6 + payloadLength;
            message = new Message(buffer[packageStartOffset..packageEndOffset]);

            if (!message.IsValidCRC)
            {
                return Error.CRC;
            }
            else
            {
                // message = ...
                return Error.Success;
            }
        }

        class DataPackage
        {
            public DataPackage(byte[][] chunks)
            {
                _chunks = chunks;
            }
            public int GetNextBytes(byte[] buffer, int offset, int count)
            {
                var bytes = Next();
                if (bytes == null)
                {
                    return 0;
                }

                var bytesToReadCount = Math.Min(count, bytes.Length);
                for (int i = 0; i < bytesToReadCount; i++)
                {
                    buffer[offset + i] = bytes[i];
                }

                if (count < bytes.Length)
                {
                    _chunkEnd = bytes[count..];
                }
                else
                {
                    _chunkEnd = null;
                }

                return bytesToReadCount;
            }

            byte[]? Next()
            {
                return _chunkEnd != null ? _chunkEnd : (index < _chunks.Length ? _chunks[index++] : null);
            }

            readonly byte[][] _chunks;
            int index = 0;
            byte[]? _chunkEnd = null;
        }

        static void Main(string[] args)
        {
            var packages = new DataPackage[] {
                new DataPackage(new byte[][]
                {
                    new byte[] {0xCC, 0xCC, 0xCC, 0xFA, 0xF0, 0xF1, 0x01, 0x00, 0x01, 0x21}
                }),
                new DataPackage(new byte[][]
                {
                    new byte[] {0xCC, 0xCC},
                    new byte[] {0xCC, 0xFA, 0xF0, 0xF1},
                    new byte[] {0x01, 0x00, 0x01},
                    new byte[] {0x21}
                }),
                new DataPackage(new byte[][]
                {
                    new byte[] {0xFF, 0xFF, 0xCC, 0xCC, 0xCC, 0xFA, 0xF0, 0xF1, 0x01, 0x00, 0x01, 0x21, 0xFF, 0xFF, 0xFF, 0xFF}
                }),
                new DataPackage(new byte[][]
                {
                    new byte[] {0xFF, 0xFF, 0xCC},
                    new byte[] {0xCC, 0xCC},
                    new byte[] {0xFA, 0xF0, 0xF1},
                    new byte[] {0x01, 0x00, 0x01},
                    new byte[] {0x21, 0xFF, 0xFF, 0xFF, 0xFF}
                }),
                new DataPackage(new byte[][]
                {
                    new byte[] {0xFF, 0xFF, 0xCC, 0xCC, 0xFF, 0xFA, 0xF0, 0xF1, 0x01, 0x00, 0x01, 0x21, 0xFF, 0xFF, 0xFF, 0xFF}
                })
            };

            int i = 1;
            foreach (var p in packages)
                Debug.WriteLine($"{i++}: {Read(out _, p)}");
        }
    }
}