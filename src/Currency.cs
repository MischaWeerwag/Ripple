using System;

namespace Ibasa.Ripple
{
    /// <summary>
    /// Represents a 64-bit decimal floating-point number with 15 units of precision.
    /// </summary>
    public struct Currency
    {
        /// <summary>
        /// Represents the largest possible value of Currency. This field is constant and read-only.
        /// </summary>
        public static readonly Currency MaxValue = new Currency(true, maxExponent, maxMantissa);
        /// <summary>
        /// Represents the number negative one (-1).
        /// </summary>
        public static readonly Currency MinusOne = new Currency(false, -15, minMantissa);
        /// <summary>
        /// Represents the smallest possible value of Currency. This field is constant and read-only.
        /// </summary>
        public static readonly Currency MinValue = new Currency(false, maxExponent, maxMantissa);
        /// <summary>
        /// Represents the number one (1).
        /// </summary>
        public static readonly Currency One = new Currency(true, -15, minMantissa);
        /// <summary>
        /// Represents the number zero (0).
        /// </summary>
        public static readonly Currency Zero = new Currency(true, 0, 0);

        private const ulong minMantissa = 1000_0000_0000_0000;
        private const ulong maxMantissa = 9999_9999_9999_9999;
        private const int minExponent = -96;
        private const int maxExponent = 80;

        private readonly ulong bits;

        private Currency(ulong bits)
        {
            this.bits = bits;
        }

        private static ulong Pack(bool isPositive, int exponent, ulong mantissa)
        {
            var signbit = isPositive ? 0x4000_0000_0000_0000u : 0x0u;
            var exponentbits = ((ulong)(exponent + 97)) << 54;
            return signbit | exponentbits | mantissa;
        }

        public override bool Equals(object obj)
        {
            if (obj is Currency)
            {
                var other = (Currency)obj;
                return other.bits == this.bits;
            }
            return false;
        }

        public Currency(decimal amount)
        {
            if (amount == 0m)
            {
                this.bits = 0;
                return;
            }

            var bits = Decimal.GetBits(amount);
            Span<byte> mantissabytes = stackalloc byte[12];
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(mantissabytes.Slice(0, 4), bits[0]);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(mantissabytes.Slice(4, 4), bits[1]);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(mantissabytes.Slice(8, 4), bits[2]);
            var bigmantissa = new System.Numerics.BigInteger(mantissabytes, true, false);
            var isPositive = (bits[3] & 0xF000_0000) == 0;
            var exponent = (bits[3] >> 16) & 0xFF;

            // C# decimal is stored as 'mantissa / 10^exponent' where exponent must be (0, 28) and mantissa is 96 bits,
            // while ripple stores decimals as 'mantissa * 10^exponent' where exponent must be (-96, 80) and mantissa is 54 bits.

            // First flip the exponent (C# divides, ripple multiplies)
            exponent = -exponent;

            // We need to scale the mantissa to be between 1000_0000_0000_0000 and 9999_9999_9999_9999
            while (bigmantissa < minMantissa)
            {
                exponent -= 1;
                bigmantissa *= 10;
            }

            while (bigmantissa > maxMantissa)
            {
                exponent += 1;
                bigmantissa /= 10;
            }

            // mantissa will now fit into 54 bits, exponent should be between -96 and 80.
            mantissabytes.Clear();
            var ok = bigmantissa.TryWriteBytes(mantissabytes, out var bytesWritten, true, false);
            System.Diagnostics.Debug.Assert(ok);
            System.Diagnostics.Debug.Assert(bytesWritten <= 7);
            var mantissa = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(mantissabytes);

            System.Diagnostics.Debug.Assert(minExponent <= exponent && exponent <= maxExponent);
            System.Diagnostics.Debug.Assert(minMantissa <= mantissa && mantissa <= maxMantissa);

            this.bits = Pack(isPositive, exponent, mantissa);
        }

        public Currency(bool isPositive, int exponent, ulong mantissa)
        {
            if (exponent == 0 && mantissa == 0)
            {
                this.bits = 0;
                return;
            }

            if (exponent < minExponent || maxExponent < exponent)
            {
                throw new ArgumentOutOfRangeException("exponent", exponent, "exponent must be between -96 and 80 (inclusive)");
            }

            if (mantissa < minMantissa || maxMantissa < mantissa)
            {
                throw new ArgumentOutOfRangeException("mantissa", mantissa, "mantissa must be between 1,000,000,000,000,000 and 9,999,999,999,999,999 (inclusive)");
            }

            this.bits = Pack(isPositive, exponent, mantissa);
        }

        public static explicit operator decimal(Currency value)
        {
            var isNegative = (value.bits & 0x4000_0000_0000_0000) == 0;
            var exponent = (int)((value.bits >> 54) & 0xFF) - 97;

            // Exponent is only zero (and thus -97 translated) if the mantissa is also zero
            if (exponent == -97)
            {
                return 0m;
            }

            Span<byte> mantissabytes = stackalloc byte[12];
            System.Buffers.Binary.BinaryPrimitives.TryWriteUInt64LittleEndian(mantissabytes, value.bits & 0x3FFFFFFFFFFFFF);

            // C# decimal is stored as 'mantissa / 10^exponent' where exponent must be (0, 28) and mantissa is 96 bits,
            // while ripple stores decimals as 'mantissa * 10^exponent' where exponent must be (-96, 80) and mantissa is 54 bits.

            // First flip the exponent (C# divides, ripple multiplies)
            exponent = -exponent;

            if (exponent < 0 || exponent > 28)
            {
                var bigmantissa = new System.Numerics.BigInteger(mantissabytes);

                // We need to scale the exponent to be positive
                if (exponent < 0)
                {
                    bigmantissa *= System.Numerics.BigInteger.Pow(10, 0 - exponent);
                    exponent = 0;
                }

                // And less than or equal to 28
                if (exponent > 28)
                {
                    bigmantissa /= System.Numerics.BigInteger.Pow(10, exponent - 28);
                    exponent = 28;
                }

                mantissabytes.Clear();
                if (!bigmantissa.TryWriteBytes(mantissabytes, out var bytesWritten, true, false))
                {
                    throw new OverflowException("Value was either too large or too small for a Decimal.");
                }
                System.Diagnostics.Debug.Assert(bytesWritten <= 12);
            }

            return new decimal(
                System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(mantissabytes.Slice(0, 4)),
                System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(mantissabytes.Slice(4, 4)),
                System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(mantissabytes.Slice(8, 4)),
                isNegative, (byte)exponent);
        }

        public override string ToString()
        {
            var isNegative = (bits & 0x4000_0000_0000_0000) == 0;
            var exponent = (int)((bits >> 54) & 0xFF) - 97;
            if (exponent == -97)
            {
                return "0";
            }
            var mantissa = bits & 0x3FFFFFFFFFFFFF;
            while (mantissa % 10 == 0)
            {
                mantissa /= 10;
                exponent += 1;
            }

            return (isNegative ? "-" : "") + mantissa.ToString() + (exponent == 0 ? "" : "E" + exponent.ToString());
        }

        public static Currency Parse(string s)
        {
            Span<char> mantissaChars = stackalloc char[17];
            int mantissaCount = 0;
            Span<char> exponentChars = stackalloc char[3];
            int exponentCount = 0;
            int fraction = 0;

            bool seenE = false;
            bool seenDecimal = false;

            for (var i = 0; i < s.Length; ++i)
            {
                var c = s[i];

                if (c == '.')
                {
                    seenDecimal = true;
                }
                else if (c == 'E' || c == 'e')
                {
                    seenE = true;
                }
                else
                {
                    if (seenE)
                    {
                        exponentChars[exponentCount++] = c;
                    }
                    else
                    {
                        mantissaChars[mantissaCount++] = c;
                        if (seenDecimal)
                        {
                            ++fraction;
                        }
                    }
                }
            }

            var exponent = (exponentCount == 0 ? 0 : int.Parse(exponentChars.Slice(0, exponentCount))) - fraction;
            var iMantissa = long.Parse(mantissaChars.Slice(0, mantissaCount));
            var isPositive = iMantissa > 0;
            var mantissa = (ulong)Math.Abs(iMantissa);

            while (mantissa < minMantissa)
            {
                exponent -= 1;
                mantissa *= 10;
            }

            while (mantissa > maxMantissa)
            {
                exponent += 1;
                mantissa /= 10;
            }

            return new Currency(isPositive, exponent, mantissa);
        }

        public static ulong ToUInt64Bits(Currency value)
        {
            // We don't store the 'not xrp' bit on the struct so that 'new CurrencyValue()' is a valid object (0).
            return value.bits | 0x8000_0000_0000_0000u;
        }

        public static Currency FromUInt64Bits(ulong value)
        {
            return new Currency(value * ~0x8000_0000_0000_0000u);
        }
    }
}