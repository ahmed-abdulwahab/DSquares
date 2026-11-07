using System.Net;

namespace Awards.Api.Tests;

public sealed class AwardCreationTests : ApiTestBase
{
    [Fact]
    public async Task New_award_returns_201_with_the_award_and_credits_the_wallet()
    {
        var response = await PostAwardAsync("S1", "T1", "W100", 25);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadJsonObjectAsync(response);
        Assert.Equal("S1", body.GetProperty("sourceId").GetString());
        Assert.Equal("T1", body.GetProperty("externalTransactionId").GetString());
        Assert.Equal("W100", body.GetProperty("walletId").GetString());
        Assert.Equal(25, body.GetProperty("points").GetInt32());

        Assert.Equal(W100Seed + 25, await GetBalanceAsync("W100"));
        Assert.Equal(W200Seed, await GetBalanceAsync("W200"));
    }

    [Fact]
    public async Task Identical_retry_returns_200_with_the_same_body_and_does_not_credit_again()
    {
        var first = await PostAwardAsync("S1", "T1", "W100", 25);
        var retry = await PostAwardAsync("S1", "T1", "W100", 25);
        var secondRetry = await PostAwardAsync("S1", "T1", "W100", 25);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondRetry.StatusCode);

        var firstBody = await first.Content.ReadAsStringAsync();
        Assert.Equal(firstBody, await retry.Content.ReadAsStringAsync());
        Assert.Equal(firstBody, await secondRetry.Content.ReadAsStringAsync());

        Assert.Equal(W100Seed + 25, await GetBalanceAsync("W100"));
    }

    [Fact]
    public async Task Retry_with_different_points_returns_409_and_changes_nothing()
    {
        await PostAwardAsync("S1", "T1", "W100", 25);

        var response = await PostAwardAsync("S1", "T1", "W100", 26);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "Award conflict");
        Assert.Equal(W100Seed + 25, await GetBalanceAsync("W100"));
    }

    [Fact]
    public async Task Retry_with_different_wallet_returns_409_and_changes_nothing()
    {
        await PostAwardAsync("S1", "T1", "W100", 25);

        var response = await PostAwardAsync("S1", "T1", "W200", 25);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "Award conflict");
        Assert.Equal(W100Seed + 25, await GetBalanceAsync("W100"));
        Assert.Equal(W200Seed, await GetBalanceAsync("W200"));
    }

    [Fact]
    public async Task Original_award_is_still_replayable_after_a_conflict()
    {
        await PostAwardAsync("S1", "T1", "W100", 25);
        await PostAwardAsync("S1", "T1", "W100", 26);

        var retry = await PostAwardAsync("S1", "T1", "W100", 25);

        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
    }

    [Fact]
    public async Task Unknown_wallet_returns_404()
    {
        var response = await PostAwardAsync("S1", "T1", "W999", 25);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Wallet not found");
    }

    [Fact]
    public async Task Rejected_unknown_wallet_request_does_not_reserve_the_identity()
    {
        await PostAwardAsync("S1", "T1", "W999", 25);

        var response = await PostAwardAsync("S1", "T1", "W100", 40);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(W100Seed + 40, await GetBalanceAsync("W100"));
    }

    [Fact]
    public async Task Existing_identity_with_unknown_wallet_returns_409_not_404()
    {
        await PostAwardAsync("S1", "T1", "W100", 25);

        var response = await PostAwardAsync("S1", "T1", "W999", 25);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "Award conflict");
    }

    [Fact]
    public async Task Wallet_ids_are_case_sensitive()
    {
        var response = await PostAwardAsync("S1", "T1", "w100", 25);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Wallet not found");
    }

    [Theory]
    [InlineData("S1", "T1", "s1", "T1")]
    [InlineData("S1", "T1", "S1", "t1")]
    public async Task Award_identity_is_case_sensitive(string sourceA, string transactionA, string sourceB, string transactionB)
    {
        var first = await PostAwardAsync(sourceA, transactionA, "W100", 10);
        var second = await PostAwardAsync(sourceB, transactionB, "W100", 20);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(W100Seed + 30, await GetBalanceAsync("W100"));
    }

    [Theory]
    [InlineData("S1", "T1", "S1", "T2")]
    [InlineData("S1", "T1", "S2", "T1")]
    public async Task Identity_uses_both_fields(string sourceA, string transactionA, string sourceB, string transactionB)
    {
        var first = await PostAwardAsync(sourceA, transactionA, "W100", 10);
        var second = await PostAwardAsync(sourceB, transactionB, "W100", 20);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    [Theory]
    [InlineData("a_b", "c", "a", "b_c")]
    [InlineData("a-b", "c", "a", "b-c")]
    public async Task Identities_that_join_to_the_same_text_do_not_collide(string sourceA, string transactionA, string sourceB, string transactionB)
    {
        var first = await PostAwardAsync(sourceA, transactionA, "W100", 10);
        var second = await PostAwardAsync(sourceB, transactionB, "W100", 20);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    [Fact]
    public async Task Same_transaction_id_can_be_used_on_two_wallets_under_different_sources()
    {
        var first = await PostAwardAsync("S1", "T1", "W100", 10);
        var second = await PostAwardAsync("S2", "T1", "W200", 20);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(W100Seed + 10, await GetBalanceAsync("W100"));
        Assert.Equal(W200Seed + 20, await GetBalanceAsync("W200"));
    }

    [Fact]
    public async Task Unknown_extra_fields_are_ignored_and_not_part_of_the_identity()
    {
        const string Fields = """ "sourceId":"S1","externalTransactionId":"T1","walletId":"W100","points":25 """;

        var first = await PostRawAsync($$"""{{{Fields}}, "note":"first"}""");
        var retry = await PostRawAsync($$"""{{{Fields}}, "note":"second"}""");

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
    }
}
