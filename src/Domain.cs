using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Ibasa.Ripple
{
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Size = 20)]
    public struct AccountId : IEquatable<AccountId>
    {
        private uint _data0, _data1, _data2, _data3, _data4;

        private static Span<byte> UnsafeAsSpan(ref AccountId account)
        {
            return System.Runtime.InteropServices.MemoryMarshal.AsBytes(System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref account._data0, 5));
        }

        public AccountId(string base58) : this()
        {
            Span<byte> content = stackalloc byte[21];
            Base58Check.ConvertFrom(base58, content);
            if (content[0] != 0x0)
            {
                throw new ArgumentException("Expected 0x0 prefix byte", "base58");
            }

            content.Slice(1).CopyTo(UnsafeAsSpan(ref this));
        }
        public AccountId(ReadOnlySpan<byte> bytes) : this()
        {
            bytes.CopyTo(UnsafeAsSpan(ref this));
        }

        public static AccountId FromPublicKey(ReadOnlySpan<byte> publicKey)
        {
            var shaHash = new byte[32];
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var done = sha256.TryComputeHash(publicKey, shaHash, out var bytesWritten);
            }
            var ripe = new Org.BouncyCastle.Crypto.Digests.RipeMD160Digest();
            ripe.BlockUpdate(shaHash, 0, 32);
            var ripeHash = new byte[20];
            var ok = ripe.DoFinal(ripeHash, 0);
            return new AccountId(ripeHash);
        }

        public override string ToString()
        {
            Span<byte> content = stackalloc byte[21];
            content[0] = 0x0;
            UnsafeAsSpan(ref this).CopyTo(content.Slice(1));

            return Base58Check.ConvertTo(content);
        }

        public void CopyTo(Span<byte> destination)
        {
            UnsafeAsSpan(ref this).CopyTo(destination);
        }

        public bool Equals(AccountId other)
        {
            var a = UnsafeAsSpan(ref this);
            var b = UnsafeAsSpan(ref other);
            for (int i = 0; i < 20; ++i)
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
            if (other is AccountId)
            {
                return Equals((AccountId)other);
            }
            return false;
        }
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Size = 16)]
    public struct Seed
    {
        private uint _data0, _data1, _data2, _data3;

        private static Span<byte> UnsafeAsSpan(ref Seed seed)
        {
            return System.Runtime.InteropServices.MemoryMarshal.AsBytes(System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref seed._data0, 4));
        }

        public Seed(string base58) : this()
        {
            Span<byte> content = stackalloc byte[17];
            Base58Check.ConvertFrom(base58, content);
            if (content[0] != 0x21)
            {
                throw new Exception("Expected 0x21 prefix byte");
            }

            content.Slice(1).CopyTo(UnsafeAsSpan(ref this));
        }

        public override string ToString()
        {
            Span<byte> content = stackalloc byte[17];
            content[0] = 0x21;
            UnsafeAsSpan(ref this).CopyTo(content.Slice(1));

            return Base58Check.ConvertTo(content);
        }

        public void Ed25519KeyPair(out byte[] publicKey, out byte[] privateKey)
        {
            privateKey = new byte[32];
            var span = UnsafeAsSpan(ref this);
            using (var sha512 = System.Security.Cryptography.SHA512.Create())
            {
                Span<byte> destination = stackalloc byte[64];
                var done = sha512.TryComputeHash(span, destination, out var bytesWritten);
                destination.Slice(0, 32).CopyTo(privateKey);
            }

            var priv = new Org.BouncyCastle.Crypto.Parameters.Ed25519PrivateKeyParameters(privateKey, 0);
            var pub = priv.GeneratePublicKey();
            publicKey = new byte[33];
            publicKey[0] = 0xed;
            pub.Encode(publicKey, 1);
        }

        public void Secp256k1KeyPair(out Secp256k1KeyPair rootKeyPair, out Secp256k1KeyPair keyPair)
        {
            Span<byte> rootSource = stackalloc byte[20];
            UnsafeAsSpan(ref this).CopyTo(rootSource);

            var secpSecretBytes = new byte[32];
            var k1Params = Org.BouncyCastle.Asn1.Sec.SecNamedCurves.GetByName("secp256k1");

            using (var sha512 = System.Security.Cryptography.SHA512.Create())
            {
                uint i;
                Org.BouncyCastle.Math.BigInteger secpRootSecret = default;
                for (i = 0; i < uint.MaxValue; ++i)
                {
                    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(rootSource.Slice(16), i);
                    Span<byte> destination = stackalloc byte[64];
                    var done = sha512.TryComputeHash(rootSource, destination, out var bytesWritten);
                    destination.Slice(0, 32).CopyTo(secpSecretBytes);

                    secpRootSecret = new Org.BouncyCastle.Math.BigInteger(1, secpSecretBytes);
                    if (secpRootSecret.CompareTo(Org.BouncyCastle.Math.BigInteger.Zero) == 1 && secpRootSecret.CompareTo(k1Params.N) == -1)
                    {
                        break;
                    }
                }

                var rootPublicKeyPoint = k1Params.G.Multiply(secpRootSecret);
                rootKeyPair = new Secp256k1KeyPair(secpRootSecret, rootPublicKeyPoint);

                // Calculate intermediate
                Span<byte> intermediateSource = stackalloc byte[41];
                rootKeyPair.GetCanonicalPublicKey().CopyTo(intermediateSource);
                System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(intermediateSource.Slice(33), 0);

                Org.BouncyCastle.Math.BigInteger secpIntermediateSecret = default;
                for (i = 0; i < uint.MaxValue; ++i)
                {
                    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(intermediateSource.Slice(37), i);
                    Span<byte> destination = stackalloc byte[64];
                    var done = sha512.TryComputeHash(intermediateSource, destination, out var bytesWritten);
                    destination.Slice(0, 32).CopyTo(secpSecretBytes);

                    secpIntermediateSecret = new Org.BouncyCastle.Math.BigInteger(1, secpSecretBytes);
                    if (secpIntermediateSecret.CompareTo(Org.BouncyCastle.Math.BigInteger.Zero) == 1 && secpIntermediateSecret.CompareTo(k1Params.N) == -1)
                    {
                        break;
                    }
                }

                var masterPrivateKey = secpRootSecret.Add(secpIntermediateSecret).Mod(k1Params.N);
                var intermediatePublicKeyPoint = k1Params.G.Multiply(secpIntermediateSecret);
                var masterPublicKey = rootPublicKeyPoint.Add(intermediatePublicKeyPoint);
                keyPair = new Secp256k1KeyPair(masterPrivateKey, masterPublicKey);
            }
        }

        public bool Equals(Seed other)
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
            if (other is Seed)
            {
                return Equals((Seed)other);
            }
            return false;
        }
    }


    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Size = 20)]
    public struct CurrencyCode : IEquatable<CurrencyCode>
    {
        private uint _data0, _data1, _data2, _data3, _data4;

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
                        throw new ArgumentException("code is not a valid hex code", "code");
                    }

                    bytes[i] = (byte)(((hi - (hi < 58 ? 48 : (hi < 97 ? 55 : 87))) << 4) | (lo - (lo < 58 ? 48 : (lo < 97 ? 55 : 87))));
                }

                if (bytes[0] == 0x0)
                {
                    throw new ArgumentException("hex code first byte can not be zero", "code");
                }
            }
            else
            {
                throw new ArgumentException("code must be either be a 3 character standard currency code, or a 40 character nonstandard hex code", "code");
            }
        }

        public bool IsStandard
        {
            get
            {
                return UnsafeAsSpan(ref this)[0] == 0;
            }
        }

        public void CopyTo(Span<byte> destination)
        {
            UnsafeAsSpan(ref this).CopyTo(destination);
        }

        public override string ToString()
        {
            var bytes = UnsafeAsSpan(ref this);
            if (bytes[0] == 0x0)
            {
                // Standard Currency Code
                return System.Text.Encoding.ASCII.GetString(bytes.Slice(12, 3));
            }
            else
            {
                // Nonstandard Currency Code
                Span<byte> utf8 = stackalloc byte[Base16.GetMaxEncodedToUtf8Length(bytes.Length)];
                var _ = Base16.EncodeToUtf8(bytes, utf8, out var _, out var _);
                return System.Text.Encoding.UTF8.GetString(utf8);
            }

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
    }

    public struct CurrencyValue
    {
        private readonly ulong bits;

        private CurrencyValue(ulong bits)
        {
            this.bits = bits;
        }

        public override bool Equals(object obj)
        {
            if (obj is CurrencyValue)
            {
                var other = (CurrencyValue)obj;
                return other.bits == this.bits;
            }
            return false;
        }

        public CurrencyValue(decimal amount)
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
            while (bigmantissa < 1000_0000_0000_0000)
            {
                exponent -= 1;
                bigmantissa *= 10;
            }

            while (bigmantissa > 9999_9999_9999_9999)
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

            System.Diagnostics.Debug.Assert(-96 <= exponent && exponent <= 80);
            System.Diagnostics.Debug.Assert(1000_0000_0000_0000 <= mantissa && mantissa <= 9999_9999_9999_9999);

            var signbit = isPositive ? 0x4000_0000_0000_0000u : 0x0u;
            var exponentbits = ((ulong)exponent + 97) << 54;
            this.bits = signbit | exponentbits | mantissa;
        }

        public CurrencyValue(bool isPositive, int exponent, ulong mantissa)
        {
            if (exponent == 0 && mantissa == 0)
            {
                this.bits = 0;
                return;
            }

            if (exponent < -96 || 80 < exponent)
            {
                throw new ArgumentOutOfRangeException("exponent", exponent, "exponent must be between -96 and 80 (inclusive)");
            }

            if (mantissa < 1000_0000_0000_0000 || 9999_9999_9999_9999 < mantissa)
            {
                throw new ArgumentOutOfRangeException("mantissa", mantissa, "mantissa must be between 1,000,000,000,000,000 and 9,999,999,999,999,999 (inclusive)");
            }

            var signbit = isPositive ? 0x4000_0000_0000_0000u : 0x0u;
            var exponentbits = ((ulong)exponent + 97) << 54;
            this.bits = signbit | exponentbits | mantissa;
        }

        public static explicit operator decimal(CurrencyValue value)
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

        public static CurrencyValue Parse(string s)
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

            while (mantissa < 1000_0000_0000_0000)
            {
                exponent -= 1;
                mantissa *= 10;
            }

            while (mantissa > 9999_9999_9999_9999)
            {
                exponent += 1;
                mantissa /= 10;
            }

            return new CurrencyValue(isPositive, exponent, mantissa);
        }

        public static ulong ToUInt64Bits(CurrencyValue value)
        {
            // We don't store the 'not xrp' bit on the struct so that 'new CurrencyValue()' is a valid object (0).
            return value.bits | 0x8000_0000_0000_0000u;
        }

        public static CurrencyValue FromUInt64Bits(ulong value)
        {
            return new CurrencyValue(value * ~0x8000_0000_0000_0000u);
        }
    }

    /// <summary>
    /// The "Amount" type is a special field type that represents an amount of currency, either XRP or an issued currency.
    /// </summary>
    public struct Amount
    {
        private readonly ulong value;
        private readonly AccountId account;

        public CurrencyCode CurrencyCode { get; }
        public AccountId? Issuer { get { return CurrencyCode.Equals(CurrencyCode.XRP) ? (AccountId?)null : account; } }

        public ulong? Drops
        {
            get
            {
                if ((value & 0x8000_0000_0000_0000) == 0)
                {
                    // XRP just return the positive drops
                    return (value & 0x3FFFFFFFFFFFFFFF);
                }
                return null;
            }
        }

        public CurrencyValue? Value
        {
            get
            {
                if ((value & 0x8000_0000_0000_0000) != 0)
                {
                    return CurrencyValue.FromUInt64Bits(value);
                }
                return null;
            }
        }

        public Amount(ulong drops)
        {
            if ((drops & 0xC000_0000_0000_0000) != 0)
            {
                throw new ArgumentOutOfRangeException("drops", drops, "drops must be less than 4,611,686,018,427,387,904");
            }
            this.value = drops | 0x4000_0000_0000_0000;
            this.CurrencyCode = CurrencyCode.XRP;
            this.account = new AccountId();
        }

        internal Amount(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                // Just plain xrp
                this.value = UInt64.Parse(element.GetString()) | 0x4000_0000_0000_0000;
                this.CurrencyCode = CurrencyCode.XRP;
                this.account = new AccountId();
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public Amount(AccountId issuer, CurrencyCode currencyCode, CurrencyValue value)
        {
            this.value = CurrencyValue.ToUInt64Bits(value);
            this.CurrencyCode = currencyCode;
            this.account = issuer;
        }
    }

    /// <summary>
    /// An "Amount" that must be in XRP.
    /// </summary>
    public struct XrpAmount
    {
        public readonly ulong Drops;

        public XrpAmount(ulong drops)
        {
            if ((drops & 0xC000_0000_0000_0000) != 0)
            {
                throw new ArgumentOutOfRangeException("drops", drops, "drops must be less than 4,611,686,018,427,387,904");
            }
            Drops = drops;
        }

        public static implicit operator Amount(XrpAmount value)
        {
            return new Amount(value.Drops);
        }

        public static XrpAmount Parse(string s)
        {
            return new XrpAmount(ulong.Parse(s));
        }
    }


    public sealed class AccountRoot
    {
        //LedgerEntryType String  UInt16 The value 0x0061, mapped to the string AccountRoot, indicates that this is an AccountRoot object.
        //Flags Number  UInt32 A bit-map of boolean flags enabled for this account.
        //PreviousTxnID String  Hash256 The identifying hash of the transaction that most recently modified this object.
        //PreviousTxnLgrSeq Number  UInt32 The index of the ledger that contains the transaction that most recently modified this object.
        //AccountTxnID String Hash256 (Optional) The identifying hash of the transaction most recently sent by this account.This field must be enabled to use the AccountTxnID transaction field.To enable it, send an AccountSet transaction with the asfAccountTxnID flag enabled.
        //
        //EmailHash String  Hash128 (Optional) The md5 hash of an email address. Clients can use this to look up an avatar through services such as Gravatar.
        //MessageKey String  VariableLength  (Optional) A public key that may be used to send encrypted messages to this account.In JSON, uses hexadecimal.No more than 33 bytes.
        //RegularKey String  AccountID(Optional) The address of a key pair that can be used to sign transactions for this account instead of the master key.Use a SetRegularKey transaction to change this value.
        //TickSize Number  UInt8   (Optional) How many significant digits to use for exchange rates of Offers involving currencies issued by this address.Valid values are 3 to 15, inclusive. (Requires the TickSize amendment.)
        //TransferRate Number  UInt32(Optional) A transfer fee to charge other users for sending currency issued by this account to each other.
        //WalletLocator   String Hash256 (Optional) DEPRECATED. Do not use.
        //WalletSize Number  UInt32  (Optional) DEPRECATED. Do not use.

        /// <summary>
        /// The identifying address of this account, such as rf1BiGeXwwQoi8Z2ueFYTEXSwuJYfV2Jpn.
        /// </summary>
        public AccountId Account { get; private set; }

        /// <summary>
        /// The account's current XRP balance in drops.
        /// </summary>
        public XrpAmount Balance { get; private set; }

        /// <summary>
        /// The number of objects this account owns in the ledger, which contributes to its owner reserve.
        /// </summary>
        public uint OwnerCount { get; private set; }

        /// <summary>
        /// (Optional) A domain associated with this account. In JSON, this is the hexadecimal for the ASCII representation of the domain.
        /// </summary>
        public byte[] Domain { get; private set; }

        /// <summary>
        /// The sequence number of the next valid transaction for this account. 
        /// (Each account starts with Sequence = 1 and increases each time a transaction is made.)
        /// </summary>
        public uint Sequence { get; private set; }

        internal AccountRoot(JsonElement json)
        {
            JsonElement element;

            Account = new AccountId(json.GetProperty("Account").GetString());
            Balance = XrpAmount.Parse(json.GetProperty("Balance").GetString());
            OwnerCount = json.GetProperty("OwnerCount").GetUInt32();
            Sequence = json.GetProperty("Sequence").GetUInt32();
            if (json.TryGetProperty("Domain", out element))
            {
                Domain = element.GetBytesFromBase16();
            }
        }

        //{id: 1,…}
        //        id: 1
        //    result: {account_data: {Account: "r3kmLJN5D28dHuH8vZNUZpMC43pEHpaocV", Balance: "7584128441", Flags: 0,…},…}
        //    account_data: {Account: "r3kmLJN5D28dHuH8vZNUZpMC43pEHpaocV", Balance: "7584128441", Flags: 0,…}
        //    Account: "r3kmLJN5D28dHuH8vZNUZpMC43pEHpaocV"
        //    Balance: "7584128441"
        //    Flags: 0
        //    LedgerEntryType: "AccountRoot"
        //    OwnerCount: 7
        //    PreviousTxnID: "309E32220F1ECEAC58262E34F19F5A7C9A22A98F882FD8C66C99A46F0B967485"
        //    PreviousTxnLgrSeq: 52708421
        //    Sequence: 345
        //    index: "B33FDD5CF3445E1A7F2BE9B06336BEBD73A5E3EE885D3EF93F7E3E2992E46F1A"
        //    ledger_hash: "C191678AE13A66BAFCAA5CA776839FAA04D283C429D1EFDE2F2AB643CFE0F2D4"
        //    ledger_index: 53870749
        //    validated: true
        //    status: "success"
        //    type: "response"
    }

    public sealed class LedgerResponse
    {
        public Hash256 LedgerHash { get; private set; }

        internal LedgerResponse(JsonElement json)
        {
            LedgerHash = new Hash256(json.GetProperty("ledger_hash").GetString());
        }
    }

    public struct LedgerSpecification
    {
        private uint index;
        private string shortcut;
        private Hash256? hash;

        /// <summary>
        /// The most recent ledger that has been validated by the whole network.
        /// </summary>
        public static LedgerSpecification Validated = new LedgerSpecification() { shortcut = "validated" };

        /// <summary>
        /// The most recent ledger that has been closed for modifications and proposed for validation.
        /// </summary>
        public static LedgerSpecification Closed = new LedgerSpecification() { shortcut = "closed" };

        /// <summary>
        /// The server's current working version of the ledger.
        /// </summary>
        public static LedgerSpecification Current = new LedgerSpecification();

        public LedgerSpecification(uint index)
        {
            if (index == 0)
            {
                throw new ArgumentOutOfRangeException("index", index, "index must be greater than zero");
            }

            this.index = index;
            this.shortcut = null;
            this.hash = new Hash256?();
        }
        public LedgerSpecification(Hash256 hash)
        {
            this.index = 0;
            this.shortcut = null;
            this.hash = new Hash256?(hash);
        }

        internal static void Write(Utf8JsonWriter writer, LedgerSpecification specification)
        {
            if (specification.index == 0)
            {
                if (specification.hash.HasValue)
                {
                    writer.WriteString("ledger_hash", specification.hash.Value.ToString());
                }
                else if (specification.shortcut != null)
                {
                    writer.WriteString("ledger_index", specification.shortcut);
                }
                else
                {
                    writer.WriteString("ledger_index", "current");
                }
            }
            else
            {
                writer.WriteNumber("ledger_index", specification.index);
            }
        }
    }

    public sealed class AccountInfoRequest
    {
        /// <summary>
        /// A unique identifier for the account, most commonly the account's Address.
        /// </summary>
        public AccountId Account { get; set; }

        /// <summary>
        /// A 20-byte hex string, or the ledger index of the ledger to use, or a shortcut string to choose a ledger automatically.
        /// </summary>
        public LedgerSpecification Ledger { get; set; }

        /// <summary>
        /// If set to True, then the account field only accepts a public key or XRP Ledger address.
        /// </summary>
        public bool Strict { get; set; }

        /// <summary>
        /// If true, and the FeeEscalation amendment is enabled, also returns stats about queued transactions associated with this account.
        /// Can only be used when querying for the data from the current open ledger.
        /// </summary>
        public bool Queue { get; set; }

        /// <summary>
        /// If true, and the MultiSign amendment is enabled, also returns any SignerList objects associated with this account.
        /// </summary>
        public bool SignerLists { get; set; }
    }

    public sealed class AccountInfoResponse
    {
        //signer_lists Array(Omitted unless the request specified signer_lists and at least one SignerList is associated with the account.) Array of SignerList ledger objects associated with this account for Multi-Signing.Since an account can own at most one SignerList, this array must have exactly one member if it is present.New in: rippled 0.31.0 
        //ledger_current_index Integer (Omitted if ledger_index is provided instead) The ledger index of the current in-progress ledger, which was used when retrieving this information.
        //ledger_index Integer (Omitted if ledger_current_index is provided instead) The ledger index of the ledger version used when retrieving this information.The information does not contain any changes from ledger versions newer than this one.
        //queue_data Object(Omitted unless queue specified as true and querying the current open ledger.) Information about queued transactions sent by this account.This information describes the state of the local rippled server, which may be different from other servers in the peer-to-peer XRP Ledger network.Some fields may be omitted because the values are calculated "lazily" by the queuing mechanism.

        /// <summary>
        /// The AccountRoot ledger object with this account's information, as stored in the ledger.
        /// </summary>
        public AccountRoot AccountData { get; private set; }

        /// <summary>
        /// True if this data is from a validated ledger version; if omitted or set to false, this data is not final.
        /// </summary>
        public bool Validated { get; private set; }

        internal AccountInfoResponse(JsonElement json)
        {
            AccountData = new AccountRoot(json.GetProperty("account_data"));
            Validated = json.GetProperty("validated").GetBoolean();
        }
    }

    public sealed class LedgerRequest
    {
        /// <summary>
        /// A 20-byte hex string, or the ledger index of the ledger to use, or a shortcut string to choose a ledger automatically.
        /// </summary>
        public LedgerSpecification Ledger { get; set; }
        /// <summary>
        /// Admin required If true, return full information on the entire ledger. Ignored if you did not specify a ledger version. 
        /// Defaults to false. (Equivalent to enabling transactions, accounts, and expand.) 
        /// Caution: This is a very large amount of data -- on the order of several hundred megabytes!
        /// </summary>
        public bool Full { get; set; }
        /// <summary>
        /// Admin required. If true, return information on accounts in the ledger. 
        /// Ignored if you did not specify a ledger version. 
        /// Defaults to false. 
        /// Caution: This returns a very large amount of data!
        /// </summary>
        public bool Accounts { get; set; }
        /// <summary>
        /// If true, return information on transactions in the specified ledger version. 
        /// Defaults to false. 
        /// Ignored if you did not specify a ledger version.
        /// </summary>
        public bool Transactions { get; set; }
        /// <summary>
        /// Provide full JSON-formatted information for transaction/account information instead of only hashes. 
        /// Defaults to false. 
        /// Ignored unless you request transactions, accounts, or both.
        /// </summary>
        public bool Expand { get; set; }
        /// <summary>
        /// If true, include owner_funds field in the metadata of OfferCreate transactions in the response. 
        /// Defaults to false. 
        /// Ignored unless transactions are included and expand is true.
        /// </summary>
        public bool OwnerFunds { get; set; }
        /// <summary>
        /// If true, and the command is requesting the current ledger, includes an array of queued transactions in the results.
        /// </summary>
        public bool Queue { get; set; }
    }

    public sealed class LedgerClosedResponse
    {
        public Hash256 LedgerHash { get; private set; }
        public uint LedgerIndex { get; private set; }

        internal LedgerClosedResponse(JsonElement json)
        {
            LedgerHash = new Hash256(json.GetProperty("ledger_hash").GetString());
            LedgerIndex = json.GetProperty("ledger_index").GetUInt32();
        }
    }

    public sealed class FeeResponseDrops
    {
        /// <summary>
        /// The transaction cost required for a reference transaction to be included in a ledger under minimum load, represented in drops of XRP.
        /// </summary>
        public XrpAmount BaseFee { get; private set; }

        /// <summary>
        /// An approximation of the median transaction cost among transactions included in the previous validated ledger, represented in drops of XRP.
        /// </summary>
        public XrpAmount MedianFee { get; private set; }

        /// <summary>
        /// The minimum transaction cost for a reference transaction to be queued for a later ledger, represented in drops of XRP.
        /// If greater than base_fee, the transaction queue is full.
        /// </summary>
        public XrpAmount MinimumFee { get; private set; }

        /// <summary>
        /// The minimum transaction cost that a reference transaction must pay to be included in the current open ledger, represented in drops of XRP.
        /// </summary>
        public XrpAmount OpenLedgerFee { get; private set; }

        internal FeeResponseDrops(JsonElement json)
        {
            BaseFee = XrpAmount.Parse(json.GetProperty("base_fee").GetString());
            MedianFee = XrpAmount.Parse(json.GetProperty("median_fee").GetString());
            MinimumFee = XrpAmount.Parse(json.GetProperty("minimum_fee").GetString());
            OpenLedgerFee = XrpAmount.Parse(json.GetProperty("open_ledger_fee").GetString());
        }
    }

    public sealed class FeeResponseLevels
    {
        /// <summary>
        /// The median transaction cost among transactions in the previous validated ledger, represented in fee levels.
        /// </summary>
        public ulong MedianLevel { get; private set; }
        /// <summary>
        /// The minimum transaction cost required to be queued for a future ledger, represented in fee levels.
        /// </summary>
        public ulong MinimumLevel { get; private set; }
        /// <summary>
        /// The minimum transaction cost required to be included in the current open ledger, represented in fee levels.
        /// </summary>
        public ulong OpenLedgerLevel { get; private set; }
        /// <summary>
        /// The equivalent of the minimum transaction cost, represented in fee levels.
        /// </summary>
        public ulong ReferenceLevel { get; private set; }

        internal FeeResponseLevels(JsonElement json)
        {
            MedianLevel = ulong.Parse(json.GetProperty("median_level").GetString());
            MinimumLevel = ulong.Parse(json.GetProperty("minimum_level").GetString());
            OpenLedgerLevel = ulong.Parse(json.GetProperty("open_ledger_level").GetString());
            ReferenceLevel = ulong.Parse(json.GetProperty("reference_level").GetString());
        }
    }

    public sealed class FeeResponse
    {
        /// <summary>
        /// Number of transactions provisionally included in the in-progress ledger.
        /// </summary>
        public ulong CurrentLedgerSize { get; private set; }

        /// <summary>
        /// Number of transactions currently queued for the next ledger.
        /// </summary>
        public ulong CurrentQueueSize { get; private set; }
        /// <summary>
        /// The maximum number of transactions that the transaction queue can currently hold.
        /// </summary>

        public ulong MaxQueueSize { get; private set; }
        /// <summary>
        /// The approximate number of transactions expected to be included in the current ledger.
        /// This is based on the number of transactions in the previous ledger.
        /// </summary>
        public ulong ExpectedLedgerSize { get; private set; }
        /// <summary>
        /// The Ledger Index of the current open ledger these stats describe.
        /// </summary>
        public uint LedgerCurrentIndex { get; private set; }

        /// <summary>
        /// Various information about the transaction cost (the Fee field of a transaction), in drops of XRP.
        /// </summary>
        public FeeResponseDrops Drops { get; private set; }

        /// <summary>
        /// Various information about the transaction cost, in fee levels.The ratio in fee levels applies to any transaction relative to the minimum cost of that particular transaction.
        /// </summary>
        public FeeResponseLevels Levels { get; private set; }

        internal FeeResponse(JsonElement json)
        {
            CurrentLedgerSize = ulong.Parse(json.GetProperty("current_ledger_size").GetString());
            CurrentQueueSize = ulong.Parse(json.GetProperty("current_queue_size").GetString());
            MaxQueueSize = ulong.Parse(json.GetProperty("max_queue_size").GetString());
            ExpectedLedgerSize = ulong.Parse(json.GetProperty("expected_ledger_size").GetString());
            LedgerCurrentIndex = json.GetProperty("ledger_current_index").GetUInt32();
            Drops = new FeeResponseDrops(json.GetProperty("drops"));
            Levels = new FeeResponseLevels(json.GetProperty("levels"));
        }
    }

    public sealed class AccountCurrenciesResponse
    {
        /// <summary>
        /// The identifying hash of the ledger version used to retrieve this data, as hex.
        /// </summary>
        public Hash256? LedgerHash { get; private set; }
        /// <summary>
        /// The ledger index of the ledger version used to retrieve this data.
        /// </summary>
        public uint LedgerIndex { get; private set; }

        /// <summary>
        /// Array of Currency Codes for currencies that this account can receive.
        /// </summary>
        public ReadOnlyCollection<CurrencyCode> ReceiveCurrencies { get; private set; }

        /// <summary>
        /// Array of Currency Codes for currencies that this account can send.
        /// </summary>
        public ReadOnlyCollection<CurrencyCode> SendCurrencies { get; private set; }

        /// <summary>
        /// If true, this data comes from a validated ledger.
        /// </summary>
        public bool Validated { get; private set; }

        internal AccountCurrenciesResponse(JsonElement json)
        {
            if (json.TryGetProperty("ledger_hash", out var hash))
            {
                LedgerHash = new Hash256(hash.GetString());
            }

            if (json.TryGetProperty("ledger_current_index", out var ledgerCurrentIndex))
            {
                LedgerIndex = ledgerCurrentIndex.GetUInt32();
            }
            else
            {
                LedgerIndex = json.GetProperty("ledger_index").GetUInt32();
            }
            Validated = json.GetProperty("validated").GetBoolean();

            JsonElement json_array;
            CurrencyCode[] codes;
            int index;

            json_array = json.GetProperty("receive_currencies");
            codes = new CurrencyCode[json_array.GetArrayLength()];
            index = 0;
            foreach (var code in json_array.EnumerateArray())
            {
                codes[index++] = new CurrencyCode(code.GetString());
            }
            ReceiveCurrencies = Array.AsReadOnly(codes);

            json_array = json.GetProperty("send_currencies");
            codes = new CurrencyCode[json_array.GetArrayLength()];
            index = 0;
            foreach (var code in json_array.EnumerateArray())
            {
                codes[index++] = new CurrencyCode(code.GetString());
            }
            SendCurrencies = Array.AsReadOnly(codes);
        }
    }

    public sealed class AccountCurrenciesRequest
    {
        /// <summary>
        /// A unique identifier for the account, most commonly the account's Address.
        /// </summary>
        public AccountId Account { get; set; }

        /// <summary>
        /// If true, only accept an address or public key for the account parameter.
        /// </summary>
        public bool Strict { get; set; }

        /// <summary>
        /// A 20-byte hex string, or the ledger index of the ledger to use, or a shortcut string to choose a ledger automatically.
        /// </summary>
        public LedgerSpecification Ledger { get; set; }
    }

    /// <summary>
    /// Depending on how the rippled server is configured, how long it has been running, and other factors, a server may be participating in the global XRP Ledger peer-to-peer network to different degrees. 
    /// This is represented as the server_state field in the responses to the server_info method and server_state method. 
    /// The possible responses follow a range of ascending interaction, with each later value superseding the previous one.
    /// </summary>
    public enum ServerState
    {
        /// <summary>
        /// The server is not connected to the XRP Ledger peer-to-peer network whatsoever. 
        /// It may be running in offline mode, or it may not be able to access the network for whatever reason.
        /// </summary>
        Disconnected = 0,
        /// <summary>
        /// The server believes it is connected to the network.
        /// </summary>
        Connected = 1,
        /// <summary>
        /// The server is currently behind on ledger versions. (It is normal for a server to spend a few minutes catching up after you start it.)
        /// </summary>
        Syncing = 2,
        /// <summary>
        /// The server is in agreement with the network
        /// </summary>
        Tracking = 3,
        /// <summary>
        /// The server is fully caught-up with the network and could participate in validation, but is not doing so (possibly because it has not been configured as a validator).
        /// </summary>
        Full = 4,
        /// <summary>
        /// The server is currently participating in validation of the ledger.
        /// </summary>
        Validating = 5,
        /// <summary>
        /// The server is participating in validation of the ledger and currently proposing its own version.
        /// </summary>
        Proposing = 6,
    }

    public sealed class ServerStateResponse
    {
        /// <summary>
        /// If true, this server is amendment blocked.
        /// </summary>
        public bool AmendmentBlocked { get; private set; }

        /// <summary>
        /// The version number of the running rippled version.
        /// </summary>
        public string BuildVersion { get; private set; }

        /// <summary>
        /// Range expression indicating the sequence numbers of the ledger versions the local rippled has in its database. 
        /// It is possible to be a disjoint sequence, e.g. "2500-5000,32570-7695432". 
        /// If the server does not have any complete ledgers (for example, it just started syncing with the network), this is the string empty.
        /// </summary>
        public string CompleteLedgers { get; private set; }

        /// <summary>
        /// Amount of time spent waiting for I/O operations, in milliseconds.
        /// If this number is not very, very low, then the rippled server is probably having serious load issues.
        /// </summary>
        public uint IoLatencyMs { get; private set; }

        /// <summary>
        /// How many other rippled servers this one is currently connected to.
        /// </summary>
        public uint Peers { get; private set; }

        /// <summary>
        /// Public key used to verify this server for peer-to-peer communications. 
        /// This node key pair is automatically generated by the server the first time it starts up. 
        /// (If deleted, the server can create a new pair of keys.) You can set a persistent value in the config file using the [node_seed] config option, which is useful for clustering.
        /// </summary>
        public string PubkeyNode { get; private set; }

        /// <summary>
        /// (Admin only) Public key used by this node to sign ledger validations.
        /// This validation key pair is derived from the[validator_token] or [validation_seed] config field.
        /// </summary>
        public string PubkeyValidator { get; private set; }

        /// <summary>
        /// A value indicating to what extent the server is participating in the network.
        /// </summary>
        public ServerState ServerState { get; private set; }

        /// <summary>
        /// The number of consecutive microseconds the server has been in the current state.
        /// </summary>
        public TimeSpan ServerStateDuration { get; private set; }

        /// <summary>
        /// Number of consecutive seconds that the server has been operational.
        /// </summary>
        public TimeSpan Uptime { get; private set; }

        //closed_ledger   Object  (May be omitted) Information on the most recently closed ledger that has not been validated by consensus.If the most recently validated ledger is available, the response omits this field and includes validated_ledger instead.The member fields are the same as the validated_ledger field.
        //load Object  (Admin only) Detailed information about the current load state of the server
        //load.job_types Array   (Admin only) Information about the rate of different types of jobs the server is doing and how much time it spends on each.
        //load.threads Number  (Admin only) The number of threads in the server's main job pool.
        //load_base Integer This is the baseline amount of server load used in transaction cost calculations.If the load_factor is equal to the load_base then only the base transaction cost is enforced.If the load_factor is higher than the load_base, then transaction costs are multiplied by the ratio between them. For example, if the load_factor is double the load_base, then transaction costs are doubled.
        //load_factor Number  The load factor the server is currently enforcing. The ratio between this value and the load_base determines the multiplier for transaction costs. The load factor is determined by the highest of the individual server's load factor, cluster's load factor, the open ledger cost, and the overall network's load factor. Updated in: rippled 0.33.0 
        //load_factor_fee_escalation Integer (May be omitted) The current multiplier to the transaction cost to get into the open ledger, in fee levels.New in: rippled 0.32.0 
        //load_factor_fee_queue Integer (May be omitted) The current multiplier to the transaction cost to get into the queue, if the queue is full, in fee levels.New in: rippled 0.32.0 
        //load_factor_fee_reference Integer (May be omitted) The transaction cost with no load scaling, in fee levels.New in: rippled 0.32.0 
        //load_factor_server Number  (May be omitted) The load factor the server is enforcing, not including the open ledger cost.New in: rippled 0.33.0 
        //state_accounting Object  A map of various server states with information about the time the server spends in each.This can be useful for tracking the long-term health of your server's connectivity to the network. New in: rippled 0.30.1 
        //state_accounting.*.duration_us String  The number of microseconds the server has spent in this state. (This is updated whenever the server transitions into another state.) New in: rippled 0.30.1 
        //state_accounting.*.transitions Number The number of times the server has transitioned into this state. New in: rippled 0.30.1 
        //uptime Number 
        //validated_ledger Object	(May be omitted) Information about the most recent fully-validated ledger. If the most recent validated ledger is not available, the response omits this field and includes closed_ledger instead.
        //validated_ledger.base_fee Unsigned Integer Base fee, in drops of XRP, for propagating a transaction to the network.
        //validated_ledger.close_time Number Time this ledger was closed, in seconds since the Ripple Epoch
        //validated_ledger.hash String Unique hash of this ledger version, as hex
        //validated_ledger.reserve_base Unsigned Integer The minimum account reserve, as of the most recent validated ledger version.
        //validated_ledger.reserve_inc Unsigned Integer The owner reserve for each item an account owns, as of the most recent validated ledger version.
        //validated_ledger.seq Unsigned Integer The ledger index of the most recently validated ledger version.
        //validation_quorum Number Minimum number of trusted validations required to validate a ledger version. Some circumstances may cause the server to require more validations.
        //validator_list_expires Number	(Admin only) When the current validator list will expire, in seconds since the Ripple Epoch, or 0 if the server has yet to load a published validator list. New in: rippled 0.80.1 

        internal ServerStateResponse(JsonElement json)
        {
            var state = json.GetProperty("state");

            if (state.TryGetProperty("amendment_blocked", out var amendment_blocked))
            {
                AmendmentBlocked = amendment_blocked.GetBoolean();
            }

            BuildVersion = state.GetProperty("build_version").GetString();
            CompleteLedgers = state.GetProperty("complete_ledgers").GetString();
            IoLatencyMs = state.GetProperty("io_latency_ms").GetUInt32();
            Peers = state.GetProperty("peers").GetUInt32();
            PubkeyNode = state.GetProperty("pubkey_node").GetString();
            if (state.TryGetProperty("pubkey_validator", out var pubkey_validator))
            {
                PubkeyValidator = pubkey_validator.GetString();
            }
            ServerState = Enum.Parse<ServerState>(state.GetProperty("server_state").GetString(), true);
            var ticksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000L;
            // Parse as ulong (no negatives) but then cast to long to pass into FromTicks
            var server_state_duration_us = (long)ulong.Parse(state.GetProperty("server_state_duration_us").GetString());
            ServerStateDuration = TimeSpan.FromTicks(server_state_duration_us * ticksPerMicrosecond);
            Uptime = TimeSpan.FromSeconds(state.GetProperty("uptime").GetUInt32());
        }
        //"state": {
        //    "build_version": "0.30.1-rc3",
        //    "complete_ledgers": "18611104-18615049",
        //    "io_latency_ms": 1,
        //    "last_close": {
        //      "converge_time": 3003,
        //      "proposers": 5
        //    },
        //    "load": {
        //      "job_types": [
        //          {
        //          "job_type": "untrustedProposal",
        //          "peak_time": 1,
        //          "per_second": 3
        //          },
        //          {
        //          "in_progress": 1,
        //          "job_type": "clientCommand"
        //          },
        //          {
        //          "avg_time": 12,
        //          "job_type": "writeObjects",
        //          "peak_time": 345,
        //          "per_second": 2
        //          },
        //          {
        //          "job_type": "trustedProposal",
        //          "per_second": 1
        //          },
        //          {
        //          "job_type": "peerCommand",
        //          "per_second": 64
        //          },
        //          {
        //          "avg_time": 33,
        //          "job_type": "diskAccess",
        //          "peak_time": 526
        //          },
        //          {
        //          "job_type": "WriteNode",
        //          "per_second": 55
        //          }
        //      ],
        //      "threads": 6
        //    },
        //    "load_base": 256,
        //    "load_factor": 256000,
        //    "peers": 10,
        //    "pubkey_node": "n94UE1ukbq6pfZY9j54sv2A1UrEeHZXLbns3xK5CzU9NbNREytaa",
        //    "pubkey_validator": "n9KM73uq5BM3Fc6cxG3k5TruvbLc8Ffq17JZBmWC4uP4csL4rFST",
        //    "server_state": "proposing",
        //    "server_state_duration_us": 92762334,
        //    "state_accounting": {
        //      "connected": {
        //          "duration_us": "150510079",
        //          "transitions": 1
        //      },
        //      "disconnected": {
        //          "duration_us": "1827731",
        //          "transitions": 1
        //      },
        //      "full": {
        //          "duration_us": "168295542987",
        //          "transitions": 1865
        //      },
        //      "syncing": {
        //          "duration_us": "6294237352",
        //          "transitions": 1866
        //      },
        //      "tracking": {
        //          "duration_us": "13035524",
        //          "transitions": 1866
        //      }
        //    },
        //    "uptime": 174748,
        //    "validated_ledger": {
        //      "base_fee": 10,
        //      "close_time": 507693650,
        //      "hash": "FEB17B15FB64E3AF8D371E6AAFCFD8B92775BB80AB953803BD73EA8EC75ECA34",
        //      "reserve_base": 20000000,
        //      "reserve_inc": 5000000,
        //      "seq": 18615049
        //    },
        //    "validation_quorum": 4,
        //    "validator_list_expires": 561139596
        //}
        //}
    }

    public sealed class AccountLinesRequest
    {
        /// <summary>
        /// A unique identifier for the account, most commonly the account's Address.
        /// </summary>
        public AccountId Account { get; set; }

        /// <summary>
        /// A 20-byte hex string, or the ledger index of the ledger to use, or a shortcut string to choose a ledger automatically.
        /// </summary>
        public LedgerSpecification Ledger { get; set; }

        /// <summary>
        /// The Address of a second account.
        /// If provided, show only lines of trust connecting the two accounts.
        /// </summary>
        public AccountId? Peer { get; set; }

    }

    public sealed class TrustLine
    {
        /// <summary>
        /// The unique Address of the counterparty to this trust line.
        /// </summary>
        public AccountId Account { get; private set; }

        /// <summary>
        /// Representation of the numeric balance currently held against this line.
        /// A positive balance means that the perspective account holds value; a negative balance means that the perspective account owes value.
        /// </summary>
        public string Balance { get; private set; }

        /// <summary>
        /// A Currency Code identifying what currency this trust line can hold.
        /// </summary>
        public CurrencyCode Currency { get; private set; }

        //limit String  The maximum amount of the given currency that this account is willing to owe the peer account
        //limit_peer String  The maximum amount of currency that the counterparty account is willing to owe the perspective account
        //quality_in Unsigned Integer Rate at which the account values incoming balances on this trust line, as a ratio of this value per 1 billion units. (For example, a value of 500 million represents a 0.5:1 ratio.) As a special case, 0 is treated as a 1:1 ratio.
        //quality_out Unsigned Integer Rate at which the account values outgoing balances on this trust line, as a ratio of this value per 1 billion units. (For example, a value of 500 million represents a 0.5:1 ratio.) As a special case, 0 is treated as a 1:1 ratio.
        //no_ripple Boolean(May be omitted) true if this account has enabled the NoRipple flag for this line.If omitted, that is the same as false.
        //no_ripple_peer Boolean(May be omitted) true if the peer account has enabled the NoRipple flag.If omitted, that is the same as false.
        //authorized Boolean (May be omitted) true if this account has authorized this trust line. If omitted, that is the same as false.
        //peer_authorized Boolean (May be omitted) true if the peer account has authorized this trust line. If omitted, that is the same as false.
        //freeze Boolean (May be omitted) true if this account has frozen this trust line. If omitted, that is the same as false.
        //freeze_peer Boolean (May be omitted) true if the peer account has frozen this trust line. If omitted, that is the same as false.
        internal TrustLine(JsonElement json)
        {
            Account = new AccountId(json.GetProperty("account").GetString());
            Balance = json.GetProperty("balance").GetString();
            Currency = new CurrencyCode(json.GetProperty("currency").GetString());
        }
    }


    public sealed class AccountLinesResponse : IAsyncEnumerable<TrustLine>
    {
        // TODO: I'm not sure about this API

        //ledger_current_index Integer - Ledger Index  (Omitted if ledger_hash or ledger_index provided) The ledger index of the current open ledger, which was used when retrieving this information. New in: rippled 0.26.4-sp1
        //ledger_index Integer - Ledger Index  (Omitted if ledger_current_index provided instead) The ledger index of the ledger version that was used when retrieving this data.New in: rippled 0.26.4-sp1
        //ledger_hash String - Hash(May be omitted) The identifying hash the ledger version that was used when retrieving this data.New in: rippled 0.26.4-sp1
        //marker  Marker Server-defined value indicating the response is paginated.Pass this to the next call to resume where this call left off.Omitted when there are no additional pages after this one.New in: rippled 0.26.4 

        /// <summary>
        /// Unique Address of the account this request corresponds to.
        /// This is the "perspective account" for purpose of the trust lines.
        /// </summary>
        public AccountId Account { get; private set; }

        private Func<JsonElement, CancellationToken, Task<JsonElement>> PostAsync;
        private JsonElement? Marker;
        private JsonElement Lines;


        internal AccountLinesResponse(JsonElement json, Func<JsonElement, CancellationToken, Task<JsonElement>> postAsync)
        {
            PostAsync = postAsync;
            Account = new AccountId(json.GetProperty("account").GetString());

            if (json.TryGetProperty("marker", out var marker))
            {
                Marker = marker;
            }

            Lines = json.GetProperty("lines");
        }

        public async IAsyncEnumerator<TrustLine> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            var marker = Marker;
            var lines = Lines;

            while (true)
            {
                foreach (var line in lines.EnumerateArray())
                {
                    yield return new TrustLine(line);
                }

                if (marker.HasValue)
                {
                    var response = await PostAsync(marker.Value, cancellationToken);
                    if (response.TryGetProperty("marker", out var newMarker))
                    {
                        marker = newMarker;
                    }
                    else
                    {
                        marker = null;
                    }

                    lines = response.GetProperty("lines");
                }
                else
                {
                    break;
                }
            }
        }
    }

    public sealed class SubmitRequest
    {
        /// <summary>
        /// Hex representation of the signed transaction to submit. This can be a multi-signed transaction.
        /// </summary>
        public byte[] TxBlob { get; set; }
        /// <summary>
        /// If true, and the transaction fails locally, do not retry or relay the transaction to other servers
        /// </summary>
        public bool FailHard { get; set; }
    }

    public sealed class SubmitResponse
    {
        /// <summary>
        /// Code indicating the preliminary result of the transaction, for example tesSUCCESS
        /// </summary>
        public EngineResult EngineResult { get; private set; }
        /// <summary>
        /// Human-readable explanation of the transaction's preliminary result
        /// </summary>
        public string EngineResultMessage { get; private set; }
        /// <summary>
        /// The complete transaction in hex string format
        /// </summary>
        public byte[] TxBlob { get; private set; }
        /// <summary>
        /// The complete transaction in JSON format
        /// </summary>
        public JsonElement TxJson { get; private set; }

        internal SubmitResponse(JsonElement json)
        {
            EngineResult = (EngineResult)json.GetProperty("engine_result_code").GetInt32();
            var engine_result = json.GetProperty("engine_result").GetString();
            if (engine_result != EngineResult.ToString())
            {
                throw new RippleException($"{EngineResult} did not match {engine_result}");
            }
            EngineResultMessage = json.GetProperty("engine_result_message").GetString();
            TxBlob = json.GetProperty("tx_blob").GetBytesFromBase16();
            TxJson = json.GetProperty("tx_json").Clone();
        }
    }

    public sealed class StWriter
    {
        //System.Text.Json.Utf8JsonWriter
        System.Buffers.IBufferWriter<byte> bufferWriter;

        public StWriter(System.Buffers.IBufferWriter<byte> bufferWriter)
        {
            this.bufferWriter = bufferWriter;
        }

        void WriteFieldId(uint typeCode, uint fieldCode)
        {
            if (typeCode < 16 && fieldCode < 16)
            {
                var span = bufferWriter.GetSpan(1);
                span[0] = (byte)(typeCode << 4 | fieldCode);
                bufferWriter.Advance(1);
            }
            else if (typeCode < 16 && fieldCode >= 16)
            {
                var span = bufferWriter.GetSpan(2);
                span[0] = (byte)(typeCode << 4);
                span[1] = (byte)fieldCode;
                bufferWriter.Advance(2);
            }
            else if (typeCode >= 16 && fieldCode < 16)
            {
                var span = bufferWriter.GetSpan(2);
                span[0] = (byte)fieldCode;
                span[1] = (byte)typeCode;
                bufferWriter.Advance(2);
            }
            else // typeCode >= 16 && fieldCode >= 16
            {
                var span = bufferWriter.GetSpan(3);
                span[0] = 0;
                span[1] = (byte)typeCode;
                span[2] = (byte)fieldCode;
                bufferWriter.Advance(3);
            }
        }

        void WriteLengthPrefix(int length)
        {
            if (length <= 192)
            {
                var span = bufferWriter.GetSpan(1);
                span[0] = (byte)(length);
                bufferWriter.Advance(1);
            }
            else if (length <= 12480)
            {
                var target = length - 193;
                var byte1 = target / 256;
                var byte2 = target - (byte1 * 256);

                // 193 + ((byte1 - 193) * 256) + byte2
                var span = bufferWriter.GetSpan(2);
                span[0] = (byte)(byte1 - 193);
                span[1] = (byte)byte2;
                bufferWriter.Advance(2);
            }
            else
            {
                var target = length - 12481;
                var byte1 = target / 65536;
                var byte2 = (target - (byte1 * 65536)) / 256;
                var byte3 = target - (byte1 * 65536) - (byte2 * 256);

                //12481 + ((byte1 - 241) * 65536) + (byte2 * 256) + byte3
                var span = bufferWriter.GetSpan(3);
                span[0] = (byte)(byte1 - 241);
                span[1] = (byte)byte2;
                span[3] = (byte)byte3;
                bufferWriter.Advance(3);
            }
        }

        public void WriteUInt16(uint fieldCode, ushort value)
        {
            WriteFieldId(1, fieldCode);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(bufferWriter.GetSpan(2), value);
            bufferWriter.Advance(2);
        }

        public void WriteUInt32(uint fieldCode, uint value)
        {
            WriteFieldId(2, fieldCode);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(bufferWriter.GetSpan(4), value);
            bufferWriter.Advance(4);
        }

        public void WriteAmount(uint fieldCode, XrpAmount value)
        {
            WriteFieldId(6, fieldCode);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(bufferWriter.GetSpan(8), value.Drops | 0x4000000000000000);
            bufferWriter.Advance(8);
        }

        public void WriteAmount(uint fieldCode, Amount value)
        {
            WriteFieldId(6, fieldCode);
            if (value.Drops.HasValue)
            {
                System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(bufferWriter.GetSpan(8), value.Drops.Value | 0x4000000000000000);
                bufferWriter.Advance(8);
            }
            else
            {
                var span = bufferWriter.GetSpan(48);
                System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(bufferWriter.GetSpan(8), CurrencyValue.ToUInt64Bits(value.Value.Value));
                value.CurrencyCode.CopyTo(span.Slice(8));
                value.Issuer.Value.CopyTo(span.Slice(28));
                bufferWriter.Advance(48);
            }
        }

        public void WriteVl(uint fieldCode, byte[] value)
        {
            WriteFieldId(7, fieldCode);
            WriteLengthPrefix(value.Length);
            value.CopyTo(bufferWriter.GetSpan(value.Length));
            bufferWriter.Advance(value.Length);
        }

        public void WriteAccount(uint fieldCode, AccountId value)
        {
            WriteFieldId(8, fieldCode);
            WriteLengthPrefix(20);
            value.CopyTo(bufferWriter.GetSpan(20));
            bufferWriter.Advance(20);
        }

        public void WriteHash256(uint fieldCode, Hash256 value)
        {
            WriteFieldId(5, fieldCode);
            value.CopyTo(bufferWriter.GetSpan(32));
            bufferWriter.Advance(32);
        }
    }



    public abstract class Transaction
    {
        /// <summary>
        /// The unique address of the account that initiated the transaction.
        /// </summary>
        public AccountId Account { get; set; }

        /// <summary>
        /// Integer amount of XRP, in drops, to be destroyed as a cost for distributing this transaction to the network. 
        /// Some transaction types have different minimum requirements.
        /// See Transaction Cost for details.
        /// </summary>
        public XrpAmount Fee { get; set; }

        /// <summary>
        /// The sequence number of the account sending the transaction. 
        /// A transaction is only valid if the Sequence number is exactly 1 greater than the previous transaction from the same account.
        /// </summary>
        public uint Sequence { get; set; }

        //TransactionType String UInt16  (Required) The type of transaction.Valid types include: Payment, OfferCreate, OfferCancel, TrustSet, AccountSet, SetRegularKey, SignerListSet, EscrowCreate, EscrowFinish, EscrowCancel, PaymentChannelCreate, PaymentChannelFund, PaymentChannelClaim, and DepositPreauth.
        //AccountTxnID String Hash256 (Optional) Hash value identifying another transaction.If provided, this transaction is only valid if the sending account's previously-sent transaction matches the provided hash.
        //Flags Unsigned Integer UInt32  (Optional) Set of bit-flags for this transaction.
        //LastLedgerSequence Number  UInt32  (Optional; strongly recommended) Highest ledger index this transaction can appear in. Specifying this field places a strict upper limit on how long the transaction can wait to be validated or rejected. See Reliable Transaction Submission for more details.
        //Memos Array of Objects    Array   (Optional) Additional arbitrary information used to identify this transaction.
        //Signers Array   Array   (Optional) Array of objects that represent a multi-signature which authorizes this transaction.
        //SourceTag Unsigned Integer UInt32  (Optional) Arbitrary integer used to identify the reason for this payment, or a sender on whose behalf this transaction is made.Conventionally, a refund should specify the initial payment's SourceTag as the refund payment's DestinationTag.
        //SigningPubKey String  Blob    (Automatically added when signing) Hex representation of the public key that corresponds to the private key used to sign this transaction.If an empty string, indicates a multi-signature is present in the Signers field instead.
        //TxnSignature    String Blob    (Automatically added when signing) The signature that verifies this transaction as originating from the account it says it is from.

        public abstract byte[] Sign(KeyPair keyPair, out Hash256 hash);
    }

    public sealed class AccountSet : Transaction
    {
        /// <summary>
        /// (Optional) The domain that owns this account, the ASCII for the domain in lowercase.
        /// </summary>
        public byte[] Domain { get; set; }

        //ClearFlag Number  UInt32(Optional) Unique identifier of a flag to disable for this account.
        //EmailHash String  Hash128(Optional) Hash of an email address to be used for generating an avatar image.Conventionally, clients use Gravatar to display this image.
        //MessageKey String  Blob    (Optional) Public key for sending encrypted messages to this account.
        //SetFlag Number  UInt32  (Optional) Integer flag to enable for this account.
        //TransferRate Unsigned Integer UInt32  (Optional) The fee to charge when users transfer this account's issued currencies, represented as billionths of a unit. Cannot be more than 2000000000 or less than 1000000000, except for the special case 0 meaning no fee.
        //TickSize Unsigned Integer UInt8   (Optional) Tick size to use for offers involving a currency issued by this address.The exchange rates of those offers is rounded to this many significant digits.Valid values are 3 to 15 inclusive, or 0 to disable. (Requires the TickSize amendment.)

        public override byte[] Sign(KeyPair keyPair, out Hash256 hash)
        {
            var publicKey = keyPair.GetCanonicalPublicKey();
            var buffer = new System.Buffers.ArrayBufferWriter<byte>();
            var writer = new StWriter(buffer);

            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(buffer.GetSpan(4), 0x53545800u);
            buffer.Advance(4);

            writer.WriteUInt16(2, 3);
            writer.WriteUInt32(4, Sequence);
            writer.WriteAmount(8, Fee);
            writer.WriteVl(3, publicKey);
            // Need signature here
            writer.WriteVl(7, Domain);
            writer.WriteAccount(1, Account);

            // Calculate signature and build again
            var signature = keyPair.Sign(buffer.WrittenSpan);
            buffer.Clear();

            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(buffer.GetSpan(4), 0x54584E00u);
            buffer.Advance(4);

            writer.WriteUInt16(2, 3);
            writer.WriteUInt32(4, Sequence);
            writer.WriteAmount(8, Fee);
            writer.WriteVl(3, publicKey);
            writer.WriteVl(4, signature);
            writer.WriteVl(7, Domain);
            writer.WriteAccount(1, Account);

            using (var sha512 = System.Security.Cryptography.SHA512.Create())
            {
                Span<byte> hashSpan = stackalloc byte[64];
                sha512.TryComputeHash(buffer.WrittenSpan, hashSpan, out var bytesWritten);
                hash = new Hash256(hashSpan.Slice(0, 32));
            }

            return buffer.WrittenMemory.Slice(4).ToArray();
        }
    }

    public sealed class Payment : Transaction
    {
        //Amount Currency Amount Amount  The amount of currency to deliver.For non-XRP amounts, the nested field names MUST be lower-case. If the tfPartialPayment flag is set, deliver up to this amount instead.
        //Destination String  Account The unique address of the account receiving the payment.
        //DestinationTag Number  UInt32  (Optional) Arbitrary tag that identifies the reason for the payment to the destination, or a hosted recipient to pay.
        //InvoiceID String  Hash256 (Optional) Arbitrary 256-bit hash representing a specific reason or identifier for this payment.
        //Paths Array of path arrays PathSet (Optional, auto-fillable) Array of payment paths to be used for this transaction.Must be omitted for XRP-to-XRP transactions.
        //SendMax Currency Amount Amount  (Optional) Highest amount of source currency this transaction is allowed to cost, including transfer fees, exchange rates, and slippage . Does not include the XRP destroyed as a cost for submitting the transaction.For non-XRP amounts, the nested field names MUST be lower-case. Must be supplied for cross-currency/cross-issue payments. Must be omitted for XRP-to-XRP payments.
        //DeliverMin Currency Amount Amount  (Optional) Minimum amount of destination currency this transaction should deliver.Only valid if this is a partial payment.For non-XRP amounts, the nested field names are lower-case.

        public Amount Amount { get; set; }
        public AccountId Destination { get; set; }
        public UInt32? DestinationTag { get; set; }
        public Hash256? InvoiceID { get; set; }
        public Amount? SendMax { get; set; }
        public Amount? DeliverMin { get; set; }

        public override byte[] Sign(KeyPair keyPair, out Hash256 hash)
        {
            var publicKey = keyPair.GetCanonicalPublicKey();
            var buffer = new System.Buffers.ArrayBufferWriter<byte>();
            var writer = new StWriter(buffer);

            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(buffer.GetSpan(4), 0x53545800u);
            buffer.Advance(4);

            writer.WriteUInt16(2, 0);
            writer.WriteUInt32(4, Sequence);
            if (DestinationTag.HasValue) { writer.WriteUInt32(14, DestinationTag.Value); }
            if (InvoiceID.HasValue) { writer.WriteHash256(17, InvoiceID.Value); }
            writer.WriteAmount(1, Amount);
            writer.WriteAmount(8, Fee);
            if (SendMax.HasValue) { writer.WriteAmount(9, SendMax.Value); }
            if (DeliverMin.HasValue) { writer.WriteAmount(10, DeliverMin.Value); }
            writer.WriteVl(3, publicKey);
            // Need signature here
            writer.WriteAccount(1, Account);
            writer.WriteAccount(3, Destination);

            // Calculate signature and build again
            var signature = keyPair.Sign(buffer.WrittenSpan);
            buffer.Clear();

            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(buffer.GetSpan(4), 0x54584E00u);
            buffer.Advance(4);


            writer.WriteUInt16(2, 0);
            writer.WriteUInt32(4, Sequence);
            if (DestinationTag.HasValue) { writer.WriteUInt32(14, DestinationTag.Value); }
            if (InvoiceID.HasValue) { writer.WriteHash256(17, InvoiceID.Value); }
            writer.WriteAmount(1, Amount);
            writer.WriteAmount(8, Fee);
            if (SendMax.HasValue) { writer.WriteAmount(9, SendMax.Value); }
            if (DeliverMin.HasValue) { writer.WriteAmount(10, DeliverMin.Value); }
            writer.WriteVl(3, publicKey);
            writer.WriteVl(4, signature);
            writer.WriteAccount(1, Account);
            writer.WriteAccount(3, Destination);

            using (var sha512 = System.Security.Cryptography.SHA512.Create())
            {
                Span<byte> hashSpan = stackalloc byte[64];
                sha512.TryComputeHash(buffer.WrittenSpan, hashSpan, out var bytesWritten);
                hash = new Hash256(hashSpan.Slice(0, 32));
            }

            return buffer.WrittenMemory.Slice(4).ToArray();
        }
    }

    public abstract class TransactionResponse
    {
        /// <summary>
        /// The SHA-512 hash of the transaction
        /// </summary>
        public Hash256 Hash { get; private set; }

        /// <summary>
        /// The ledger index of the ledger that includes this transaction.
        /// </summary>
        public uint? LedgerIndex { get; private set; }

        // meta Object  Various metadata about the transaction.

        /// <summary>
        /// True if this data is from a validated ledger version; if omitted or set to false, this data is not final.
        /// </summary>
        public bool Validated { get; private set; }

        protected TransactionResponse(JsonElement json)
        {
            Hash = new Hash256(json.GetProperty("hash").GetString());
            Validated = json.GetProperty("validated").GetBoolean();
            if (json.TryGetProperty("ledger_index", out var element))
            {
                LedgerIndex = element.GetUInt32();
            }
        }

        internal static TransactionResponse ReadJson(JsonElement json)
        {
            var transactionType = json.GetProperty("TransactionType").GetString();
            if (transactionType == "AccountSet")
            {
                return new AccountSetResponse(json);
            }
            else if (transactionType == "Payment")
            {
                return new PaymentResponse(json);
            }

            throw new NotImplementedException();
        }
    }

    public sealed class AccountSetResponse : TransactionResponse
    {
        /// <summary>
        /// (Optional) The domain that owns this account, the ASCII for the domain in lowercase.
        /// </summary>
        public byte[] Domain { get; private set; }

        internal AccountSetResponse(JsonElement json) : base(json)
        {
            JsonElement element;

            if (json.TryGetProperty("Domain", out element))
            {
                Domain = element.GetBytesFromBase16();
            }
        }
    }
    public sealed class PaymentResponse : TransactionResponse
    {
        public Amount Amount{ get; private set; }

        internal PaymentResponse(JsonElement json) : base(json)
        {
            JsonElement element;

            if (json.TryGetProperty("Amount", out element))
            {
                Amount = new Amount(element);
            }
        }
    }

    public sealed class TransactionEntryRequest
    {
        /// <summary>
        /// Unique hash of the transaction you are looking up.
        /// </summary>
        public Hash256 TxHash { get; set; }

        /// <summary>
        /// A 20-byte hex string, or the ledger index of the ledger to use, or a shortcut string to choose a ledger automatically.
        /// </summary>
        public LedgerSpecification Ledger { get; set; }
    }
}