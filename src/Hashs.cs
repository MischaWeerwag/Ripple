using System;

namespace Ibasa.Ripple
{
    public struct Hash128 : IEquatable<Hash128>
    {
        readonly long a;
        readonly long b;

        public Hash128(string hex)
        {
            a = long.Parse(hex.Substring(0, 16), System.Globalization.NumberStyles.HexNumber);
            b = long.Parse(hex.Substring(16, 16), System.Globalization.NumberStyles.HexNumber);
        }

        public Hash128(Span<byte> bytes)
        {
            a = System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(bytes.Slice(0, 8));
            b = System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(bytes.Slice(8, 8));
        }

        public override string ToString()
        {
            return String.Format("{0,16:X}{1,16:X}", a, b).Replace(' ', '0');
        }

        public override bool Equals(object obj)
        {
            if (obj is Hash128)
            {
                return Equals((Hash128)obj);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(a, b);
        }

        public bool Equals(Hash128 other)
        {
            return a == other.a && b == other.b;
        }

        /// <summary>
        /// Returns a value that indicates whether two Hash128 values are equal.
        /// </summary>
        /// <param name="c1">The first value to compare.</param>
        /// <param name="c2">The second value to compare.</param>
        /// <returns>true if c1 and c2 are equal; otherwise, false.</returns>
        public static bool operator ==(Hash128 c1, Hash128 c2)
        {
            return c1.Equals(c2);
        }

        /// <summary>
        /// Returns a value that indicates whether two Hash128 objects have different values.
        /// </summary>
        /// <param name="c1">The first value to compare.</param>
        /// <param name="c2">The second value to compare.</param>
        /// <returns>true if c1 and c2 are not equal; otherwise, false.</returns>
        public static bool operator !=(Hash128 c1, Hash128 c2)
        {
            return !c1.Equals(c2);
        }
    }

    public struct Hash256 : IEquatable<Hash256>
    {
        readonly long a;
        readonly long b;
        readonly long c;
        readonly long d;

        public Hash256(string hex)
        {
            a = long.Parse(hex.Substring(0, 16), System.Globalization.NumberStyles.HexNumber);
            b = long.Parse(hex.Substring(16, 16), System.Globalization.NumberStyles.HexNumber);
            c = long.Parse(hex.Substring(32, 16), System.Globalization.NumberStyles.HexNumber);
            d = long.Parse(hex.Substring(48, 16), System.Globalization.NumberStyles.HexNumber);
        }

        public Hash256(ReadOnlySpan<byte> bytes)
        {
            a = System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(bytes.Slice(0, 8));
            b = System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(bytes.Slice(8, 8));
            c = System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(bytes.Slice(16, 8));
            d = System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(bytes.Slice(24, 8));
        }

        public void CopyTo(Span<byte> destination)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(destination.Slice(0, 8), a);
            System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(destination.Slice(8, 8), b);
            System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(destination.Slice(16, 8), c);
            System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(destination.Slice(24, 8), d);
        }

        public override string ToString()
        {
            return String.Format("{0,16:X}{1,16:X}{2,16:X}{3,16:X}", a, b, c, d).Replace(' ', '0');
        }

        public override bool Equals(object obj)
        {
            if (obj is Hash256)
            {
                return Equals((Hash256)obj);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(a, b, c, d);
        }

        public bool Equals(Hash256 other)
        {
            return a == other.a && b == other.b && c == other.c && d == other.d;
        }

        /// <summary>
        /// Returns a value that indicates whether two Hash256 values are equal.
        /// </summary>
        /// <param name="c1">The first value to compare.</param>
        /// <param name="c2">The second value to compare.</param>
        /// <returns>true if c1 and c2 are equal; otherwise, false.</returns>
        public static bool operator ==(Hash256 c1, Hash256 c2)
        {
            return c1.Equals(c2);
        }

        /// <summary>
        /// Returns a value that indicates whether two Hash256 objects have different values.
        /// </summary>
        /// <param name="c1">The first value to compare.</param>
        /// <param name="c2">The second value to compare.</param>
        /// <returns>true if c1 and c2 are not equal; otherwise, false.</returns>
        public static bool operator !=(Hash256 c1, Hash256 c2)
        {
            return !c1.Equals(c2);
        }
    }
}