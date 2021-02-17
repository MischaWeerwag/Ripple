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

        protected static Hash256 CalculateId(ushort addressSpace, ReadOnlySpan<byte> data)
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
            var fieldId = reader.ReadFieldId();
            if(fieldId.TypeCode != StTypeCode.UInt16 || fieldId.FieldCode != 1)
            {
                throw new RippleException(
                    string.Format("Expected LedgerEntryType field, got {0}", fieldId));
            }
            var type = (StLedgerEntryType)reader.ReadUInt16();

            switch(type)
            {
                case StLedgerEntryType.AccountRoot:
                    return new AccountRoot(reader);
            //   case StLedgerEntryTypes.DirectoryNode:
            //       return new DirectoryNode(reader);
            //   case StLedgerEntryTypes.RippleState:
            //       return new RippleState(reader);
            //   case StLedgerEntryTypes.Ticket:
            //       return new Ticket(reader);
            //   case StLedgerEntryTypes.SignerList:
            //       return new SignerList(reader);
            //   case StLedgerEntryTypes.Offer:
            //       return new Offer(reader);
            //   case StLedgerEntryTypes.LedgerHashes:
            //       return new LedgerHashes(reader);
            //   case StLedgerEntryTypes.Amendments:
            //       return new Amendments(reader);
            //   case StLedgerEntryTypes.FeeSettings:
            //       return new FeeSettings(reader);
            //   case StLedgerEntryTypes.Escrow:
            //       return new Escrow(reader);
            //   case StLedgerEntryTypes.PayChannel:
            //       return new PayChannel(reader);
            //   case StLedgerEntryTypes.DepositPreauth:
            //       return new DepositPreauth(reader);
            //   case StLedgerEntryTypes.Check:
            //       return new Check(reader);
            //   case StLedgerEntryTypes.Nickname:
            //       return new Nickname(reader);
            //   case StLedgerEntryTypes.Contract:
            //       return new Contract(reader);
            //   case StLedgerEntryTypes.GeneratorMap:
            //       return new GeneratorMap(reader);
            //   case StLedgerEntryTypes.NegativeUNL:
            //       return new NegativeUNL(reader);
            }

            throw new RippleException(string.Format("Unrecognized ledger entry type: {0}", type));
        }
    }

    /// <summary>
    /// A Check object describes a check, similar to a paper personal check, which can be cashed by its destination to get money from its sender.
    /// (The potential payment has already been approved by its sender, but no money moves until it is cashed. Unlike an Escrow, the money for a Check is not set aside, so cashing the Check could fail due to lack of funds.)
    /// </summary>
    public sealed class Check : LedgerObject
    {
        /// <summary>
        /// The sender of the Check.
        /// Cashing the Check debits this address's balance.
        /// </summary>
        public AccountId Account { get; private set; }

        /// <summary>
        /// The sequence number of the CheckCreate transaction that created this check.
        /// </summary>
        public uint Sequence { get; private set; }

        //LedgerEntryType String  UInt16 The value 0x0043, mapped to the string Check, indicates that this object is a Check object.
        //Destination String  Account The intended recipient of the Check.Only this address can cash the Check, using a CheckCash transaction.
        //Flags   Number UInt32  A bit-map of boolean flags. No flags are defined for Checks, so this value is always 0.
        //OwnerNode String  UInt64 A hint indicating which page of the sender's owner directory links to this object, in case the directory consists of multiple pages. Note: The object does not contain a direct link to the owner directory containing it, since that value can be derived from the Account.
        //PreviousTxnID String  Hash256 The identifying hash of the transaction that most recently modified this object.
        //PreviousTxnLgrSeq Number  UInt32 The index of the ledger that contains the transaction that most recently modified this object.
        //SendMax String or Object    Amount The maximum amount of currency this Check can debit the sender.If the Check is successfully cashed, the destination is credited in the same currency for up to this amount.
        //DestinationNode String  UInt64  (Optional) A hint indicating which page of the destination's owner directory links to this object, in case the directory consists of multiple pages.
        //DestinationTag Number  UInt32  (Optional) An arbitrary tag to further specify the destination for this Check, such as a hosted recipient at the destination address.
        //Expiration Number  UInt32  (Optional) Indicates the time after which this Check is considered expired. See Specifying Time for details.
        //InvoiceID String  Hash256 (Optional) Arbitrary 256-bit hash provided by the sender as a specific reason or identifier for this Check.
        //SourceTag Number  UInt32  (Optional) An arbitrary tag to further specify the source for this Check, such as a hosted recipient at the sender's address.

        public static Hash256 CalculateId(AccountId account, uint sequence)
        {
            Span<byte> buffer = stackalloc byte[20 + 4];
            account.CopyTo(buffer);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(20), sequence);
            return CalculateId(0x0043, buffer);
        }


        public override Hash256 ID => CalculateId(Account, Sequence);


        internal Check(JsonElement json)
        {
            if (json.GetProperty("LedgerEntryType").GetString() != "Check")
            {
                throw new ArgumentException("Expected property \"LedgerEntryType\" to be \"Check\"", "json");
            }
        }

        internal Check(StReader reader)
        {
            while (reader.TryReadFieldId(out var fieldId))
            {
                if (fieldId == StFieldId.AccountID_Account)
                {
                    Account = reader.ReadAccount();
                } 
                else if(fieldId == StFieldId.UInt32_Sequence)
                {
                    Sequence = reader.ReadUInt32();
                }
                else
                {
                    throw new RippleException(
                        string.Format("Got unexpected field {0} while reading Check", fieldId));
                }
            }
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

        internal SignerList(StReader reader)
        {

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
            if (json.GetProperty("LedgerEntryType").GetString() != "AccountRoot")
            {
                throw new ArgumentException("Expected property \"LedgerEntryType\" to be \"AccountRoot\"", "json");
            }

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
            while(reader.TryReadFieldId(out var fieldId))
            {
                if (fieldId == StFieldId.AccountID_Account)
                {
                    Account = reader.ReadAccount();
                }
                else if (fieldId == StFieldId.Amount_Balance)
                {
                    var amount = reader.ReadAmount();
                    var xrpAmount = amount.XrpAmount;
                    if (xrpAmount.HasValue)
                    {
                        Balance = xrpAmount.Value;
                    }
                    else
                    {
                        throw new RippleException(
                            string.Format("Got unexpected issued amount while reading Balance field AccountRoot", amount.IssuedAmount));

                    }
                }
                else if (fieldId == StFieldId.UInt32_Flags)
                {
                    Flags = (AccountRootFlags)reader.ReadUInt32();
                }
                else if (fieldId == StFieldId.UInt32_OwnerCount)
                {
                    OwnerCount = reader.ReadUInt32();
                }
                else if (fieldId == StFieldId.Hash256_PreviousTxnID)
                {
                    PreviousTxnID = reader.ReadHash256();
                }
                else if (fieldId == StFieldId.UInt32_PreviousTxnLgrSeq)
                {
                    PreviousTxnLgrSeq = reader.ReadUInt32();
                }
                else if (fieldId == StFieldId.UInt32_Sequence)
                {
                    Sequence = reader.ReadUInt32();
                }
                else if (fieldId == StFieldId.Hash256_AccountTxnID)
                {
                    AccountTxnID = reader.ReadHash256();
                }
                else if (fieldId == StFieldId.Blob_Domain)
                {
                    Domain = reader.ReadBlob();
                }
                else if (fieldId == StFieldId.Hash128_EmailHash)
                {
                    EmailHash = reader.ReadHash128();
                }
                else if (fieldId == StFieldId.Blob_MessageKey)
                {
                    MessageKey = reader.ReadBlob();
                }
                else if (fieldId == StFieldId.AccountID_RegularKey)
                {
                    RegularKey = reader.ReadAccount();
                }
                else if (fieldId == StFieldId.UInt8_TickSize)
                {
                    TickSize = reader.ReadUInt8();
                }
                else if (fieldId == StFieldId.UInt32_TransferRate)
                {
                    TransferRate = reader.ReadUInt32();
                }
                else
                { 
                    throw new RippleException(
                        string.Format("Got unexpected field {0} while reading AccountRoot", fieldId));
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