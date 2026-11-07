using System.Net;

namespace Awards.Api.Tests;

public sealed class WalletTests : ApiTestBase
{
    [Theory]
    [InlineData("W100", W100Seed)]
    [InlineData("W200", W200Seed)]
    public async Task Seeded_wallet_returns_its_id_and_balance(string walletId, long balance)
    {
        var response = await Client.GetAsync($"/wallets/{walletId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonObjectAsync(response);
        Assert.Equal(walletId, body.GetProperty("walletId").GetString());
        Assert.Equal(balance, body.GetProperty("balance").GetInt64());
    }

    [Theory]
    [InlineData("W999")]
    [InlineData("w100")]
    [InlineData("W2OO")]
    public async Task Unknown_wallet_returns_404(string walletId)
    {
        var response = await Client.GetAsync($"/wallets/{walletId}");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Wallet not found");
    }

    [Theory]
    [InlineData("W%20100")]
    [InlineData("W100%0A")]
    [InlineData("W%2F100")]
    [InlineData("W.100")]
    [InlineData("W%C3%A9")]
    public async Task Invalid_wallet_id_returns_400(string encodedWalletId)
    {
        var response = await Client.GetAsync($"/wallets/{encodedWalletId}");

        await AssertFieldErrorAsync(response, "walletId");
    }

    [Fact]
    public async Task Wallet_id_length_boundary_separates_404_from_400()
    {
        var atLimit = await Client.GetAsync($"/wallets/{new string('a', 64)}");
        var overLimit = await Client.GetAsync($"/wallets/{new string('a', 65)}");

        await AssertProblemAsync(atLimit, HttpStatusCode.NotFound, "Wallet not found");
        await AssertFieldErrorAsync(overLimit, "walletId");
    }
}
