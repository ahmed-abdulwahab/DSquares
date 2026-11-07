using Awards.Api.Domain;

namespace Awards.Api.Contracts;

public sealed record AwardResponse(string SourceId, string ExternalTransactionId, string WalletId, int Points)
{
    public static AwardResponse From(Award award) =>
        new(award.SourceId, award.ExternalTransactionId, award.WalletId, award.Points);
}
