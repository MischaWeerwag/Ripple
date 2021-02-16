using System;
using System.Text.Json;

namespace Ibasa.Ripple
{
    public static class JsonElementExtensions
    {
        public static byte[] GetBytesFromBase16(this JsonElement element)
        {
            return Base16.Decode(element.GetString());
        }
    }
}
