using System;
using System.Buffers;

namespace Ibasa.Ripple
{
    public struct StWriter
    {
        readonly IBufferWriter<byte> bufferWriter;

        public StWriter(IBufferWriter<byte> bufferWriter)
        {
            this.bufferWriter = bufferWriter;
        }

        void WriteFieldId(StTypeCode typeCode, uint fieldCode)
        {
            var iTypeCode = (uint)typeCode;
            if (iTypeCode < 16 && fieldCode < 16)
            {
                var span = bufferWriter.GetSpan(1);
                span[0] = (byte)(iTypeCode << 4 | fieldCode);
                bufferWriter.Advance(1);
            }
            else if (iTypeCode < 16 && fieldCode >= 16)
            {
                var span = bufferWriter.GetSpan(2);
                span[0] = (byte)(iTypeCode << 4);
                span[1] = (byte)fieldCode;
                bufferWriter.Advance(2);
            }
            else if (iTypeCode >= 16 && fieldCode < 16)
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
            WriteFieldId(StTypeCode.UInt16, fieldCode);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(bufferWriter.GetSpan(2), value);
            bufferWriter.Advance(2);
        }

        public void WriteUInt32(uint fieldCode, uint value)
        {
            WriteFieldId(StTypeCode.UInt32, fieldCode);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(bufferWriter.GetSpan(4), value);
            bufferWriter.Advance(4);
        }

        public void WriteAmount(uint fieldCode, XrpAmount value)
        {
            WriteFieldId(StTypeCode.Amount, fieldCode);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(bufferWriter.GetSpan(8), value.Drops | 0x4000000000000000);
            bufferWriter.Advance(8);
        }

        public void WriteAmount(uint fieldCode, Amount value)
        {
            WriteFieldId(StTypeCode.Amount, fieldCode);
            var xrp = value.XrpAmount;
            var issued = value.IssuedAmount;
            if (xrp.HasValue)
            {
                var amount = xrp.Value;
                System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(bufferWriter.GetSpan(8), amount.Drops | 0x4000000000000000);
                bufferWriter.Advance(8);
            }
            else
            {
                var amount = issued.Value;
                var span = bufferWriter.GetSpan(48);
                System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(bufferWriter.GetSpan(8), Currency.ToUInt64Bits(amount.Value));
                amount.CurrencyCode.CopyTo(span.Slice(8));
                amount.Issuer.CopyTo(span.Slice(28));
                bufferWriter.Advance(48);
            }
        }

        public void WriteVl(uint fieldCode, byte[] value)
        {
            WriteFieldId(StTypeCode.Vl, fieldCode);
            WriteLengthPrefix(value.Length);
            value.CopyTo(bufferWriter.GetSpan(value.Length));
            bufferWriter.Advance(value.Length);
        }

        public void WriteAccount(uint fieldCode, AccountId value)
        {
            WriteFieldId(StTypeCode.Account, fieldCode);
            WriteLengthPrefix(20);
            value.CopyTo(bufferWriter.GetSpan(20));
            bufferWriter.Advance(20);
        }

        public void WriteHash256(uint fieldCode, Hash256 value)
        {
            WriteFieldId(StTypeCode.Hash256, fieldCode);
            value.CopyTo(bufferWriter.GetSpan(32));
            bufferWriter.Advance(32);
        }
    }

    public enum StTypeCode
    {
        // special types
        Unknown = -2,
        Done = -1,
        NotPresent = 0,

        // // types (common)
        UInt16 = 1,
        UInt32 = 2,
        UInt64 = 3,
        Hash128 = 4,
        Hash256 = 5,
        Amount = 6,
        Vl = 7,
        Account = 8,
        // 9-13 are reserved
        Object = 14,
        Array = 15,

        // types (uncommon)
        UInt8 = 16,
        Hash160 = 17,
        Pathset = 18,
        Vector256 = 19,

        // high level types
        // cannot be serialized inside other types
        Transaction = 10001,
        Ledgerentry = 10002,
        Validation = 10003,
        Metadata = 10004,
    }


    public ref struct StReader
    {
        readonly ReadOnlySpan<byte> data;
        public int ConsumedBytes { get; private set; }

        public StReader(ReadOnlySpan<byte> stData)
        {
            this.data = stData;
            this.ConsumedBytes = 0;
        }

        public bool TryReadFieldId(out StTypeCode typeCode, out uint fieldCode)
        {
            if (data.Length <= ConsumedBytes)
            {
                typeCode = StTypeCode.NotPresent;
                fieldCode = 0;
                return false;
            }

            var byte1 = data[ConsumedBytes];
            if(byte1 == 0)
            {
                if(data.Length <= ConsumedBytes + 2)
                {
                    typeCode = StTypeCode.NotPresent;
                    fieldCode = 0;
                    return false;
                }

                typeCode = (StTypeCode)data[ConsumedBytes + 1];
                fieldCode = data[ConsumedBytes + 2];
                ConsumedBytes += 3;
                return true;
            }
            else if (byte1 < 16)
            {
                if (data.Length <= ConsumedBytes + 1)
                {
                    typeCode = StTypeCode.NotPresent;
                    fieldCode = 0;
                    return false;
                }

                fieldCode = byte1;
                typeCode = (StTypeCode)data[ConsumedBytes + 1];
                ConsumedBytes += 2;
                return true;
            }
            else if (byte1 < 241)
            {
                if (data.Length <= ConsumedBytes + 1)
                {
                    typeCode = StTypeCode.NotPresent;
                    fieldCode = 0;
                    return false;
                }

                typeCode = (StTypeCode)byte1;
                fieldCode = data[ConsumedBytes + 1];
                ConsumedBytes += 2;
                return true;
            }
            else
            {
                typeCode = (StTypeCode)(byte1 >> 4);
                fieldCode = (uint)byte1 & 0xFF;
                ConsumedBytes += 1;
                return true;
            }
        }

        bool TryReadLengthPrefix(out int length)
        {
            if (data.Length <= ConsumedBytes)
            {
                length = 0;
                return false;
            }

            var byte1 = data[ConsumedBytes];
            if(byte1 <= 192)
            {
                ConsumedBytes += 1;
                length = byte1;
                return true;
            } 
            else if(byte1 <= 241)
            {
                if (data.Length <= ConsumedBytes + 1)
                {
                    length = 0;
                    return false;
                }

                var byte2 = data[ConsumedBytes + 1];
                ConsumedBytes += 2;
                length = 192 + ((byte1 - 193) * 256) + byte2;
                return true;
            }
            else
            {
                if (data.Length <= ConsumedBytes + 2)
                {
                    length = 0;
                    return false;
                }

                var byte2 = data[ConsumedBytes + 1];
                var byte3 = data[ConsumedBytes + 3];
                ConsumedBytes += 3;
                length = 12481 + ((byte1 - 241) * 65536) + (byte2 * 256) + byte3;
                return true;
            }
        }

        public bool TryReadUInt8(out byte value)
        {
            if (data.Length < ConsumedBytes + 1)
            {
                value = 0;
                return false;
            }

            value = data[ConsumedBytes];
            ConsumedBytes += 1;
            return true;
        }

        public byte ReadUInt8()
        {
            if (!TryReadUInt8(out var value))
            {
                throw new Exception();
            }
            return value;
        }

        public bool TryReadUInt16(out ushort value)
        {
            if (data.Length < ConsumedBytes + 2)
            {
                value = 0;
                return false;
            }

            value = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(data.Slice(ConsumedBytes, 2));
            ConsumedBytes += 2;
            return true;
        }

        public ushort ReadUInt16()
        {
            if(!TryReadUInt16(out var value))
            {
                throw new Exception();
            }
            return value;
        }

        public bool TryReadUInt32(out uint value)
        {
            if (data.Length < ConsumedBytes + 4)
            {
                value = 0;
                return false;
            }

            value = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(data.Slice(ConsumedBytes, 4));
            ConsumedBytes += 4;
            return true;
        }

        public uint ReadUInt32()
        {
            if (!TryReadUInt32(out var value))
            {
                throw new Exception();
            }
            return value;
        }

        public bool TryReadUInt64(out ulong value)
        {
            if (data.Length < ConsumedBytes + 8)
            {
                value = 0;
                return false;
            }

            value = System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(data.Slice(ConsumedBytes, 8));
            ConsumedBytes += 8;
            return true;
        }

        public ulong ReadUInt64()
        {
            if (!TryReadUInt64(out var value))
            {
                throw new Exception();
            }
            return value;
        }

        public bool TryReadAmount(out Amount value)
        {
            if (data.Length < ConsumedBytes + 8)
            {
                value = default;
                return false;
            }

            var amount = System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(data.Slice(ConsumedBytes, 8));
            if ((amount & 0x8000000000000000) == 0)
            {
                // XRP
                value = new Amount(amount & ~0x4000000000000000u);
                ConsumedBytes += 8;
                return true;
            }
            else
            {
                if (data.Length < ConsumedBytes + 48)
                {
                    value = default;
                    return false;
                }

                var currency = Currency.FromUInt64Bits(amount);
                var code = new CurrencyCode(data.Slice(ConsumedBytes + 8, 20));
                var issuer = new AccountId(data.Slice(ConsumedBytes + 28, 20));
                value = new Amount(issuer, code, currency);
                ConsumedBytes += 48;
                return true;
            }
        }

        public Amount ReadAmount()
        {
            if (!TryReadAmount(out var value))
            {
                throw new Exception();
            }
            return value;
        }

        public bool TryReadVl(out byte[] value)
        {
            if(!TryReadLengthPrefix(out var length))
            {
                value = default;
                return false;
            }

            if (data.Length < ConsumedBytes + length)
            {
                value = default;
                return false;
            }

            value = data.Slice(ConsumedBytes, length).ToArray();
            ConsumedBytes += length;
            return true;
        }

        public byte[] ReadVl()
        {
            if (!TryReadVl(out var value))
            {
                throw new Exception();
            }
            return value;
        }

        public bool TryReadAccount(out AccountId value)
        {
            var currentOffset = ConsumedBytes;
            if(!TryReadLengthPrefix(out var length))
            {
                value = default;
                return false;
            }

            if(length != 20)
            {
                ConsumedBytes = currentOffset;
                value = default;
                return false;
            }

            if (data.Length < ConsumedBytes + 20)
            {
                value = default;
                return false;
            }


            value = new AccountId(data.Slice(ConsumedBytes, 20));
            ConsumedBytes += 20;
            return true;
        }

        public AccountId ReadAccount()
        {
            if (!TryReadAccount(out var value))
            {
                throw new Exception();
            }
            return value;
        }

        public bool TryReadHash256(out Hash256 value)
        {
            if (data.Length < ConsumedBytes + 32)
            {
                value = default;
                return false;
            }

            value = new Hash256(data.Slice(ConsumedBytes, 32));
            ConsumedBytes += 32;
            return true;
        }

        public Hash256 ReadHash256()
        {
            if (!TryReadHash256(out var value))
            {
                throw new Exception();
            }
            return value;
        }
    }
}