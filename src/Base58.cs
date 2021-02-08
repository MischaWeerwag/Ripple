using System;
using System.Buffers;

namespace Ibasa.Ripple
{
    // TODO: Maybe this should write to an ArrayBuffer? Could write directly to the json streams we send.

    public static class Base58
    {
        private static string alphabet = "rpshnaf39wBUDNEGHJKLM4PQRST7VWXYZ2bcdeCg65jkm8oFqi1tuvAxyz";

        private static int[] index;

        static Base58()
        {
            index = new int[123];

            for (int i = 0; i < index.Length; i++)
            {
                index[i] = -1;
            }
            for (int i = 0; i < alphabet.Length; i++)
            {
                index[alphabet[i]] = i;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="base58"></param>
        /// <param name="buffer"></param>
        /// <returns>The number of bytes decoded into buffer</returns>
        public static int ConvertFrom(ReadOnlySpan<char> base58, Span<byte> buffer)
        {
            if (base58.Length == 0)
            {
                return 0;
            }

            byte[] input58 = new byte[base58.Length];
            // Transform the String to a base58 byte sequence
            for (int i = 0; i < base58.Length; ++i)
            {
                char c = base58[i];

                int digit58 = -1;
                if (c >= 0 && c < 123)
                {
                    digit58 = index[c];
                }
                if (digit58 < 0)
                {
                    throw new Exception("Illegal character " + c + " at " + i);
                }

                input58[i] = (byte)digit58;
            }
            // Count leading zeroes
            var zeroCount = 0;
            while (zeroCount < input58.Length && input58[zeroCount] == 0)
            {
                ++zeroCount;
            }
            // The encoding
            var temp = new byte[base58.Length];
            var j = temp.Length;

            var startAt = zeroCount;
            while (startAt < input58.Length)
            {
                var mod = DivMod256(input58, startAt);
                if (input58[startAt] == 0)
                {
                    ++startAt;
                }

                temp[--j] = mod;
            }
            // Do no add extra leading zeroes, move j to first non null byte.
            while (j < temp.Length && temp[j] == 0)
            {
                ++j;
            }

            CopyOfRange(temp, j - zeroCount, temp.Length, buffer);
            return temp.Length - (j - zeroCount);
        }
        private static void CopyOfRange(byte[] source, int from, int to, Span<byte> dest)
        {
            new Span<byte>(source, from, to - from).CopyTo(dest);
        }

        private static byte DivMod256(byte[] number58, int startAt)
        {
            var remainder = 0;
            for (var i = startAt; i < number58.Length; i++)
            {
                var digit58 = number58[i];
                var temp = remainder * 58 + digit58;

                number58[i] = (byte)(temp / 256);

                remainder = temp % 256;
            }

            return (byte)remainder;
        }

        public static string ConvertTo(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length == 0)
            {
                return "";
            }
            // Count leading zeroes.
            int zeroCount = 0;
            while (zeroCount < bytes.Length && bytes[zeroCount] == 0)
            {
                ++zeroCount;
            }
            // The actual encoding.
            byte[] temp = new byte[bytes.Length * 2];
            int j = temp.Length;

            byte[] buffer = new byte[bytes.Length];
            bytes.CopyTo(buffer);

            int startAt = zeroCount;
            while (startAt < bytes.Length)
            {
                byte mod = DivMod58(buffer, startAt);
                if (buffer[startAt] == 0)
                {
                    ++startAt;
                }
                temp[--j] = (byte)alphabet[mod];
            }

            // Strip extra '1' if there are some after decoding.
            while (j < temp.Length && temp[j] == alphabet[0])
            {
                ++j;
            }
            // Add as many leading '1' as there were leading zeros.
            while (--zeroCount >= 0)
            {
                temp[--j] = (byte)alphabet[0];
            }

            var output = new byte[temp.Length - j];
            CopyOfRange(temp, j, temp.Length, output);
            return System.Text.Encoding.ASCII.GetString(output);
        }
        private static byte DivMod58(Span<byte> number, int startAt)
        {
            var remainder = 0;
            for (var i = startAt; i < number.Length; i++)
            {
                var digit256 = number[i];
                var temp = remainder * 256 + digit256;

                number[i] = (byte)(temp / 58);

                remainder = temp % 58;
            }

            return (byte)remainder;
        }
    }

    public static class Base58Check
    {
        public static int ConvertFrom(ReadOnlySpan<char> base58, Span<byte> bytes)
        {
            Span<byte> buffer = stackalloc byte[bytes.Length + 4];
            var count = Base58.ConvertFrom(base58, buffer);
            if(count < 4)
            {
                throw new ArgumentException("Base58 text did not contain enough for a 4 byte hash code", "base58");
            }
            // Rest of this function just cares about the non-hash code byte count
            count -= 4;

            Span<byte> firstHash = stackalloc byte[32];
            Span<byte> secondHash = stackalloc byte[32];

            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                if (!sha256.TryComputeHash(buffer.Slice(0, count), firstHash, out var written) || written != 32)
                {
                    throw new Exception("Unexpected failure of SHA256");
                }

                if (!sha256.TryComputeHash(firstHash, secondHash, out written) || written != 32)
                {
                    throw new Exception("Unexpected failure of SHA256");
                }
            }

            if (!buffer.Slice(count, 4).SequenceEqual(secondHash.Slice(0, 4)))
            {
                throw new Exception("Base58 hash code did not match");
            }

            buffer.Slice(0, count).CopyTo(bytes);
            return count;
        }

        public static string ConvertTo(ReadOnlySpan<byte> bytes)
        {
            Span<byte> buffer = stackalloc byte[bytes.Length + 4];
            bytes.CopyTo(buffer);

            Span<byte> firstHash = stackalloc byte[32];

            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                if (!sha256.TryComputeHash(buffer.Slice(0, bytes.Length), firstHash, out var written) || written != 32)
                {
                    throw new Exception("Unexpected failure of SHA256");
                }

                Span<byte> secondHash = stackalloc byte[32];

                if (!sha256.TryComputeHash(firstHash, secondHash, out written) || written != 32)
                {
                    throw new Exception("Unexpected failure of SHA256");
                }

                secondHash.Slice(0, 4).CopyTo(buffer.Slice(bytes.Length, 4));
            }

            return Base58.ConvertTo(buffer);
        }
    }
}
