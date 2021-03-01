open System
open System.IO
open System.Text.Json


/// Find the StDefinitions.cs file
let rec findPath searchPath = 
    let foundSrc =
        IO.Path.Combine(searchPath, "src")
        |> IO.Directory.Exists

    if foundSrc then 
        IO.Path.Combine(searchPath, "src", "StDefinitions.cs")
    else
        let parent = Directory.GetParent searchPath
        findPath parent.FullName
        
/// Download st definitions to json
let downloadDefinitions () =
    let request = System.Net.WebRequest.Create("https://raw.githubusercontent.com/ripple/ripple-binary-codec/master/src/enums/definitions.json")
    use response = request.GetResponse()
    use stream = response.GetResponseStream()
    JsonDocument.Parse(stream)

let trimSt (str : string) =
    if str.StartsWith("ST") then
        str.Substring(2)
    else
        str

/// Emit the TYPES
let emitTypes (writer : TextWriter) (document : JsonDocument) =
    let types = document.RootElement.GetProperty("TYPES")
    
    writer.WriteLine("    /// <summary>")
    writer.WriteLine("    /// Map of data types to their \"type code\" for constructing field IDs and sorting fields in canonical order.")
    writer.WriteLine("    /// Codes below 1 should not appear in actual data;")
    writer.WriteLine("    /// codes above 10000 represent special \"high-level\" object types such as \"Transaction\" that cannot be serialized inside other objects.")
    writer.WriteLine("    /// See the Type List for details of how to serialize each type.")
    writer.WriteLine("    /// </summary>")
    writer.WriteLine("    public enum StTypeCode")
    writer.WriteLine("    {")
    for entry in types.EnumerateObject() do
        // trim ST off the front of types, we're nesting this in StTypeCode anyway
        let key = trimSt entry.Name
        let value = entry.Value.GetInt32()
        writer.WriteLine("        {0} = {1},", key, value)
    writer.WriteLine("    }")
    writer.WriteLine()

/// Emit the LEDGER_ENTRY_TYPES
let emitLedgerEntry (writer : TextWriter) (document : JsonDocument) =
    let types = document.RootElement.GetProperty("LEDGER_ENTRY_TYPES")
        
    writer.WriteLine("    /// <summary>")
    writer.WriteLine("    /// Map of ledger objects to their data type.")
    writer.WriteLine("    /// These appear in ledger state data, and in the \"affected nodes\" section of processed transactions' metadata.")
    writer.WriteLine("    /// </summary>")
    writer.WriteLine("    public enum StLedgerEntryType")
    writer.WriteLine("    {")
    for entry in types.EnumerateObject() do
        let value = entry.Value.GetInt32()
        writer.WriteLine("        {0} = {1},", entry.Name, value)
    writer.WriteLine("    }")
    writer.WriteLine()
    
/// Emit the TRANSACTION_TYPES
let emitTransactionType (writer : TextWriter) (document : JsonDocument) =
    let types = document.RootElement.GetProperty("TRANSACTION_TYPES")
    
    writer.WriteLine("    /// <summary>")
    writer.WriteLine("    /// The type of a transaction (TransactionType field) is the most fundamental information about a transaction.")
    writer.WriteLine("    /// This indicates what type of operation the transaction is supposed to do.")    
    writer.WriteLine("    /// </summary>")
    writer.WriteLine("    public enum StTransactionType : ushort")
    writer.WriteLine("    {")
    for entry in types.EnumerateObject() do
        let value = entry.Value.GetInt32()
        writer.WriteLine("        {0} = {1},", entry.Name, uint16 value)
    writer.WriteLine("    }")
    writer.WriteLine()
    
/// Emit the FIELDS
let emitFields (writer : TextWriter) (document : JsonDocument) =
    let types = document.RootElement.GetProperty("FIELDS")

    // We want to map by type, then map by name to get index
    let fields =
        types.EnumerateArray()
        |> Seq.map(fun tuple ->
            let name = tuple.[0].GetString()
            let object = tuple.[1]
            let typ = object.GetProperty("type").GetString()
            let nth = object.GetProperty("nth").GetInt32()
            // trim ST off the front of types, we're nesting this in St anyway
            let key = trimSt typ
            key, (name, nth)
        )
        |> Seq.groupBy fst
        |> Map.ofSeq
        |> Map.map (fun _ seq -> seq |> Seq.map snd |> Map.ofSeq)

    // For each type emit the field codes (eg. UInt8FieldCode.CloseResolution = 1)
    for typeFields in fields do
        writer.WriteLine("    public enum St{0}FieldCode", typeFields.Key)
        writer.WriteLine("    {")

        for field in typeFields.Value do 
            writer.WriteLine("        {0} = {1},", field.Key, field.Value)
        writer.WriteLine("    }")
        writer.WriteLine()

    // For each field emit the FieldId
    writer.WriteLine("    public partial struct StFieldId")
    writer.WriteLine("    {")
    for typeFields in fields do
        for field in typeFields.Value do 
            writer.WriteLine("        public static readonly StFieldId {0}_{1} = new StFieldId(StTypeCode.{0}, {2});", typeFields.Key, field.Key, uint field.Value)
        writer.WriteLine("")
    writer.WriteLine("    }")
    writer.WriteLine()

type LedgerField = { Name : string; Type : string; Optional : bool; Doc : string } 
type LedgerType = { Name : string; Doc : string; Fields : LedgerField list }

let knownTypes = Map.ofList [
    // Directory nodes have AccountIds as hexstrings
    "Hash160_AccountId", (true, "AccountId", "new AccountId({0}.GetBytesFromBase16())", "ToAccountId(reader.ReadHash160())")
    "AccountID", (true, "AccountId", "new AccountId({0}.GetString())", "reader.ReadAccount()")
    "Amount", (true, "Amount", "Amount.ReadJson({0})", "reader.ReadAmount()")
    "XrpAmount", (true, "XrpAmount", "XrpAmount.ReadJson({0})", "reader.ReadXrpAmount()")
    "IssuedAmount", (true, "IssuedAmount", "IssuedAmount.ReadJson({0})", "reader.ReadIssuedAmount()")
    "UInt64", (true, "ulong", "ulong.Parse({0}.GetString(), System.Globalization.NumberStyles.AllowHexSpecifier)", "reader.ReadUInt64()")
    "UInt32", (true, "uint", "{0}.GetUInt32()", "reader.ReadUInt32()")
    "UInt16", (true, "ushort", "{0}.GetUInt16()", "reader.ReadUInt16()")
    "UInt8", (true, "byte", "{0}.GetByte()", "reader.ReadUInt8()")
    "Hash256", (true, "Hash256", "new Hash256({0}.GetString())", "reader.ReadHash256()")
    "Hash160_CurrencyCode", (true, "CurrencyCode", "new CurrencyCode({0}.GetBytesFromBase16())", "ToCurrencyCode(reader.ReadHash160())")
    "CurrencyCode", (true, "CurrencyCode", "new CurrencyCode({0}.GetString())", "reader.ReadCurrencyCode()")
    "Hash128", (true, "Hash128", "new Hash128({0}.GetString())", "reader.ReadHash128()")
    "Blob", (true, "ReadOnlyMemory<byte>", "{0}.GetBytesFromBase16()", "reader.ReadBlob()")
    "Array<SignerEntry>", (false, "ReadOnlyCollection<SignerEntry>", "new SignerEntry({0})", "new SignerEntry(ref reader)")
    "Array<DisabledValidator>", (false, "ReadOnlyCollection<DisabledValidator>", "new DisabledValidator({0})", "new DisabledValidator(ref reader)")
    "Vector256", (false, "ReadOnlyCollection<Hash256>", "new Hash256({0}.GetString())", "reader.ReadVector256()")
    "Array<Majority>", (false, "ReadOnlyCollection<Majority>", "new Majority({0})", "new Majority(ref reader)")
    "AccountRootFlags", (true, "AccountRootFlags", "(AccountRootFlags){0}.GetUInt32()", "(AccountRootFlags)reader.ReadUInt32()")
    "RippleStateFlags", (true, "RippleStateFlags", "(RippleStateFlags){0}.GetUInt32()", "(RippleStateFlags)reader.ReadUInt32()")
    "SignerListFlags", (true, "SignerListFlags", "(SignerListFlags){0}.GetUInt32()", "(SignerListFlags)reader.ReadUInt32()")
    "OfferFlags", (true, "OfferFlags", "(OfferFlags){0}.GetUInt32()", "(OfferFlags)reader.ReadUInt32()")
    "DateTimeOffset", (true, "DateTimeOffset", "Epoch.ToDateTimeOffset({0}.GetUInt32())", "Epoch.ToDateTimeOffset(reader.ReadUInt32())")
]

let getFieldType (ledgerField : LedgerField) : string =
    let isValueType, netType, _, _ = knownTypes.[ledgerField.Type]
    if not ledgerField.Optional then
        netType
    else
        if isValueType then
            sprintf "%s?" netType
        else
            netType
            
let getInnerType (ledgerField : LedgerField) : string Option =
    if ledgerField.Type.StartsWith("Array<") then
        let _, collection, _, _ = knownTypes.[ledgerField.Type]
        let index = collection.IndexOf '<' + 1
        Some (collection.Substring(index, collection.Length - (index + 1)))
    elif ledgerField.Type = "Vector256" then
        Some "Hash256"
    else
        None
        
let readJsonField (ledgerField : LedgerField) (json : string) : string =
    let _, _, jsonReader, _ = knownTypes.[ledgerField.Type]
    String.Format(jsonReader, json)
            
let readStField (ledgerField : LedgerField) : string =
    let _, _, _, stReader = knownTypes.[ledgerField.Type]
    stReader

/// Emit the LedgerObjects
let emitLedger (writer : TextWriter) (document : JsonDocument) =
    // Need the type index so we know the order to read ST fields in    
    let types = 
        document.RootElement.GetProperty("TYPES").EnumerateObject()
        |> Seq.map(fun kv -> trimSt kv.Name, kv.Value.GetInt32())
        |> Map.ofSeq

    // We want to map by name then by type
    let fields =
        document.RootElement.GetProperty("FIELDS").EnumerateArray()
        |> Seq.map(fun tuple ->
            let name = tuple.[0].GetString()
            let object = tuple.[1]
            let typ = object.GetProperty("type").GetString()
            let nth = object.GetProperty("nth").GetInt32()
            // trim ST off the front of types, we're nesting this in St anyway
            let key = trimSt typ
            name, (key, nth)
        )
        |> Map.ofSeq
        
    let field name doc =
        let fieldType, _ = fields.[name]
        { Name = name; Doc = doc; Optional = false; Type = fieldType }

    let fieldOpt name doc =
        let fieldType, _ = fields.[name]
        { Name = name; Doc = doc; Optional = true; Type = fieldType }

    let withOverride (typeName : string) (field : LedgerField) = 
        { field with Type = typeName}

    // For each ledger type we want to emit each field, then the Json constructor then the StReader constructor.
    // We'll write the Id calculation functions manually
    
    let ownerNode = { Name = "OwnerNode"; Type = "UInt64"; Optional = false; Doc = "A hint indicating which page of the sender's owner directory links to this object, in case the directory consists of multiple pages. Note: The object does not contain a direct link to the owner directory containing it, since that value can be derived from the Account." }
    let previousTxnID = { Name = "PreviousTxnID"; Type = "Hash256"; Optional = false; Doc = "The identifying hash of the transaction that most recently modified this object." }
    let previousTxnLgrSeq = { Name = "PreviousTxnLgrSeq"; Type = "UInt32"; Optional = false; Doc = "The index of the ledger that contains the transaction that most recently modified this object." }

    let ledgerTypes = [
        {
            Name = "AccountRoot"
            Doc = "The AccountRoot object type describes a single account, its settings, and XRP balance."
            Fields = [
                field "Account" "The identifying address of this account, such as rf1BiGeXwwQoi8Z2ueFYTEXSwuJYfV2Jpn."
                field "Balance" "The account's current XRP balance in drops." |> withOverride "XrpAmount"
                field "Flags" "A bit-map of boolean flags enabled for this account." |> withOverride "AccountRootFlags"
                field "OwnerCount" "The number of objects this account owns in the ledger, which contributes to its owner reserve."
                previousTxnID
                previousTxnLgrSeq
                field "Sequence" "The sequence number of the next valid transaction for this account. (Each account starts with Sequence = 1 and increases each time a transaction is made.)"
                fieldOpt "AccountTxnID" "The identifying hash of the transaction most recently sent by this account. This field must be enabled to use the AccountTxnID transaction field. To enable it, send an AccountSet transaction with the asfAccountTxnID flag enabled."
                fieldOpt "Domain" "A domain associated with this account. In JSON, this is the hexadecimal for the ASCII representation of the domain."
                fieldOpt "EmailHash" "The md5 hash of an email address. Clients can use this to look up an avatar through services such as Gravatar."
                fieldOpt "MessageKey" "A public key that may be used to send encrypted messages to this account. In JSON, uses hexadecimal. Must be exactly 33 bytes, with the first byte indicating the key type: 0x02 or 0x03 for secp256k1 keys, 0xED for Ed25519 keys."
                fieldOpt "RegularKey" "The address of a key pair that can be used to sign transactions for this account instead of the master key. Use a SetRegularKey transaction to change this value."
                fieldOpt "TickSize" "How many significant digits to use for exchange rates of Offers involving currencies issued by this address. Valid values are 3 to 15, inclusive. (Added by the TickSize amendment.)"
                fieldOpt "TransferRate" "A transfer fee to charge other users for sending currency issued by this account to each other."
            ]
        }
        {
            Name = "Amendments"
            Doc = "The Amendments object type contains a list of Amendments that are currently active. Each ledger version contains at most one Amendments object."
            Fields = [
                fieldOpt "Amendments" "Array of 256-bit amendment IDs for all currently-enabled amendments. If omitted, there are no enabled amendments."
                fieldOpt "Majorities" "Array of objects describing the status of amendments that have majority support but are not yet enabled. If omitted, there are no pending amendments with majority support." |> withOverride "Array<Majority>"
                field "Flags" "A bit-map of boolean flags. No flags are defined for the Amendments object type, so this value is always 0."
            ]
        }
        {
            Name = "Check"
            Doc = "A Check object describes a check, similar to a paper personal check, which can be cashed by its destination to get money from its sender. (The potential payment has already been approved by its sender, but no money moves until it is cashed. Unlike an Escrow, the money for a Check is not set aside, so cashing the Check could fail due to lack of funds.)"
            Fields = [
                field "Account" "The sender of the Check. Cashing the Check debits this address's balance."
                field "Destination" "The intended recipient of the Check. Only this address can cash the Check, using a CheckCash transaction."
                field "Flags" "A bit-map of boolean flags. No flags are defined for Checks, so this value is always 0."
                ownerNode
                previousTxnID
                previousTxnLgrSeq
                field "SendMax" "The maximum amount of currency this Check can debit the sender. If the Check is successfully cashed, the destination is credited in the same currency for up to this amount."
                field "Sequence" "The sequence number of the CheckCreate transaction that created this check."
                fieldOpt "DestinationNode" "A hint indicating which page of the destination's owner directory links to this object, in case the directory consists of multiple pages."
                fieldOpt "DestinationTag" "An arbitrary tag to further specify the destination for this Check, such as a hosted recipient at the destination address."
                fieldOpt "Expiration" "Indicates the time after which this Check is considered expired. See Specifying Time for details." |> withOverride "DateTimeOffset"
                fieldOpt "InvoiceID" "Arbitrary 256-bit hash provided by the sender as a specific reason or identifier for this Check."
                fieldOpt "SourceTag" "An arbitrary tag to further specify the source for this Check, such as a hosted recipient at the sender's address."
            ]
        }
        {
            Name = "DepositPreauth"
            Doc = "A DepositPreauth object tracks a preauthorization from one account to another. DepositPreauth transactions create these objects."
            Fields = [
                field "Account" "The account that granted the preauthorization. (The destination of the preauthorized payments.)"
                field "Authorize" "The account that received the preauthorization. (The sender of the preauthorized payments.)"
                field "Flags" "A bit-map of boolean flags. No flags are defined for DepositPreauth objects, so this value is always 0."
                ownerNode
                previousTxnID
                previousTxnLgrSeq
            ]
        }
        {
            Name = "DirectoryNode"
            Doc = "The DirectoryNode object type provides a list of links to other objects in the ledger's state tree. A single conceptual Directory takes the form of a doubly linked list, with one or more DirectoryNode objects each containing up to 32 IDs of other objects. The first object is called the root of the directory, and all objects other than the root object can be added or deleted as necessary."
            Fields = [
                field "Flags" "A bit-map of boolean flags enabled for this directory. Currently, the protocol defines no flags for DirectoryNode objects."
                field "RootIndex" "The ID of root object for this directory."
                field "Indexes" "The contents of this Directory: an array of IDs of other objects."
                fieldOpt "IndexNext" "If this Directory consists of multiple pages, this ID links to the next object in the chain, wrapping around at the end."
                fieldOpt "IndexPrevious" "If this Directory consists of multiple pages, this ID links to the previous object in the chain, wrapping around at the beginning."
                fieldOpt "Owner" "(Owner Directories only) The address of the account that owns the objects in this directory."
                fieldOpt "ExchangeRate" "(Offer Directories only) DEPRECATED. Do not use."
                fieldOpt "TakerPaysCurrency" "(Offer Directories only) The currency code of the TakerPays amount from the offers in this directory." |> withOverride "Hash160_CurrencyCode"
                fieldOpt "TakerPaysIssuer" "(Offer Directories only) The issuer of the TakerPays amount from the offers in this directory." |> withOverride "Hash160_AccountId"
                fieldOpt "TakerGetsCurrency" "(Offer Directories only) The currency code of the TakerGets amount from the offers in this directory." |> withOverride "Hash160_CurrencyCode"
                fieldOpt "TakerGetsIssuer" "(Offer Directories only) The issuer of the TakerGets amount from the offers in this directory." |> withOverride "Hash160_AccountId"
            ]
        }
        {
            Name = "Escrow"
            Doc = "The Escrow object type represents a held payment of XRP waiting to be executed or canceled. An EscrowCreate transaction creates an Escrow object in the ledger."
            Fields = [
                field "Account" "The address of the owner (sender) of this held payment. This is the account that provided the XRP, and gets it back if the held payment is canceled."
                field "Destination" "The destination address where the XRP is paid if the held payment is successful."
                field "Amount" "The amount of XRP, in drops, to be delivered by the held payment."
                fieldOpt "Condition" "A PREIMAGE-SHA-256 crypto-condition , as hexadecimal. If present, the EscrowFinish transaction must contain a fulfillment that satisfies this condition."
                fieldOpt "CancelAfter" "The held payment can be canceled if and only if this field is present and the time it specifies has passed. Specifically, this is specified as seconds since the Ripple Epoch and it \"has passed\" if it's earlier than the close time of the previous validated ledger." |> withOverride "DateTimeOffset"
                fieldOpt "FinishAfter" "The time, in seconds since the Ripple Epoch, after which this held payment can be finished. Any EscrowFinish transaction before this time fails. (Specifically, this is compared with the close time of the previous validated ledger.)" |> withOverride "DateTimeOffset"
                field "Flags" "A bit-map of boolean flags. No flags are defined for the Escrow type, so this value is always 0."
                fieldOpt "SourceTag" "An arbitrary tag to further specify the source for this held payment, such as a hosted recipient at the owner's address."
                fieldOpt "DestinationTag" "An arbitrary tag to further specify the destination for this held payment, such as a hosted recipient at the destination address."
                fieldOpt "DestinationNode" "A hint indicating which page of the destination's owner directory links to this object, in case the directory consists of multiple pages. Omitted on escrows created before enabling the fix1523 amendment."
                ownerNode
                previousTxnID
                previousTxnLgrSeq
            ]
        }
        {
            Name = "FeeSettings"
            Doc = "The FeeSettings object type contains the current base transaction cost and reserve amounts as determined by fee voting. Each ledger version contains at most one FeeSettings object."
            Fields = [
                field "BaseFee" "The transaction cost of the \"reference transaction\" in drops of XRP as hexadecimal."
                field "ReferenceFeeUnits" "The BaseFee translated into \"fee units\"."
                field "ReserveBase" "The base reserve for an account in the XRP Ledger, as drops of XRP."
                field "ReserveIncrement" "The incremental owner reserve for owning objects, as drops of XRP."
                field "Flags" "A bit-map of boolean flags for this object. No flags are defined for this type."
            ]
        }
        {
            Name = "LedgerHashes"
            Doc = "The LedgerHashes object type contains a history of prior ledgers that led up to this ledger version, in the form of their hashes. Objects of this ledger type are modified automatically in the process of closing a ledger."
            Fields = [            
                field "LastLedgerSequence" "The Ledger Index of the last entry in this object's Hashes array."
                field "Hashes" "An array of up to 256 ledger hashes. The contents depend on which sub-type of LedgerHashes object this is."
                field "Flags" "A bit-map of boolean flags for this object. No flags are defined for this type."            
            ]
        }
        {
            Name = "NegativeUNL"
            Doc = "The NegativeUNL object type contains the current status of the Negative UNL, a list of trusted validators currently believed to be offline."
            Fields = [
                fieldOpt "DisabledValidators" "A list of DisabledValidator objects (see below), each representing a trusted validator that is currently disabled." |> withOverride "Array<DisabledValidator>"
                fieldOpt "ValidatorToDisable" "The public key of a trusted validator that is scheduled to be disabled in the next flag ledger."
                fieldOpt "ValidatorToReEnable" "The public key of a trusted validator in the Negative UNL that is scheduled to be re-enabled in the next flag ledger."
            ]
        }
        {
            Name = "Offer"
            Doc = "The Offer object type describes an offer to exchange currencies, more traditionally known as an order, in the XRP Ledger's distributed exchange. An OfferCreate transaction only creates an Offer object in the ledger when the offer cannot be fully executed immediately by consuming other offers already in the ledger."
            Fields = [
                field "Flags" "A bit-map of boolean flags enabled for this offer." |> withOverride "OfferFlags"
                field "Account" "The address of the account that owns this offer."
                field "Sequence" "The Sequence value of the OfferCreate transaction that created this Offer object. Used in combination with the Account to identify this Offer."
                field "TakerPays" "The remaining amount and type of currency requested by the offer creator."
                field "TakerGets" "The remaining amount and type of currency being provided by the offer creator."
                field "BookDirectory" "The ID of the Offer Directory that links to this offer."
                field "BookNode" "A hint indicating which page of the offer directory links to this object, in case the directory consists of multiple pages."
                fieldOpt "Expiration" "Indicates the time after which this offer is considered unfunded. See Specifying Time for details." |> withOverride "DateTimeOffset"
                ownerNode
                previousTxnID
                previousTxnLgrSeq
            ]
        }
        {
            Name = "PayChannel"
            Doc = "The PayChannel object type represents a payment channel. Payment channels enable small, rapid off-ledger payments of XRP that can be later reconciled with the consensus ledger. A payment channel holds a balance of XRP that can only be paid out to a specific destination address until the channel is closed. Any unspent XRP is returned to the channel's owner (the source address that created and funded it) when the channel closes."
            Fields = [
                field "Account" "The source address that owns this payment channel. This comes from the sending address of the transaction that created the channel."
                field "Destination" "The destination address for this payment channel. While the payment channel is open, this address is the only one that can receive XRP from the channel. This comes from the Destination field of the transaction that created the channel."
                field "Amount" "Total XRP, in drops, that has been allocated to this channel. This includes XRP that has been paid to the destination address. This is initially set by the transaction that created the channel and can be increased if the source address sends a PaymentChannelFund transaction."
                field "Balance" "Total XRP, in drops, already paid out by the channel. The difference between this value and the Amount field is how much XRP can still be paid to the destination address with PaymentChannelClaim transactions. If the channel closes, the remaining difference is returned to the source address."
                field "PublicKey" "Public key, in hexadecimal, of the key pair that can be used to sign claims against this channel. This can be any valid secp256k1 or Ed25519 public key. This is set by the transaction that created the channel and must match the public key used in claims against the channel. The channel source address can also send XRP from this channel to the destination without signed claims."
                field "SettleDelay" "Number of seconds the source address must wait to close the channel if it still has any XRP in it. Smaller values mean that the destination address has less time to redeem any outstanding claims after the source address requests to close the channel. Can be any value that fits in a 32-bit unsigned integer (0 to 2^32-1). This is set by the transaction that creates the channel."
                ownerNode
                previousTxnID
                previousTxnLgrSeq
                field "Flags" "A bit-map of boolean flags enabled for this payment channel. Currently, the protocol defines no flags for PayChannel objects."
                fieldOpt "DestinationNode" "A hint indicating which page of the destination's owner directory links to this object, in case the directory consists of multiple pages."
                fieldOpt "Expiration" "The mutable expiration time for this payment channel, in seconds since the Ripple Epoch. The channel is expired if this value is present and smaller than the previous ledger's close_time field. See Setting Channel Expiration for more details." |> withOverride "DateTimeOffset"
                fieldOpt "CancelAfter" "The immutable expiration time for this payment channel, in seconds since the Ripple Epoch. This channel is expired if this value is present and smaller than the previous ledger's close_time field. This is optionally set by the transaction that created the channel, and cannot be changed." |> withOverride "DateTimeOffset"
                fieldOpt "SourceTag" "An arbitrary tag to further specify the source for this payment channel, such as a hosted recipient at the owner's address."
                fieldOpt "DestinationTag" "An arbitrary tag to further specify the destination for this payment channel, such as a hosted recipient at the destination address."
            ]
        }
        {
            Name = "RippleState"
            Doc = "The RippleState object type connects two accounts in a single currency. Conceptually, a RippleState object represents two trust lines between the accounts, one from each side. Each account can change the settings for its side of the RippleState object, but the balance is a single shared value. A trust line that is entirely in its default state is considered the same as a trust line that does not exist, so rippled deletes RippleState objects when their properties are entirely default."
            Fields = [
                field "Flags" "A bit-map of boolean options enabled for this object." |> withOverride "RippleStateFlags"
                field "Balance" "The balance of the trust line, from the perspective of the low account. A negative balance indicates that the low account has issued currency to the high account. The issuer in this is always set to the neutral value ACCOUNT_ONE." |> withOverride "IssuedAmount"
                field "LowLimit" "The limit that the low account has set on the trust line. The issuer is the address of the low account that set this limit." |> withOverride "IssuedAmount"
                field "HighLimit" "The limit that the high account has set on the trust line. The issuer is the address of the high account that set this limit." |> withOverride "IssuedAmount"
                previousTxnID
                previousTxnLgrSeq
                field "LowNode" "(Omitted in some historical ledgers) A hint indicating which page of the low account's owner directory links to this object, in case the directory consists of multiple pages."
                field "HighNode" "(Omitted in some historical ledgers) A hint indicating which page of the high account's owner directory links to this object, in case the directory consists of multiple pages."
                fieldOpt "LowQualityIn" "The inbound quality set by the low account, as an integer in the implied ratio LowQualityIn:1,000,000,000. As a special case, the value 0 is equivalent to 1 billion, or face value."
                fieldOpt "LowQualityOut" "The outbound quality set by the low account, as an integer in the implied ratio LowQualityOut:1,000,000,000. As a special case, the value 0 is equivalent to 1 billion, or face value."
                fieldOpt "HighQualityIn" "The inbound quality set by the high account, as an integer in the implied ratio HighQualityIn:1,000,000,000. As a special case, the value 0 is equivalent to 1 billion, or face value."
                fieldOpt "HighQualityOut" "The outbound quality set by the high account, as an integer in the implied ratio HighQualityOut:1,000,000,000. As a special case, the value 0 is equivalent to 1 billion, or face value."
            ]
        }
        {
            Name = "SignerList"
            Doc = "The SignerList object type represents a list of parties that, as a group, are authorized to sign a transaction in place of an individual account. You can create, replace, or remove a signer list using a SignerListSet transaction."
            Fields = [
            
                field "Flags" "A bit-map of Boolean flags enabled for this signer list. For more information, see SignerList Flags." |> withOverride "SignerListFlags"
                ownerNode
                previousTxnID
                previousTxnLgrSeq
                field "SignerEntries" "An array of Signer Entry objects representing the parties who are part of this signer list." |> withOverride "Array<SignerEntry>"
                field "SignerListID" "An ID for this signer list. Currently always set to 0. If a future amendment allows multiple signer lists for an account, this may change."
                field "SignerQuorum" "A target number for signer weights. To produce a valid signature for the owner of this SignerList, the signers must provide valid signatures whose weights sum to this value or more."
            ]
        }
        {
            Name = "Ticket"
            Doc = "The Ticket object type represents a Ticket, which tracks an account sequence number that has been set aside for future use. You can create new tickets with a TicketCreate transaction. New in: rippled 1.7.0 "
            Fields = [
                field "Account" "The account that owns this Ticket."
                field "Flags" "A bit-map of Boolean flags enabled for this Ticket. Currently, there are no flags defined for Tickets."
                field "TicketSequence" "The Sequence Number this Ticket sets aside."
                ownerNode
                previousTxnID
                previousTxnLgrSeq
            ]
        }
    ]

    for ledgerType in ledgerTypes do
        
        writer.WriteLine("    /// <summary>")
        writer.WriteLine("    /// {0}", ledgerType.Doc)
        writer.WriteLine("    /// </summary>")
        writer.WriteLine("    public sealed partial class {0} : LedgerObject", ledgerType.Name)
        writer.WriteLine("    {")
        for field in ledgerType.Fields do
            // Need to override Ammendments because it can't be the same name as the type
            let fieldName = if field.Name = "Amendments" then "AmendmentIDs" else field.Name

            writer.WriteLine("        /// <summary>")
            let opt = if field.Optional then "(Optional) " else ""
            writer.WriteLine("        /// {0}{1}", opt, field.Doc)
            writer.WriteLine("        /// </summary>")
            writer.WriteLine("        public {0} {1} {{ get; private set; }}", getFieldType field, fieldName);
            writer.WriteLine()

        // JSON Constructor
        writer.WriteLine("        internal {0}(JsonElement json)", ledgerType.Name)
        writer.WriteLine("        {")
        writer.WriteLine("            if (json.GetProperty(\"LedgerEntryType\").GetString() != \"{0}\")", ledgerType.Name)
        writer.WriteLine("            {")
        writer.WriteLine("                throw new ArgumentException(\"Expected property \\\"LedgerEntryType\\\" to be \\\"{0}\\\"\", \"json\");", ledgerType.Name);
        writer.WriteLine("            }")
        if ledgerType.Fields |> Seq.exists (fun field -> field.Optional || field.Type.StartsWith "Array<" || field.Type = "Vector256") then
            writer.WriteLine("            JsonElement element;")
        writer.WriteLine()
        for field in ledgerType.Fields do
            let fieldName = if field.Name = "Amendments" then "AmendmentIDs" else field.Name

            let writeArray inner =
                writer.WriteLine("            var {0}Array = new {1}[element.GetArrayLength()];", fieldName, inner)
                writer.WriteLine("            for (int i = 0; i < {0}Array.Length; ++i)", fieldName)
                writer.WriteLine("            {")
                writer.WriteLine("                {0}Array[i] = {1};", fieldName, readJsonField field "element[i]")
                writer.WriteLine("            }")
                writer.WriteLine("            {0} = Array.AsReadOnly({0}Array);", fieldName)

            if field.Optional then
                writer.WriteLine("            if (json.TryGetProperty(\"{0}\", out element))", field.Name)
                writer.WriteLine("            {")
                match getInnerType field with
                | Some inner ->
                    writeArray inner
                | None ->
                    writer.WriteLine("                {0} = {1};", fieldName, readJsonField field "element")
                writer.WriteLine("            }")
            else
                let reader = sprintf "json.GetProperty(\"%s\")" field.Name
                match getInnerType field with
                | Some inner ->
                    writer.WriteLine("            element = {0};", reader)
                    writeArray inner
                | None ->
                    writer.WriteLine("            {0} = {1};", fieldName, readJsonField field reader)
        writer.WriteLine("        }")
        writer.WriteLine()

        // StReader Constructor
        writer.WriteLine("        internal {0}(ref StReader reader)", ledgerType.Name)
        writer.WriteLine("        {")        
        writer.WriteLine("            StFieldId fieldId = reader.ReadFieldId();")

        // Need to sort the fields based on type and field index
        let sortedFields = 
            ledgerType.Fields
            |> List.sortBy (fun field -> 
                let (fieldType, fieldNth) = fields.[field.Name]
                let typeIndex = types.[fieldType]
                typeIndex, fieldNth            
            )
            |> List.toArray

        for i = 0 to sortedFields.Length - 1 do
            let field = sortedFields.[i]
            let (originalType, _) = fields.[field.Name]
            let fieldName = if field.Name = "Amendments" then "AmendmentIDs" else field.Name

            // Read the first field id
            let readNextField (spacer : string) =
                // Won't be any field if we're on the last field
                if i <> sortedFields.Length - 1 then                
                    writer.WriteLine("{0}if (!reader.TryReadFieldId(out fieldId))", spacer)
                    writer.WriteLine("{0}{{", spacer)
                    // If we couldn't find another field id but there are still non-optional fields then throw, else just early return
                    if sortedFields |> Seq.skip (i + 1) |> Seq.exists (fun field -> not field.Optional) then
                        writer.WriteLine("{0}    throw new Exception(\"End of st data reached but non-optional fields still not set\");", spacer)
                    else
                        writer.WriteLine("{0}    return;", spacer)
                    writer.WriteLine("{0}}}", spacer)

            let fieldId = sprintf "StFieldId.%s_%s" originalType field.Name

            let writeArray (spacer : string) (inner : string) =
                writer.WriteLine("{0}var {1}List = new System.Collections.Generic.List<{2}>();", spacer, fieldName, inner)
                writer.WriteLine("{0}while (true)", spacer)
                writer.WriteLine("{0}{{", spacer)
                writer.WriteLine("{0}    fieldId = reader.ReadFieldId();", spacer)
                writer.WriteLine("{0}    if (fieldId == StFieldId.Array_ArrayEndMarker)", spacer)
                writer.WriteLine("{0}    {{", spacer)
                readNextField (sprintf "%s        " spacer)
                writer.WriteLine("{0}        break;", spacer)
                writer.WriteLine("{0}    }}", spacer)
                writer.WriteLine("{0}    if (fieldId != StFieldId.Object_{1})", spacer, inner)
                writer.WriteLine("{0}    {{", spacer)
                writer.WriteLine("{0}        throw new Exception(string.Format(\"Expected {{0}} but got {{1}}\", StFieldId.Object_{1}, fieldId));", spacer, inner)
                writer.WriteLine("{0}    }}", spacer)


                writer.WriteLine("{0}    {1}List.Add({2});", spacer, fieldName, readStField field)
                writer.WriteLine("{0}}}", spacer)
                writer.WriteLine("{0}{1} = {1}List.AsReadOnly();", spacer, fieldName)

            if field.Optional then
                writer.WriteLine("            if (fieldId == {0})", fieldId)
                writer.WriteLine("            {")
                if field.Type = "Vector256" then
                    // Looks like an array but isn't in ST format
                    writer.WriteLine("                {0} = Array.AsReadOnly({1});", fieldName, readStField field)
                    readNextField "            "

                else
                    match getInnerType field with
                    | Some inner ->
                        writeArray "                " inner
                    | None ->
                        writer.WriteLine("                {0} = {1};", fieldName, readStField field)
                        readNextField "                "
                writer.WriteLine("            }")
                
            else
                writer.WriteLine("            if (fieldId != {0})", fieldId)
                writer.WriteLine("            {")
                writer.WriteLine("                throw new Exception(string.Format(\"Expected {{0}} but got {{1}}\", {0}, fieldId));", fieldId)
                writer.WriteLine("            }")
                if field.Type = "Vector256" then
                    writer.WriteLine("            {0} = Array.AsReadOnly({1});", fieldName, readStField field)
                    readNextField "            "
                else
                    match getInnerType field with
                    | Some inner ->
                        writeArray "            " inner
                    | None ->
                        writer.WriteLine("            {0} = {1};", fieldName, readStField field)
                        readNextField "            "


        writer.WriteLine("        }")
        writer.WriteLine()

        writer.WriteLine("    }")
        writer.WriteLine()

[<EntryPoint>]
let main argv =
    let stDefinitionsPath =
        Directory.GetCurrentDirectory()
        |> findPath

    printfn "Emiting definitions to %s" stDefinitionsPath

    let definitions = downloadDefinitions ()

    use writer = new StreamWriter(IO.File.Open(stDefinitionsPath, FileMode.Create, FileAccess.Write))

    writer.WriteLine("// This file is auto generated by GenerateDefinitions, do not edit by hand")
    writer.WriteLine("using System;")
    writer.WriteLine("using System.Buffers;")
    writer.WriteLine("using System.Collections.ObjectModel;")
    writer.WriteLine("using System.Text.Json;")
    writer.WriteLine("using Ibasa.Ripple.St;")
    
    writer.WriteLine("namespace Ibasa.Ripple.St")
    writer.WriteLine("{")

    emitTransactionType writer definitions
    emitLedgerEntry writer definitions
    emitTypes writer definitions
    emitFields writer definitions
    writer.WriteLine("}")

    
    writer.WriteLine("namespace Ibasa.Ripple")
    writer.WriteLine("{")

    emitLedger writer definitions
    
    writer.WriteLine("}")
    writer.WriteLine("")

    0