using System;
using System.Buffers;
using System.Text.Json;

namespace Ibasa.Ripple
{
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
        //Signers Array   Array   (Optional) Array of objects that represent a multi-signature which authorizes this transaction.
        //SourceTag Unsigned Integer UInt32  (Optional) Arbitrary integer used to identify the reason for this payment, or a sender on whose behalf this transaction is made.Conventionally, a refund should specify the initial payment's SourceTag as the refund payment's DestinationTag.


        public Transaction()
        {

        }

        public virtual void ReadJson(JsonElement json)
        {
            Account = new AccountId(json.GetProperty("Account").GetString());

            var fee = json.GetProperty("Fee");
            if (fee.ValueKind == JsonValueKind.Number)
            {
                Fee = new XrpAmount(fee.GetUInt64());
            } 
            else 
            {
                Fee = new XrpAmount(ulong.Parse(fee.GetString()));
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

        public abstract void Serialize(System.Buffers.IBufferWriter<byte> bufferWriter, bool forSigning);

        public byte[] Sign(KeyPair keyPair, out Hash256 hash)
        {         
            this.SigningPubKey = keyPair.GetCanonicalPublicKey();
            var bufferWriter = new System.Buffers.ArrayBufferWriter<byte>();

            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(bufferWriter.GetSpan(4), 0x53545800u);
            bufferWriter.Advance(4);
            this.Serialize(bufferWriter, true);

            // Calculate signature and serialize again
            this.TxnSignature = keyPair.Sign(bufferWriter.WrittenSpan);
            bufferWriter.Clear();

            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(bufferWriter.GetSpan(4), 0x54584E00u);
            bufferWriter.Advance(4);

            this.Serialize(bufferWriter, false);

            using (var sha512 = System.Security.Cryptography.SHA512.Create())
            {
                Span<byte> hashSpan = stackalloc byte[64];
                sha512.TryComputeHash(bufferWriter.WrittenSpan, hashSpan, out var bytesWritten);
                hash = new Hash256(hashSpan.Slice(0, 32));
            }

            return bufferWriter.WrittenMemory.Slice(4).ToArray();
        }

        internal static Transaction FromJson(JsonElement json)
        {
            var transactionType = json.GetProperty("TransactionType").GetString();
            Transaction transaction;
            if (transactionType == "AccountSet")
            {
                transaction = new AccountSet();
            }
            else if (transactionType == "Payment")
            {
                transaction = new Payment();
            }
            else if (transactionType == "TrustSet")
            {
                transaction = new TrustSet();
            }
            else if (transactionType == "SetRegularKey")
            {
                transaction = new SetRegularKey();
            }
            else if (transactionType == "AccountDelete")
            {
                transaction = new AccountDelete();
            }
            else
            {
                throw new NotImplementedException(
                    string.Format("Transaction type '{0}' not implemented", transactionType));
            }

            transaction.ReadJson(json);
            return transaction;
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

        public override void ReadJson(JsonElement json) 
        {
            base.ReadJson(json);
            if (json.TryGetProperty("RegularKey", out var element))
            {
                RegularKey = new AccountId(element.GetString());
            }
        }

        public override void Serialize(IBufferWriter<byte> bufferWriter, bool forSigning)
        {
            var writer = new StWriter(bufferWriter);
            writer.WriteTransactionType(TransactionType.SetRegularKey);
            writer.WriteUInt32(4, Sequence);
            if (LastLedgerSequence.HasValue) { writer.WriteUInt32(27, LastLedgerSequence.Value); }
            writer.WriteAmount(8, Fee);
            writer.WriteVl(3, this.SigningPubKey);
            if (!forSigning)
            {
                writer.WriteVl(4, this.TxnSignature);
            }
            writer.WriteAccount(1, Account);
            if (RegularKey.HasValue) { writer.WriteAccount(8, RegularKey.Value); }
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
        public byte[] Domain { get; set; }

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

        public override void ReadJson(JsonElement json)
        {
            base.ReadJson(json);

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
            writer.WriteTransactionType(TransactionType.AccountSet);
            writer.WriteUInt32(4, Sequence);
            if (LastLedgerSequence.HasValue) { writer.WriteUInt32(27, LastLedgerSequence.Value); }
            if (SetFlag.HasValue)
            {
                writer.WriteUInt32(33, (uint)SetFlag.Value);
            }
            if (ClearFlag.HasValue)
            {
                writer.WriteUInt32(34, (uint)ClearFlag.Value);
            }
            writer.WriteAmount(8, Fee);
            writer.WriteVl(3, this.SigningPubKey);
            if (!forSigning)
            {
                writer.WriteVl(4, this.TxnSignature);
            }
            if (Domain != null) { writer.WriteVl(7, Domain); }
            writer.WriteAccount(1, Account);
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

        public override void ReadJson(JsonElement json)
        {
            base.ReadJson(json);

            if (json.TryGetProperty("Amount", out var element))
            {
                Amount = new Amount(element);
            }
        }

        public override void Serialize(IBufferWriter<byte> bufferWriter, bool forSigning)
        {
            var writer = new StWriter(bufferWriter);
            writer.WriteTransactionType(TransactionType.Payment);
            writer.WriteUInt32(4, Sequence);
            if (DestinationTag.HasValue) { writer.WriteUInt32(14, DestinationTag.Value); }
            if (LastLedgerSequence.HasValue) { writer.WriteUInt32(27, LastLedgerSequence.Value); }
            if (InvoiceID.HasValue) { writer.WriteHash256(17, InvoiceID.Value); }
            writer.WriteAmount(1, Amount);
            writer.WriteAmount(8, Fee);
            if (SendMax.HasValue) { writer.WriteAmount(9, SendMax.Value); }
            if (DeliverMin.HasValue) { writer.WriteAmount(10, DeliverMin.Value); }
            writer.WriteVl(3, this.SigningPubKey);
            if (!forSigning)
            {
                writer.WriteVl(4, this.TxnSignature);
            }
            writer.WriteAccount(1, Account);
            writer.WriteAccount(3, Destination);
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
        ///  (Optional) Value outgoing balances on this trust line at the ratio of this number per 1,000,000,000 units.
        ///  A value of 0 is shorthand for treating balances at face value.
        /// </summary>
        public UInt32? QualityOut { get; set; }

        /// <summary>
        /// (Optional) Set of bit-flags for this transaction.
        /// </summary>
        public TrustFlags Flags { get; set; }

        public TrustSet()
        {
        }

        public override void ReadJson(JsonElement json)
        {
            base.ReadJson(json);
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

            if (json.TryGetProperty("LimitAmount", out element))
            {
                LimitAmount = IssuedAmount.ReadJson(element);
            }
        }

        public override void Serialize(IBufferWriter<byte> bufferWriter, bool forSigning)
        {
            var writer = new StWriter(bufferWriter);
            writer.WriteTransactionType(TransactionType.TrustSet);
            writer.WriteUInt32(2, (uint)Flags);
            writer.WriteUInt32(4, Sequence);
            if (QualityIn.HasValue) { writer.WriteUInt32(20, QualityIn.Value); }
            if (QualityOut.HasValue) { writer.WriteUInt32(21, QualityOut.Value); }
            if (LastLedgerSequence.HasValue) { writer.WriteUInt32(27, LastLedgerSequence.Value); }
            writer.WriteAmount(3, LimitAmount);
            writer.WriteAmount(8, Fee);
            writer.WriteVl(3, this.SigningPubKey);
            if (!forSigning)
            {
                writer.WriteVl(4, this.TxnSignature);
            }
            writer.WriteAccount(1, Account);
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

        public override void ReadJson(JsonElement json)
        {
            base.ReadJson(json);
            JsonElement element;

            if (json.TryGetProperty("Destination", out element))
            {
                Destination = new AccountId(element.GetString());
            }

            if (json.TryGetProperty("DestinationTag", out element))
            {
                DestinationTag = element.GetUInt32();
            }
        }

        public override void Serialize(IBufferWriter<byte> bufferWriter, bool forSigning)
        {
            var writer = new StWriter(bufferWriter);
            writer.WriteTransactionType(TransactionType.AccountDelete);
            //writer.WriteUInt32(2, (uint)Flags);
            writer.WriteUInt32(4, Sequence);
            if (DestinationTag.HasValue) { writer.WriteUInt32(14, DestinationTag.Value); }
            if (LastLedgerSequence.HasValue) { writer.WriteUInt32(27, LastLedgerSequence.Value); }
            writer.WriteAmount(8, Fee);
            writer.WriteVl(3, this.SigningPubKey);
            if (!forSigning)
            {
                writer.WriteVl(4, this.TxnSignature);
            }
            writer.WriteAccount(1, Account);
            writer.WriteAccount(3, Destination);
        }
    }
}