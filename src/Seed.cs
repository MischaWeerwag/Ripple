using System;
using System.Diagnostics;

namespace Ibasa.Ripple
{
    public enum KeyType
    {
        Ed25519,
        Secp256k1
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Size = 16)]
    public struct Seed
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
        private KeyType _type;

        private static Span<byte> UnsafeAsSpan(ref Seed seed)
        {
            return System.Runtime.InteropServices.MemoryMarshal.AsBytes(System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref seed._data0, 4));
        }

        public KeyType Type { get { return _type; } }

        public Seed(string base58) : this()
        {
            Span<byte> content = stackalloc byte[19];
            Base58Check.ConvertFrom(base58, content);
            if (content[0] == 0x21)
            {
                _type = KeyType.Secp256k1;
                content.Slice(1, 16).CopyTo(UnsafeAsSpan(ref this));
            }
            else if (content[0] == 0x01 && content[1] == 0xE1 && content[2] == 0x4B)
            {
                _type = KeyType.Ed25519;
                content.Slice(3, 16).CopyTo(UnsafeAsSpan(ref this));
            }
            else
            {
                throw new Exception("Expected prefix of either 0x21 or 0x01, 0xE1, 0x4B");
            }
        }

        public Seed(ReadOnlySpan<byte> entropy, KeyType type) : this()
        {
            if (entropy.Length != 16)
            {
                throw new ArgumentException("entropy must have length of 16", "entropy");
            }

            _type = type;
            entropy.CopyTo(UnsafeAsSpan(ref this));
        }

        public override string ToString()
        {
            if (_type == KeyType.Secp256k1)
            {
                Span<byte> content = stackalloc byte[17];
                content[0] = 0x21;
                UnsafeAsSpan(ref this).CopyTo(content.Slice(1));
                return Base58Check.ConvertTo(content);
            }
            else
            {
                Span<byte> content = stackalloc byte[19];
                content[0] = 0x01;
                content[1] = 0xE1;
                content[2] = 0x4B;
                UnsafeAsSpan(ref this).CopyTo(content.Slice(3));
                return Base58Check.ConvertTo(content);
            }
        }

        private Ed25519KeyPair Ed25519KeyPair()
        {
            var privateKey = new byte[32];
            var span = UnsafeAsSpan(ref this);
            using (var sha512 = System.Security.Cryptography.SHA512.Create())
            {
                Span<byte> destination = stackalloc byte[64];
                var done = sha512.TryComputeHash(span, destination, out var bytesWritten);
                destination.Slice(0, 32).CopyTo(privateKey);
            }

            return new Ed25519KeyPair(privateKey);
        }

        private void Secp256k1KeyPair(out Secp256k1KeyPair rootKeyPair, out Secp256k1KeyPair keyPair)
        {
            Span<byte> rootSource = stackalloc byte[20];
            UnsafeAsSpan(ref this).CopyTo(rootSource);

            var secpSecretBytes = new byte[32];
            var k1Params = Org.BouncyCastle.Asn1.Sec.SecNamedCurves.GetByName("secp256k1");

            using (var sha512 = System.Security.Cryptography.SHA512.Create())
            {
                Span<byte> destination = stackalloc byte[64];

                uint i;
                Org.BouncyCastle.Math.BigInteger secpRootSecret = default;
                for (i = 0; i < uint.MaxValue; ++i)
                {
                    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(rootSource.Slice(16), i);
                    var done = sha512.TryComputeHash(rootSource, destination, out var bytesWritten);
                    destination.Slice(0, 32).CopyTo(secpSecretBytes);

                    secpRootSecret = new Org.BouncyCastle.Math.BigInteger(1, secpSecretBytes);
                    if (secpRootSecret.CompareTo(Org.BouncyCastle.Math.BigInteger.Zero) == 1 && secpRootSecret.CompareTo(k1Params.N) == -1)
                    {
                        break;
                    }
                }

                rootKeyPair = new Secp256k1KeyPair(secpRootSecret);

                // Calculate intermediate
                Span<byte> intermediateSource = stackalloc byte[41];
                rootKeyPair.GetCanonicalPublicKey().CopyTo(intermediateSource);
                System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(intermediateSource.Slice(33), 0);

                Org.BouncyCastle.Math.BigInteger secpIntermediateSecret = default;
                for (i = 0; i < uint.MaxValue; ++i)
                {
                    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(intermediateSource.Slice(37), i);
                    var done = sha512.TryComputeHash(intermediateSource, destination, out var bytesWritten);
                    destination.Slice(0, 32).CopyTo(secpSecretBytes);

                    secpIntermediateSecret = new Org.BouncyCastle.Math.BigInteger(1, secpSecretBytes);
                    if (secpIntermediateSecret.CompareTo(Org.BouncyCastle.Math.BigInteger.Zero) == 1 && secpIntermediateSecret.CompareTo(k1Params.N) == -1)
                    {
                        break;
                    }
                }

                var masterPrivateKey = secpRootSecret.Add(secpIntermediateSecret).Mod(k1Params.N);
                keyPair = new Secp256k1KeyPair(masterPrivateKey);
            }
        }

        public void KeyPair(out KeyPair rootKeyPair, out KeyPair keyPair)
        {
            if (_type == KeyType.Secp256k1)
            {
                Secp256k1KeyPair(out var secpRootKeyPair, out var secpKeyPair);
                rootKeyPair = secpRootKeyPair;
                keyPair = secpKeyPair;
            }
            else
            {
                rootKeyPair = null;
                keyPair = Ed25519KeyPair();
            }
        }

        public void CopyTo(Span<byte> buffer)
        {
            var span = UnsafeAsSpan(ref this);
            span.CopyTo(buffer);
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
            return this._type == other._type;
        }

        public override int GetHashCode()
        {
            var hash = new System.HashCode();
            hash.Add(_type);
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
}
