namespace Awards.Api.Services;

public enum AwardOutcome
{
    /// <summary>A new award was recorded and the wallet was credited.</summary>
    Created,

    /// <summary>An identical award already exists, so nothing changed.</summary>
    Replayed,

    /// <summary>The identity exists with a different wallet or points, so nothing changed.</summary>
    Conflict,

    /// <summary>The identity is new but the wallet does not exist, so nothing changed.</summary>
    WalletNotFound,
}
