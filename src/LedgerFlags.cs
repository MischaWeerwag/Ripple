using System;

namespace Ibasa.Ripple
{
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
    /// There are several options which can be either enabled or disabled for a trust line.
    /// These options can be changed with a TrustSet transaction.
    /// </summary>
    [Flags]
    public enum RippleStateFlags : uint
    {
        None = 0x0,

        /// <summary>
        /// This RippleState object contributes to the low account's owner reserve.
        /// </summary>
        lsfLowReserve = 0x00010000,
        /// <summary>
        /// This RippleState object contributes to the high account's owner reserve.
        /// </summary>
        lsfHighReserve = 0x00020000,
        /// <summary>
        /// The low account has authorized the high account to hold the low account's issued currency.
        /// </summary>
        lsfLowAuth = 0x00040000,
        /// <summary>
        /// The high account has authorized the low account to hold the high account's issued currency.
        /// </summary>
        lsfHighAuth = 0x00080000,
        /// <summary>
        /// The low account has disabled rippling from this trust line.
        /// </summary>
        lsfLowNoRipple = 0x00100000,
        /// <summary>
        /// The high account has disabled rippling from this trust line.
        /// </summary>
        lsfHighNoRipple = 0x00200000,
        /// <summary>
        /// The low account has frozen the trust line, preventing the high account from transferring the asset.
        /// </summary>
        lsfLowFreeze = 0x00400000,
        /// <summary>
        /// The high account has frozen the trust line, preventing the low account from transferring the asset.
        /// </summary>
        lsfHighFreeze = 0x00800000,
    }

    /// <summary>
    /// SignerList objects can have the following flag value.
    /// </summary>
    [Flags]
    public enum SignerListFlags : uint
    {
        None = 0x0,

        /// <summary>
        /// If this flag is enabled, this SignerList counts as one item for purposes of the owner reserve.
        /// Otherwise, this list counts as N+2 items, where N is the number of signers it contains.
        /// This flag is automatically enabled if you add or update a signer list after the MultiSignReserve amendment is enabled.
        /// </summary>
        lsfOneOwnerCount = 0x00010000,
    }

    /// <summary>
    /// There are several options which can be either enabled or disabled when an OfferCreate transaction creates an offer object. In the ledger, flags are represented as binary values that can be combined with bitwise-or operations. The bit values for the flags in the ledger are different than the values used to enable or disable those flags in a transaction. Ledger flags have names that begin with lsf.
    /// </summary>
    [Flags]
    public enum OfferFlags : uint
    {
        None = 0x0,

        /// <summary>
        /// The object was placed as a passive offer. 
        /// This has no effect on the object in the ledger.
        /// </summary>
        lsfPassive = 0x00010000,
        /// <summary>
        /// The object was placed as a sell offer.
        /// This has no effect on the object in the ledger (because tfSell only matters if you get a better rate than you asked for, which cannot happen after the object enters the ledger).
        /// </summary>
        lsfSell = 0x00020000,
    }
}
