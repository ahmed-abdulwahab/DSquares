using Awards.Api.Domain;
using Awards.Api.Storage;

namespace Awards.Api.Services;

/// <summary>Business rules for awards and wallet balances.</summary>
public sealed class PointsService(InMemoryStore store)
{
    /// <summary>
    /// Applies an already validated award. The whole decision runs under the store's lock,
    /// and the order of the checks below defines the API's error precedence.
    /// </summary>
    /// <remarks>
    /// The existing identity is checked first because reusing it with different values is a
    /// conflict whichever wallet the retry names. A replay cannot hit the wallet check, since
    /// a stored award always refers to a wallet that exists.
    /// </remarks>
    public AwardOutcome CreateAward(Award award) => store.Atomically(transaction =>
    {
        var existing = transaction.FindAward(award.Key);
        if (existing is not null)
        {
            return existing == award ? AwardOutcome.Replayed : AwardOutcome.Conflict;
        }

        if (!transaction.WalletExists(award.WalletId))
        {
            return AwardOutcome.WalletNotFound;
        }

        transaction.Commit(award);
        return AwardOutcome.Created;
    });

    /// <summary>Returns the wallet's balance, or null when the wallet does not exist.</summary>
    public long? GetBalance(string walletId) =>
        store.Atomically(transaction => transaction.FindBalance(walletId));
}
