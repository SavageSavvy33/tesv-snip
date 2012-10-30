﻿namespace TESVSnip.Domain.Services
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
                    msg = "ZLibStreamWrapper.CopyInputBufferToOutputBuffer: Static input buffer is empty.";
                    Clipboard.SetText(msg);
                    throw new TESParserException(msg);
                }

                if (dataSize > MaxBufferSize)
                {
                    msg = string.Format(
                        "ZLibStreamWrapper.CopyInputBufferToOutputBuffer: Static buffer is too small. DataSize={0}  /  Buffer={1}",
                        dataSize.ToString(CultureInfo.InvariantCulture),
                        MaxBufferSize.ToString(CultureInfo.InvariantCulture));
                    Clipboard.SetText(msg);
                    throw new TESParserException(msg);
                }

                if (InputBufferLength > 0)
                    Array.Copy(_inputBuffer, _outputBuffer, dataSize);
            }
            catch (Exception ex)
            {
                msg = "ZLibStreamWrapper.CopyInputBufferToOutputBuffer" + Environment.NewLine +
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
        /// Copy a piece of stream into input buffer
        /// </summary>
        /// <param name="fs">A file stream</param>
        /// <param name="bytesToRead">Number of bytes to read (copy to buffer).</param>
        public static void CopyStreamToInputBuffer(FileStream fs, uint bytesToRead)
        {
            ResetBufferSizeAndPosition();

            if ((fs.Position + bytesToRead) > fs.Length)
            {
                string msg = "ZLibStreamWrapper.CopyStreamToInputBuffer: ZLib wrapper error. Copy size " +
                             (fs.Position + bytesToRead).ToString(CultureInfo.InvariantCulture) +
                             " is over stream length " +
                             fs.Length.ToString(CultureInfo.InvariantCulture);
                Clipboard.SetText(msg);
                throw new TESParserException(msg);
            }

            if (bytesToRead > MaxBufferSize)
            {
                string msg = "ZLibStreamWrapper.CopyStreamToInputBuffer: ZLib wrapper error. Bytes to read (" +
                             bytesToRead.ToString(CultureInfo.InvariantCulture) + ")" +
                             " exceed buffer size ( " + MaxBufferSize.ToString(CultureInfo.InvariantCulture) + ")";
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
                string msg = "ZLibStreamWrapper.CopyByteArrayToInputBuffer: ZLib wrapper error. Bytes to read (" +
                             bytesToCopy.ToString(CultureInfo.InvariantCulture) + ")" +
                             " exceed max buffer size ( " + MaxBufferSize.ToString(CultureInfo.InvariantCulture) + ")";
                Clipboard.SetText(msg);
                throw new TESParserException(msg);
            }

            Array.Copy(byteArray, offset, _inputBuffer, 0, bytesToCopy);

            InputBufferLength = bytesToCopy;
        }

        /// <summary>
        /// Copy a piece of output buffer into a bytes array
        /// </summary>
        /// <param name="byteArray">The byte Array.</param>
        public static void CopyOutputBufferIntoByteArray(byte[] byteArray)
        {
            if (OutputBufferLength <= 0)
            {
                string msg = "ZLibStreamWrapper.CopyOutputBufferIntoByteArray: ZLib wrapper error. Output buffer is empty.";
                Clipboard.SetText(msg);
                throw new TESParserException(msg);
            }

            Array.Copy(_outputBuffer, 0, byteArray, 0, OutputBufferLength);
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
                        "ZLibStreamWrapper.SetPositionInInputBuffer: The position cannot be greater than buffer size ({0}).",
                        bufferSize.ToString(CultureInfo.InvariantCulture));
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
                string msg = string.Format(
                    "ZLibStreamWrapper.Read2Bytes: ZLib inflate error. The final position ({0}) in output buffer is over the buffer size {1}",
                    (OutputBufferPosition + 2).ToString(CultureInfo.InvariantCulture),
                    OutputBufferLength.ToString(CultureInfo.InvariantCulture));
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
                string msg = string.Format(
                    "ZLibStreamWrapper.Read4Bytes: ZLib inflate error. The final position ({0}) in output buffer is over the buffer size {1}",
                    (OutputBufferPosition + 4).ToString(CultureInfo.InvariantCulture),
                    OutputBufferLength.ToString(CultureInfo.InvariantCulture));
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
            if (data == null)
            {
                throw new ArgumentNullException("data");
            }

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
                string msg = string.Format(
                    "ZLibStreamWrapper.ReadBytes: ZLib inflate error. The final position ({0}) in {1} buffer is over the buffer size {2}",
                    newPosition.ToString(CultureInfo.InvariantCulture),
                    bufferType.ToString(),
                    bufferSize.ToString(CultureInfo.InvariantCulture));
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
                string msg = string.Format(
                    "ZLibStreamWrapper.ReadBytes: ZLib inflate error. The final position ({0}) in {1} buffer is over the buffer size {2}",
                    newPosition.ToString(CultureInfo.InvariantCulture),
                    bufferType.ToString(),
                    bufferSize.ToString(CultureInfo.InvariantCulture));
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
                const string msg = "ZLibStreamWrapper.WriteInOutputBuffer: The position cannot be negative.";
                Clipboard.SetText(msg);
                throw new TESParserException(msg);
            }

            if (count < 0)
            {
                const string msg =
                    "ZLibStreamWrapper.WriteInOutputBuffer: The number of bytes to write cannot be negative.";
                Clipboard.SetText(msg);
                throw new TESParserException(msg);
            }

            if ((startIndex + count) > data.Length)
            {
                string msg =
                    string.Format(
                        "ZLibStreamWrapper.WriteInOutputBuffer: The starting position is {0}. A reading of {1} bytes is requested. The number of bytes that can be read in the buffer is {2}",
                        startIndex.ToString(CultureInfo.InvariantCulture),
                        count.ToString(CultureInfo.InvariantCulture),
                        (data.Length - startIndex).ToString(CultureInfo.InvariantCulture));

                Clipboard.SetText(msg);
                throw new TESParserException(msg);
            }

            if ((OutputBufferPosition + count) > MaxBufferSize)
            {
                string msg =
                    string.Format(
                        "ZLibStreamWrapper.WriteInOutputBuffer: The output buffer is to small. Buffer size={0}, Number of bytes to write={1}",
                        MaxBufferSize.ToString(CultureInfo.InvariantCulture),
                        (OutputBufferPosition + count).ToString(CultureInfo.InvariantCulture));
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