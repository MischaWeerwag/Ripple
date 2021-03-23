using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Ibasa.Ripple
{
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Size = 20)]
    public struct CurrencyCode : IEquatable<CurrencyCode>
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private uint _data0;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private uint _data1;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private uint _data2;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private uint _data3;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private uint _data4;

        private static Span<byte> UnsafeAsSpan(ref CurrencyCode currencyCode)
        {
            return System.Runtime.InteropServices.MemoryMarshal.AsBytes(System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref currencyCode._data0, 5));
        }

        public static readonly CurrencyCode XRP = new CurrencyCode();

        public CurrencyCode(string code) : this()
        {
            var bytes = UnsafeAsSpan(ref this);
            if (code.Length == 3)
            {
                // Standard Currency Code
                if (code == "XRP")
                {
                    // Short circuit XRP to all zeros
                    return;
                }

                for (var i = 0; i < 3; ++i)
                {
                    var c = (ushort)code[i];

                    // The following characters are permitted: all uppercase and lowercase letters, digits, as well as the symbols ?, !, @, #, $, %, ^, &, *, <, >, (, ), {, }, [, ], and |.
                    if (c == 33 || (35 <= c && c <= 38) || (40 <= c && c <= 42) || (48 <= c && c <= 57) || c == 60 || (62 <= c && c <= 94) || (97 <= c && c <= 125))
                    {
                        bytes[12 + i] = (byte)c;
                    }
                    else
                    {
                        throw new ArgumentException(string.Format("'{0}' is not a valid standard currency code character", (char)c), "code");
                    }
                }
            }
            else if (code.Length == 40)
            {
                // Nonstandard Currency Code
                for (int i = 0; i < bytes.Length; ++i)
                {
                    var hi = (int)code[i * 2];
                    var lo = (int)code[i * 2 + 1];

                    if (hi < 48 || (hi > 57 && hi < 65) || (hi > 90 && hi < 97) || hi > 122 || lo < 48 || (lo > 57 && lo < 65) || (lo > 90 && lo < 97) || lo > 122)
                    {
                        throw new ArgumentException(string.Format("'{0}' is not a valid hex code", code), "code");
                    }

                    bytes[i] = (byte)(((hi - (hi < 58 ? 48 : (hi < 97 ? 55 : 87))) << 4) | (lo - (lo < 58 ? 48 : (lo < 97 ? 55 : 87))));
                }
            }
            else
            {
                throw new ArgumentException(string.Format("'{0}' is not valid, must be either be a 3 character standard currency code, or a 40 character nonstandard hex code", code), "code");
            }
        }

        public CurrencyCode(ReadOnlySpan<byte> bytes) : this()
        {
            bytes.CopyTo(UnsafeAsSpan(ref this));
        }

        public void CopyTo(Span<byte> destination)
        {
            UnsafeAsSpan(ref this).CopyTo(destination);
        }

        /// <summary>
        /// Returns true if this is a standard 3 character currency code (that isn't XRP).
        /// </summary>
        public bool IsStandard
        {
            get
            {
                var bytes = UnsafeAsSpan(ref this);
                if (bytes[0] == 0x0)
                {
                    bool allZero = true;
                    bool standard = true;
                    for (int i = 1; i < bytes.Length; ++i)
                    {
                        if (bytes[i] != 0)
                        {
                            allZero = false;
                            if (i < 12 || 14 < i)
                            {
                                standard = false;
                            }
                        }
                    }

                    if (allZero)
                    {
                        return false; // Is XRP, that's not standard
                    }
                    return standard;
                }

                return false;
            }
        }

        public override string ToString()
        {
            var bytes = UnsafeAsSpan(ref this);
            if (IsStandard)
            { 
                var slice = bytes.Slice(12, 3);
                return System.Text.Encoding.ASCII.GetString(slice);                
            }
            else if (this == XRP)
            {
                return "XRP";
            }

            // Nonstandard Currency Code
            Span<byte> utf8 = stackalloc byte[Base16.GetEncodedToUtf8Length(bytes.Length)];
            var _ = Base16.EncodeToUtf8(bytes, utf8, out var _, out var _);
            return System.Text.Encoding.UTF8.GetString(utf8);
        }

        public bool Equals(CurrencyCode other)
        {
            var a = UnsafeAsSpan(ref this);
            var b = UnsafeAsSpan(ref other);
            for (int i = 0; i < 16; ++i)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }
            return true;
        }

        public override int GetHashCode()
        {
            var hash = new System.HashCode();
            foreach (var b in UnsafeAsSpan(ref this))
            {
                hash.Add(b);
            }
            return hash.ToHashCode();
        }

        public override bool Equals(object other)
        {
            if (other is CurrencyCode)
            {
                return Equals((CurrencyCode)other);
            }
            return false;
        }

        /// <summary>
        /// Returns a value that indicates whether two CurrencyCode values are equal.
        /// </summary>
        /// <param name="c1">The first value to compare.</param>
        /// <param name="c2">The second value to compare.</param>
        /// <returns>true if c1 and c2 are equal; otherwise, false.</returns>
        public static bool operator ==(CurrencyCode c1, CurrencyCode c2)
        {
            return c1.Equals(c2);
        }

        /// <summary>
        /// Returns a value that indicates whether two CuCurrencyCoderrency objects have different values.
        /// </summary>
        /// <param name="c1">The first value to compare.</param>
        /// <param name="c2">The second value to compare.</param>
        /// <returns>true if c1 and c2 are not equal; otherwise, false.</returns>
        public static bool operator !=(CurrencyCode c1, CurrencyCode c2)
        {
            return !c1.Equals(c2);
        }
    }

    /// <summary>
    /// The "Amount" type is a special field type that represents an amount of currency, either XRP or an issued currency.
    /// </summary>
    public readonly struct Amount
    {
        private readonly ulong value;
        private readonly AccountId issuer;
        private readonly CurrencyCode currencyCode;

        public XrpAmount? XrpAmount
        {
            get
            {
                if ((value & 0x8000_0000_0000_0000) == 0)
                {
                    // XRP just return the positive drops
                    return Ripple.XrpAmount.FromDrops(value & 0x3FFFFFFFFFFFFFFF);
                }
                return null;
            }
        }

        public IssuedAmount? IssuedAmount
        {
            get
            {
                if ((value & 0x8000_0000_0000_0000) != 0)
                {
                    return new IssuedAmount(issuer, currencyCode, Currency.FromUInt64Bits(value));
                }
                return null;
            }
        }

        public Amount(ulong drops)
        {
            if (drops > 100000000000000000)
            {
                throw new ArgumentOutOfRangeException("drops", drops, "drops must be less than or equal to 100,000,000,000,000,000");
            }
            this.value = drops | 0x4000_0000_0000_0000;
            // These fields are only used for IssuedAmount but struct constructor has to set all fields.
            this.currencyCode = default;
            this.issuer = default;
        }

        public Amount(AccountId issuer, CurrencyCode currencyCode, Currency value)
        {
            if (currencyCode == CurrencyCode.XRP)
            {
                throw new ArgumentException("Can not be XRP", "currencyCode");
            }
            this.value = Currency.ToUInt64Bits(value);
            this.currencyCode = currencyCode;
            this.issuer = issuer;
        }

        internal static Amount ReadJson(JsonElement json)
        {
            if (json.ValueKind == JsonValueKind.Object)
            {
                return Ripple.IssuedAmount.ReadJson(json);
            }
            else
            {
                return Ripple.XrpAmount.ReadJson(json);
            }
        }

        public override string ToString()
        {
            var xrp = XrpAmount;
            if (xrp.HasValue)
            {
                return xrp.Value.ToString();
            }
            else
            {
                var issued = IssuedAmount;
                if (issued.HasValue)
                {
                    return issued.Value.ToString();
                }
                else
                {
                    throw new Exception("Unreachable");
                }
            }
        }
    }

    /// <summary>
    /// An "Amount" that must be in XRP.
    /// </summary>
    public readonly struct XrpAmount
    {
        public readonly ulong Drops;

        public decimal XRP
        {
            get
            {
                return ((decimal)Drops) / 1000000;
            }
        }

        private XrpAmount(ulong drops)
        {
            if (drops > 100000000000000000)
            {
                throw new ArgumentOutOfRangeException("drops", drops, "drops must be less than or equal to 100,000,000,000,000,000");
            }
            Drops = drops;
        }

        public static XrpAmount FromDrops(ulong drops)
        {
            return new XrpAmount(drops);
        }

        public static XrpAmount FromXrp(decimal xrp)
        {
            if (xrp < 0)
            {
                throw new ArgumentOutOfRangeException("xrp", xrp, "xrp must be positive");
            }
            if (xrp > 100000000000)
            {
                throw new ArgumentOutOfRangeException("xrp", xrp, "xrp must be less than or equal to 100,000,000,000");
            }

            return new XrpAmount((ulong)(xrp * 1000000));
        }

        public static implicit operator Amount(XrpAmount value)
        {
            return new Amount(value.Drops);
        }

        public static XrpAmount ReadJson(JsonElement json)
        {
            return new XrpAmount(ulong.Parse(json.GetString()));
        }

        public static XrpAmount Parse(string s)
        {
            return new XrpAmount(ulong.Parse(s));
        }

        public override string ToString()
        {
            return string.Format("{0} XRP", XRP);
        }
    }

    /// <summary>
    /// An "Amount" that must be an issued currency.
    /// </summary>
    public readonly struct IssuedAmount
    {
        public readonly Currency Value;
        public readonly AccountId Issuer;
        public readonly CurrencyCode CurrencyCode;

        public IssuedAmount(AccountId issuer, CurrencyCode currencyCode, Currency value)
        {
            if (currencyCode == CurrencyCode.XRP)
            {
                throw new ArgumentException("Can not be XRP", "currencyCode");
            }

            this.Issuer = issuer;
            this.CurrencyCode = currencyCode;
            this.Value = value;
        }

        public static implicit operator Amount(IssuedAmount value)
        {
            return new Amount(value.Issuer, value.CurrencyCode, value.Value);
        }

        public static IssuedAmount ReadJson(JsonElement json)
        {
            return new IssuedAmount(
                new AccountId(json.GetProperty("issuer").GetString()),
                new CurrencyCode(json.GetProperty("currency").GetString()),
                Currency.Parse(json.GetProperty("value").GetString()));
        }

        public override string ToString()
        {
            return string.Format("{0} {1}({2})", Value, CurrencyCode, Issuer);
        }
    }
}