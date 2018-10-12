using System.Diagnostics;

namespace TESVSnip.Domain.Services
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Windows.Forms;
    using Model;

    /// <summary>
    /// SnipStreamWrapper
    /// </summary>
    public class SnipStreamWrapper
    {
        private const int MaxBufferSize = 52428800; // 5 Mb = 5242880 / 30 Mb = 31457280 bytes / 50 Mb =52428800 bytes

        public byte[] OutputBuffer;

        private readonly long _streamSize;
        private byte[] _bytes2 = new byte[2];

        private byte[] _bytes4 = new byte[4];

        /// <summary>
        /// Initializes a new instance of the <see cref="SnipStreamWrapper"/> class.
        /// </summary>
        /// <param name="fs">FileStream</param>
        public SnipStreamWrapper(FileStream fs)
        {
            SnipStream = fs;
            _streamSize = SnipStream.Length;
            SnipStream.Seek(0, SeekOrigin.Begin);
        }

        public uint MaxOutputBufferPosition { get; private set; } // for calculate an optimized buffer size

        public uint OutputBufferLength { get; private set; }

        public uint OutputBufferPosition { get; private set; }

        /// <summary>
        /// Gets the stream of FileStream
        /// </summary>
        public FileStream SnipStream { get; private set; }

        /// <summary>
        /// Gets the current position in stream
        /// </summary>
        protected long CurrentStreamPosition
        {
            get
            {
                if (SnipStream != null)
                    return SnipStream.Position;
                return -1;
            }
        }

        /// <summary>
        /// Allocate the buffers size
        /// </summary>
        public void AllocateBuffers()
        {
            if (OutputBuffer != null) ReleaseBuffers();
            OutputBuffer = new byte[MaxBufferSize];
            ResetBuffer();
        }

        /// <summary>
        /// Close the file stream
        /// </summary>
        public void CloseAndDisposeFileStream()
        {
            if (SnipStream != null)
            {
                SnipStream.Close();
                SnipStream.Dispose();
            }

            SnipStream = null;
        }

        /// <summary>
        /// Copy the input buffer to output buffer
        /// </summary>
        /// <param name="b">The data Size.</param>
        public void CopyOutputBufferToData(ref byte[] b)
        {
            try
            {
                if (OutputBufferLength > 0)
                {
                    b = new byte[OutputBufferLength];
                    Array.Copy(OutputBuffer, 0, b, 0, OutputBufferLength);
                }
            }
            catch (Exception ex)
            {
                string msg = "SnipStreamWrapper.CopyOutputBufferToData" +
                             Environment.NewLine +
                             "Message: " + ex.Message +
                             Environment.NewLine +
                             "StackTrace: " + ex.StackTrace;
                Clipboard.SetText(msg);
                throw new TESParserException(msg);
            }
        }

        /// <summary>
        /// Gets the End Of File
        /// </summary>
        /// <returns>True if end of file</returns>
        public bool Eof()
        {
            if (SnipStream != null)
                return SnipStream.Position == SnipStream.Length;
            return true; // force end of file
        }

        /// <summary>
        /// Jump to position in stream from offset
        /// </summary>
        /// <param name="offset">Offset (can be negative or positive)</param>
        /// <param name="from">Reference points in stream</param>
        /// <exception cref="Exception">An exception if size or position error</exception>
        public void JumpTo(int offset, SeekOrigin from)
        {
            if (SnipStream != null)
            {
                long newPosition = SnipStream.Position;

                if (@from == SeekOrigin.Begin) newPosition = offset;
                if (@from == SeekOrigin.Current) newPosition += offset;
                if (@from == SeekOrigin.End) newPosition += offset;

                if (newPosition > _streamSize)
                    throw new Exception(
                        string.Format(
                            format: TranslateUI.TranslateUiGlobalization.ResManager.GetString(name: "SnipStreamWrapper_JumpTo_GreaterSize"),
                            arg0: newPosition.ToString(CultureInfo.InvariantCulture),
                            arg1: _streamSize.ToString(CultureInfo.InvariantCulture)));


                if (newPosition < 0)
                    throw new Exception(
                        string.Format(
                            format: TranslateUI.TranslateUiGlobalization.ResManager.GetString(name: "SnipStreamWrapper_JumpTo_PositionNegative"),
                            arg0: newPosition.ToString(CultureInfo.InvariantCulture)));

                SnipStream.Seek(offset, from);
            }
        }

        /// <summary>
        /// Read bytes in file stream from the current position
        /// </summary>
        /// <param name="count">The maximum number of bytes to read.</param>
        /// <returns>Contains the specified byte array.</returns>
        public byte[] ReadBytes(int count)
        {
            var b = new byte[count];
            SnipStream.Read(b, 0, count);
            return b;
        }

        /// <summary>
        /// Read UInt16 in file stream from the current position
        /// </summary>
        /// <returns>An UInt16 number</returns>
        public UInt16 ReadUInt16()
        {
            SnipStream.Read(_bytes2, 0, 2);
            return BitConverter.ToUInt16(_bytes2, 0);
        }

        /// <summary>
        /// Read UInt32 in file stream from the current position
        /// </summary>
        /// <returns>An UInt32 number</returns>
        public UInt32 ReadUInt32()
        {
            SnipStream.Read(_bytes4, 0, 4);
            return BitConverter.ToUInt32(_bytes4, 0);
        }

        /// <summary>
        /// Release the buffers
        /// </summary>
        public void ReleaseBuffers()
        {
            OutputBuffer = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        /// <summary>
        /// Reset output buffer
        /// </summary>
        public void ResetBuffer()
        {
            ResetBufferSizeAndPosition();
            MaxOutputBufferPosition = 0;
            Array.Clear(OutputBuffer, 0, MaxBufferSize);
        }

        /// <summary>
        /// Reset size and position of output buffer
        /// </summary>
        public void ResetBufferSizeAndPosition()
        {
            OutputBufferLength = 0;
            OutputBufferPosition = 0;
        }

        /// <summary>
        /// Writes the output buffer in the file stream
        /// </summary>
        public void WriteOutputBufferInFileStream()
        {
            if (SnipStream == null) return;
            if (OutputBufferLength <= 0) return;
            SnipStream.Write(OutputBuffer, 0, (int) OutputBufferLength);
        }

        /// <summary>
        /// Writes an array of bytes in the file stream
        /// </summary>
        /// <param name="b">A byte array.</param>
        public void WriteBytesArrayInFileStream(byte[] b)
        {
            if (SnipStream == null) return;
            SnipStream.Write(b, 0, b.Length);
        }

        /// <summary>
        /// Write byte[] in output buffer
        /// </summary>
        /// <param name="b">An array of bytes.</param>
        /// <param name="sourceIndex">starting index</param>
        public void WriteByteToBuffer(ref byte[] b, long sourceIndex)
        {
            Array.Copy(b, sourceIndex, OutputBuffer, OutputBufferPosition, b.Length);
            OutputBufferPosition += (uint) b.Length;
            OutputBufferLength += (uint) b.Length;
        }

        /// <summary>
        /// Write Int in output buffer
        /// </summary>
        /// <param name="value">A Int value.</param>
        public void WriteIntToBuffer(int value)
        {
            _bytes4 = Framework.TypeConverter.si2h(value);
            Array.Copy(_bytes4, 0, OutputBuffer, OutputBufferPosition, 4);
            OutputBufferPosition += 4;
            OutputBufferLength += 4;
        }

        /// <summary>
        /// Write ushort into buffer
        /// </summary>
        /// <param name="value">An ushort value.</param>
        public void WriteIntToBuffer(ushort value)
        {
            _bytes4 = Framework.TypeConverter.si2h(value);
            Array.Copy(_bytes4, 0, OutputBuffer, OutputBufferPosition, 4);
            OutputBufferPosition += 4;
            OutputBufferLength += 4;
        }

        /// <summary>
        /// Writes a string in the file stream
        /// </summary>
        /// <param name="s">String to write</param>
        public void WriteStringInFileStream(string s)
        {
            if (SnipStream == null) return;
            if (string.IsNullOrWhiteSpace(s)) return;
            var b = new byte[s.Length];
            for (int i = 0; i < s.Length; i++)
                b[i] = (byte) s[i];

            SnipStream.Write(b, 0, s.Length);
        }

        /// <summary>
        /// Write a string in buffer
        /// </summary>
        /// <param name="s">String to write</param>
        public void WriteStringToBuffer(string s)
        {
            foreach (var t in s)
            {
                OutputBuffer[OutputBufferPosition] = (byte) t;
                OutputBufferPosition++;
                OutputBufferLength++;
            }
        }

        /// <summary>
        /// Write UInt16 in buffer
        /// </summary>
        /// <param name="value">A UInt16 value.</param>
        public void WriteUInt16ToBuffer(UInt16 value)
        {
            _bytes2 = Framework.TypeConverter.s2h(value);
            Array.Copy(_bytes2, 0, OutputBuffer, OutputBufferPosition, 2);
            OutputBufferPosition += 2;
            OutputBufferLength += 2;
        }

        /// <summary>
        /// Write UInt32 in file stream
        /// </summary>
        /// <param name="value">A UInt32 value.</param>
        public void WriteUInt32(UInt32 value)
        {
            if (SnipStream != null)
            {
                _bytes4 = Framework.TypeConverter.i2h(value);
                SnipStream.Write(_bytes4, 0, _bytes4.Length);
            }
        }

        /// <summary>
        /// Write UInt32 in buffer
        /// </summary>
        /// <param name="value">A UInt32 value.</param>
        public void WriteUInt32ToBuffer(UInt32 value)
        {
            _bytes4 = Framework.TypeConverter.i2h(value);
            Array.Copy(_bytes4, 0, OutputBuffer, OutputBufferPosition, 4);
            OutputBufferPosition += 4;
            OutputBufferLength += 4;
        }
    }
}