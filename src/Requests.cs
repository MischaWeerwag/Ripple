﻿using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Ibasa.Ripple
{
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

        public LedgerHeader(ReadOnlySpan<byte> data)
        {
            var reader = new StReader(data);
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

    public sealed class LedgerResponse
    {
        /// <summary>
        /// Unique identifying hash of the entire ledger.
        /// </summary>
        public Hash256? LedgerHash { get; private set; }

        /// <summary>
        /// The Ledger Index of this ledger.
        /// </summary>
        public uint LedgerIndex { get; private set; }

        /// <summary>
        /// The complete header data of this ledger.
        /// </summary>
        public LedgerHeader Ledger { get; private set; }

        /// <summary>
        /// Whether or not this ledger has been closed.
        /// </summary>
        public bool Closed { get; private set; }

        /// <summary>
        /// True if this data is from a validated ledger version.
        /// </summary>
        public bool Validated { get; private set; }

        public Hash256[] Transactions { get; private set; }

        internal LedgerResponse(JsonElement json)
        {
            if(json.TryGetProperty("ledger_hash", out var ledger_hash))
            {
                LedgerHash = new Hash256(ledger_hash.GetString());
            }
            var ledger = json.GetProperty("ledger");
            Closed = ledger.GetProperty("closed").GetBoolean();
            Validated = json.GetProperty("validated").GetBoolean();

            if(json.TryGetProperty("ledger_index", out var ledger_index))
            {
                LedgerIndex = ledger_index.GetUInt32();
                Ledger = new LedgerHeader(ledger.GetProperty("ledger_data").GetBytesFromBase16());
            }
            else
            {
                LedgerIndex = json.GetProperty("ledger_current_index").GetUInt32();
            }

            if (ledger.TryGetProperty("transactions", out var transactions))
            {
                var length = transactions.GetArrayLength();
                Transactions = new Hash256[length];
                for(int i = 0; i < length; ++i)
                {
                    Transactions[i] = new Hash256(transactions[i].GetString());
                }
            }

            // TODO Transactions, Accounts etc

            // expand false: "transactions":["6D41BB39ECCDC0BCA035A1563F29CD80B35ADFBB78A260A9525DCBECE3CC0952","A069F973CBA19394FCCABB68408FDF07473129380D32AF0E281F7889F6D459B6","A3460DAAD793FC551B50CDFB70F67E3DE037EFE524B8F00FC40AB14F0DC68B89","F955E18F5566B9A9CCE5C00718127369E3D9F0F028FC0BC5D6491B605CDC5DC7"]
            // expand true: "transactions":[{"meta":"201C00000001F8E511006125005C38FC5554658610A874324875A716AD50C16A8C8767DA4EBC1CE6430D90540CF6C1EE1D5631794F29F9E987DC45A7997416503E0E3A5C0D114B050845B76F2D9D9FF9DC1FE6240090108C624000000604DBF0C9E1E72200000000240090108D2D00000000624000000603AAC3BD811486FFE2A17E861BA0FE9A3ED8352F895D80E789E0E1E1E511006125005C38FC5554658610A874324875A716AD50C16A8C8767DA4EBC1CE6430D90540CF6C1EE1D568EEC72369A874DEC57AC3C11F40714D79D56F0079AA1948B38CC044D3F6F79FFE6624000000003BE3A33E1E7220000000024009013A92D00000000624000000004EF67338114F9CB4ADAC227928306BDC5E679C485D4A676BFB8E1E1F1031000","tx_blob":"1200002280000000240090108C201B005C38FD614000000001312D0068400000000000000C732102E6CB923A531044CB194A2F7477B38C6D2B499FA67FFC38203CEADC7D8A7DFF5474473045022100DAF0D300099F6F652272B410C53D968B3CCA45D8CDAA509B2A06754A63D2CAD802203605C51E41FC9C4E780ED1465AFFE97E2941A6B0B8225D43DFF2BC880878ABAC811486FFE2A17E861BA0FE9A3ED8352F895D80E789E08314F9CB4ADAC227928306BDC5E679C485D4A676BFB8"},{"meta":"201C00000003F8E511006125005C38FC552A88AA2C45B33B7667884EC93A81BEE343428E86207DB0C439415594B89D9E225631794F29F9E987DC45A7997416503E0E3A5C0D114B050845B76F2D9D9FF9DC1FE6624000000604DBF0BDE1E72200000000240090108D2D000000006240000006060D1DBD811486FFE2A17E861BA0FE9A3ED8352F895D80E789E0E1E1E511006125005C38FC552A88AA2C45B33B7667884EC93A81BEE343428E86207DB0C439415594B89D9E22568EEC72369A874DEC57AC3C11F40714D79D56F0079AA1948B38CC044D3F6F79FFE624009013AA624000000003BE3A27E1E7220000000024009013AB2D000000006240000000028D0D1B8114F9CB4ADAC227928306BDC5E679C485D4A676BFB8E1E1F1031000","tx_blob":"120000228000000024009013AA201B005C38FD614000000001312D0068400000000000000C732102A61C710649C858A03DF50C8D24563613FC4D905B141EEBE019364675929AB8047446304402203C2D53C61994AF198000F65AEFE34F72507A63522A11725687A8C4CFB4FAC03502207E2E241B9BE874C51C4BBE241A62E230EE9A1981DA1DD338A7A117A65471333B8114F9CB4ADAC227928306BDC5E679C485D4A676BFB8831486FFE2A17E861BA0FE9A3ED8352F895D80E789E0"},{"meta":"201C00000002F8E511006125005C38FC5502E27B4348FCF907B9AA31D908BE2E1834360A976A03FD7C26BB566F24F4852C5631794F29F9E987DC45A7997416503E0E3A5C0D114B050845B76F2D9D9FF9DC1FE6624000000603AAC3BDE1E72200000000240090108D2D00000000624000000604DBF0BD811486FFE2A17E861BA0FE9A3ED8352F895D80E789E0E1E1E511006125005C38FC5502E27B4348FCF907B9AA31D908BE2E1834360A976A03FD7C26BB566F24F4852C568EEC72369A874DEC57AC3C11F40714D79D56F0079AA1948B38CC044D3F6F79FFE624009013A9624000000004EF6733E1E7220000000024009013AA2D00000000624000000003BE3A278114F9CB4ADAC227928306BDC5E679C485D4A676BFB8E1E1F1031000","tx_blob":"120000228000000024009013A9201B005C38FD614000000001312D0068400000000000000C732102A61C710649C858A03DF50C8D24563613FC4D905B141EEBE019364675929AB80474463044022061C358C25358B5B7EC8218553D1126A1E07720C52F10DAEE86B56C7058F56F5802201C78B7FC9E73826F22312DF197C595A5971CE522039630552DE26041A32DCC878114F9CB4ADAC227928306BDC5E679C485D4A676BFB8831486FFE2A17E861BA0FE9A3ED8352F895D80E789E0"},{"meta":"201C00000000F8E511006125005C38FB55FDBD261F2B0C99BE16A3419095C9C36DB1D6121F64806DECE6A067C5A3AD54625631794F29F9E987DC45A7997416503E0E3A5C0D114B050845B76F2D9D9FF9DC1FE6240090108B6240000006060D1DD5E1E72200000000240090108C2D00000000624000000604DBF0C9811486FFE2A17E861BA0FE9A3ED8352F895D80E789E0E1E1E511006125005C38FB55FDBD261F2B0C99BE16A3419095C9C36DB1D6121F64806DECE6A067C5A3AD5462568EEC72369A874DEC57AC3C11F40714D79D56F0079AA1948B38CC044D3F6F79FFE66240000000028D0D33E1E7220000000024009013A92D00000000624000000003BE3A338114F9CB4ADAC227928306BDC5E679C485D4A676BFB8E1E1F1031000","tx_blob":"1200002280000000240090108B201B005C38FD614000000001312D0068400000000000000C732102E6CB923A531044CB194A2F7477B38C6D2B499FA67FFC38203CEADC7D8A7DFF5474473045022100FFD3CB1B4A1BD54597F71CDE4F986206073DFEAF8485B32EF4786CACFB29BAF802207C8611E95D5F6FA444761BB180D4C5B9EE0FAC3DDE7FD006E68B82C21737CE25811486FFE2A17E861BA0FE9A3ED8352F895D80E789E08314F9CB4ADAC227928306BDC5E679C485D4A676BFB8"}]
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
        public Currency Balance { get; private set; }

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
            Balance = Ripple.Currency.Parse(json.GetProperty("balance").GetString());
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
        /// <summary>
        /// The value true indicates that the transaction was applied, queued, broadcast, or kept for later. 
        /// The value false indicates that none of those happened, so the transaction cannot possibly succeed as long as you do not submit it again and have not already submitted it another time.
        /// </summary>
        public bool Accepted { get; private set; }
        /// <summary>
        /// The next Sequence Number available for the sending account after all pending and queued transactions.
        /// </summary>
        public uint AccountSequenceAvailable { get; private set; }
        /// <summary>
        /// The next Sequence Number for the sending account after all transactions that have been provisionally applied, but not transactions in the queue.
        /// </summary>
        public uint AccountSequenceNext { get; private set; }
        /// <summary>
        /// The value true indicates that this transaction was applied to the open ledger.
        /// In this case, the transaction is likely, but not guaranteed, to be validated in the next ledger version.
        /// </summary>
        public bool Applied { get; private set; }
        /// <summary>
        /// The value true indicates this transaction was broadcast to peer servers in the peer - to - peer XRP Ledger network. 
        /// (Note: if the server has no peers, such as in stand - alone mode, the server uses the value true for cases where it would have broadcast the transaction.) 
        /// The value false indicates the transaction was not broadcast to any other servers.
        /// </summary>
        public bool Broadcast { get; private set; }
        /// <summary>
        /// The value true indicates that the transaction was kept to be retried later.
        /// </summary>
        public bool Kept { get; private set; }
        /// <summary>
        /// The value true indicates the transaction was put in the Transaction Queue, which means it is likely to be included in a future ledger version.
        /// </summary>
        public bool Queued { get; private set; }
        /// <summary>
        /// The current open ledger cost before processing this transaction.
        /// Transactions with a lower cost are likely to be queued.
        /// </summary>
        public ulong OpenLedgerCost { get; private set; }
        /// <summary>
        /// The ledger index of the newest validated ledger at the time of submission.
        /// This provides a lower bound on the ledger versions that the transaction can appear in as a result of this request. 
        /// (The transaction could only have been validated in this ledger version or earlier if it had already been submitted before.)
        /// </summary>
        public uint ValidatedLedgerIndex { get; private set; }

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
            Accepted = json.GetProperty("accepted").GetBoolean();
            AccountSequenceAvailable = json.GetProperty("account_sequence_available").GetUInt32();
            AccountSequenceNext = json.GetProperty("account_sequence_next").GetUInt32();
            Applied = json.GetProperty("applied").GetBoolean();
            Broadcast = json.GetProperty("broadcast").GetBoolean();
            Kept = json.GetProperty("kept").GetBoolean();
            Queued = json.GetProperty("queued").GetBoolean();
            OpenLedgerCost = ulong.Parse(json.GetProperty("open_ledger_cost").GetString());
            ValidatedLedgerIndex = json.GetProperty("validated_ledger_index").GetUInt32();
        }
    }

    public sealed class TransactionResponse
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

        public Transaction Transaction { get; private set; }

        internal TransactionResponse(JsonElement json)
        {
            Hash = new Hash256(json.GetProperty("hash").GetString());
            Validated = json.GetProperty("validated").GetBoolean();
            if (json.TryGetProperty("ledger_index", out var element))
            {
                LedgerIndex = element.GetUInt32();
            }
            Transaction = Transaction.FromJson(json);
        }
    }

    public sealed class TxRequest
    {
        /// <summary>
        /// The 256-bit hash of the transaction, as hex.
        /// </summary>
        public Hash256 Transaction { get; set; }

        /// <summary>
        /// (Optional) Use this with max_ledger to specify a range of up to 1000 ledger indexes, starting with this ledger (inclusive). 
        /// If the server cannot find the transaction, it confirms whether it was able to search all the ledgers in this range.
        /// </summary>
        public uint? MinLedger { get; set; }

        /// <summary>
        /// (Optional) Use this with min_ledger to specify a range of up to 1000 ledger indexes, ending with this ledger (inclusive). 
        /// If the server cannot find the transaction, it confirms whether it was able to search all the ledgers in the requested range.
        /// </summary>
        public uint? MaxLedger { get; set; }
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

    public sealed class NoRippleCheckRequest
    {
        /// <summary>
        /// A unique identifier for the account, most commonly the account's address.
        /// </summary>
        public AccountId Account { get; set; }

        /// <summary>
        /// Whether the address refers to a gateway or user.
        /// Recommendations depend on the role of the account. Issuers must have Default Ripple enabled and must disable No Ripple on all trust lines. Users should have Default Ripple disabled, and should enable No Ripple on all trust lines.
        /// </summary>
        public string Role { get; set; }

        /// <summary>
        /// If true, include an array of suggested transactions, as JSON objects, that you can sign and submit to fix the problems. Defaults to false.
        /// </summary>
        public bool Transactions { get; set; }

        /// <summary>
        /// (Optional) The maximum number of trust line problems to include in the results. Defaults to 300.
        /// </summary>
        public uint? Limit { get; set; }

        /// <summary>
        /// A 20-byte hex string, or the ledger index of the ledger to use, or a shortcut string to choose a ledger automatically.
        /// </summary>
        public LedgerSpecification Ledger { get; set; }
    }

    public sealed class NoRippleCheckResponse
    {
        /// <summary>
        /// The identifying hash of the ledger version used to calculate these results, as hex.
        /// </summary>
        public Hash256? LedgerHash { get; private set; }

        /// <summary>
        /// The ledger index of the ledger used to calculate these results.
        /// </summary>
        public uint LedgerIndex { get; private set; }

        /// <summary>
        /// If true, this data comes from a validated ledger.
        /// </summary>
        public bool Validated { get; private set; }

        /// <summary>
        /// Array of strings with human-readable descriptions of the problems. This includes up to one entry if the account's Default Ripple setting is not as recommended, plus up to limit entries for trust lines whose No Ripple setting is not as recommended.
        /// </summary>
        public ReadOnlyCollection<string> Problems { get; private set; }

        /// <summary>
        /// (May be omitted) If the request specified transactions as true, this is an array of JSON objects, each of which is the JSON form of a transaction that should fix one of the described problems.The length of this array is the same as the problems array, and each entry is intended to fix the problem described at the same index into that array.
        /// </summary>
        public ReadOnlyCollection<Transaction> Transactions { get; private set; }

        internal NoRippleCheckResponse(JsonElement json)
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

            var json_array = json.GetProperty("problems");
            var index = 0;
            var problems = new string[json_array.GetArrayLength()];
            foreach (var problem in json_array.EnumerateArray())
            {
                problems[index++] = problem.GetString();
            }
            Problems = Array.AsReadOnly(problems);

            if (json.TryGetProperty("transactions", out json_array))
            {
                index = 0;
                var transactions = new Transaction[json_array.GetArrayLength()];
                foreach (var transaction in json_array.EnumerateArray())
                {
                    transactions[index++] = Transaction.FromJson(transaction);
                }
                Transactions = Array.AsReadOnly(transactions);
            }
            else
            {
                Transactions = Array.AsReadOnly(Array.Empty<Transaction>());
            }
        }
    }

    public sealed class WalletProposeRequest
    {
        /// <summary>
        /// Which signing algorithm to use to derive this key pair. 
        /// Valid values are ed25519 and secp256k1 (all lower case). 
        /// The default is secp256k1.
        /// </summary>
        public KeyType? KeyType { get; set; }

        /// <summary>
        /// (Optional) Generate a key pair and address from this seed value. 
        /// This value can be formatted in hexadecimal, the XRP Ledger's base58 format, RFC-1751, or as an arbitrary string. 
        /// Cannot be used with seed or seed_hex.
        /// </summary>
        public string Passphrase { get; set; }

        /// <summary>
        /// (Optional) Generate the key pair and address from this seed value in the XRP Ledger's base58-encoded format. 
        /// Cannot be used with passphrase or seed_hex.
        /// </summary>
        public string Seed { get; set; }

        /// <summary>
        /// (Optional) Generate the key pair and address from this seed value in hexadecimal format. 
        /// Cannot be used with passphrase or seed.
        /// </summary>
        public string SeedHex { get; set; }
    }

    public sealed class WalletProposeResponse
    {
        public string KeyType { get; private set; }
        public string MasterSeed{ get; private set; }
        public string AccountId { get; private set; }
        public string PublicKey { get; private set; }
        public string Warning { get; private set; }

        internal WalletProposeResponse(JsonElement json)
        {
            KeyType = json.GetProperty("key_type").GetString();
            MasterSeed = json.GetProperty("master_seed_hex").GetString();
            AccountId = json.GetProperty("account_id").GetString();
            PublicKey = json.GetProperty("public_key_hex").GetString();

            if (json.TryGetProperty("warning", out var element))
            {
                Warning = element.GetString();
            }
        }
    }
}