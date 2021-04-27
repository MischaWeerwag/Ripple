using System;
using System.Text.Json;

namespace Ibasa.Ripple
{
    /// <summary>
    /// The "CurrencyType" type is a special field type that represents an issued currency with a code and optionally an issuer or XRP.
    /// </summary>
    public struct CurrencyType
    {
        public readonly AccountId? Issuer;
        public readonly CurrencyCode CurrencyCode;

        public static readonly CurrencyType XRP = new CurrencyType();

        public CurrencyType(CurrencyCode currencyCode)
        {
            CurrencyCode = currencyCode;
            Issuer = null;
        }

        public CurrencyType(AccountId issuer, CurrencyCode currencyCode)
        {
            if (currencyCode == CurrencyCode.XRP)
            {
                throw new ArgumentException("Can not be XRP", "currencyCode");
            }
            CurrencyCode = currencyCode;
            Issuer = issuer;
        }

        internal void WriteJson(Utf8JsonWriter writer)
        {
            writer.WriteStartObject();
            writer.WriteString("currency", CurrencyCode.ToString());
            if (Issuer.HasValue)
            {
                writer.WriteString("issuer", Issuer.Value.ToString());
            }
            writer.WriteEndObject();
        }

        internal static CurrencyType ReadJson(JsonElement json)
        {
            var currencyCode = new CurrencyCode(json.GetProperty("currency").GetString());

            if (json.TryGetProperty("issuer", out var element))
            {
                return new CurrencyType(new AccountId(element.GetString()), currencyCode);
            }
            else
            {
                return new CurrencyType(currencyCode);
            }
        }

        public override string ToString()
        {
            if (Issuer.HasValue)
            {
                return string.Format("{1}({2})", CurrencyCode, Issuer.Value);
            }
            else
            {
                return CurrencyCode.ToString();
            }
        }

        public static implicit operator CurrencyType(CurrencyCode value)
        {
            return new CurrencyType(value);
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

        public void WriteJson(Utf8JsonWriter writer)
        {
            var xrp = XrpAmount;
            if (xrp.HasValue)
            {
                xrp.Value.WriteJson(writer);
            }
            else
            {
                var issued = IssuedAmount;
                if (issued.HasValue)
                {
                    issued.Value.WriteJson(writer);
                }
                else
                {
                    throw new Exception("Unreachable");
                }
            }
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

        public void WriteJson(Utf8JsonWriter writer)
        {
            writer.WriteStringValue(Drops.ToString());
        }

        public static XrpAmount ReadJson(JsonElement json)
        {
            if (json.ValueKind == JsonValueKind.String)
            {
                return new XrpAmount(ulong.Parse(json.GetString()));
            }
            else if (json.ValueKind == JsonValueKind.Number)
            {
                return new XrpAmount(json.GetUInt64());
            }
            else
            {
                var message = String.Format("The requested operation requires an element of type 'String' or 'Number', but the target element has type '{0}'", json.ValueKind);
                throw new ArgumentException(message, "json");
            }
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

            Issuer = issuer;
            CurrencyCode = currencyCode;
            Value = value;
        }

        public static implicit operator Amount(IssuedAmount value)
        {
            return new Amount(value.Issuer, value.CurrencyCode, value.Value);
        }

        public void WriteJson(Utf8JsonWriter writer)
        {
            writer.WriteStartObject();
            writer.WriteString("currency", CurrencyCode.ToString());
            writer.WriteString("issuer", Issuer.ToString());
            writer.WriteString("value", Value.ToString());
            writer.WriteEndObject();
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