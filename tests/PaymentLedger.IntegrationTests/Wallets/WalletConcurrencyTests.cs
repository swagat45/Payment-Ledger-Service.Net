using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PaymentLedger.Repository.Entities;
using PaymentLedger.Repository.Persistence;
using PaymentLedger.IntegrationTests.Infrastructure;
using PaymentLedger.Shared.Constants;
using PaymentLedger.Shared.Responses;

namespace PaymentLedger.IntegrationTests.Wallets;

public sealed class WalletConcurrencyTests : PostgreSqlIntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [DockerFact]
    public async Task ConcurrentDebits_OnLowBalanceWallet_SerializesAndPreventsNegativeBalance()
    {
        // 1. Arrange: Seed a user and a wallet with 100.00 USD initial balance in PostgreSQL
        var walletId = Guid.NewGuid();

        await using (var scope = Host.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var user = User.Create("Concurrency Customer", $"concurrent-{Guid.NewGuid():N}@customer.local", DateTimeOffset.UtcNow);
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();

            var wallet = Wallet.Create(user.Id, "USD", DateTimeOffset.UtcNow);
            wallet.Credit(100.00m, DateTimeOffset.UtcNow);
            dbContext.Wallets.Add(wallet);
            await dbContext.SaveChangesAsync();

            walletId = wallet.Id;
        }

        using var client = Host.CreateClient();

        // 2. Act: Fire 10 concurrent HTTP debit requests of 30.00 USD each with unique idempotency keys
        const int concurrentRequests = 10;
        const decimal debitAmount = 30.00m;

        var tasks = Enumerable.Range(1, concurrentRequests).Select(async i =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/wallets/{walletId}/debit")
            {
                Content = JsonContent.Create(new
                {
                    Amount = debitAmount,
                    Currency = "USD",
                    Description = $"Concurrent debit test #{i}"
                })
            };
            request.Headers.Add("Idempotency-Key", $"concurrency-test-key-{i}-{Guid.NewGuid():N}");

            return await client.SendAsync(request);
        });

        var responses = await Task.WhenAll(tasks);

        // 3. Assert HTTP status codes:
        // Exactly 3 requests should succeed (HTTP 200 OK)
        // Exactly 7 requests should fail with InsufficientBalance (HTTP 409 Conflict)
        var successResponses = responses.Where(r => r.StatusCode == HttpStatusCode.OK).ToList();
        var conflictResponses = responses.Where(r => r.StatusCode == HttpStatusCode.Conflict).ToList();

        successResponses.Should().HaveCount(3, "only 3 debits of 30.00 can succeed from an initial balance of 100.00");
        conflictResponses.Should().HaveCount(7, "the remaining 7 debits must fail due to insufficient balance");

        // Verify the 409 error envelope has ErrorCodes.InsufficientBalance
        foreach (var conflictResponse in conflictResponses)
        {
            var body = await conflictResponse.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
            body.Should().NotBeNull();
            body!.Success.Should().BeFalse();
            body.Errors.Should().Contain(e => e.Code == ErrorCodes.InsufficientBalance);
        }

        // 4. Assert Final State in Database via AppDbContext
        await using (var scope = Host.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var persistedWallet = await dbContext.Wallets.AsNoTracking().SingleAsync(w => w.Id == walletId);
            persistedWallet.Balance.Should().Be(10.00m, "initial 100.00 minus three 30.00 debits leaves exactly 10.00");

            // Ensure no wallet has a negative balance
            var anyNegative = await dbContext.Wallets.AnyAsync(w => w.Balance < 0);
            anyNegative.Should().BeFalse("database row-level lock must guarantee balance is never negative");

            // Verify exactly 3 debit transactions were recorded for this wallet
            var debitTransactions = await dbContext.Transactions
                .Where(t => t.SourceWalletId == walletId && t.Type == PaymentLedger.Repository.Enums.TransactionType.Debit)
                .ToListAsync();

            debitTransactions.Should().HaveCount(3);
        }
    }

    [DockerFact]
    public async Task ConcurrentDebits_WithSameIdempotencyKey_DeduplicatesAndDebitsOnlyOnce()
    {
        // 1. Arrange: Seed a user and wallet with 100.00 USD
        var walletId = Guid.NewGuid();

        await using (var scope = Host.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var user = User.Create("Idem Customer", $"idem-{Guid.NewGuid():N}@customer.local", DateTimeOffset.UtcNow);
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();

            var wallet = Wallet.Create(user.Id, "USD", DateTimeOffset.UtcNow);
            wallet.Credit(100.00m, DateTimeOffset.UtcNow);
            dbContext.Wallets.Add(wallet);
            await dbContext.SaveChangesAsync();

            walletId = wallet.Id;
        }

        using var client = Host.CreateClient();

        // 2. Act: Fire 5 concurrent requests with identical idempotency key
        const int concurrentRequests = 5;
        const decimal debitAmount = 40.00m;
        var sharedKey = $"shared-idem-key-{Guid.NewGuid():N}";

        var tasks = Enumerable.Range(1, concurrentRequests).Select(async _ =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/wallets/{walletId}/debit")
            {
                Content = JsonContent.Create(new
                {
                    Amount = debitAmount,
                    Currency = "USD",
                    Description = "Duplicate test"
                })
            };
            request.Headers.Add("Idempotency-Key", sharedKey);

            return await client.SendAsync(request);
        });

        var responses = await Task.WhenAll(tasks);

        // 3. Assert:
        // Responses must either be 200 (executed / cached) or 429 (anti-double-click throttled).
        // Under no circumstances should balance be debited more than once.
        responses.Should().AllSatisfy(r =>
        {
            ((int)r.StatusCode == 200 || (int)r.StatusCode == 429).Should().BeTrue();
        });

        var okResponses = responses.Where(r => r.StatusCode == HttpStatusCode.OK).ToList();
        okResponses.Should().NotBeEmpty();

        await using (var scope = Host.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var persistedWallet = await dbContext.Wallets.AsNoTracking().SingleAsync(w => w.Id == walletId);
            persistedWallet.Balance.Should().Be(60.00m, "100.00 minus single 40.00 debit leaves 60.00");

            var debitTransactions = await dbContext.Transactions
                .Where(t => t.SourceWalletId == walletId && t.IdempotencyKey == sharedKey)
                .ToListAsync();

            debitTransactions.Should().HaveCount(1, "only 1 transaction must be recorded for this idempotency key");
        }
    }
}
