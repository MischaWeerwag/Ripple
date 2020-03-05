using System;
using System.Text.Json;

namespace Ibasa.Ripple
{
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Size = 20)]
    public struct AccountID
    {
        readonly ulong a;
        readonly ulong b;
        readonly uint c;

        public AccountID(string base58) : this()
        {
            Span<byte> content = stackalloc byte[21];
            Base58Check.ConvertFrom(base58, content);
            if (content[0] != 0x0)
            {
                throw new Exception("Expected 0x0 prefix byte");
            }
            a = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(content.Slice(1));
            b = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(content.Slice(9));
            c = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(content.Slice(17));
        }

        public override string ToString()
        {
            Span<byte> content = stackalloc byte[21];

            content[0] = 0x0;
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(content.Slice(1), a);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(content.Slice(9), b);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(content.Slice(17), c);

            return Base58Check.ConvertTo(content);
        }
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Size = 20)]
    public struct CurrencyCode : IEquatable<CurrencyCode>
    {
        readonly ulong a;
        readonly ulong b;
        readonly uint c;

        public static readonly CurrencyCode XRP = new CurrencyCode();

        public CurrencyCode(string code) : this()
        {
            var bytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref this, 1));
            if (code.Length == 3)
            {
                // Standard Currency Code
                
                for(var i = 0; i < 3; ++i)
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

                if(bytes[0] == 0x0)
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
                return (a & 0xFF00000000000000UL) == 0;
            }
        }

        public override string ToString()
        {
            var bytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref this, 1));
            if (bytes[0] == 0x0)
            {
                // Standard Currency Code
                return System.Text.Encoding.ASCII.GetString(bytes.Slice(12, 3));
            } 
            else
            {
                // Nonstandard Currency Code
                Span<char> chars = stackalloc char[bytes.Length * 2];
                
                for (int i = 0; i < bytes.Length; i++)
                {
                    var b = bytes[i] >> 4;
                    chars[i * 2] = (char)(55 + b + (((b - 10) >> 31) & -7));
                    b = bytes[i] & 0xF;
                    chars[i * 2 + 1] = (char)(55 + b + (((b - 10) >> 31) & -7));
                }

                return new string(chars);
            }

        }

        public override bool Equals(object obj)
        {
            if(obj is CurrencyCode)
            {
                return Equals((CurrencyCode)obj);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(a, b, c);
        }

        public bool Equals(CurrencyCode other)
        {
            return a == other.a && b == other.b && c == other.c;
        }
    }

    /// <summary>
    /// The "Amount" type is a special field type that represents an amount of currency, either XRP or an issued currency.
    /// </summary>
    public struct Amount
    {
        private ulong amount;
        private CurrencyCode code;
        private AccountID account;

        public Amount(ulong drops)
        {
            if((drops & 0x1FFFFFFFFFFFFFFUL) != 0)
            {
                throw new ArgumentOutOfRangeException("drops", drops, "drops must be less than 144,115,188,075,855,872");
            }
            this.amount = 0x4000000000000000UL | drops;
            this.code = CurrencyCode.XRP;
            this.account = new AccountID();
        }
    }


    public sealed class AccountRoot
    {
        //LedgerEntryType String  UInt16 The value 0x0061, mapped to the string AccountRoot, indicates that this is an AccountRoot object.
        //Flags Number  UInt32 A bit-map of boolean flags enabled for this account.
        //PreviousTxnID String  Hash256 The identifying hash of the transaction that most recently modified this object.
        //PreviousTxnLgrSeq Number  UInt32 The index of the ledger that contains the transaction that most recently modified this object.
        //Sequence Number  UInt32 The sequence number of the next valid transaction for this account. (Each account starts with Sequence = 1 and increases each time a transaction is made.)
        //AccountTxnID String Hash256 (Optional) The identifying hash of the transaction most recently sent by this account.This field must be enabled to use the AccountTxnID transaction field.To enable it, send an AccountSet transaction with the asfAccountTxnID flag enabled.
        //Domain String  VariableLength  (Optional) A domain associated with this account.In JSON, this is the hexadecimal for the ASCII representation of the domain.
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
        public AccountID Account { get; private set; }

        /// <summary>
        /// The account's current XRP balance in drops.
        /// </summary>
        public ulong Balance { get; private set; }

        /// <summary>
        /// The number of objects this account owns in the ledger, which contributes to its owner reserve.
        /// </summary>
        public uint OwnerCount { get; private set; }

        internal AccountRoot(JsonElement json)
        {
            Account = new AccountID(json.GetProperty("Account").GetString());
            Balance = ulong.Parse(json.GetProperty("Balance").GetString());
            OwnerCount = json.GetProperty("OwnerCount").GetUInt32();
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

    public static class Epoch
    {
        private static DateTime epoch = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static uint FromDateTime(DateTime dateTime)
        {
            var utc = dateTime.ToUniversalTime();
            if (utc < epoch)
            {
                throw new ArgumentOutOfRangeException("dateTime", "dateTime is before the ripple epoch of 2000-01-01");
            }

            return (uint)(utc.Subtract(epoch).TotalSeconds);
        }
        public static DateTime ToDateTime(uint timestamp)
        {
            return epoch.AddSeconds(timestamp);
        }
    }


    public sealed class LedgerResponse
    {
        public Hash256 LedgerHash { get; private set; }

        internal LedgerResponse(JsonElement json)
        {
            LedgerHash = new Hash256(json.GetProperty("ledger_hash").GetString());
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
    }

    public struct LedgerSpecification
    {
        private int index;
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

        public LedgerSpecification(int index)
        {
            if (index < 1)
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
        public string Account { get; set; }

        /// <summary>
        /// A 20-byte hex strinh, or the ledger index of the ledger to use, or a shortcut string to choose a ledger automatically.
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
        /// A 20-byte hex strinh, or the ledger index of the ledger to use, or a shortcut string to choose a ledger automatically.
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
        public ulong BaseFee { get; private set; }

        /// <summary>
        /// An approximation of the median transaction cost among transactions included in the previous validated ledger, represented in drops of XRP.
        /// </summary>
        public ulong MedianFee { get; private set; }

        /// <summary>
        /// The minimum transaction cost for a reference transaction to be queued for a later ledger, represented in drops of XRP.
        /// If greater than base_fee, the transaction queue is full.
        /// </summary>
        public ulong MinimumFee { get; private set; }

        /// <summary>
        /// The minimum transaction cost that a reference transaction must pay to be included in the current open ledger, represented in drops of XRP.
        /// </summary>
        public ulong OpenLedgerFee { get; private set; }

        internal FeeResponseDrops(JsonElement json)
        {
            BaseFee = ulong.Parse(json.GetProperty("base_fee").GetString());
            MedianFee = ulong.Parse(json.GetProperty("median_fee").GetString());
            MinimumFee = ulong.Parse(json.GetProperty("minimum_fee").GetString());
            OpenLedgerFee = ulong.Parse(json.GetProperty("open_ledger_fee").GetString());
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

    }

    public sealed class AccountCurrenciesRequest
    {

    }
}