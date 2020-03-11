using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Ibasa.Ripple
{
    public class RippleException : System.Exception
    {
        public RippleException(string error) 
            : base(error) 
        {
        }
    }
    public sealed class RippleRequestException : RippleException
    {
        public System.Text.Json.JsonElement Request { get; private set; }
        public RippleRequestException(string error, System.Text.Json.JsonElement request)
            : base(error)
        {
            Request = request;
        }
    }


    public abstract class Api : IAsyncDisposable
    {
        public abstract ValueTask DisposeAsync();

        /// <summary>
        /// The ping command returns an acknowledgement, so that clients can test the connection status and latency.
        /// </summary>
        public abstract Task Ping(CancellationToken cancellationToken = default);

        /// <summary>
        /// The random command provides a random number to be used as a source of entropy for random number generation by clients.
        /// </summary>
        /// <returns>Random 256-bit hex value.</returns>
        public abstract Task<Hash256> Random(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieve information about the public ledger.
        /// </summary>
        public abstract Task<LedgerResponse> Ledger(LedgerRequest request = default, CancellationToken cancellationToken = default);

        /// <summary>
        /// The ledger_closed method returns the unique identifiers of the most recently closed ledger. 
        /// (This ledger is not necessarily validated and immutable yet.)
        /// </summary>
        public abstract Task<LedgerClosedResponse> LedgerClosed(CancellationToken cancellationToken = default);

        /// <summary>
        /// The ledger_current method returns the unique identifiers of the current in-progress ledger. 
        /// This command is mostly useful for testing, because the ledger returned is still in flux.
        /// </summary>
        public abstract Task<uint> LedgerCurrent(CancellationToken cancellationToken = default);

        /// <summary>
        /// The fee command reports the current state of the open-ledger requirements for the transaction cost.
        /// </summary>
        public abstract Task<FeeResponse> Fee(CancellationToken cancellationToken = default);

        /// <summary>
        /// The account_info command retrieves information about an account, its activity, and its XRP balance. 
        /// All information retrieved is relative to a particular version of the ledger.
        /// </summary>
        public abstract Task<AccountInfoResponse> AccountInfo(AccountInfoRequest request = default, CancellationToken cancellationToken = default);

        /// <summary>
        /// The account_currencies command retrieves a list of currencies that an account can send or receive, based on its trust lines. 
        /// (This is not a thoroughly confirmed list, but it can be used to populate user interfaces.)
        /// </summary>
        public abstract Task<AccountCurrenciesResponse> AccountCurrencies(AccountCurrenciesRequest request = default, CancellationToken cancellationToken = default);

        /// <summary>
        /// The server_state command asks the server for various machine-readable information about the rippled server's current state.
        /// </summary>
        public abstract Task<ServerStateResponse> ServerState(CancellationToken cancellationToken = default);

        /// <summary>
        /// The account_lines method returns information about an account's trust lines, including balances in all non-XRP currencies and assets. 
        /// All information retrieved is relative to a particular version of the ledger.
        /// </summary>
        public abstract Task<AccountLinesResponse> AccountLines(AccountLinesRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// The submit method applies a transaction and sends it to the network to be confirmed and included in future ledgers.
        /// Submit-only mode takes a signed, serialized transaction as a binary blob, and submits it to the network as-is. 
        /// Since signed transaction objects are immutable, no part of the transaction can be modified or automatically filled in after submission.
        /// </summary>
        public abstract Task<SubmitResponse> Submit(SubmitRequest request, CancellationToken cancellationToken = default);
    }
}
