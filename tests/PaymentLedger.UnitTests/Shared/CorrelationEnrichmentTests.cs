using PaymentLedger.Shared.Context;
using PaymentLedger.Shared.Responses;

namespace PaymentLedger.UnitTests.Shared;

public class CorrelationEnrichmentTests
{
    [Fact]
    public void ApiResponse_Ok_WhenCorrelationContextSet_AutomaticallyEnrichesCorrelationId()
    {
        // Arrange
        const string expectedId = "ambient-corr-123";
        CorrelationContext.Current = expectedId;

        try
        {
            // Act
            var response = ApiResponse<string>.Ok("sample data");

            // Assert
            Assert.Equal(expectedId, response.CorrelationId);
        }
        finally
        {
            CorrelationContext.Current = null;
        }
    }

    [Fact]
    public void ApiResponse_Ok_WhenExplicitCorrelationIdProvided_PrefersExplicitId()
    {
        // Arrange
        const string ambientId = "ambient-corr-123";
        const string explicitId = "explicit-corr-456";
        CorrelationContext.Current = ambientId;

        try
        {
            // Act
            var response = ApiResponse<string>.Ok("sample data", correlationId: explicitId);

            // Assert
            Assert.Equal(explicitId, response.CorrelationId);
        }
        finally
        {
            CorrelationContext.Current = null;
        }
    }

    [Fact]
    public void ErrorResponse_Create_WhenCorrelationContextSet_AutomaticallyEnrichesCorrelationId()
    {
        // Arrange
        const string expectedId = "ambient-error-corr-789";
        CorrelationContext.Current = expectedId;

        try
        {
            // Act
            var response = ErrorResponse.Create("An error occurred.");

            // Assert
            Assert.Equal(expectedId, response.CorrelationId);
        }
        finally
        {
            CorrelationContext.Current = null;
        }
    }

    [Fact]
    public void ErrorResponse_Create_WhenExplicitCorrelationIdProvided_PrefersExplicitId()
    {
        // Arrange
        const string ambientId = "ambient-error-corr-789";
        const string explicitId = "explicit-error-corr-999";
        CorrelationContext.Current = ambientId;

        try
        {
            // Act
            var response = ErrorResponse.Create("An error occurred.", correlationId: explicitId);

            // Assert
            Assert.Equal(explicitId, response.CorrelationId);
        }
        finally
        {
            CorrelationContext.Current = null;
        }
    }
}
