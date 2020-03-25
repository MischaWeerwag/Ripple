using System;
using System.Text.Json;

namespace Ibasa.Ripple
{
    public static class Utf8JsonWriterExtensions
    {
        public static void WriteBase16String(this Utf8JsonWriter writer, string propertyName, byte[] bytes)
        {
            writer.WritePropertyName(propertyName);

            byte[] rented = null;

            var count = Base16.GetMaxEncodedToUtf8Length(bytes.Length);
            Span<byte> utf8 = count <= 256 ? stackalloc byte[count] : (rented = System.Buffers.ArrayPool<byte>.Shared.Rent(count));

            var _ = Base16.EncodeToUtf8(bytes, utf8, out var _, out var written);

            writer.WriteStringValue(utf8.Slice(0, written));

            if (rented != null)
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }
}
