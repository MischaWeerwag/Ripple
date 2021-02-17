using System;
using System.Collections.ObjectModel;
using System.Text.Json;
using Ibasa.Ripple.St;

namespace Ibasa.Ripple
{
    public abstract class LedgerObject
    {
        /// <summary>
        /// Each object in a ledger's state data has a unique ID. 
        /// The ID is derived by hashing important contents of the object, along with a namespace identifier.
        /// The ledger object type determines which namespace identifier to use and which contents to include in the hash.
        /// This ensures every ID is unique.
        /// To calculate the hash, rippled uses SHA-512 and then truncates the result to the first 256 bits.
        /// This algorithm, informally called SHA-512Half, provides an output that has comparable security to SHA-256, but runs faster on 64-bit processors.
        /// 
        /// Generally, a ledger object's ID is returned as the index field in JSON, at the same level as the object's contents.
        /// In transaction metadata, the ledger object's ID in JSON is LedgerIndex.
        /// </summary>
        public abstract Hash256 ID { get; }

        internal static Hash256 CalculateId(ushort addressSpace, ReadOnlySpan<byte> data)
        {
            Span<byte> buffer = stackalloc byte[2 + data.Length];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(buffer, addressSpace);
            data.CopyTo(buffer.Slice(2));
            using (var sha512 = System.Security.Cryptography.SHA512.Create())
            {
                Span<byte> hashBuffer = stackalloc byte[64];
                sha512.TryComputeHash(buffer, hashBuffer, out var bytesWritten);
                return new Hash256(hashBuffer);
            }
        }


        internal static LedgerObject FromSt(StReader reader)
        {
            reader.TryReadFieldId(out var typeCode, out var fieldCode);
            if(typeCode != StTypeCode.UInt16 || fieldCode != 1)
            {
                throw new ArgumentException(
                    string.Format("Expected LedgerEntryType field, got ({0}, {1}", typeCode, fieldCode),
                    "reader");
            }
            var type = (StLedgerEntryTypes)reader.ReadUInt16();

            switch(type)
            {
                case StLedgerEntryTypes.AccountRoot:
                    return new AccountRoot(reader);                
            }

            return null;


            //throw new Exception("Not yet implemented");
        }
    }

    /// <summary>
    /// A Check object describes a check, similar to a paper personal check, which can be cashed by its destination to get money from its sender.
    /// (The potential payment has already been approved by its sender, but no money moves until it is cashed. Unlike an Escrow, the money for a Check is not set aside, so cashing the Check could fail due to lack of funds.)
    /// </summary>
    public sealed class Check 
    {
        public static Hash256 ID(AccountId account, uint sequence)
        {
            //The ID of a Check object is the SHA - 512Half of the following values, concatenated in order:
            //The Check space key(0x0043)
            //The AccountID of the sender of the CheckCreate transaction that created the Check object
            //The Sequence number of the CheckCreate transaction that created the Check object
            Span<byte> buffer = stackalloc byte[20 + 4];
            account.CopyTo(buffer);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(20), sequence);
            return LedgerObject.CalculateId(0x0043, buffer);
        }
    }

    /// <summary>
    /// SignerList objects can have the following flag value.
    /// </summary>
    [Flags]
    public enum SignerListFlags : uint
    {
        /// <summary>
        /// If this flag is enabled, this SignerList counts as one item for purposes of the owner reserve.
        /// Otherwise, this list counts as N+2 items, where N is the number of signers it contains.
        /// This flag is automatically enabled if you add or update a signer list after the MultiSignReserve amendment is enabled.
        /// </summary>
        lsfOneOwnerCount = 0x00010000,
    }

    /// <summary>
    /// The SignerList object type represents a list of parties that, as a group, are authorized to sign a transaction in place of an individual account.
    /// You can create, replace, or remove a signer list using a SignerListSet transaction.
    /// </summary>
    public sealed class SignerList
    {
        /// <summary>
        /// A bit-map of Boolean flags enabled for this signer list. 
        /// For more information, see SignerList Flags.
        /// </summary>
        public SignerListFlags Flags { get; private set; }

        /// <summary>
        /// The identifying hash of the transaction that most recently modified this object.
        /// </summary>
        public Hash256 PreviousTxnID { get; private set; }

        /// <summary>
        /// The index of the ledger that contains the transaction that most recently modified this object.
        /// </summary>
        public UInt32 PreviousTxnLgrSeq { get; private set; }

        /// <summary>
        /// A hint indicating which page of the owner directory links to this object, in case the directory consists of multiple pages.
        /// </summary>
        public UInt64 OwnerNode { get; private set; }

        /// <summary>
        /// An array of Signer Entry objects representing the parties who are part of this signer list.
        /// </summary>
        public ReadOnlyCollection<SignerEntry> SignerEntries { get; private set; }

        /// <summary>
        /// An ID for this signer list.
        /// Currently always set to 0.
        /// If a future amendment allows multiple signer lists for an account, this may change.
        /// </summary>
        public UInt32 SignerListID { get; private set; }

        /// <summary>
        /// A target number for signer weights.
        /// To produce a valid signature for the owner of this SignerList, the signers must provide valid signatures whose weights sum to this value or more.
        /// </summary>
        public UInt32 SignerQuorum { get; private set; }

        internal SignerList(JsonElement json)
        {
            if(json.GetProperty("LedgerEntryType").GetString() != "SignerList")
            {
                throw new ArgumentException("Expected property \"LedgerEntryType\" to be \"SignerList\"", "json");
            }

            Flags = (SignerListFlags)json.GetProperty("Flags").GetUInt32();
            PreviousTxnID = new Hash256(json.GetProperty("PreviousTxnID").GetString());
            PreviousTxnLgrSeq = json.GetProperty("PreviousTxnLgrSeq").GetUInt32();
            OwnerNode = ulong.Parse(json.GetProperty("OwnerNode").GetString());
            var signerEntriesJson = json.GetProperty("SignerEntries");
            var signerEntries = new SignerEntry[signerEntriesJson.GetArrayLength()];
            for(int i = 0; i < signerEntries.Length; ++i)
            {
                signerEntries[i] = new SignerEntry(signerEntriesJson[i]);
            }
            SignerEntries = Array.AsReadOnly(signerEntries);
            SignerListID = json.GetProperty("SignerListID").GetUInt32();
            SignerQuorum = json.GetProperty("SignerQuorum").GetUInt32();
        }
    }

    [Flags]
    public enum AccountRootFlags : uint
    {
        None = 0x0,

        /// <summary>
        /// Enable rippling on this addresses's trust lines by default.
        /// Required for issuing addresses; discouraged for others.
        /// </summary>
        DefaultRipple = 0x00800000,
        /// <summary>
        /// This account can only receive funds from transactions it sends, and from preauthorized accounts.
        /// (It has DepositAuth enabled.)
        /// </summary>
        DepositAuth = 0x01000000,
        /// <summary>
        /// Disallows use of the master key to sign transactions for this account.
        /// </summary>
        DisableMaster = 0x00100000,
        /// <summary>
        /// Client applications should not send XRP to this account. Not enforced by rippled.
        /// </summary>
        DisallowXRP = 0x00080000,
        /// <summary>
        /// All assets issued by this address are frozen.
        /// </summary>
        GlobalFreeze = 0x00400000,
        /// <summary>
        /// This address cannot freeze trust lines connected to it. 
        /// Once enabled, cannot be disabled.
        /// </summary>
        NoFreeze = 0x00200000,
        /// <summary>
        /// The account has used its free SetRegularKey transaction.
        /// </summary>
        PasswordSpent = 0x00010000,
        /// <summary>
        /// This account must individually approve other users for those users to hold this account's issued currencies.
        /// </summary>
        RequireAuth = 0x00040000,
        /// <summary>
        /// Requires incoming payments to specify a Destination Tag.
        /// </summary>
        RequireDestTag = 0x00020000,
    }

    /// <summary>
    /// The AccountRoot object type describes a single account, its settings, and XRP balance.
    /// </summary>
    public sealed class AccountRoot : LedgerObject
    {
        /// <summary>
        /// The identifying address of this account, such as rf1BiGeXwwQoi8Z2ueFYTEXSwuJYfV2Jpn.
        /// </summary>
        public AccountId Account { get; private set; }

        /// <summary>
        /// The account's current XRP balance in drops.
        /// </summary>
        public XrpAmount Balance { get; private set; }

        /// <summary>
        /// A bit-map of boolean flags enabled for this account.
        /// </summary>
        public AccountRootFlags Flags { get; private set; }

        /// <summary>
        /// The number of objects this account owns in the ledger, which contributes to its owner reserve.
        /// </summary>
        public uint OwnerCount { get; private set; }

        /// <summary>
        /// The identifying hash of the transaction that most recently modified this object.
        /// </summary>
        public Hash256 PreviousTxnID { get; private set; }

        /// <summary>
        /// The index of the ledger that contains the transaction that most recently modified this object.
        /// </summary>
        public uint PreviousTxnLgrSeq { get; private set; }

        /// <summary>
        /// The sequence number of the next valid transaction for this account. 
        /// (Each account starts with Sequence = 1 and increases each time a transaction is made.)
        /// </summary>
        public uint Sequence { get; private set; }

        /// <summary>
        /// (Optional) The identifying hash of the transaction most recently sent by this account.
        /// This field must be enabled to use the AccountTxnID transaction field.
        /// To enable it, send an AccountSet transaction with the asfAccountTxnID flag enabled.
        /// </summary>
        public Hash256? AccountTxnID { get; private set; }

        /// <summary>
        /// (Optional) A domain associated with this account. In JSON, this is the hexadecimal for the ASCII representation of the domain.
        /// </summary>
        public byte[] Domain { get; private set; }

        /// <summary>
        /// (Optional) The md5 hash of an email address.
        /// Clients can use this to look up an avatar through services such as Gravatar.
        /// </summary>
        public Hash128? EmailHash { get; private set; }

        /// <summary>
        /// (Optional) A public key that may be used to send encrypted messages to this account.
        /// In JSON, uses hexadecimal.
        /// Must be exactly 33 bytes, with the first byte indicating the key type: 0x02 or 0x03 for secp256k1 keys, 0xED for Ed25519 keys.
        /// </summary>
        public byte[] MessageKey { get; private set; }

        /// <summary>
        /// (Optional) The address of a key pair that can be used to sign transactions for this account instead of the master key.
        /// Use a SetRegularKey transaction to change this value.
        /// </summary>
        public AccountId? RegularKey { get; private set; }

        /// <summary>
        /// (Optional) How many significant digits to use for exchange rates of Offers involving currencies issued by this address.
        /// Valid values are 3 to 15, inclusive.
        /// (Added by the TickSize amendment.)
        /// </summary>
        public byte? TickSize { get; private set; }

        /// <summary>
        /// (Optional) A transfer fee to charge other users for sending currency issued by this account to each other.
        /// </summary>
        public uint? TransferRate { get; private set; }

        public override Hash256 ID 
        { 
            get 
            {
                Span<byte> buffer = stackalloc byte[20];
                Account.CopyTo(buffer);
                return CalculateId(0x0061, buffer);
            }
        }

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

        internal AccountRoot(StReader reader)
        {
            while(reader.TryReadFieldId(out var typeCode, out var fieldCode))
            {
                switch(typeCode, fieldCode)
                {
                    case (StTypeCode.Account, 1):
                        Account = reader.ReadAccount();
                        break;
                    case (StTypeCode.Amount, 2):
                        var amount  = reader.ReadAmount();
                        var xrpAmount = amount.XrpAmount;
                        if (xrpAmount.HasValue)
                        {
                            Balance = xrpAmount.Value;
                        }
                        else
                        {
                            throw new ArgumentException(
                                string.Format("Got unexpected issued amount while reading Balance field AccountRoot", amount.IssuedAmount),
                                "reader");

                        }
                        break;
                    case (StTypeCode.UInt32, 2):
                        Flags = (AccountRootFlags)reader.ReadUInt32();
                        break;
                    case (StTypeCode.UInt32, 13):
                        OwnerCount = reader.ReadUInt32();
                        break;
                    case (StTypeCode.Hash256, 5):
                        PreviousTxnID = reader.ReadHash256();
                        break;
                    case (StTypeCode.UInt32, 5):
                        PreviousTxnLgrSeq = reader.ReadUInt32();
                        break;
                    case (StTypeCode.UInt32, 4):
                        Sequence = reader.ReadUInt32();
                        break;
                    case (StTypeCode.Hash256, 9):
                        AccountTxnID = reader.ReadHash256();
                        break;
                    case (StTypeCode.Vl, 7):
                        Domain = reader.ReadVl();
                        break;
                    case (StTypeCode.Hash128, 1):
                        EmailHash = reader.ReadHash128();
                        break;
                    case (StTypeCode.Vl, 2):
                        MessageKey = reader.ReadVl();
                        break;
                    case (StTypeCode.Account, 8):
                        RegularKey = reader.ReadAccount();
                        break;
                    case (StTypeCode.UInt8, 16):
                        TickSize = reader.ReadUInt8();
                        break;
                    case (StTypeCode.UInt32, 11):
                        TransferRate = reader.ReadUInt32();
                        break;
                    
                    default:
                        throw new ArgumentException(
                            string.Format("Got unexpected field ({0}, {1}) while reading AccountRoot", typeCode, fieldCode),
                            "reader");
                }
            }
        }
    }

    public sealed class LedgerHeader
    {
        /// <summary>
        /// The ledger index of the ledger.
        /// </summary>
        public uint Sequence { get; private set; }

        /// <summary>
        /// The SHA-512Half of this ledger's state tree information.
        /// </summary>
        public Hash256 AccountHash { get; private set; }

        /// <summary>
        /// The approximate time this ledger version closed. This value is rounded based on the close_time_resolution.
        /// </summary>
        public DateTimeOffset CloseTime { get; private set; }

        /// <summary>
        /// The ledger_hash value of the previous ledger version that is the direct predecessor of this one.
        /// If there are different versions of the previous ledger index, this indicates from which one the ledger was derived.
        /// </summary>
        public Hash256 ParentHash { get; private set; }

        /// <summary>
        /// The approximate time the parnet ledger version closed.
        /// </summary>
        public DateTimeOffset ParentCloseTime { get; private set; }

        /// <summary>
        /// The total number of drops of XRP owned by accounts in the ledger. 
        /// This omits XRP that has been destroyed by transaction fees. 
        /// The actual amount of XRP in circulation is lower because some accounts are "black holes" whose keys are not known by anyone.
        /// </summary>
        public ulong TotalCoins { get; private set; }

        /// <summary>
        /// The SHA-512Half of the transactions included in this ledger.
        /// </summary>
        public Hash256 TransactionHash { get; private set; }

        /// <summary>
        /// An integer in the range [2,120] indicating the maximum number of seconds by which the close_time could be rounded.
        /// </summary>
        public byte CloseTimeResolution { get; private set; }

        /// <summary>
        /// A bit-map of flags relating to the closing of this ledger.
        /// </summary>
        public int CloseFlags { get; private set; }

        public LedgerHeader(StReader reader)
        {
            Sequence = reader.ReadUInt32();
            TotalCoins = reader.ReadUInt64();
            ParentHash = reader.ReadHash256();
            TransactionHash = reader.ReadHash256();
            AccountHash = reader.ReadHash256();
            ParentCloseTime = Epoch.ToDateTimeOffset(reader.ReadUInt32());
            CloseTime = Epoch.ToDateTimeOffset(reader.ReadUInt32());
            CloseTimeResolution = reader.ReadUInt8();
            CloseFlags = reader.ReadUInt8();
        }
    }
}