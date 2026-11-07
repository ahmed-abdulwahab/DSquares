using System.Net;
using System.Text;

namespace Awards.Api.Tests;

public sealed class AwardValidationTests : ApiTestBase
{
    private static readonly string[] IdentifierFields = ["sourceId", "externalTransactionId", "walletId"];

    private static readonly string[] InvalidIdentifierValues =
    [
        "null",
        "123",
        "true",
        "[]",
        "{}",
        "\"\"",
        "\" \"",
        "\"a b\"",
        "\" W100\"",
        "\"W100 \"",
        "\"W100\\n\"",
        "\"W100\\u0000\"",
        "\"a.b\"",
        "\"a/b\"",
        "\"a|b\"",
        "\"\u00E9\"",
        "\"\\u00E9\"",
        "\"\u0663\"",
        $"\"{new string('a', 65)}\"",
    ];

    public static TheoryData<string> MalformedBodies =>
    [
        "",
        "   ",
        "{",
        "}",
        "not json",
        "{\"sourceId\":",
        "{\"sourceId\":\"S1\"",
        "{'sourceId':'S1'}",
        "{\"points\":1,}",
        "{} {}",
        "{}garbage",
        "/* comment */ {}",
    ];

    public static TheoryData<string> NonObjectBodies =>
    [
        "null",
        "[]",
        "[{}]",
        "\"text\"",
        "42",
        "true",
    ];

    public static TheoryData<string, string> InvalidIdentifiers
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var name in IdentifierFields)
            {
                foreach (var value in InvalidIdentifierValues)
                {
                    data.Add(name, value);
                }
            }

            return data;
        }
    }

    public static TheoryData<string> InvalidPoints =>
    [
        "null",
        "\"\"",
        "\"5\"",
        "true",
        "[]",
        "[5]",
        "{}",
        "0",
        "-0",
        "-1",
        "1001",
        "1.5",
        "1.0",
        "1e2",
        "2147483648",
        "99999999999999999999",
    ];

    [Theory]
    [MemberData(nameof(MalformedBodies))]
    public async Task Malformed_json_returns_400(string body)
    {
        await AssertProblemAsync(await PostRawAsync(body), HttpStatusCode.BadRequest, ValidationTitle);
    }

    [Theory]
    [MemberData(nameof(NonObjectBodies))]
    public async Task Non_object_body_returns_400(string body)
    {
        await AssertProblemAsync(await PostRawAsync(body), HttpStatusCode.BadRequest, ValidationTitle);
    }

    [Theory]
    [MemberData(nameof(InvalidIdentifiers))]
    public async Task Invalid_identifier_returns_400_naming_the_field(string field, string value)
    {
        await AssertFieldErrorAsync(await PostRawAsync(BodyWith(field, value)), field);
    }

    [Theory]
    [MemberData(nameof(InvalidPoints))]
    public async Task Invalid_points_returns_400_naming_the_field(string value)
    {
        await AssertFieldErrorAsync(await PostRawAsync(BodyWith("points", value)), "points");
    }

    [Theory]
    [InlineData("sourceId")]
    [InlineData("externalTransactionId")]
    [InlineData("walletId")]
    [InlineData("points")]
    public async Task Missing_field_returns_400_naming_the_field(string field)
    {
        await AssertFieldErrorAsync(await PostRawAsync(BodyWithout(field)), field);
    }

    [Fact]
    public async Task Empty_object_reports_every_field()
    {
        var body = await AssertProblemAsync(await PostRawAsync("{}"), HttpStatusCode.BadRequest, ValidationTitle);

        var names = body.GetProperty("errors").EnumerateObject().Select(error => error.Name).Order(StringComparer.Ordinal);
        Assert.Equal(["externalTransactionId", "points", "sourceId", "walletId"], names);
    }

    [Theory]
    [InlineData("SourceId", "sourceId")]
    [InlineData("WALLETID", "walletId")]
    [InlineData("Points", "points")]
    public async Task Property_names_are_case_sensitive(string wrongName, string expectedField)
    {
        var body = BodyWithout(expectedField).Replace("}", $",\"{wrongName}\":\"x\"}}", StringComparison.Ordinal);

        await AssertFieldErrorAsync(await PostRawAsync(body), expectedField);
    }

    [Theory]
    [InlineData("sourceId", "\"S2\"")]
    [InlineData("externalTransactionId", "\"T2\"")]
    [InlineData("walletId", "\"W200\"")]
    [InlineData("points", "999")]
    public async Task Duplicate_property_returns_400(string field, string secondValue)
    {
        var body = ValidBody().Replace("}", $",\"{field}\":{secondValue}}}", StringComparison.Ordinal);

        await AssertProblemAsync(await PostRawAsync(body), HttpStatusCode.BadRequest, ValidationTitle);
        Assert.Equal(W100Seed, await GetBalanceAsync("W100"));
    }

    [Theory]
    [InlineData("sourceId")]
    [InlineData("externalTransactionId")]
    [InlineData("walletId")]
    public async Task Lone_surrogate_in_a_value_returns_400_not_500(string field)
    {
        var response = await PostRawAsync(BodyWith(field, "\"\\uD800\""));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, ValidationTitle);
    }

    [Fact]
    public async Task Lone_surrogate_in_a_property_name_returns_400_not_500()
    {
        var body = ValidBody().Replace("}", ",\"\\uD800\":1}", StringComparison.Ordinal);

        await AssertProblemAsync(await PostRawAsync(body), HttpStatusCode.BadRequest, ValidationTitle);
    }

    [Fact]
    public async Task Invalid_utf8_returns_400_not_500()
    {
        byte[] bytes =
        [
            .. Encoding.UTF8.GetBytes("{\"sourceId\":\""),
            0xFF,
            .. Encoding.UTF8.GetBytes("\",\"externalTransactionId\":\"T1\",\"walletId\":\"W100\",\"points\":10}"),
        ];
        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new("application/json");

        var response = await Client.PostAsync("/awards", content);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, ValidationTitle);
    }

    [Fact]
    public async Task Excessively_nested_json_returns_400()
    {
        var nested = new string('[', 100) + new string(']', 100);
        var body = ValidBody().Replace("}", $",\"extra\":{nested}}}", StringComparison.Ordinal);

        await AssertProblemAsync(await PostRawAsync(body), HttpStatusCode.BadRequest, ValidationTitle);
    }

    [Fact]
    public async Task Validation_errors_do_not_expose_dotnet_type_names()
    {
        var response = await PostRawAsync(BodyWith("points", "\"5\""));

        Assert.DoesNotContain("System.", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("SourceId")]
    [InlineData("ExternalTransactionId")]
    [InlineData("WalletId")]
    [InlineData("Points")]
    public async Task Validation_messages_do_not_suggest_the_wrong_property_casing(string wrongName)
    {
        var response = await PostRawAsync("""{"points":0}""");

        Assert.DoesNotContain(wrongName, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1000)]
    public async Task Points_boundaries_are_accepted(int points)
    {
        var response = await PostAwardAsync("S1", "T1", "W100", points);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(W100Seed + points, await GetBalanceAsync("W100"));
    }

    [Theory]
    [InlineData("a")]
    [InlineData("Z")]
    [InlineData("0")]
    [InlineData("_")]
    [InlineData("-")]
    [InlineData("aZ09_-")]
    public async Task Allowed_identifier_characters_are_accepted(string identifier)
    {
        var response = await PostAwardAsync(identifier, identifier, "W100", 1);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Identifier_of_64_characters_is_accepted()
    {
        var identifier = new string('a', 64);

        var response = await PostAwardAsync(identifier, identifier, "W100", 1);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Wallet_id_of_64_characters_passes_validation_and_is_then_unknown()
    {
        var response = await PostAwardAsync("S1", "T1", new string('a', 64), 1);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Wallet not found");
    }

    [Fact]
    public async Task Validation_runs_before_the_wallet_check()
    {
        await AssertFieldErrorAsync(await PostAwardAsync("S1", "T1", "W999", 0), "points");
    }

    [Fact]
    public async Task Validation_runs_before_the_existing_award_check()
    {
        await PostAwardAsync("S1", "T1", "W100", 25);

        await AssertFieldErrorAsync(await PostAwardAsync("S1", "T1", "W100", 0), "points");
        Assert.Equal(W100Seed + 25, await GetBalanceAsync("W100"));
    }

    [Fact]
    public async Task Invalid_request_does_not_reserve_the_identity()
    {
        await PostAwardAsync("S1", "T1", "W100", 0);

        var response = await PostAwardAsync("S1", "T1", "W100", 25);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static string ValidBody() =>
        """{"sourceId":"S1","externalTransactionId":"T1","walletId":"W100","points":10}""";

    private static string BodyWith(string field, string jsonValue) =>
        ToJson(ValidFields().Select(pair => pair.Key == field ? (pair.Key, jsonValue) : (pair.Key, pair.Value)));

    private static string BodyWithout(string field) =>
        ToJson(ValidFields().Where(pair => pair.Key != field).Select(pair => (pair.Key, pair.Value)));

    private static Dictionary<string, string> ValidFields() => new()
    {
        ["sourceId"] = "\"S1\"",
        ["externalTransactionId"] = "\"T1\"",
        ["walletId"] = "\"W100\"",
        ["points"] = "10",
    };

    private static string ToJson(IEnumerable<(string Key, string Value)> fields) =>
        "{" + string.Join(",", fields.Select(field => $"\"{field.Key}\":{field.Value}")) + "}";
}
