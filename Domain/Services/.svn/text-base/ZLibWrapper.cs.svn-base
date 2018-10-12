namespace TESVSnip.Domain.Services
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Windows.Forms;
    using Model;

    /// <summary>
    /// ZLibWrapper : Wrapper for ZLib
    /// </summary>
    public static class ZLibWrapper
    {
        private const int MaxBufferSize = 5242880; // 5 Mb = 5242880 / 30 Mb = 31457280 bytes / 50 Mb =52428800 bytes

        private static readonly byte[] Bytes2 = new byte[2];
        private static readonly byte[] Bytes4 = new byte[4];

        private static byte[] _inputBuffer;
        private static byte[] _outputBuffer;

        public static uint InputBufferLength { get; private set; }

        public static uint InputBufferPosition { get; private set; }

        public static uint MaxOutputBufferPosition { get; private set; } // for calculate an optimized buffer size

        public static uint OutputBufferLength { get; private set; }

        public static uint OutputBufferPosition { get; private set; }

        /// <summary>
        /// Allocate the buffers size
        /// </summary>
        public static void AllocateBuffers()
        {
            if (_inputBuffer != null | _outputBuffer != null) ReleaseBuffers();
            _inputBuffer = new byte[MaxBufferSize];
            _outputBuffer = new byte[MaxBufferSize];
            ResetBuffer();
        }

        /// <summary>
        /// Copy a piece of bytes array into input buffer
        /// </summary>
        /// <param name="byteArray">The byte Array.</param>
        /// <param name="offset">Offset in byte array.</param>
        /// <param name="bytesToCopy">Number of bytes to read (copy to buffer).</param>
        public static void CopyByteArrayToInputBuffer(byte[] byteArray, int offset, uint bytesToCopy)
        {
            ResetBufferSizeAndPosition();

            if ((bytesToCopy + offset) > MaxBufferSize)
            {
                string msg =
                    "ZLibWrapper.CopyByteArrayToInputBuffer: " +
                    string.Format(format: TranslateUI.TranslateUiGlobalization.ResManager.GetString("ZLibWrapper_ExceedMaxBufferSize"),
                                  arg0: bytesToCopy.ToString(CultureInfo.InvariantCulture),
                                  arg1: MaxBufferSize.ToString(CultureInfo.InvariantCulture));
                Clipboard.SetText(msg);
                throw new TESParserException(msg);
            }

            Array.Copy(byteArray, offset, _inputBuffer, 0, bytesToCopy);

            InputBufferLength = bytesToCopy;
        }

        /// <summary>
        /// Copy the input buffer to oupput buffer
        /// </summary>
        /// <param name="dataSize">
        /// The data Size.
        /// </param>
        public static void CopyInputBufferToOutputBuffer(uint dataSize)
        {
            string msg;
            try
            {
                if (InputBufferLength == 0)
                {
                    msg = "ZLibWrapper.CopyInputBufferToOutputBuffer: " +
                          TranslateUI.TranslateUiGlobalization.ResManager.GetString(name: "ZLibWrapper_InputBufferIsEmpty");
                    Clipboard.SetText(msg);
                    throw new TESParserException(msg);
                }

                if (dataSize > MaxBufferSize)
                {
                    msg = string.Format(
                        "ZLibWrapper.CopyInputBufferToOutputBuffer: " +
                        TranslateUI.TranslateUiGlobalization.ResManager.GetString(name: "ZLibWrapper_BufferIsTooSmall"),
                        dataSize.ToString(CultureInfo.InvariantCulture),
                        MaxBufferSize.ToString(CultureInfo.InvariantCulture));
                    Clipboard.SetText(msg);
                    throw new TESParserException(msg);
                }

                if (InputBufferLength > 0)
                {
                    Array.Copy(_inputBuffer, _outputBuffer, dataSize);
                    OutputBufferLength = dataSize;
                }
            }
            catch (Exception ex)
            {
                msg = "ZLibWrapper.CopyInputBufferToOutputBuffer" + Environment.NewLine +
                      "Message: " + ex.Message +
                      Environment.NewLine +
                      "StackTrace: " + ex.StackTrace;
                Clipboard.SetText(msg);
                throw new TESParserException(msg);
            }
            finally
            {
                OutputBufferLength = InputBufferLength;
                OutputBufferPosition = 0;
                InputBufferPosition = 0;
            }
        }

        /// <summary>
        /// Copy a piece of output buffer into a bytes array
        /// </summary>
        /// <param name="byteArray">The byte Array.</param>
        public static void CopyOutputBufferIntoByteArray(byte[] byteArray)
        {
            if (OutputBufferLength <= 0)
            {
                string msg = "ZLibWrapper.CopyOutputBufferIntoByteArray: " +
                             TranslateUI.TranslateUiGlobalization.ResManager.GetString(name: "ZLibWrapper_OutputBufferIsEmpty");
                Clipboard.SetText(msg);
                throw new TESParserException(msg);
            }

            Array.Copy(_outputBuffer, 0, byteArray, 0, OutputBufferLength);
        }

        /// <summary>
        /// Copy a piece of stream into input buffer
        /// </summary>
        /// <param name="fs">A file stream</param>
        /// <param name="bytesToRead">Number of bytes to read (copy to buffer).</param>
        public static void CopyStreamToInputBuffer(FileStream fs, uint bytesToRead)
        {
            ResetBufferSizeAndPosition();

            if ((fs.Position + bytesToRead) > fs.Length)
            {
                string msg = "ZLibWrapper.CopyStreamToInputBuffer: " +
                             string.Format(format: TranslateUI.TranslateUiGlobalization.ResManager.GetString(name: "ZLibWrapper_SizeOverStreamSize"),
                                           arg0: (fs.Position + bytesToRead).ToString(CultureInfo.InvariantCulture),
                                           arg1: fs.Length.ToString(CultureInfo.InvariantCulture));
                Clipboard.SetText(msg);
                throw new TESParserException(msg);
            }

            if (bytesToRead > MaxBufferSize)
            {
                string msg = "ZLibWrapper.CopyStreamToInputBuffer: " +
                             string.Format(format: TranslateUI.TranslateUiGlobalization.ResManager.GetString(name: "ZLibWrapper_ExceedMaxBufferSize"),
                                           arg0: bytesToRead.ToString(CultureInfo.InvariantCulture),
                                           arg1: MaxBufferSize.ToString(CultureInfo.InvariantCulture));
                Clipboard.SetText(msg);
                throw new TESParserException(msg);
            }

            var numBytesAddressing = (int)bytesToRead;
            int offset = 0;
            while (numBytesAddressing > 0u)
            {
                var numBytes = (uint)Math.Min(numBytesAddressing, 8192u); // 8192u 65536u
                int bytesRead = fs.Read(_inputBuffer, offset, (int)numBytes);
                offset += bytesRead;
                numBytesAddressing -= bytesRead;
            }

            InputBufferLength = bytesToRead;
        }
        /// <summary>
        /// Set position in input/output buffer
        /// </summary>
        /// <param name="position">New position in buffer</param>
        /// <param name="bufferType">Buffer type (input or output)</param>
        public static void Position(uint position, ZLibBufferType bufferType)
        {
            uint bufferSize = bufferType == ZLibBufferType.OutputBuffer ? OutputBufferLength : InputBufferLength;

            if (position > bufferSize)
            {
                string msg =
                    string.Format(
                        "ZLibWrapper.Position: " +
                        string.Format(format: TranslateUI.TranslateUiGlobalization.ResManager.GetString(name: "ZLibWrapper_PositionOverBufferSize"),
                                      arg0: bufferSize.ToString(CultureInfo.InvariantCulture)));
                Clipboard.SetText(msg);
                throw new TESParserException(msg);
            }

            if (bufferType == ZLibBufferType.OutputBuffer)
                OutputBufferPosition = position;
            else
                InputBufferPosition = position;
        }

        /// <summary>
        /// Read 2 bytes in output buffer from the current position
        /// </summary>
        /// <returns>Contains the specified byte array.</returns>
        public static byte[] Read2Bytes()
        {
            if ((OutputBufferPosition + 2) > OutputBufferLength)
            {
                string msg = "ZLibWrapper.Read2Bytes: " +
                             string.Format(format: TranslateUI.TranslateUiGlobalization.ResManager.GetString(name: "ZLibWrapper_FinalPositionOverBufferSize"),
                                           arg0: (OutputBufferPosition + 2).ToString(CultureInfo.InvariantCulture),
                                           arg1: OutputBufferLength.ToString(CultureInfo.InvariantCulture));
                Clipboard.SetText(msg);
                throw new TESParserException(msg);
            }

            Array.Copy(_outputBuffer, OutputBufferPosition, Bytes2, 0, 2);
            OutputBufferPosition += 2;
            return Bytes2;
        }

        /// <summary>
        /// Read 4 bytes in output buffer from the current position
        /// </summary>
        /// <returns>Contains the specified byte array.</returns>
        public static byte[] Read4Bytes()
        {
            if ((OutputBufferPosition + 4) > OutputBufferLength)
            {
                string msg = "ZLibWrapper.Read4Bytes: " +
                             string.Format(format: TranslateUI.TranslateUiGlobalization.ResManager.GetString(name: "ZLibWrapper_FinalPositionOverBufferSize"),
                                           arg0: (OutputBufferPosition + 4).ToString(CultureInfo.InvariantCulture),
                                           arg1: OutputBufferLength.ToString(CultureInfo.InvariantCulture));
                Clipboard.SetText(msg);
                throw new TESParserException(msg);
            }

            Array.Copy(_outputBuffer, OutputBufferPosition, Bytes4, 0, 4);
            OutputBufferPosition += 4;
            return Bytes4;
        }

        /// <summary>
        /// Read bytes in input/output buffer from the current position
        /// </summary>
        /// <param name="data">The data.</param>
        /// <param name="count">The maximum number of bytes to read.</param>
        /// <param name="bufferType">Read in input or output buffer.</param>
        public static void ReadBytes(ref byte[] data, int count, ZLibBufferType bufferType)
        {
            if (data == null) throw new ArgumentNullException("data");

            uint newPosition;
            uint bufferSize;

            if (bufferType == ZLibBufferType.OutputBuffer)
            {
                newPosition = (uint)(OutputBufferPosition + count);
                bufferSize = OutputBufferLength;
            }
            else
            {
                newPosition = (uint)(InputBufferPosition + count);
                bufferSize = InputBufferLength;
            }

            if (newPosition > bufferSize)
            {
                string msg = "ZLibWrapper.ReadBytes: " +
                             string.Format(format: TranslateUI.TranslateUiGlobalization.ResManager.GetString(name: "ZLibWrapper_FinalPositionOverBufferSize2"),
                                           arg0: newPosition.ToString(CultureInfo.InvariantCulture),
                                           arg1: bufferType.ToString(),
                                           arg2: bufferSize.ToString(CultureInfo.InvariantCulture));
                Clipboard.SetText(msg);
                throw new TESParserException(msg);
            }

            if (bufferType == ZLibBufferType.OutputBuffer)
            {
                Array.Copy(_outputBuffer, OutputBufferPosition, data, 0, count);
                OutputBufferPosition += (uint)count;
            }
            else
            {
                Array.Copy(_inputBuffer, InputBufferPosition, data, 0, count);
                InputBufferPosition += (uint)count;
            }
        }

        /// <summary>
        /// Read bytes in input/output buffer from the current position
        /// </summary>
        /// <param name="count">The maximum number of bytes to read.</param>
        /// <param name="bufferType">Read in input or output buffer.</param>
        /// <returns>Contains the specified byte array.</returns>
        public static byte[] ReadBytes(int count, ZLibBufferType bufferType)
        {
            uint newPosition;
            uint bufferSize;

            if (bufferType == ZLibBufferType.OutputBuffer)
            {
                newPosition = (uint)(OutputBufferPosition + count);
                bufferSize = OutputBufferLength;
            }
            else
            {
                newPosition = (uint)(InputBufferPosition + count);
                bufferSize = InputBufferLength;
            }

            if (newPosition > bufferSize)
            {
                string msg = "ZLibWrapper.ReadBytes: " +
                             string.Format(format: TranslateUI.TranslateUiGlobalization.ResManager.GetString(name: "ZLibWrapper_FinalPositionOverBufferSize2"),
                                           arg0: newPosition.ToString(CultureInfo.InvariantCulture),
                                           arg1: bufferType.ToString(),
                                           arg2: bufferSize.ToString(CultureInfo.InvariantCulture));
                Clipboard.SetText(msg);
                throw new TESParserException(msg);
            }

            var b = new byte[count];

            if (bufferType == ZLibBufferType.OutputBuffer)
            {
                Array.Copy(_outputBuffer, OutputBufferPosition, b, 0, count);
                OutputBufferPosition += (uint)count;
            }
            else
            {
                Array.Copy(_inputBuffer, InputBufferPosition, b, 0, count);
                InputBufferPosition += (uint)count;
            }

            return b;
        }

        /// <summary>
        /// Read UInt16 in output buffer from the current position
        /// </summary>
        /// <returns>An UInt16 number</returns>
        public static ushort ReadUInt16()
        {
            return BitConverter.ToUInt16(Read2Bytes(), 0);
        }

        /// <summary>
        /// Read UInt32 in output buffer from the current position
        /// </summary>
        /// <returns>An UInt32 number</returns>
        public static uint ReadUInt32()
        {
            return BitConverter.ToUInt32(Read4Bytes(), 0);
        }

        /// <summary>
        /// Release the buffers
        /// </summary>
        public static void ReleaseBuffers()
        {
            _inputBuffer = null;
            _outputBuffer = null;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        /// <summary>
        /// Reset input/Output buffer with zero
        /// </summary>
        public static void ResetBuffer()
        {
            ResetBufferSizeAndPosition();
            MaxOutputBufferPosition = 0;
            Array.Clear(_inputBuffer, 0, MaxBufferSize);
            Array.Clear(_outputBuffer, 0, MaxBufferSize);
        }

        /// <summary>
        /// Reset size and position of input and output buffer
        /// </summary>
        public static void ResetBufferSizeAndPosition()
        {
            InputBufferLength = 0;
            OutputBufferLength = 0;
            InputBufferPosition = 0;
            OutputBufferPosition = 0;
        }

        /// <summary>
        /// Write in the output buffer the ZLib inflate bytes byte[] data, int startIndex, int count
        /// </summary>
        /// <param name="data"> </param>
        /// <param name="startIndex"> </param>
        /// <param name="count"> </param>
        public static void WriteInOutputBuffer(byte[] data, int startIndex, int count)
        {
            if (startIndex < 0)
            {
                string msg = "ZLibWrapper.WriteInOutputBuffer: " +
                             TranslateUI.TranslateUiGlobalization.ResManager.GetString(name: "ZLibWrapper_PositionIsNegative");
                Clipboard.SetText(msg);
                throw new TESParserException(msg);
            }

            if (count < 0)
            {
                string msg = "ZLibWrapper.WriteInOutputBuffer: " +
                             TranslateUI.TranslateUiGlobalization.ResManager.GetString(name: "ZLibWrapper_BytesToWriteIsNegative");
                Clipboard.SetText(msg);
                throw new TESParserException(msg);
            }

            if ((startIndex + count) > data.Length)
            {
                string msg =
                    "ZLibWrapper.WriteInOutputBuffer: " +
                    string.Format(format: TranslateUI.TranslateUiGlobalization.ResManager.GetString(name: "ZLibWrapper_RequestedReadByteError"),
                                  arg0: startIndex.ToString(CultureInfo.InvariantCulture),
                                  arg1: count.ToString(CultureInfo.InvariantCulture),
                                  arg2: (data.Length - startIndex).ToString(CultureInfo.InvariantCulture));
                Clipboard.SetText(msg);
                throw new TESParserException(msg);
            }

            if ((OutputBufferPosition + count) > MaxBufferSize)
            {
                string msg =
                    string.Format(format: TranslateUI.TranslateUiGlobalization.ResManager.GetString(name: "ZLibWrapper_OutputBufferIsTooSmall"),
                                  arg0: MaxBufferSize.ToString(CultureInfo.InvariantCulture),
                                  arg1: (OutputBufferPosition + count).ToString(CultureInfo.InvariantCulture));
                Clipboard.SetText(msg);
                throw new TESParserException(msg);
            }

            Array.Copy(data, startIndex, _outputBuffer, OutputBufferPosition, count);
            OutputBufferPosition += (uint)count;
            OutputBufferLength = OutputBufferPosition;
            if (OutputBufferPosition > MaxOutputBufferPosition) MaxOutputBufferPosition = OutputBufferPosition;
        }
    }
}