namespace Awards.Api.Domain;

/// <summary>
/// An award's identity. A composite key rather than a joined string, because the
/// allowed characters leave no safe delimiter: "a_b" + "c" and "a" + "b_c" would collide.
/// String equality here is ordinal, which makes identity case-sensitive.
/// </summary>
public readonly record struct AwardKey(string SourceId, string ExternalTransactionId);

/// <summary>
/// An immutable award. Record equality compares all fields, so two awards with the
/// same key are equal only when they also agree on wallet and points.
/// </summary>
public sealed record Award(string SourceId, string ExternalTransactionId, string WalletId, int Points)
{
    public AwardKey Key => new(SourceId, ExternalTransactionId);
}
