using System;
using System.Buffers;
using System.Buffers.Text;

namespace Ibasa.Ripple
{
    /// <summary>
    /// Converts between binary data and UTF-8 encoded text that is represented in base
    /// 16
    /// </summary>
    public static class Base16
    {
        //
        // Summary:
        //     Decodes the span of UTF-8 encoded text represented as base 64 into binary data.
        //     If the input is not a multiple of 4, it will decode as much as it can, to the
        //     closest multiple of 4.
        //
        // Parameters:
        //   utf8:
        //     The input span that contains UTF-8 encoded text in base 64 that needs to be decoded.
        //
        //   bytes:
        //     The output span that contains the result of the operation, that is, the decoded
        //     binary data.
        //
        //   bytesConsumed:
        //     The number of input bytes consumed during the operation. This can be used to
        //     slice the input for subsequent calls, if necessary.
        //
        //   bytesWritten:
        //     The number of bytes written into the output span. This can be used to slice the
        //     output for subsequent calls, if necessary.
        //
        //   isFinalBlock:
        //     true (default) if the input span contains the entire data to decode. false if
        //     the input span contains partial data with more data to follow.
        //
        // Returns:
        //     One of the enumeration values that indicates the status of the decoding operation.
        public static OperationStatus DecodeFromUtf8(ReadOnlySpan<byte> utf8, Span<byte> bytes, out int bytesConsumed, out int bytesWritten, bool isFinalBlock = true)
        {
            bytesWritten = 0;
            for (bytesConsumed = 0; bytesConsumed < utf8.Length; bytesConsumed += 2)
            {
                if (bytes.Length <= bytesWritten)
                {
                    return OperationStatus.DestinationTooSmall;
                }

                int high = utf8[bytesConsumed];

                if (48 <= high && high <= 57)
                {
                    high -= 48;
                }
                else if (65 <= high && high <= 70)
                {
                    high = (high - 65) + 10;
                }
                else if (97 <= high && high <= 102)
                {
                    high = (high - 97) + 10;
                }
                else
                {
                    return OperationStatus.InvalidData;
                }

                int low = 0;
                if (utf8.Length > bytesConsumed + 1)
                {
                    low = utf8[bytesConsumed + 1];

                    if (48 <= low && low <= 57)
                    {
                        low -= 48;
                    }
                    else if (65 <= low && low <= 70)
                    {
                        low = (low - 65) + 10;
                    }
                    else if (97 <= low && low <= 102)
                    {
                        low = (low - 97) + 10;
                    }
                    else
                    {
                        return OperationStatus.InvalidData;
                    }
                }

                bytes[bytesWritten++] = (byte)(high << 4 | low);
            }

            return OperationStatus.Done;
        }

        //
        // Summary:
        //     Decodes the span of UTF-8 encoded text in base 64 (in-place) into binary data.
        //     The decoded binary output is smaller than the text data contained in the input
        //     (the operation deflates the data). If the input is not a multiple of 4, the method
        //     will not decode any data.
        //
        // Parameters:
        //   buffer:
        //     The input span that contains the base-64 text data that needs to be decoded.
        //
        //   bytesWritten:
        //     The number of bytes written into the buffer.
        //
        // Returns:
        //     One of the enumeration values that indicates the status of the decoding operation.
        public static OperationStatus DecodeFromUtf8InPlace(Span<byte> buffer, out int bytesWritten)
        {
            throw new NotImplementedException();
        }

        //
        // Summary:
        //     Encodes the span of binary data into UTF-8 encoded text represented as base 64.
        //
        // Parameters:
        //   bytes:
        //     The input span that contains binary data that needs to be encoded.
        //
        //   utf8:
        //     The output span that contains the result of the operation, that is, the UTF-8
        //     encoded text in base 64.
        //
        //   bytesConsumed:
        //     The number of input bytes consumed during the operation. This can be used to
        //     slice the input for subsequent calls, if necessary.
        //
        //   bytesWritten:
        //     The number of bytes written into the output span. This can be used to slice the
        //     output for subsequent calls, if necessary.
        //
        //   isFinalBlock:
        //     true (the default) if the input span contains the entire data to encode. false
        //     if the input span contains partial data with more data to follow.
        //
        // Returns:
        //     One of the enumeration values that indicates the status of the encoding operation.
        public static OperationStatus EncodeToUtf8(ReadOnlySpan<byte> bytes, Span<byte> utf8, out int bytesConsumed, out int bytesWritten, bool isFinalBlock = true)
        {
            bytesWritten = 0;
            for (bytesConsumed = 0; bytesConsumed < bytes.Length; ++bytesConsumed)
            {
                if (utf8.Length <= bytesWritten + 1)
                {
                    return OperationStatus.DestinationTooSmall;
                }

                var b = bytes[bytesConsumed];

                var high = (b >> 4);
                utf8[bytesWritten++] = (byte)(55 + high + (((high - 10) >> 31) & -7));
                var low = (b & 0xF);
                utf8[bytesWritten++] = (byte)(55 + low + (((low - 10) >> 31) & -7));
            }

            return OperationStatus.Done;
        }

        //
        // Summary:
        //     Encodes the span of binary data (in-place) into UTF-8 encoded text represented
        //     as base 64. The encoded text output is larger than the binary data contained
        //     in the input (the operation inflates the data).
        //
        // Parameters:
        //   buffer:
        //     The input span that contains binary data that needs to be encoded. Because the
        //     method performs an in-place conversion, it needs to be large enough to store
        //     the result of the operation.
        //
        //   dataLength:
        //     The number of bytes of binary data contained within the buffer that needs to
        //     be encoded. This value must be smaller than the buffer length.
        //
        //   bytesWritten:
        //     The number of bytes written into the buffer.
        //
        // Returns:
        //     One of the enumeration values that indicates the status of the encoding operation.
        public static OperationStatus EncodeToUtf8InPlace(Span<byte> buffer, int dataLength, out int bytesWritten)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns the maximum length (in bytes) of the result if you were to decode base-16 encoded text within a byte span of size length.
        /// </summary>
        public static int GetMaxDecodedFromUtf8Length(int length)
        {
            return (length + 1) / 2;
        }

        /// <summary>
        /// Returns the maximum length (in bytes) of the result if you were to encode binary data within a byte span of size length.
        /// </summary>
        public static int GetMaxEncodedToUtf8Length(int length)
        {
            return length * 2;
        }

        public static byte[] Decode(string base16)
        {
            var utf8 = System.Text.Encoding.UTF8.GetBytes(base16);
            var buffer = new byte[GetMaxDecodedFromUtf8Length(utf8.Length)];
            var status = DecodeFromUtf8(utf8, buffer, out var bytesConsumed, out var bytesWritten);
            if (status != OperationStatus.Done || bytesWritten != buffer.Length || bytesConsumed != utf8.Length)
            {
                throw new Exception("Unreachable");
            }
            return buffer;
        }

        public static string Encode(byte[] bytes)
        {
            var utf8 = new byte[GetMaxEncodedToUtf8Length(bytes.Length)];
            var status = EncodeToUtf8(bytes, utf8, out var bytesConsumed, out var bytesWritten);
            if (status != OperationStatus.Done || bytesWritten != utf8.Length || bytesConsumed != bytes.Length)
            {
                throw new Exception("Unreachable");
            }
            return System.Text.Encoding.UTF8.GetString(utf8);
        }
    }
}
