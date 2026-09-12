using PaymentLedger.Domain.Entities;
using PaymentLedger.Domain.Enums;
using PaymentLedger.Domain.Exceptions;

namespace PaymentLedger.UnitTests.Domain;

public class UserTests
{
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    [Fact]
    public void Create_WithValidInputs_ShouldInitializeUserCorrectly()
    {
        // Act
        var user = User.Create("John Doe", "john@example.com", _now);

        // Assert
        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("John Doe", user.FullName);
        Assert.Equal("john@example.com", user.Email);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal(_now, user.CreatedAt);
        Assert.Equal(_now, user.UpdatedAt);
    }

    [Theory]
    [InlineData("", "john@example.com")]
    [InlineData("   ", "john@example.com")]
    [InlineData(null, "john@example.com")]
    public void Create_WithInvalidFullName_ShouldThrowDomainException(string? fullName, string email)
    {
        // Act & Assert
        Assert.Throws<DomainException>(() => User.Create(fullName!, email, _now));
    }

    [Theory]
    [InlineData("John Doe", "")]
    [InlineData("John Doe", "   ")]
    [InlineData("John Doe", null)]
    public void Create_WithInvalidEmail_ShouldThrowDomainException(string fullName, string? email)
    {
        // Act & Assert
        Assert.Throws<DomainException>(() => User.Create(fullName, email!, _now));
    }

    [Fact]
    public void Update_WithValidInputs_ShouldUpdateFullNameEmailAndTimestamp()
    {
        // Arrange
        var user = User.Create("Original Name", "orig@example.com", _now);
        var updateTime = _now.AddHours(1);

        // Act
        user.Update("Updated Name", "updated@example.com", updateTime);

        // Assert
        Assert.Equal("Updated Name", user.FullName);
        Assert.Equal("updated@example.com", user.Email);
        Assert.Equal(updateTime, user.UpdatedAt);
    }

    [Fact]
    public void Deactivate_ShouldSetStatusToInactive()
    {
        // Arrange
        var user = User.Create("John Doe", "john@example.com", _now);
        var deactivateTime = _now.AddMinutes(5);

        // Act
        user.Deactivate(deactivateTime);

        // Assert
        Assert.Equal(UserStatus.Inactive, user.Status);
        Assert.Equal(deactivateTime, user.UpdatedAt);
    }

    [Fact]
    public void Activate_ShouldSetStatusToActive()
    {
        // Arrange
        var user = User.Create("John Doe", "john@example.com", _now);
        user.Deactivate(_now);
        var activateTime = _now.AddMinutes(10);

        // Act
        user.Activate(activateTime);

        // Assert
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal(activateTime, user.UpdatedAt);
    }
}
