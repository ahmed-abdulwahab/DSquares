using System.Net;

namespace Awards.Api.Tests;

public sealed class ConcurrencyTests : ApiTestBase
{
    private const int Rounds = 20;

    [Fact]
    public async Task Identical_requests_produce_one_201_and_the_rest_200()
    {
        const int Requests = 64;

        var statuses = await RunSimultaneouslyAsync(Requests, _ => PostStatusAsync("S1", "T1", "W100", 25));

        Assert.Equal(1, statuses.Count(status => status == HttpStatusCode.Created));
        Assert.Equal(Requests - 1, statuses.Count(status => status == HttpStatusCode.OK));
        Assert.Equal(W100Seed + 25, await GetBalanceAsync("W100"));
    }

    [Fact]
    public async Task Identical_requests_stay_correct_over_repeated_rounds()
    {
        const int Requests = 16;

        for (var round = 0; round < Rounds; round++)
        {
            var transactionId = $"T{round}";

            var statuses = await RunSimultaneouslyAsync(Requests, _ => PostStatusAsync("S1", transactionId, "W100", 5));

            Assert.Equal(1, statuses.Count(status => status == HttpStatusCode.Created));
            Assert.Equal(Requests - 1, statuses.Count(status => status == HttpStatusCode.OK));
        }

        Assert.Equal(W100Seed + (Rounds * 5), await GetBalanceAsync("W100"));
    }

    [Fact]
    public async Task Distinct_awards_to_one_wallet_all_update_its_balance()
    {
        const int Requests = 500;

        var statuses = await RunSimultaneouslyAsync(Requests, i => PostStatusAsync("S1", $"T{i}", "W100", (i % 1000) + 1));

        Assert.All(statuses, status => Assert.Equal(HttpStatusCode.Created, status));
        var expected = Enumerable.Range(0, Requests).Sum(i => (i % 1000) + 1);
        Assert.Equal(W100Seed + expected, await GetBalanceAsync("W100"));
    }

    [Fact]
    public async Task Same_identity_with_different_points_produces_one_201_and_one_409()
    {
        var expected = W100Seed;

        for (var round = 0; round < Rounds; round++)
        {
            var transactionId = $"T{round}";

            var statuses = await RunSimultaneouslyAsync(2, i => PostStatusAsync("S1", transactionId, "W100", i == 0 ? 10 : 20));

            Assert.Single(statuses, status => status == HttpStatusCode.Created);
            Assert.Single(statuses, status => status == HttpStatusCode.Conflict);

            expected += statuses[0] == HttpStatusCode.Created ? 10 : 20;
            Assert.Equal(expected, await GetBalanceAsync("W100"));
        }
    }

    [Fact]
    public async Task Mixed_values_for_one_identity_are_judged_against_the_winner()
    {
        const int Requests = 64;

        // Half the requests carry 10 points and half carry 20, and the winner decides the rest.
        var statuses = await RunSimultaneouslyAsync(Requests, i => PostStatusAsync("S1", "T1", "W100", i % 2 == 0 ? 10 : 20));

        Assert.Equal(1, statuses.Count(status => status == HttpStatusCode.Created));
        Assert.Equal(Requests / 2 - 1, statuses.Count(status => status == HttpStatusCode.OK));
        Assert.Equal(Requests / 2, statuses.Count(status => status == HttpStatusCode.Conflict));

        var winnerIndex = Array.IndexOf(statuses, HttpStatusCode.Created);
        var winnerPoints = winnerIndex % 2 == 0 ? 10 : 20;
        Assert.Equal(W100Seed + winnerPoints, await GetBalanceAsync("W100"));
    }

    [Fact]
    public async Task Same_identity_on_two_wallets_credits_only_one()
    {
        var totalBefore = W100Seed + W200Seed;

        for (var round = 0; round < Rounds; round++)
        {
            var transactionId = $"T{round}";

            var statuses = await RunSimultaneouslyAsync(2, i => PostStatusAsync("S1", transactionId, i == 0 ? "W100" : "W200", 10));

            Assert.Single(statuses, status => status == HttpStatusCode.Created);
            Assert.Single(statuses, status => status == HttpStatusCode.Conflict);
        }

        var totalAfter = await GetBalanceAsync("W100") + await GetBalanceAsync("W200");
        Assert.Equal(totalBefore + (Rounds * 10), totalAfter);
    }

    [Fact]
    public async Task Awards_to_different_wallets_do_not_interfere()
    {
        const int Requests = 200;

        var statuses = await RunSimultaneouslyAsync(Requests, i => PostStatusAsync("S1", $"T{i}", i % 2 == 0 ? "W100" : "W200", 3));

        Assert.All(statuses, status => Assert.Equal(HttpStatusCode.Created, status));
        Assert.Equal(W100Seed + (Requests / 2 * 3), await GetBalanceAsync("W100"));
        Assert.Equal(W200Seed + (Requests / 2 * 3), await GetBalanceAsync("W200"));
    }

    [Fact]
    public async Task Reads_during_writes_only_see_valid_balances()
    {
        const int Requests = 200;
        var writes = RunSimultaneouslyAsync(Requests, i => PostStatusAsync("S1", $"T{i}", "W100", 1));

        var previous = W100Seed;
        while (!writes.IsCompleted)
        {
            var balance = await GetBalanceAsync("W100");

            Assert.InRange(balance, previous, W100Seed + Requests);
            previous = balance;
        }

        Assert.All(await writes, status => Assert.Equal(HttpStatusCode.Created, status));
        Assert.Equal(W100Seed + Requests, await GetBalanceAsync("W100"));
    }

    private async Task<HttpStatusCode> PostStatusAsync(string sourceId, string externalTransactionId, string walletId, int points)
    {
        using var response = await PostAwardAsync(sourceId, externalTransactionId, walletId, points);
        return response.StatusCode;
    }

    /// <summary>Queues every request on the thread pool behind one gate, then releases them together.</summary>
    private static async Task<T[]> RunSimultaneouslyAsync<T>(int count, Func<int, Task<T>> request)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var tasks = Enumerable.Range(0, count)
            .Select(i => Task.Run(async () =>
            {
                await gate.Task;
                return await request(i);
            }))
            .ToArray();

        gate.SetResult();
        return await Task.WhenAll(tasks);
    }
}
