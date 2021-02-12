using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ibasa.Ripple
{
    public class RippleException : Exception
    {
        public RippleException(string message)
            : base(message)
        {
        }
    }

    public class RippleRequestException : RippleException
    {
        public System.Text.Json.JsonElement Request { get; private set; }

        public string Error { get; private set; }

        protected RippleRequestException(string message, string error, System.Text.Json.JsonElement request)
            : base(message)
        {
            Error = error;
            Request = request;
        }

        public RippleRequestException(string error, System.Text.Json.JsonElement request)
            : base(error)
        {
            Error = error;
            Request = request;
        }
    }

    public class RippleSubmitRequestException : RippleRequestException
    {
        public string ErrorException { get; private set; }

        public RippleSubmitRequestException(string error, string errorException, System.Text.Json.JsonElement request)
            : base(errorException == null ? error : error + ": " + errorException, error, request)
        {
            ErrorException = errorException;
        }
    }

    public abstract class Api : IAsyncDisposable
    {
        private readonly System.Buffers.ArrayBufferWriter<byte> jsonBuffer;
        private readonly System.Text.Json.Utf8JsonWriter jsonWriter;

        protected Api()
        {
            jsonBuffer = new System.Buffers.ArrayBufferWriter<byte>();
            var jsonOptions = new System.Text.Json.JsonWriterOptions() { SkipValidation = true };
            jsonWriter = new System.Text.Json.Utf8JsonWriter(jsonBuffer, jsonOptions);
        }

        public virtual async ValueTask DisposeAsync()
        {
            await jsonWriter.DisposeAsync();
        }

        protected abstract uint WriteHeader(System.Text.Json.Utf8JsonWriter writer, string command);

        protected abstract void WriteFooter(System.Text.Json.Utf8JsonWriter writer);

        protected abstract Task<System.Text.Json.JsonElement> SendReceiveAsync(uint requestId, ReadOnlyMemory<byte> json, CancellationToken cancellationToken);

        /// <summary>
        /// The ping command returns an acknowledgement, so that clients can test the connection status and latency.
        /// </summary>
        public async Task Ping(CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            jsonWriter.Reset();
            jsonWriter.WriteStartObject();
            var requestId = WriteHeader(jsonWriter, "ping");
            WriteFooter(jsonWriter);
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();
            var _ = await SendReceiveAsync(requestId, jsonBuffer.WrittenMemory, cancellationToken);
        }

        /// <summary>
        /// The random command provides a random number to be used as a source of entropy for random number generation by clients.
        /// </summary>
        /// <returns>Random 256-bit hex value.</returns>
        public async Task<Hash256> Random(CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            jsonWriter.Reset();
            jsonWriter.WriteStartObject();
            var requestId = WriteHeader(jsonWriter, "random");
            WriteFooter(jsonWriter);
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();
            var response = await SendReceiveAsync(requestId, jsonBuffer.WrittenMemory, cancellationToken);
            return new Hash256(response.GetProperty("random").GetString());
        }

        /// <summary>
        /// Retrieve information about the public ledger.
        /// </summary>
        public async Task<LedgerResponse> Ledger(LedgerRequest request, CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            jsonWriter.Reset();
            jsonWriter.WriteStartObject();
            var requestId = WriteHeader(jsonWriter, "ledger");
            LedgerSpecification.Write(jsonWriter, request.Ledger);
            jsonWriter.WriteBoolean("binary", true);
            jsonWriter.WriteBoolean("full", request.Full);
            jsonWriter.WriteBoolean("accounts", request.Accounts);
            jsonWriter.WriteBoolean("transactions", request.Transactions);
            jsonWriter.WriteBoolean("expand", request.Expand);
            jsonWriter.WriteBoolean("owner_funds", request.OwnerFunds);
            jsonWriter.WriteBoolean("queue", request.Queue);
            WriteFooter(jsonWriter);
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();
            var response = await SendReceiveAsync(requestId, jsonBuffer.WrittenMemory, cancellationToken);
            return new LedgerResponse(response);
        }

        /// <summary>
        /// The ledger_closed method returns the unique identifiers of the most recently closed ledger. 
        /// (This ledger is not necessarily validated and immutable yet.)
        /// </summary>
        public async Task<LedgerClosedResponse> LedgerClosed(CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            jsonWriter.Reset();
            jsonWriter.WriteStartObject();
            var requestId = WriteHeader(jsonWriter, "ledger_closed");
            WriteFooter(jsonWriter);
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();
            var response = await SendReceiveAsync(requestId, jsonBuffer.WrittenMemory, cancellationToken);
            return new LedgerClosedResponse(response);
        }

        /// <summary>
        /// The ledger_current method returns the unique identifiers of the current in-progress ledger. 
        /// This command is mostly useful for testing, because the ledger returned is still in flux.
        /// </summary>
        public async Task<uint> LedgerCurrent(CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            jsonWriter.Reset();
            jsonWriter.WriteStartObject();
            var requestId = WriteHeader(jsonWriter, "ledger_current");
            WriteFooter(jsonWriter);
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();
            var response = await SendReceiveAsync(requestId, jsonBuffer.WrittenMemory, cancellationToken);
            return response.GetProperty("ledger_current_index").GetUInt32();
        }

        /// <summary>
        /// The ledger_data method retrieves contents of the specified ledger.
        /// You can iterate through several calls to retrieve the entire contents of a single ledger version.
        /// </summary>
        public async Task<LedgerDataResponse> LedgerData(LedgerDataRequest request, CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            jsonWriter.Reset();
            jsonWriter.WriteStartObject();
            var requestId = WriteHeader(jsonWriter, "ledger_data");
            LedgerSpecification.Write(jsonWriter, request.Ledger);
            if (request.Limit.HasValue)
            {
                jsonWriter.WriteNumber("limit", request.Limit.Value);
            }
            WriteFooter(jsonWriter);
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();
            var response = await SendReceiveAsync(requestId, jsonBuffer.WrittenMemory, cancellationToken);
            return new LedgerDataResponse(response);
        }

        /// <summary>
        /// The fee command reports the current state of the open-ledger requirements for the transaction cost.
        /// </summary>
        public async Task<FeeResponse> Fee(CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            jsonWriter.Reset();
            jsonWriter.WriteStartObject();
            var requestId = WriteHeader(jsonWriter, "fee");
            WriteFooter(jsonWriter);
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();
            var response = await SendReceiveAsync(requestId, jsonBuffer.WrittenMemory, cancellationToken);
            return new FeeResponse(response);
        }

        /// <summary>
        /// The account_info command retrieves information about an account, its activity, and its XRP balance. 
        /// All information retrieved is relative to a particular version of the ledger.
        /// </summary>
        public async Task<AccountInfoResponse> AccountInfo(AccountInfoRequest request, CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            jsonWriter.Reset();
            jsonWriter.WriteStartObject();
            var requestId = WriteHeader(jsonWriter, "account_info");
            LedgerSpecification.Write(jsonWriter, request.Ledger);
            jsonWriter.WriteString("account", request.Account.ToString());
            jsonWriter.WriteBoolean("queue", request.Queue);
            jsonWriter.WriteBoolean("signer_lists", request.SignerLists);
            WriteFooter(jsonWriter);
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();
            var response = await SendReceiveAsync(requestId, jsonBuffer.WrittenMemory, cancellationToken);
            return new AccountInfoResponse(response);
        }

        /// <summary>
        /// The account_currencies command retrieves a list of currencies that an account can send or receive, based on its trust lines. 
        /// (This is not a thoroughly confirmed list, but it can be used to populate user interfaces.)
        /// </summary>
        public async Task<AccountCurrenciesResponse> AccountCurrencies(AccountCurrenciesRequest request, CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            jsonWriter.Reset();
            jsonWriter.WriteStartObject();
            var requestId = WriteHeader(jsonWriter, "account_currencies");
            LedgerSpecification.Write(jsonWriter, request.Ledger);
            jsonWriter.WriteString("account", request.Account.ToString());
            WriteFooter(jsonWriter);
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();
            var response = await SendReceiveAsync(requestId, jsonBuffer.WrittenMemory, cancellationToken);
            return new AccountCurrenciesResponse(response);
        }

        /// <summary>
        /// The server_state command asks the server for various machine-readable information about the rippled server's current state.
        /// </summary>
        public async Task<ServerStateResponse> ServerState(CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            jsonWriter.Reset();
            jsonWriter.WriteStartObject();
            var requestId = WriteHeader(jsonWriter, "server_state");
            WriteFooter(jsonWriter);
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();
            var response = await SendReceiveAsync(requestId, jsonBuffer.WrittenMemory, cancellationToken);
            return new ServerStateResponse(response);
        }

        /// <summary>
        /// The account_lines method returns information about an account's trust lines, including balances in all non-XRP currencies and assets. 
        /// All information retrieved is relative to a particular version of the ledger.
        /// </summary>
        public async Task<AccountLinesResponse> AccountLines(AccountLinesRequest request, CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            jsonWriter.Reset();
            jsonWriter.WriteStartObject();
            var requestId = WriteHeader(jsonWriter, "account_lines");
            LedgerSpecification.Write(jsonWriter, request.Ledger);
            jsonWriter.WriteString("account", request.Account.ToString());
            if (request.Peer.HasValue)
            {
                jsonWriter.WriteString("peer", request.Peer.Value.ToString());
            }
            WriteFooter(jsonWriter);
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();
            var response = await SendReceiveAsync(requestId, jsonBuffer.WrittenMemory, cancellationToken);
            return new AccountLinesResponse(response, async (marker, cancellationToken) =>
                {
                    jsonBuffer.Clear();
                    jsonWriter.Reset();
                    jsonWriter.WriteStartObject();
                    var requestId = WriteHeader(jsonWriter, "account_lines");
                    LedgerSpecification.Write(jsonWriter, request.Ledger);
                    jsonWriter.WriteString("account", request.Account.ToString());
                    if (request.Peer.HasValue)
                    {
                        jsonWriter.WriteString("peer", request.Peer.Value.ToString());
                    }
                    jsonWriter.WritePropertyName("marker");
                    marker.WriteTo(jsonWriter);
                    WriteFooter(jsonWriter);
                    jsonWriter.WriteEndObject();
                    jsonWriter.Flush();
                    return await SendReceiveAsync(requestId, jsonBuffer.WrittenMemory, cancellationToken);
                }
            );
        }

        /// <summary>
        /// The submit method applies a transaction and sends it to the network to be confirmed and included in future ledgers.
        /// Submit-only mode takes a signed, serialized transaction as a binary blob, and submits it to the network as-is. 
        /// Since signed transaction objects are immutable, no part of the transaction can be modified or automatically filled in after submission.
        /// </summary>
        public async Task<SubmitResponse> Submit(SubmitRequest request, CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            jsonWriter.Reset();
            jsonWriter.WriteStartObject();
            var requestId = WriteHeader(jsonWriter, "submit");
            jsonWriter.WriteBase16String("tx_blob", request.TxBlob.Span);
            jsonWriter.WriteBoolean("fail_hard", request.FailHard);
            WriteFooter(jsonWriter);
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();
            var response = await SendReceiveAsync(requestId, jsonBuffer.WrittenMemory, cancellationToken);
            return new SubmitResponse(response);
        }

        /// <summary>
        /// The tx method retrieves information on a single transaction.
        /// </summary>
        public async Task<TransactionResponse> Tx(TxRequest request, CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            jsonWriter.Reset();
            jsonWriter.WriteStartObject();
            var requestId = WriteHeader(jsonWriter, "tx");
            jsonWriter.WriteString("transaction", request.Transaction.ToString());
            if (request.MinLedger.HasValue) { jsonWriter.WriteNumber("min_ledger", request.MinLedger.Value); }
            if (request.MaxLedger.HasValue) { jsonWriter.WriteNumber("max_ledger", request.MaxLedger.Value); }
            WriteFooter(jsonWriter);
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();
            var response = await SendReceiveAsync(requestId, jsonBuffer.WrittenMemory, cancellationToken);
            return new TransactionResponse(response);
        }

        /// <summary>
        /// The transaction_entry method retrieves information on a single transaction from a specific ledger version. 
        /// (The tx method, by contrast, searches all ledgers for the specified transaction. We recommend using that method instead.)
        /// </summary>
        public async Task<TransactionResponse> TransactionEntry(TransactionEntryRequest request, CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            jsonWriter.Reset();
            jsonWriter.WriteStartObject();
            var requestId = WriteHeader(jsonWriter, "transaction_entry");
            LedgerSpecification.Write(jsonWriter, request.Ledger);
            jsonWriter.WriteString("tx_hash", request.TxHash.ToString());
            WriteFooter(jsonWriter);
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();
            var response = await SendReceiveAsync(requestId, jsonBuffer.WrittenMemory, cancellationToken);
            return new TransactionResponse(response);
        }

        /// <summary>
        /// The noripple_check command provides a quick way to check the status of the Default Ripple field for an account and the No Ripple flag of its trust lines, compared with the recommended settings.
        /// </summary>
        public async Task<NoRippleCheckResponse> NoRippleCheck(NoRippleCheckRequest request, CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            jsonWriter.Reset();
            jsonWriter.WriteStartObject();
            var requestId = WriteHeader(jsonWriter, "noripple_check");
            LedgerSpecification.Write(jsonWriter, request.Ledger);
            jsonWriter.WriteString("account", request.Account.ToString());
            jsonWriter.WriteString("role", request.Role);
            jsonWriter.WriteBoolean("transactions", request.Transactions);
            if (request.Limit.HasValue) { jsonWriter.WriteNumber("limit", request.Limit.Value); }
            WriteFooter(jsonWriter);
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();
            var response = await SendReceiveAsync(requestId, jsonBuffer.WrittenMemory, cancellationToken);
            return new NoRippleCheckResponse(response);
        }

        /// <summary>
        /// Use the wallet_propose method to generate a key pair and XRP Ledger address. 
        /// This command only generates key and address values, and does not affect the XRP Ledger itself in any way. 
        /// To become a funded address stored in the ledger, the address must receive a Payment transaction that provides enough XRP to meet the reserve requirement.
        /// </summary>
        public async Task<WalletProposeResponse> WalletPropose(WalletProposeRequest request, CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            jsonWriter.Reset();
            jsonWriter.WriteStartObject();
            var requestId = WriteHeader(jsonWriter, "wallet_propose");
            if (request.KeyType.HasValue)
            {
                var type = request.KeyType.Value == KeyType.Secp256k1 ? "secp256k1" : "ed25519";
                jsonWriter.WriteString("key_type", type);
            }
            if (request.Passphrase != null) { jsonWriter.WriteString("passphrase", request.Passphrase); }
            if (request.Seed != null) { jsonWriter.WriteString("seed", request.Seed); }
            if (request.SeedHex != null) { jsonWriter.WriteString("seed_hex", request.SeedHex); }
            WriteFooter(jsonWriter);
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();
            var response = await SendReceiveAsync(requestId, jsonBuffer.WrittenMemory, cancellationToken);
            return new WalletProposeResponse(response);
        }

        /// <summary>
        /// The gateway_balances command calculates the total balances issued by a given account, optionally excluding amounts held by operational addresses. 
        /// New in: rippled 0.28.2 
        /// </summary>
        public async Task<GatewayBalancesResponse> GatewayBalances(GatewayBalancesRequest request, CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            jsonWriter.Reset();
            jsonWriter.WriteStartObject();
            var requestId = WriteHeader(jsonWriter, "gateway_balances");
            LedgerSpecification.Write(jsonWriter, request.Ledger);
            jsonWriter.WriteString("account", request.Account.ToString());
            if (request.HotWallet != null)
            {
                if (request.HotWallet.Length == 1)
                {
                    jsonWriter.WriteString("hotwallet", request.HotWallet[0].ToString());
                }
                else
                {
                    jsonWriter.WritePropertyName("hotwallet");
                    jsonWriter.WriteStartArray();
                    foreach (var account in request.HotWallet)
                    {
                        jsonWriter.WriteStringValue(account.ToString());
                    }
                    jsonWriter.WriteEndArray();
                }
            }
            WriteFooter(jsonWriter);
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();
            var response = await SendReceiveAsync(requestId, jsonBuffer.WrittenMemory, cancellationToken);
            return new GatewayBalancesResponse(response);
        }
    }
}
