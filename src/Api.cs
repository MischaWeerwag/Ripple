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
        protected readonly System.Buffers.ArrayBufferWriter<byte> jsonBuffer;
        protected readonly System.Text.Json.Utf8JsonWriter jsonWriter;

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
            jsonWriter.WriteBoolean("binary", true);
            if (request.Limit.HasValue)
            {
                jsonWriter.WriteNumber("limit", request.Limit.Value);
            }
            if (request.Marker.HasValue)
            {
                jsonWriter.WritePropertyName("marker");
                request.Marker.Value.WriteTo(jsonWriter);
            }
            WriteFooter(jsonWriter);
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();
            var response = await SendReceiveAsync(requestId, jsonBuffer.WrittenMemory, cancellationToken);
            return new LedgerDataResponse(response);
        }

        /// <summary>
        /// The ledger_entry method returns a single ledger object from the XRP Ledger in its raw format.
        /// See ledger format for information on the different types of objects you can retrieve.
        /// </summary>
        public async Task<LedgerEntryResponse> LedgerEntry(LedgerEntryRequest request, CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            jsonWriter.Reset();
            jsonWriter.WriteStartObject();
            var requestId = WriteHeader(jsonWriter, "ledger_entry");
            LedgerSpecification.Write(jsonWriter, request.Ledger);
            jsonWriter.WriteBoolean("binary", true);
            jsonWriter.WriteString("index", request.Index.ToString());
            WriteFooter(jsonWriter);
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();
            var response = await SendReceiveAsync(requestId, jsonBuffer.WrittenMemory, cancellationToken);
            return new LedgerEntryResponse(response);
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
            if (request.Limit.HasValue)
            {
                jsonWriter.WriteNumber("limit", request.Limit.Value);
            }
            if (request.Marker.HasValue)
            {
                jsonWriter.WritePropertyName("marker");
                request.Marker.Value.WriteTo(jsonWriter);
            }
            WriteFooter(jsonWriter);
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();
            var response = await SendReceiveAsync(requestId, jsonBuffer.WrittenMemory, cancellationToken);
            return new AccountLinesResponse(response, request, this);
        }

        /// <summary>
        /// The account_channels method returns information about an account's Payment Channels.
        /// This includes only channels where the specified account is the channel's source, not the destination.
        /// (A channel's "source" and "owner" are the same.)
        /// All information retrieved is relative to a particular version of the ledger.
        /// </summary>
        public async Task<AccountChannelsResponse> AccountChannels(AccountChannelsRequest request, CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            jsonWriter.Reset();
            jsonWriter.WriteStartObject();
            var requestId = WriteHeader(jsonWriter, "account_channels");
            LedgerSpecification.Write(jsonWriter, request.Ledger);
            jsonWriter.WriteString("account", request.Account.ToString());
            if (request.DestinationAccount.HasValue)
            {
                jsonWriter.WriteString("destination_account", request.DestinationAccount.Value.ToString());
            }
            if (request.Limit.HasValue)
            {
                jsonWriter.WriteNumber("limit", request.Limit.Value);
            }
            if (request.Marker.HasValue)
            {
                jsonWriter.WritePropertyName("marker");
                request.Marker.Value.WriteTo(jsonWriter);
            }
            WriteFooter(jsonWriter);
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();
            var response = await SendReceiveAsync(requestId, jsonBuffer.WrittenMemory, cancellationToken);
            return new AccountChannelsResponse(response, request, this);
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
                    jsonWriter.WriteStartArray("hotwallet");
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

        /// <summary>
        /// The deposit_authorized command indicates whether one account is authorized to send payments directly to another.
        /// See Deposit Authorization for information on how to require authorization to deliver money to your account.
        /// </summary>
        public async Task<DepositAuthorizedResponse> DepositAuthorized(DepositAuthorizedRequest request, CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            jsonWriter.Reset();
            jsonWriter.WriteStartObject();
            var requestId = WriteHeader(jsonWriter, "deposit_authorized");
            LedgerSpecification.Write(jsonWriter, request.Ledger);
            jsonWriter.WriteString("source_account", request.SourceAccount.ToString());
            jsonWriter.WriteString("destination_account", request.DestinationAccount.ToString());
            WriteFooter(jsonWriter);
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();
            var response = await SendReceiveAsync(requestId, jsonBuffer.WrittenMemory, cancellationToken);
            return new DepositAuthorizedResponse(response);
        }

        /// <summary>
        /// The book_offers method retrieves a list of offers, also known as the order book, between two currencies.
        /// </summary>
        public async Task<BookOffersResponse> BookOffers(BookOffersRequest request, CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            jsonWriter.Reset();
            jsonWriter.WriteStartObject();
            var requestId = WriteHeader(jsonWriter, "book_offers");
            LedgerSpecification.Write(jsonWriter, request.Ledger);
            if (request.Limit.HasValue)
            {
                jsonWriter.WriteNumber("limit", request.Limit.Value);
            }
            if (request.Taker.HasValue)
            {
                jsonWriter.WriteString("taker", request.Taker.Value.ToString());
            }
            jsonWriter.WritePropertyName("taker_gets");
            request.TakerGets.WriteJson(jsonWriter);
            jsonWriter.WritePropertyName("taker_pays");
            request.TakerPays.WriteJson(jsonWriter);
            WriteFooter(jsonWriter);
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();
            var response = await SendReceiveAsync(requestId, jsonBuffer.WrittenMemory, cancellationToken);
            return new BookOffersResponse(response);
        }

        /// <summary>
        /// The ripple_path_find method is a simplified version of the path_find method that provides a single response with a payment path you can use right away.
        /// It is available in both the WebSocket and JSON-RPC APIs. However, the results tend to become outdated as time passes.
        /// Instead of making multiple calls to stay updated, you should instead use the path_find method to subscribe to continued updates where possible.
        /// </summary>
        public async Task<RipplePathFindResponse> RipplePathFind(RipplePathFindRequest request, CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            jsonWriter.Reset();
            jsonWriter.WriteStartObject();
            var requestId = WriteHeader(jsonWriter, "ripple_path_find");
            LedgerSpecification.Write(jsonWriter, request.Ledger);
            jsonWriter.WriteString("source_account", request.SourceAccount.ToString());
            jsonWriter.WriteString("destination_account", request.DestinationAccount.ToString());
            jsonWriter.WritePropertyName("destination_amount");
            request.DestinationAmount.WriteJson(jsonWriter);
            if (request.SendMax.HasValue)
            {
                jsonWriter.WritePropertyName("send_max");
                request.SendMax.Value.WriteJson(jsonWriter);
            }
            if (request.SourceCurrencies != null)
            {
                jsonWriter.WriteStartArray("source_currencies");
                foreach (var entry in request.SourceCurrencies)
                {
                    entry.WriteJson(jsonWriter);
                }
                jsonWriter.WriteEndArray();
            }
            WriteFooter(jsonWriter);
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();
            var response = await SendReceiveAsync(requestId, jsonBuffer.WrittenMemory, cancellationToken);
            return new RipplePathFindResponse(response);
        }

        /// <summary>
        /// The channel_authorize method creates a signature that can be used to redeem a specific amount of XRP from a payment channel.
        /// </summary>
        public async Task<ReadOnlyMemory<byte>> ChannelAuthorize(ChannelAuthorizeRequest request, CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            jsonWriter.Reset();
            jsonWriter.WriteStartObject();
            var requestId = WriteHeader(jsonWriter, "channel_authorize");
            jsonWriter.WriteString("channel_id", request.ChannelId.ToString());
            jsonWriter.WriteString("amount", request.Amount.Drops.ToString());
            if (request.Seed.HasValue)
            {
                var seed = request.Seed.Value;
                var type = seed.Type == KeyType.Secp256k1 ? "secp256k1" : "ed25519";
                jsonWriter.WriteString("key_type", type);
                jsonWriter.WriteString("seed", seed.ToString());
            }
            if (request.Passphrase != null) { jsonWriter.WriteString("passphrase", request.Passphrase); }
            if (request.Secret != null) { jsonWriter.WriteString("secret", request.Secret); }
            WriteFooter(jsonWriter);
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();
            var response = await SendReceiveAsync(requestId, jsonBuffer.WrittenMemory, cancellationToken);
            return response.GetProperty("signature").GetBytesFromBase16();
        }

        /// <summary>
        /// The channel_verify method checks the validity of a signature that can be used to redeem a specific amount of XRP from a payment channel.
        /// </summary>
        public async Task<bool> ChannelVerify(ChannelVerifyRequest request, CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            jsonWriter.Reset();
            jsonWriter.WriteStartObject();
            var requestId = WriteHeader(jsonWriter, "channel_verify");
            jsonWriter.WriteString("channel_id", request.ChannelId.ToString());
            jsonWriter.WriteString("amount", request.Amount.Drops.ToString());
            jsonWriter.WriteBase16String("public_key", request.PublicKey.GetCanoncialBytes());
            jsonWriter.WriteBase16String("signature", request.Signature.Span);
            WriteFooter(jsonWriter);
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();
            var response = await SendReceiveAsync(requestId, jsonBuffer.WrittenMemory, cancellationToken);
            return response.GetProperty("signature_verified").GetBoolean();
        }
    }
}
