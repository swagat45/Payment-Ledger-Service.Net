using PaymentLedger.Application.Users.Commands.CreateUser;
using PaymentLedger.Application.Users.Commands.DeleteUser;
using PaymentLedger.Application.Users.Commands.UpdateUser;
using PaymentLedger.Application.Users.Queries.GetUsers;

namespace PaymentLedger.UnitTests.Application.Users;

public class UserValidatorTests
{
    [Fact]
    public void CreateUserCommandValidator_ShouldValidateCorrectly()
    {
        var validator = new CreateUserCommandValidator();

        var validCommand = new CreateUserCommand("Alice", "alice@example.com");
        var invalidCommand = new CreateUserCommand("", "invalid-email");

        Assert.True(validator.Validate(validCommand).IsValid);
        Assert.False(validator.Validate(invalidCommand).IsValid);
    }

    [Fact]
    public void UpdateUserCommandValidator_ShouldValidateCorrectly()
    {
        var validator = new UpdateUserCommandValidator();

        var validCommand = new UpdateUserCommand(Guid.NewGuid(), "Bob", "bob@example.com");
        var invalidCommand = new UpdateUserCommand(Guid.Empty, "", "not-an-email");

        Assert.True(validator.Validate(validCommand).IsValid);
        Assert.False(validator.Validate(invalidCommand).IsValid);
    }

    [Fact]
    public void DeleteUserCommandValidator_ShouldValidateCorrectly()
    {
        var validator = new DeleteUserCommandValidator();

        var validCommand = new DeleteUserCommand(Guid.NewGuid());
        var invalidCommand = new DeleteUserCommand(Guid.Empty);

        Assert.True(validator.Validate(validCommand).IsValid);
        Assert.False(validator.Validate(invalidCommand).IsValid);
    }

    [Fact]
    public void GetUsersQueryValidator_ShouldValidateCorrectly()
    {
        var validator = new GetUsersQueryValidator();

        var validQuery = new GetUsersQuery(1, 20);
        var invalidQuery1 = new GetUsersQuery(0, 20);
        var invalidQuery2 = new GetUsersQuery(1, 101);

        Assert.True(validator.Validate(validQuery).IsValid);
        Assert.False(validator.Validate(invalidQuery1).IsValid);
        Assert.False(validator.Validate(invalidQuery2).IsValid);
    }
}
