using System;

namespace Ibasa.Ripple
{
    public abstract class PublicKey
    {
        public abstract bool Verify(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature);

        public abstract byte[] GetCanoncialBytes();
    }

    public sealed class Secp256k1PublicKey : PublicKey
    {
        private readonly Org.BouncyCastle.Crypto.Signers.ECDsaSigner signer;
        private readonly Org.BouncyCastle.Math.EC.ECPoint publicKey;

        internal Secp256k1PublicKey(Org.BouncyCastle.Asn1.X9.X9ECParameters k1Params, Org.BouncyCastle.Math.EC.ECPoint publicKey)
        {
            this.publicKey = publicKey;
            this.signer = new Org.BouncyCastle.Crypto.Signers.ECDsaSigner(
                new Org.BouncyCastle.Crypto.Signers.HMacDsaKCalculator(
                    new Org.BouncyCastle.Crypto.Digests.Sha256Digest()));
            var parameters = new Org.BouncyCastle.Crypto.Parameters.ECPublicKeyParameters(
                this.publicKey,
                new Org.BouncyCastle.Crypto.Parameters.ECDomainParameters(k1Params.Curve, k1Params.G, k1Params.N, k1Params.H));
            signer.Init(false, parameters);
        }

        public override byte[] GetCanoncialBytes()
        {
            return publicKey.GetEncoded(true);
        }

        public override bool Verify(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature)
        {
            using (var sha512 = System.Security.Cryptography.SHA512.Create())
            {
                Span<byte> hashSpan = stackalloc byte[64];
                sha512.TryComputeHash(data, hashSpan, out var bytesWritten);
                var hash256 = hashSpan.Slice(0, 32).ToArray();

                var parser = new Org.BouncyCastle.Asn1.Asn1StreamParser(signature.ToArray());
                var derSequence = parser.ReadObject() as Org.BouncyCastle.Asn1.DerSequenceParser;
                var r = derSequence.ReadObject() as Org.BouncyCastle.Asn1.DerInteger;
                var s = derSequence.ReadObject() as Org.BouncyCastle.Asn1.DerInteger;

                return signer.VerifySignature(hash256, r.Value, s.Value);
            }
        }
    }

    public sealed class Ed25519PublicKey : PublicKey
    {
        private readonly Org.BouncyCastle.Crypto.Signers.Ed25519Signer signer;
        private readonly Org.BouncyCastle.Crypto.Parameters.Ed25519PublicKeyParameters publicKey;

        internal Ed25519PublicKey(Org.BouncyCastle.Crypto.Parameters.Ed25519PublicKeyParameters publicKey)
        {
            this.publicKey = publicKey;
            this.signer = new Org.BouncyCastle.Crypto.Signers.Ed25519Signer();
            this.signer.Init(false, this.publicKey);
        }

        public override byte[] GetCanoncialBytes()
        {
            var key = new byte[33];
            key[0] = 0xed;
            publicKey.Encode(key, 1);
            return key;
        }

        public override bool Verify(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature)
        {
            var dataArray = data.ToArray();
            signer.BlockUpdate(dataArray, 0, dataArray.Length);
            return signer.VerifySignature(signature.ToArray());
        }
    }

    public abstract class KeyPair
    {
        public abstract byte[] GetPrivateKey();

        public abstract PublicKey PublicKey { get; }

        public abstract byte[] Sign(ReadOnlySpan<byte> data);
    }

    public sealed class Secp256k1KeyPair : KeyPair
    {
        private readonly Org.BouncyCastle.Math.BigInteger privateKey;
        private readonly Org.BouncyCastle.Asn1.X9.X9ECParameters k1Params;
        private readonly Org.BouncyCastle.Crypto.Signers.ECDsaSigner signer;
        private readonly Secp256k1PublicKey publicKey;

        internal Secp256k1KeyPair(Org.BouncyCastle.Math.BigInteger privateKey)
        {
            this.privateKey = privateKey;

            this.k1Params = Org.BouncyCastle.Asn1.Sec.SecNamedCurves.GetByName("secp256k1");
            this.publicKey = new Secp256k1PublicKey(k1Params, k1Params.G.Multiply(privateKey));

            signer = new Org.BouncyCastle.Crypto.Signers.ECDsaSigner(
                new Org.BouncyCastle.Crypto.Signers.HMacDsaKCalculator(
                    new Org.BouncyCastle.Crypto.Digests.Sha256Digest()));
            var parameters = new Org.BouncyCastle.Crypto.Parameters.ECPrivateKeyParameters(
                this.privateKey,
                new Org.BouncyCastle.Crypto.Parameters.ECDomainParameters(k1Params.Curve, k1Params.G, k1Params.N, k1Params.H));
            signer.Init(true, parameters);
        }

        public override byte[] GetPrivateKey()
        {
            return privateKey.ToByteArray();
        }

        public override PublicKey PublicKey => publicKey;

        public override byte[] Sign(ReadOnlySpan<byte> data)
        {
            using (var sha512 = System.Security.Cryptography.SHA512.Create())
            {
                Span<byte> hashSpan = stackalloc byte[64];
                sha512.TryComputeHash(data, hashSpan, out var bytesWritten);
                var hash256 = hashSpan.Slice(0, 32).ToArray();

                var signatures = signer.GenerateSignature(hash256);
                var r = signatures[0];
                var s = signatures[1];
                var sprime = k1Params.N.Subtract(s);
                if (s.CompareTo(sprime) == 1)
                {
                    s = sprime;
                }

                var bos = new System.IO.MemoryStream(72);
                var generator = new Org.BouncyCastle.Asn1.DerSequenceGenerator(bos);
                generator.AddObject(new Org.BouncyCastle.Asn1.DerInteger(r));
                generator.AddObject(new Org.BouncyCastle.Asn1.DerInteger(s));
                generator.Close();
                return bos.ToArray();
            }
        }
    }

    public sealed class Ed25519KeyPair : KeyPair
    {
        private Ed25519PublicKey publicKey;
        private Org.BouncyCastle.Crypto.Parameters.Ed25519PrivateKeyParameters privateKey;
        private Org.BouncyCastle.Crypto.Signers.Ed25519Signer signer;

        internal Ed25519KeyPair(byte[] privateKey)
        {
            this.privateKey = new Org.BouncyCastle.Crypto.Parameters.Ed25519PrivateKeyParameters(privateKey, 0);
            this.publicKey = new Ed25519PublicKey(this.privateKey.GeneratePublicKey());
            signer = new Org.BouncyCastle.Crypto.Signers.Ed25519Signer();
            signer.Init(true, this.privateKey);
        }

        public override byte[] GetPrivateKey()
        {
            var key = new byte[32];
            privateKey.Encode(key, 0);
            return key;
        }

        public override PublicKey PublicKey => publicKey;

        public override byte[] Sign(ReadOnlySpan<byte> data)
        {
            var dataArray = data.ToArray();
            signer.BlockUpdate(dataArray, 0, dataArray.Length);
            return signer.GenerateSignature();
        }
    }
}
