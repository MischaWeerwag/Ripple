using System;
using System.Text.Json;

namespace Ibasa.Ripple
{
    public static class JsonElementExtensions
    {
        public static byte[] GetBytesFromBase16(this JsonElement element)
        {
            var str = element.GetString();
            var utf8 = System.Text.Encoding.UTF8.GetBytes(str);
            
            var count = Base16.GetMaxEncodedToUtf8Length(utf8.Length);
            var bytes = new byte[count];
            var _ = Base16.DecodeFromUtf8(utf8, bytes, out var _, out var _);
            return bytes;
        }
    }
}
