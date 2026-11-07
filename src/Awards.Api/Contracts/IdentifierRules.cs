namespace Awards.Api.Contracts;

/// <summary>The identifier format shared by the award request and the wallet route.</summary>
public static class IdentifierRules
{
    // This pattern is also published in the Swagger document, and Swagger UI runs it as a JavaScript
    // regex, so it must avoid .NET-only syntax such as \z. A trailing newline cannot slip past the $
    // because RegularExpressionAttribute requires the match to cover the whole value.
    public const string Pattern = "^[A-Za-z0-9_-]{1,64}$";

    public const string ErrorMessage = "Must be 1 to 64 ASCII letters, digits, underscores, or hyphens.";
}
