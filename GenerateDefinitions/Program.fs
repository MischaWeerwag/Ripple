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
    writer.WriteLine("")

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
    writer.WriteLine("")
    
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
    writer.WriteLine("")
    
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
        writer.WriteLine("")

    // For each field emit the FieldId
    writer.WriteLine("    public partial struct StFieldId")
    writer.WriteLine("    {")
    for typeFields in fields do
        for field in typeFields.Value do 
            writer.WriteLine("        public static readonly StFieldId {0}_{1} = new StFieldId(StTypeCode.{0}, {2});", typeFields.Key, field.Key, uint field.Value)
        writer.WriteLine("")
    writer.WriteLine("    }")
    writer.WriteLine("")
            

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
    
    writer.WriteLine("namespace Ibasa.Ripple.St")
    writer.WriteLine("{")

    emitTransactionType writer definitions
    emitLedgerEntry writer definitions
    emitTypes writer definitions
    emitFields writer definitions
    
    writer.WriteLine("}")
    writer.WriteLine("")

    0
