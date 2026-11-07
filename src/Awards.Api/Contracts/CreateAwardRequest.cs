using System.ComponentModel.DataAnnotations;
using Awards.Api.Domain;

namespace Awards.Api.Contracts;

/// <summary>
/// The body of POST /awards. Properties are nullable so a missing value fails [Required]
/// instead of silently becoming an empty default, such as 0 for points.
/// </summary>
/// <remarks>
/// The messages are explicit because the defaults name the C# property, such as "SourceId",
/// and JSON property names are case-sensitive here, so following that text would not work.
/// </remarks>
public sealed class CreateAwardRequest
{
    private const string RequiredMessage = "This field is required.";

    [Required(ErrorMessage = RequiredMessage)]
    [RegularExpression(IdentifierRules.Pattern, ErrorMessage = IdentifierRules.ErrorMessage)]
    public string? SourceId { get; init; }

    [Required(ErrorMessage = RequiredMessage)]
    [RegularExpression(IdentifierRules.Pattern, ErrorMessage = IdentifierRules.ErrorMessage)]
    public string? ExternalTransactionId { get; init; }

    [Required(ErrorMessage = RequiredMessage)]
    [RegularExpression(IdentifierRules.Pattern, ErrorMessage = IdentifierRules.ErrorMessage)]
    public string? WalletId { get; init; }

    [Required(ErrorMessage = RequiredMessage)]
    [Range(1, 1000, ErrorMessage = "Must be an integer between 1 and 1000.")]
    public int? Points { get; init; }

    /// <summary>Call only after validation, which guarantees every value is present.</summary>
    public Award ToAward() => new(SourceId!, ExternalTransactionId!, WalletId!, Points!.Value);
}
