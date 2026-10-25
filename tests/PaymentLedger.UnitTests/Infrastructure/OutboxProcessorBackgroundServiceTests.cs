using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PaymentLedger.Api.BackgroundServices;
using PaymentLedger.Business.Services;
using PaymentLedger.Shared.Options;

namespace PaymentLedger.UnitTests.Infrastructure;

public class OutboxProcessorBackgroundServiceTests
{
    [Fact]
    public async Task Worker_WhenEnabled_ProcessesBatchWithinLifecycle()
    {
        // Arrange
        var dispatcherMock = new Mock<IOutboxDispatcher>();
        var tcs = new TaskCompletionSource<int>();

        dispatcherMock
            .Setup(d => d.ProcessBatchAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                tcs.TrySetResult(1);
                return Task.FromResult(1);
            });

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IOutboxDispatcher)))
            .Returns(dispatcherMock.Object);

        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(serviceProviderMock.Object);

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock.Setup(sf => sf.CreateScope()).Returns(scopeMock.Object);

        var options = Options.Create(new OutboxOptions
        {
            Enabled = true,
            IntervalSeconds = 1
        });

        var worker = new OutboxProcessorBackgroundService(
            scopeFactoryMock.Object,
            options,
            NullLogger<OutboxProcessorBackgroundService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // Act
        var executeTask = worker.StartAsync(cts.Token);

        // Wait until at least one batch is processed or timeout
        var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(1500, cts.Token));

        // Cancel and stop
        await worker.StopAsync(CancellationToken.None);
        await executeTask;

        // Assert
        Assert.True(tcs.Task.IsCompletedSuccessfully, "Dispatcher ProcessBatchAsync was not invoked by background service.");
        dispatcherMock.Verify(d => d.ProcessBatchAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Worker_WhenDisabled_DoesNotProcessBatch()
    {
        // Arrange
        var dispatcherMock = new Mock<IOutboxDispatcher>();

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IOutboxDispatcher)))
            .Returns(dispatcherMock.Object);

        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(serviceProviderMock.Object);

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock.Setup(sf => sf.CreateScope()).Returns(scopeMock.Object);

        var options = Options.Create(new OutboxOptions
        {
            Enabled = false,
            IntervalSeconds = 1
        });

        var worker = new OutboxProcessorBackgroundService(
            scopeFactoryMock.Object,
            options,
            NullLogger<OutboxProcessorBackgroundService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        // Act
        await worker.StartAsync(cts.Token);
        await Task.Delay(100);
        await worker.StopAsync(CancellationToken.None);

        // Assert
        dispatcherMock.Verify(d => d.ProcessBatchAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
