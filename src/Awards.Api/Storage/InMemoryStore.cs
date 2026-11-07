using Awards.Api.Domain;

namespace Awards.Api.Storage;

/// <summary>
/// Process-wide state, registered as a singleton. The data is reachable only through
/// <see cref="Atomically{T}"/>, so unlocked access and check-then-act races cannot be written.
/// </summary>
/// <remarks>
/// A single lock is deliberate. An award's identity is independent of its wallet, so per-wallet
/// locks would not protect it, and the critical sections are short and never perform I/O.
/// </remarks>
public sealed class InMemoryStore
{
    private readonly Lock _gate = new();
    private readonly StoreTransaction _transaction = new();

    /// <summary>
    /// Runs <paramref name="work"/> while holding the lock. The transaction it receives
    /// must not be kept or used after <paramref name="work"/> returns.
    /// </summary>
    public T Atomically<T>(Func<StoreTransaction, T> work)
    {
        lock (_gate)
        {
            return work(_transaction);
        }
    }

    public sealed class StoreTransaction
    {
        private readonly Dictionary<AwardKey, Award> _awards = [];

        private readonly Dictionary<string, long> _balances = new(StringComparer.Ordinal)
        {
            ["W100"] = 100,
            ["W200"] = 50,
        };

        internal StoreTransaction()
        {
        }

        public Award? FindAward(AwardKey key) => _awards.GetValueOrDefault(key);

        public bool WalletExists(string walletId) => _balances.ContainsKey(walletId);

        public long? FindBalance(string walletId) =>
            _balances.TryGetValue(walletId, out var balance) ? balance : null;

        /// <summary>
        /// Records the award and credits its wallet. Every step that can throw runs before
        /// the first write, so a failure cannot leave an award without its credit.
        /// </summary>
        public void Commit(Award award)
        {
            var newBalance = checked(_balances[award.WalletId] + award.Points);
            _awards.Add(award.Key, award);
            _balances[award.WalletId] = newBalance;
        }
    }
}
