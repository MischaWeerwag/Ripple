using System;
using System.Buffers;
using System.Text.Json;
using Ibasa.Ripple.St;

namespace Ibasa.Ripple
{
    public struct Signer
    {
        public AccountId Account;
        public byte[] SigningPubKey;
        public byte[] TxnSignature;
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

        /// <summary>
        /// (Automatically added when signing) Hex representation of the public key that corresponds to the private key used to sign this transaction.
        /// If an empty string, indicates a multi-signature is present in the Signers field instead.
        /// </summary>
        public byte[] SigningPubKey { get; set; }

        /// <summary>
        /// (Optional) Array of objects that represent a multi-signature which authorizes this transaction.
        /// </summary>
        public Signer[] Signers { get; set; }

        /// <summary>
        /// (Automatically added when signing) The signature that verifies this transaction as originating from the account it says it is from.
        /// </summary>
        public byte[] TxnSignature { get; set; }

        /// <summary>
        /// (Optional; strongly recommended) Highest ledger index this transaction can appear in.
        /// Specifying this field places a strict upper limit on how long the transaction can wait to be validated or rejected.
        /// See Reliable Transaction Submission for more details.
        /// </summary>
        public UInt32? LastLedgerSequence { get; set; }

        //TransactionType String UInt16  (Required) The type of transaction.Valid types include: Payment, OfferCreate, OfferCancel, TrustSet, AccountSet, SetRegularKey, SignerListSet, EscrowCreate, EscrowFinish, EscrowCancel, PaymentChannelCreate, PaymentChannelFund, PaymentChannelClaim, and DepositPreauth.
        //AccountTxnID String Hash256 (Optional) Hash value identifying another transaction.If provided, this transaction is only valid if the sending account's previously-sent transaction matches the provided hash.
        //Flags Unsigned Integer UInt32  (Optional) Set of bit-flags for this transaction.

        //Memos Array of Objects    Array   (Optional) Additional arbitrary information used to identify this transaction.
        //SourceTag Unsigned Integer UInt32  (Optional) Arbitrary integer used to identify the reason for this payment, or a sender on whose behalf this transaction is made.Conventionally, a refund should specify the initial payment's SourceTag as the refund payment's DestinationTag.

        public Transaction()
        {

        }

        internal Transaction(JsonElement json)
        {
            Account = new AccountId(json.GetProperty("Account").GetString());

            var fee = json.GetProperty("Fee");
            if (fee.ValueKind == JsonValueKind.Number)
            {
                Fee = XrpAmount.FromDrops(fee.GetUInt64());
            } 
            else 
            {
                Fee = XrpAmount.FromDrops(ulong.Parse(fee.GetString()));
            }
            JsonElement element;
            Sequence = json.GetProperty("Sequence").GetUInt32();
            if (json.TryGetProperty("SigningPubKey", out element))
            {
                SigningPubKey = element.GetBytesFromBase16();
            }
            if (json.TryGetProperty("TxnSignature", out element))
            {
                TxnSignature = element.GetBytesFromBase16();
            }
            if(json.TryGetProperty("LastLedgerSequence", out element))
            {
                LastLedgerSequence = element.GetUInt32();
            }
        }

        public ReadOnlyMemory<byte> Serialize(bool forSigning)
        {
            var bufferWriter = new System.Buffers.ArrayBufferWriter<byte>();
            Serialize(bufferWriter, forSigning);
            return bufferWriter.WrittenMemory;
        }

        public abstract void Serialize(IBufferWriter<byte> bufferWriter, bool forSigning);

        protected void WriteSigner(StWriter writer, bool forSigning)
        {
            writer.WriteBlob(3, SigningPubKey);
            if (TxnSignature != null)
            {
                if (!forSigning)
                {
                    writer.WriteBlob(4, TxnSignature);
                }
            }
        }

        protected void WriteSigners(StWriter writer, bool forSigning)
        {
            if (Signers != null)
            {
                if (!forSigning)
                {
                    writer.WriteStartArray(StArrayFieldCode.Signers);
                    foreach (var signer in Signers)
                    {
                        writer.WriteStartObject(StObjectFieldCode.Signer);

                        writer.WriteBlob(3, signer.SigningPubKey);
                        writer.WriteBlob(4, signer.TxnSignature);
                        writer.WriteAccount(StAccountIDFieldCode.Account, signer.Account);

                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray();
                }
            }
        }

        private uint hpTXN = 0x54584E00u; // TXN
        private uint hpSTX = 0x53545800u; // STX
        private uint hpSMT = 0x534D5400u; // SMT

        public ReadOnlyMemory<byte> Sign(KeyPair keyPair, out Hash256 hash)
        {
            Signers = null;
            TxnSignature = null;
            SigningPubKey = keyPair.GetCanonicalPublicKey();
            var bufferWriter = new ArrayBufferWriter<byte>();

            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(bufferWriter.GetSpan(4), hpSTX);
            bufferWriter.Advance(4);
            Serialize(bufferWriter, true);

            // Calculate signature and serialize again
            TxnSignature = keyPair.Sign(bufferWriter.WrittenSpan);
            bufferWriter.Clear();

            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(bufferWriter.GetSpan(4), hpTXN);
            bufferWriter.Advance(4);

            Serialize(bufferWriter, false);

            using (var sha512 = System.Security.Cryptography.SHA512.Create())
            {
                Span<byte> hashSpan = stackalloc byte[64];
                sha512.TryComputeHash(bufferWriter.WrittenSpan, hashSpan, out var bytesWritten);
                hash = new Hash256(hashSpan.Slice(0, 32));
            }

            return bufferWriter.WrittenMemory.Slice(4);
        }

        public ReadOnlyMemory<byte> Sign(ReadOnlySpan<ValueTuple<AccountId, KeyPair>> signers, out Hash256 hash)
        {
            Signers = new Signer[signers.Length];
            for (int i = 0; i < signers.Length; ++i)
            {
                var signer = new Signer();
                signer.Account = signers[i].Item1;
                signer.SigningPubKey = signers[i].Item2.GetCanonicalPublicKey();
                signer.TxnSignature = null;
                Signers[i] = signer;
            }
            TxnSignature = null;
            SigningPubKey = null;
            var bufferWriter = new ArrayBufferWriter<byte>();

            // Calculate signatures and then serialize again
            for(int i = 0; i < signers.Length; ++i)
            {
                // For each signer we need to write the account being signed for the the buffer, sign that then rewind
                System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(bufferWriter.GetSpan(4), hpSMT);
                bufferWriter.Advance(4);
                Serialize(bufferWriter, true);
                Signers[i].Account.CopyTo(bufferWriter.GetSpan(20));
                bufferWriter.Advance(20);

                Signers[i].TxnSignature = signers[i].Item2.Sign(bufferWriter.WrittenSpan);
                bufferWriter.Clear();
            }

            Array.Sort<Signer>(Signers, (x, y) => x.Account.CompareTo(y.Account));


            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(bufferWriter.GetSpan(4), hpTXN);
            bufferWriter.Advance(4);

            Serialize(bufferWriter, false);

            using (var sha512 = System.Security.Cryptography.SHA512.Create())
            {
                Span<byte> hashSpan = stackalloc byte[64];
                sha512.TryComputeHash(bufferWriter.WrittenSpan, hashSpan, out var bytesWritten);
                hash = new Hash256(hashSpan.Slice(0, 32));
            }

            return bufferWriter.WrittenMemory.Slice(4);
        }

        public static Transaction ReadJson(JsonElement json)
        {
            var transactionType = json.GetProperty("TransactionType").GetString();
            Transaction transaction;
            if (transactionType == "AccountSet")
            {
                return new AccountSet(json);
            }
            else if (transactionType == "Payment")
            {
                return new Payment(json);
            }
            else if (transactionType == "TrustSet")
            {
                return new TrustSet(json);
            }
            else if (transactionType == "SetRegularKey")
            {
                return new SetRegularKey(json);
            }
            else if (transactionType == "AccountDelete")
            {
                return new AccountDelete(json);
            }
            else if (transactionType == "SignerListSet")
            {
                return new SignerListSet(json);
            }
            else if (transactionType == "CheckCreate")
            {
                return new CheckCreate(json);
            }
            else if (transactionType == "CheckCancel")
            {
                return new CheckCancel(json);
            }
            else if (transactionType == "CheckCash")
            {
                return new CheckCash(json);
            }
            else
            {
                throw new NotImplementedException(
                    string.Format("Transaction type '{0}' not implemented", transactionType));
            }
        }
    }

    /// <summary>
    /// A SetRegularKey transaction assigns, changes, or removes the regular key pair associated with an account.
    /// 
    /// You can protect your account by assigning a regular key pair to it and using it instead of the master key pair to sign transactions whenever possible.
    /// If your regular key pair is compromised, but your master key pair is not, you can use a SetRegularKey transaction to regain control of your account.
    /// </summary>
    public sealed class SetRegularKey : Transaction
    {
        /// <summary>
        /// A base-58-encoded Address that indicates the regular key pair to be assigned to the account.
        /// If omitted, removes any existing regular key pair from the account.
        /// Must not match the master key pair for the address.
        /// </summary>
        public AccountId? RegularKey { get; set; }

        public SetRegularKey()
        {

        }

        internal SetRegularKey(JsonElement json) : base(json)
        {
            if (json.TryGetProperty("RegularKey", out var element))
            {
                RegularKey = new AccountId(element.GetString());
            }
        }

        public override void Serialize(IBufferWriter<byte> bufferWriter, bool forSigning)
        {
            var writer = new StWriter(bufferWriter);
            writer.WriteTransactionType(StTransactionType.SetRegularKey);
            writer.WriteUInt32(StUInt32FieldCode.Sequence, Sequence);
            if (LastLedgerSequence.HasValue) { writer.WriteUInt32(StUInt32FieldCode.LastLedgerSequence, LastLedgerSequence.Value); }
            writer.WriteAmount(8, Fee);
            WriteSigner(writer, forSigning);
            writer.WriteAccount(StAccountIDFieldCode.Account, Account);
            if (RegularKey.HasValue) { writer.WriteAccount(StAccountIDFieldCode.RegularKey, RegularKey.Value); }
            WriteSigners(writer, forSigning);
        }
    }

    [Flags]
    public enum AccountSetFlags : uint
    {
        /// <summary>
        /// Require a destination tag to send transactions to this account.
        /// </summary>
        RequireDest = 1,

        /// <summary>
        /// Require authorization for users to hold balances issued by this address. Can only be enabled if the address has no trust lines connected to it.
        /// </summary>
        RequireAuth = 2,

        /// <summary>
        /// XRP should not be sent to this account. (Enforced by client applications, not by rippled)
        /// </summary>
        DisallowXRP = 3,

        /// <summary>
        /// Disallow use of the master key pair. Can only be enabled if the account has configured another way to sign transactions, such as a Regular Key or a Signer List.
        /// </summary>
        DisableMaster = 4,

        /// <summary>
        /// Track the ID of this account's most recent transaction. Required for AccountTxnID
        /// </summary>
        AccountTxnID = 5,

        /// <summary>
        /// Permanently give up the ability to freeze individual trust lines or disable Global Freeze. This flag can never be disabled after being enabled.
        /// </summary>
        NoFreeze = 6,

        /// <summary>
        /// Freeze all assets issued by this account.
        /// </summary>
        GlobalFreeze = 7,

        /// <summary>
        /// Enable rippling on this account's trust lines by default. New in: rippled 0.27.3
        /// </summary>
        DefaultRipple = 8,

        /// <summary>
        /// Enable Deposit Authorization on this account. (Added by the DepositAuth amendment.)
        /// </summary>
        DepositAuth = 9,
    }

    public sealed class AccountSet : Transaction
    {
        /// <summary>
        /// (Optional) The domain that owns this account, the ASCII for the domain in lowercase.
        /// </summary>
        public ReadOnlyMemory<byte>? Domain { get; set; }

        /// <summary>
        /// (Optional) Unique identifier of a flag to disable for this account.
        /// </summary>
        public AccountSetFlags? ClearFlag { get; set; }

        /// <summary>
        /// (Optional) Integer flag to enable for this account.
        /// </summary>
        public AccountSetFlags? SetFlag { get; set; }

        //EmailHash String  Hash128(Optional) Hash of an email address to be used for generating an avatar image.Conventionally, clients use Gravatar to display this image.
        //MessageKey String  Blob    (Optional) Public key for sending encrypted messages to this account.
        //TransferRate Unsigned Integer UInt32  (Optional) The fee to charge when users transfer this account's issued currencies, represented as billionths of a unit. Cannot be more than 2000000000 or less than 1000000000, except for the special case 0 meaning no fee.
        //TickSize Unsigned Integer UInt8   (Optional) Tick size to use for offers involving a currency issued by this address.The exchange rates of those offers is rounded to this many significant digits.Valid values are 3 to 15 inclusive, or 0 to disable. (Requires the TickSize amendment.)
        
        public AccountSet()
        {

        }

        internal AccountSet(JsonElement json) : base(json)
        {
            JsonElement element;
            if (json.TryGetProperty("Domain", out element))
            {
                Domain = element.GetBytesFromBase16();
            }

            if (json.TryGetProperty("ClearFlag", out element))
            {
                ClearFlag = (AccountSetFlags)element.GetUInt32();
            }

            if (json.TryGetProperty("SetFlag", out element))
            {
                SetFlag = (AccountSetFlags)element.GetUInt32();
            }
        }

        public override void Serialize(IBufferWriter<byte> bufferWriter, bool forSigning)
        {
            var writer = new StWriter(bufferWriter);
            writer.WriteTransactionType(StTransactionType.AccountSet);
            writer.WriteUInt32(StUInt32FieldCode.Sequence, Sequence);
            if (LastLedgerSequence.HasValue) { writer.WriteUInt32(StUInt32FieldCode.LastLedgerSequence, LastLedgerSequence.Value); }
            if (SetFlag.HasValue)
            {
                writer.WriteUInt32(StUInt32FieldCode.SetFlag, (uint)SetFlag.Value);
            }
            if (ClearFlag.HasValue)
            {
                writer.WriteUInt32(StUInt32FieldCode.ClearFlag, (uint)ClearFlag.Value);
            }
            writer.WriteAmount(8, Fee);
            WriteSigner(writer, forSigning);
            if (Domain.HasValue) { writer.WriteBlob(7, Domain.Value.Span); }
            writer.WriteAccount(StAccountIDFieldCode.Account, Account);
            WriteSigners(writer, forSigning);
        }
    }

    public sealed class Payment : Transaction
    {
        //TODO Paths support
        //Paths Array of path arrays PathSet (Optional, auto-fillable) Array of payment paths to be used for this transaction.Must be omitted for XRP-to-XRP transactions.=

        /// <summary>
        /// The amount of currency to deliver.
        /// If the tfPartialPayment flag is set, deliver up to this amount instead.
        /// </summary>
        public Amount Amount { get; set; }
        /// <summary>
        /// The unique address of the account receiving the payment.
        /// </summary>
        public AccountId Destination { get; set; }
        /// <summary>
        /// (Optional) Arbitrary tag that identifies the reason for the payment to the destination, or a hosted recipient to pay.
        /// </summary>
        public UInt32? DestinationTag { get; set; }
        /// <summary>
        /// (Optional) Arbitrary 256-bit hash representing a specific reason or identifier for this payment.
        /// </summary>
        public Hash256? InvoiceID { get; set; }
        /// <summary>
        /// (Optional) Highest amount of source currency this transaction is allowed to cost, including transfer fees, exchange rates, and slippage.
        /// Does not include the XRP destroyed as a cost for submitting the transaction.
        /// Must be supplied for cross-currency/cross-issue payments.
        /// Must be omitted for XRP-to-XRP payments.
        /// </summary>
        public Amount? SendMax { get; set; }
        /// <summary>
        /// (Optional) Minimum amount of destination currency this transaction should deliver.
        /// Only valid if this is a partial payment.
        /// For non-XRP amounts, the nested field names are lower-case.
        /// </summary>
        public Amount? DeliverMin { get; set; }

        public Payment()
        {
        }

        internal Payment(JsonElement json) : base(json)
        {
            if (json.TryGetProperty("Amount", out var element))
            {
                Amount = Amount.ReadJson(element);
            }
        }

        public override void Serialize(IBufferWriter<byte> bufferWriter, bool forSigning)
        {
            var writer = new StWriter(bufferWriter);
            writer.WriteTransactionType(StTransactionType.Payment);
            writer.WriteUInt32(StUInt32FieldCode.Sequence, Sequence);
            if (DestinationTag.HasValue) { writer.WriteUInt32(StUInt32FieldCode.DestinationTag, DestinationTag.Value); }
            if (LastLedgerSequence.HasValue) { writer.WriteUInt32(StUInt32FieldCode.LastLedgerSequence, LastLedgerSequence.Value); }
            if (InvoiceID.HasValue) { writer.WriteHash256(17, InvoiceID.Value); }
            writer.WriteAmount(1, Amount);
            writer.WriteAmount(8, Fee);
            if (SendMax.HasValue) { writer.WriteAmount(9, SendMax.Value); }
            if (DeliverMin.HasValue) { writer.WriteAmount(10, DeliverMin.Value); }
            WriteSigner(writer, forSigning);
            writer.WriteAccount(StAccountIDFieldCode.Account, Account);
            writer.WriteAccount(StAccountIDFieldCode.Destination, Destination);
            WriteSigners(writer, forSigning);
        }
    }

    /// <summary>
    /// Transactions of the TrustSet type support additional values in the Flags field, as follows:
    /// </summary>
    [Flags]
    public enum TrustFlags : uint
    {
        /// <summary>
        /// Authorize the other party to hold currency issued by this account.
        /// (No effect unless using the asfRequireAuth AccountSet flag.)
        /// Cannot be unset.
        /// </summary>
        SetfAuth = 0x00010000,
        /// <summary>
        /// Enable the No Ripple flag, which blocks rippling between two trust lines of the same currency if this flag is enabled on both.
        /// </summary>
        SetNoRipple = 0x00020000,
        /// <summary>
        /// Disable the No Ripple flag, allowing rippling on this trust line.)
        /// </summary>
        ClearNoRipple = 0x00040000,
        /// <summary>
        /// Freeze the trust line.
        /// </summary>
        SetFreeze = 0x00100000,
        /// <summary>
        /// Unfreeze the trust line.
        /// </summary>
        ClearFreeze = 0x00200000,
    }

    public sealed class TrustSet : Transaction
    {
        /// <summary>
        /// Object defining the trust line to create or modify, in the format of a Currency Amount.
        /// </summary>
        public IssuedAmount LimitAmount { get; set; }

        /// <summary>
        /// (Optional) Value incoming balances on this trust line at the ratio of this number per 1,000,000,000 units.
        /// A value of 0 is shorthand for treating balances at face value.
        /// </summary>
        public UInt32? QualityIn { get; set; }

        /// <summary>
        /// (Optional) Value outgoing balances on this trust line at the ratio of this number per 1,000,000,000 units.
        /// A value of 0 is shorthand for treating balances at face value.
        /// </summary>
        public UInt32? QualityOut { get; set; }

        /// <summary>
        /// (Optional) Set of bit-flags for this transaction.
        /// </summary>
        public TrustFlags Flags { get; set; }

        public TrustSet()
        {
        }

        internal TrustSet(JsonElement json) : base(json)
        {
            JsonElement element;

            if (json.TryGetProperty("Flags", out element))
            {
                Flags = (TrustFlags)element.GetUInt32();
            }

            if (json.TryGetProperty("QualityIn", out element))
            {
                QualityIn = element.GetUInt32();
            }

            if (json.TryGetProperty("QualityOut", out element))
            {
                QualityOut = element.GetUInt32();
            }

            LimitAmount = IssuedAmount.ReadJson(json.GetProperty("LimitAmount"));
        }

        public override void Serialize(IBufferWriter<byte> bufferWriter, bool forSigning)
        {
            var writer = new StWriter(bufferWriter);
            writer.WriteTransactionType(StTransactionType.TrustSet);
            writer.WriteUInt32(StUInt32FieldCode.Flags, (uint)Flags);
            writer.WriteUInt32(StUInt32FieldCode.Sequence, Sequence);
            if (QualityIn.HasValue) { writer.WriteUInt32(StUInt32FieldCode.QualityIn, QualityIn.Value); }
            if (QualityOut.HasValue) { writer.WriteUInt32(StUInt32FieldCode.QualityOut, QualityOut.Value); }
            if (LastLedgerSequence.HasValue) { writer.WriteUInt32(StUInt32FieldCode.LastLedgerSequence, LastLedgerSequence.Value); }
            writer.WriteAmount(3, LimitAmount);
            writer.WriteAmount(8, Fee);
            WriteSigner(writer, forSigning);
            writer.WriteAccount(StAccountIDFieldCode.Account, Account);
            WriteSigners(writer, forSigning);
        }
    }

    /// <summary>
    /// An AccountDelete transaction deletes an account and any objects it owns in the XRP Ledger, if possible, sending the account's remaining XRP to a specified destination account. 
    /// See Deletion of Accounts for the requirements to delete an account.
    /// </summary>
    public sealed class AccountDelete : Transaction
    {
        /// <summary>
        /// The address of an account to receive any leftover XRP after deleting the sending account. 
        /// Must be a funded account in the ledger, and must not be the sending account.
        /// </summary>
        public AccountId Destination { get; set; }

        /// <summary>
        /// (Optional) Arbitrary destination tag that identifies a hosted recipient or other information for the recipient of the deleted account's leftover XRP.
        /// </summary>
        public UInt32? DestinationTag { get; set; }
        
        public AccountDelete()
        {
        }

        internal AccountDelete(JsonElement json) : base(json)
        {            
            Destination = new AccountId(json.GetProperty("Destination").GetString());

            if (json.TryGetProperty("DestinationTag", out var element))
            {
                DestinationTag = element.GetUInt32();
            }
        }

        public override void Serialize(IBufferWriter<byte> bufferWriter, bool forSigning)
        {
            var writer = new StWriter(bufferWriter);
            writer.WriteTransactionType(StTransactionType.AccountDelete);
            //writer.WriteUInt32(2, (uint)Flags);
            writer.WriteUInt32(StUInt32FieldCode.Sequence, Sequence);
            if (DestinationTag.HasValue) { writer.WriteUInt32(StUInt32FieldCode.DestinationTag, DestinationTag.Value); }
            if (LastLedgerSequence.HasValue) { writer.WriteUInt32(StUInt32FieldCode.LastLedgerSequence, LastLedgerSequence.Value); }
            writer.WriteAmount(8, Fee);
            WriteSigner(writer, forSigning);
            writer.WriteAccount(StAccountIDFieldCode.Account, Account);
            writer.WriteAccount(StAccountIDFieldCode.Destination, Destination);
            WriteSigners(writer, forSigning);
        }
    }

    /// <summary>
    /// The SignerListSet transaction creates, replaces, or removes a list of signers that can be used to multi-sign a transaction.
    /// This transaction type was introduced by the MultiSign amendment.
    /// New in: rippled 0.31.0
    /// </summary>
    public sealed class SignerListSet : Transaction
    {
        /// <summary>
        /// A target number for the signer weights.
        /// A multi-signature from this list is valid only if the sum weights of the signatures provided is greater than or equal to this value.
        /// To delete a signer list, use the value 0.
        /// </summary>
        public UInt32 SignerQuorum { get; set; }

        /// <summary>
        /// (Omitted when deleting) Array of SignerEntry objects, indicating the addresses and weights of signers in this list.
        /// This signer list must have at least 1 member and no more than 8 members.
        /// No address may appear more than once in the list, nor may the Account submitting the transaction appear in the list.
        /// </summary>
        public SignerEntry[] SignerEntries { get; set; }

        public SignerListSet()
        {
        }

        internal SignerListSet(JsonElement json) : base(json)
        {
            JsonElement element;

            if (json.TryGetProperty("SignerQuorum", out element))
            {
                SignerQuorum = element.GetUInt32();
            }

            if (json.TryGetProperty("SignerEntries", out element))
            {
                SignerEntries = new SignerEntry[element.GetArrayLength()];
                for (var i = 0; i < SignerEntries.Length; ++i)
                {
                    SignerEntries[i] = new SignerEntry(element[i]);
                }
            }
        }

        public override void Serialize(IBufferWriter<byte> bufferWriter, bool forSigning)
        {
            var writer = new StWriter(bufferWriter);
            writer.WriteTransactionType(StTransactionType.SignerListSet);
            //writer.WriteUInt32(2, (uint)Flags);
            writer.WriteUInt32(StUInt32FieldCode.Sequence, Sequence);
            if (LastLedgerSequence.HasValue) { writer.WriteUInt32(StUInt32FieldCode.LastLedgerSequence, LastLedgerSequence.Value); }
            writer.WriteUInt32(StUInt32FieldCode.SignerQuorum, SignerQuorum);
            writer.WriteAmount(8, Fee);
            WriteSigner(writer, forSigning);
            writer.WriteAccount(StAccountIDFieldCode.Account, Account);
            WriteSigners(writer, forSigning);
            if (SignerEntries != null)
            {
                writer.WriteStartArray(StArrayFieldCode.SignerEntries);
                foreach (var entry in SignerEntries)
                {
                    writer.WriteStartObject(StObjectFieldCode.SignerEntry);
                    writer.WriteUInt16(StUInt16FieldCode.SignerWeight, entry.SignerWeight);
                    writer.WriteAccount(StAccountIDFieldCode.Account, entry.Account);
                    writer.WriteEndObject();                    
                }
                writer.WriteEndArray();
            }
        }
    }

    /// <summary>
    /// Create a Check object in the ledger, which is a deferred payment that can be cashed by its intended destination.
    /// The sender of this transaction is the sender of the Check.
    /// </summary>
    public sealed class CheckCreate : Transaction
    {
        /// <summary>
        /// The unique address of the account that can cash the Check.
        /// </summary>
        public AccountId Destination { get; set; }

        /// <summary>
        /// Maximum amount of source currency the Check is allowed to debit the sender, including transfer fees on non-XRP currencies.
        /// The Check can only credit the destination with the same currency (from the same issuer, for non-XRP currencies).
        /// For non-XRP amounts, the nested field names MUST be lower-case.
        /// </summary>
        public Amount SendMax { get; set; }

        /// <summary>
        /// (Optional) Arbitrary tag that identifies the reason for the Check, or a hosted recipient to pay.
        /// </summary>
        public UInt32? DestinationTag { get; set; }

        /// <summary>
        /// (Optional) Time after which the Check is no longer valid, in seconds since the Ripple Epoch.
        /// </summary>
        public DateTimeOffset? Expiration { get; set; }

        /// <summary>
        /// (Optional) Arbitrary 256-bit hash representing a specific reason or identifier for this Check.
        /// </summary>
        public Hash256? InvoiceID { get; set; }

        public CheckCreate()
        {

        }

        internal CheckCreate(JsonElement json) : base(json)
        {
            JsonElement element;

            Destination = new AccountId(json.GetProperty("Destination").GetString());
            SendMax = Amount.ReadJson(json.GetProperty("SendMax"));

            if (json.TryGetProperty("DestinationTag", out element))
            {
                DestinationTag = element.GetUInt32();
            }

            if (json.TryGetProperty("Expiration", out element))
            {
                Expiration = Epoch.ToDateTimeOffset(element.GetUInt32());
            }

            if (json.TryGetProperty("InvoiceID", out element))
            {
                InvoiceID = new Hash256(element.GetString());
            }
        }

        public override void Serialize(IBufferWriter<byte> bufferWriter, bool forSigning)
        {
            var writer = new StWriter(bufferWriter);
            writer.WriteTransactionType(StTransactionType.CheckCreate);
            //writer.WriteUInt32(2, (uint)Flags);
            writer.WriteUInt32(StUInt32FieldCode.Sequence, Sequence);
            if (Expiration.HasValue)
            {
                writer.WriteUInt32(StUInt32FieldCode.Expiration, Epoch.FromDateTimeOffset(Expiration.Value));
            }
            if (DestinationTag.HasValue) { writer.WriteUInt32(StUInt32FieldCode.DestinationTag, DestinationTag.Value); }
            if (LastLedgerSequence.HasValue) { writer.WriteUInt32(StUInt32FieldCode.LastLedgerSequence, LastLedgerSequence.Value); }
            if (InvoiceID.HasValue) { writer.WriteHash256(17, InvoiceID.Value); }
            writer.WriteAmount(8, Fee);
            writer.WriteAmount(9, SendMax);
            WriteSigner(writer, forSigning);
            writer.WriteAccount(StAccountIDFieldCode.Account, Account);
            writer.WriteAccount(StAccountIDFieldCode.Destination, Destination);
            WriteSigners(writer, forSigning);
        }
    }

    /// <summary>
    /// Cancels an unredeemed Check, removing it from the ledger without sending any money.
    /// The source or the destination of the check can cancel a Check at any time using this transaction type.
    /// If the Check has expired, any address can cancel it.
    /// </summary>
    public sealed class CheckCancel : Transaction
    {
        /// <summary>
        /// The ID of the Check ledger object to cancel, as a 64-character hexadecimal string.
        /// </summary>
        public Hash256 CheckID { get; set; }

        public CheckCancel()
        {

        }

        internal CheckCancel(JsonElement json) : base(json)
        {
            CheckID = new Hash256(json.GetProperty("CheckID").GetString());
        }

        public override void Serialize(IBufferWriter<byte> bufferWriter, bool forSigning)
        {
            var writer = new StWriter(bufferWriter);
            writer.WriteTransactionType(StTransactionType.CheckCancel);
            //writer.WriteUInt32(2, (uint)Flags);
            writer.WriteUInt32(StUInt32FieldCode.Sequence, Sequence);
            if (LastLedgerSequence.HasValue) { writer.WriteUInt32(StUInt32FieldCode.LastLedgerSequence, LastLedgerSequence.Value); }
            writer.WriteHash256(24, CheckID);
            writer.WriteAmount(8, Fee);
            WriteSigner(writer, forSigning);
            writer.WriteAccount(StAccountIDFieldCode.Account, Account);
            WriteSigners(writer, forSigning);
        }
    }

    /// <summary>
    /// Attempts to redeem a Check object in the ledger to receive up to the amount authorized by the corresponding CheckCreate transaction. Only the Destination address of a Check can cash it with a CheckCash transaction. Cashing a check this way is similar to executing a Payment initiated by the destination.
    /// Since the funds for a check are not guaranteed, redeeming a Check can fail because the sender does not have a high enough balance or because there is not enough liquidity to deliver the funds.If this happens, the Check remains in the ledger and the destination can try to cash it again later, or for a different amount.
    /// </summary>
    public sealed class CheckCash : Transaction
    {
        /// <summary>
        /// The ID of the Check ledger object to cash, as a 64-character hexadecimal string.
        /// </summary>
        public Hash256 CheckID { get; set; }

        /// <summary>
        /// (Optional) Redeem the Check for exactly this amount, if possible.
        /// The currency must match that of the SendMax of the corresponding CheckCreate transaction.
        /// You must provide either this field or DeliverMin.
        /// </summary>
        public Amount? Amount { get; set; }

        /// <summary>
        /// (Optional) Redeem the Check for at least this amount and for as much as possible.
        /// The currency must match that of the SendMax of the corresponding CheckCreate transaction.
        /// You must provide either this field or Amount.
        /// </summary>
        public Amount? DeliverMin { get; set; }

        public CheckCash()
        {

        }

        internal CheckCash(JsonElement json) : base(json)
        {
            CheckID = new Hash256(json.GetProperty("CheckID").GetString());

            JsonElement element;
            if (json.TryGetProperty("Amount", out element))
            {
                Amount = Ripple.Amount.ReadJson(element);
            }
            if (json.TryGetProperty("DeliverMin", out element))
            {
                DeliverMin = Ripple.Amount.ReadJson(element);
            }
        }

        public override void Serialize(IBufferWriter<byte> bufferWriter, bool forSigning)
        {
            var writer = new StWriter(bufferWriter);
            writer.WriteTransactionType(StTransactionType.CheckCash);
            //writer.WriteUInt32(2, (uint)Flags);
            writer.WriteUInt32(StUInt32FieldCode.Sequence, Sequence);
            if (LastLedgerSequence.HasValue) { writer.WriteUInt32(StUInt32FieldCode.LastLedgerSequence, LastLedgerSequence.Value); }
            writer.WriteHash256(24, CheckID);
            if (Amount.HasValue)
            {
                writer.WriteAmount(1, Amount.Value);
            }
            writer.WriteAmount(8, Fee);
            if (DeliverMin.HasValue)
            {
                writer.WriteAmount(10, DeliverMin.Value);
            }
            WriteSigner(writer, forSigning);
            writer.WriteAccount(StAccountIDFieldCode.Account, Account);
            WriteSigners(writer, forSigning);
        }
    }
}