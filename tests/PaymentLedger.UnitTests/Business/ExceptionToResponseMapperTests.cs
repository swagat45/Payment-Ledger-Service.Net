using PaymentLedger.Business.Exceptions;
using PaymentLedger.Repository.Exceptions;
using PaymentLedger.Shared.Constants;

namespace PaymentLedger.UnitTests.Business;

public class ExceptionToResponseMapperTests
{
    private readonly ExceptionToResponseMapper _mapper = new();
    private const string TestCorrelationId = "test-correlation-id-123";

    [Fact]
    public void Map_ValidationException_ReturnsBadRequestWithFieldErrors()
    {
        // Arrange
        var errors = new Dictionary<string, string[]>
        {
            ["Amount"] = ["Amount must be greater than 0."],
            ["Currency"] = ["Currency is required."]
        };
        var exception = new ValidationException(errors);

        // Act
        var (statusCode, response) = _mapper.Map(exception, TestCorrelationId);

        // Assert
        Assert.Equal(400, statusCode);
        Assert.False(response.Success);
        Assert.Equal(TestCorrelationId, response.CorrelationId);
        Assert.NotNull(response.Errors);
        Assert.Equal(2, response.Errors.Count);

        var amountError = response.Errors.FirstOrDefault(e => e.Field == "Amount");
        Assert.NotNull(amountError);
        Assert.Equal(ErrorCodes.ValidationFailed, amountError.Code);
        Assert.Equal("Amount must be greater than 0.", amountError.Message);

        var currencyError = response.Errors.FirstOrDefault(e => e.Field == "Currency");
        Assert.NotNull(currencyError);
        Assert.Equal(ErrorCodes.ValidationFailed, currencyError.Code);
        Assert.Equal("Currency is required.", currencyError.Message);
    }

    [Fact]
    public void Map_DomainException_WithInsufficientBalance_ReturnsConflict()
    {
        // Arrange
        const string errorMessage = "Insufficient balance to perform debit.";
        var exception = new DomainException(errorMessage, ErrorCodes.InsufficientBalance);

        // Act
        var (statusCode, response) = _mapper.Map(exception, TestCorrelationId);

        // Assert
        Assert.Equal(409, statusCode);
        Assert.False(response.Success);
        Assert.Equal(errorMessage, response.Message);
        Assert.Equal(TestCorrelationId, response.CorrelationId);
        Assert.Single(response.Errors);
        Assert.Equal(ErrorCodes.InsufficientBalance, response.Errors[0].Code);
    }

    [Theory]
    [InlineData(ErrorCodes.IdempotencyConflict)]
    [InlineData(ErrorCodes.ConcurrencyConflict)]
    public void Map_DomainException_WithConflictErrors_ReturnsConflict(string errorCode)
    {
        // Arrange
        var exception = new DomainException("Conflict error occurred.", errorCode);

        // Act
        var (statusCode, response) = _mapper.Map(exception, TestCorrelationId);

        // Assert
        Assert.Equal(409, statusCode);
        Assert.False(response.Success);
        Assert.Equal(TestCorrelationId, response.CorrelationId);
        Assert.Equal(errorCode, response.Errors[0].Code);
    }

    [Theory]
    [InlineData(ErrorCodes.WalletNotFound)]
    [InlineData(ErrorCodes.UserNotFound)]
    [InlineData(ErrorCodes.TransactionNotFound)]
    public void Map_DomainException_WithNotFoundErrors_ReturnsNotFound(string errorCode)
    {
        // Arrange
        var exception = new DomainException("Resource not found.", errorCode);

        // Act
        var (statusCode, response) = _mapper.Map(exception, TestCorrelationId);

        // Assert
        Assert.Equal(404, statusCode);
        Assert.False(response.Success);
        Assert.Equal(TestCorrelationId, response.CorrelationId);
        Assert.Equal(errorCode, response.Errors[0].Code);
    }

    [Fact]
    public void Map_DomainException_WithOtherBusinessErrors_ReturnsBadRequest()
    {
        // Arrange
        const string errorMessage = "Wallet is not active.";
        var exception = new DomainException(errorMessage, ErrorCodes.WalletNotActive);

        // Act
        var (statusCode, response) = _mapper.Map(exception, TestCorrelationId);

        // Assert
        Assert.Equal(400, statusCode);
        Assert.False(response.Success);
        Assert.Equal(TestCorrelationId, response.CorrelationId);
        Assert.Equal(ErrorCodes.WalletNotActive, response.Errors[0].Code);
    }

    [Fact]
    public void Map_DomainException_WithPrefixInMessage_ExtractsErrorCode()
    {
        // Arrange
        var exception = new DomainException("INSUFFICIENT_BALANCE: Insufficient balance in wallet.");

        // Act
        var (statusCode, response) = _mapper.Map(exception, TestCorrelationId);

        // Assert
        Assert.Equal(409, statusCode);
        Assert.False(response.Success);
        Assert.Equal(ErrorCodes.InsufficientBalance, response.Errors[0].Code);
    }

    [Fact]
    public void Map_GeneralException_ReturnsInternalServerErrorWithoutLeakingDetails()
    {
        // Arrange
        const string sensitiveMessage = "Cannot connect to SQL database at 10.0.0.5:1433; Password=secret";
        var exception = new InvalidOperationException(sensitiveMessage);

        // Act
        var (statusCode, response) = _mapper.Map(exception, TestCorrelationId);

        // Assert
        Assert.Equal(500, statusCode);
        Assert.False(response.Success);
        Assert.DoesNotContain("database", response.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", response.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("An unexpected error occurred. Please try again later.", response.Message);
        Assert.Equal(TestCorrelationId, response.CorrelationId);
        Assert.Single(response.Errors);
        Assert.Equal(ErrorCodes.InternalServerError, response.Errors[0].Code);
    }
}
