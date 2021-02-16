using System;
using System.Text.Json;

namespace Ibasa.Ripple
{
    public static class Utf8JsonWriterExtensions
    {
        public static void WriteBase16String(this Utf8JsonWriter writer, string propertyName, ReadOnlySpan<byte> bytes)
        {
            writer.WritePropertyName(propertyName);

            byte[] rented = null;

            var count = Base16.GetEncodedToUtf8Length(bytes.Length);
            Span<byte> utf8 = count <= 256 ? stackalloc byte[count] : (rented = System.Buffers.ArrayPool<byte>.Shared.Rent(count));
            try
            {
                var status = Base16.EncodeToUtf8(bytes, utf8, out var _, out var written);
                if(status != System.Buffers.OperationStatus.Done)
                {
                    throw new Exception("Unexpected failure in Base16 encode");
                }
                writer.WriteStringValue(utf8.Slice(0, written));
            }
            finally
            {
                if (rented != null)
                {
                    System.Buffers.ArrayPool<byte>.Shared.Return(rented);
                }
            }
        }
    }
}
