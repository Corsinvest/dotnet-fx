namespace Corsinvest.Fx.Functional.Tests;

public class ResultOfValidationTests
{
    // ============================================
    // CollectErrors
    // ============================================

    [Fact]
    public void CollectErrors_WithNoValidators_ReturnsOk()
    {
        var user = new User { Age = 25, Email = "test@example.com", Name = "Alice" };

        var result = ResultOf.CollectErrors<User, string>(user);

        Assert.True(result.IsOk);
    }

    [Fact]
    public void CollectErrors_WithAllPassingValidators_ReturnsOk()
    {
        var user = new User { Age = 25, Email = "test@example.com", Name = "Alice" };

        var result = ResultOf.CollectErrors(user,
            u => u.Age >= 18 ? ResultOf.Ok<User, string>(u) : ResultOf.Fail<User, string>("Must be 18+"),
            u => !string.IsNullOrEmpty(u.Email) ? ResultOf.Ok<User, string>(u) : ResultOf.Fail<User, string>("Email required"),
            u => u.Name.Length > 2 ? ResultOf.Ok<User, string>(u) : ResultOf.Fail<User, string>("Name too short")
        );

        Assert.True(result.IsOk);
        Assert.True(result.TryGetOk(out var ok));
        Assert.Equal(user, ok.Value);
    }

    [Fact]
    public void CollectErrors_WithSomeFailingValidators_CollectsAllErrors()
    {
        var user = new User { Age = 15, Email = string.Empty, Name = "A" };

        var result = ResultOf.CollectErrors(user,
            u => u.Age >= 18 ? ResultOf.Ok<User, string>(u) : ResultOf.Fail<User, string>("Must be 18+"),
            u => !string.IsNullOrEmpty(u.Email) ? ResultOf.Ok<User, string>(u) : ResultOf.Fail<User, string>("Email required"),
            u => u.Name.Length > 2 ? ResultOf.Ok<User, string>(u) : ResultOf.Fail<User, string>("Name too short")
        );

        Assert.True(result.IsFail);
        Assert.True(result.TryGetFail(out var error));
        Assert.Equal(3, error.ErrorValue.Count);
        Assert.Contains("Must be 18+", error.ErrorValue);
        Assert.Contains("Email required", error.ErrorValue);
        Assert.Contains("Name too short", error.ErrorValue);
    }

    [Fact]
    public void CollectErrors_WithOneFailingValidator_ReturnsError()
    {
        var user = new User { Age = 15, Email = "test@example.com", Name = "Alice" };

        var result = ResultOf.CollectErrors(user,
            u => u.Age >= 18 ? ResultOf.Ok<User, string>(u) : ResultOf.Fail<User, string>("Must be 18+"),
            u => !string.IsNullOrEmpty(u.Email) ? ResultOf.Ok<User, string>(u) : ResultOf.Fail<User, string>("Email required")
        );

        Assert.True(result.IsFail);
        Assert.True(result.TryGetFail(out var error));
        Assert.Single(error.ErrorValue);
        Assert.Contains("Must be 18+", error.ErrorValue);
    }

    // ============================================
    // Validate
    // ============================================

    [Fact]
    public void Validate_WithNoValidations_ReturnsOk()
    {
        var user = new User { Age = 25, Email = "test@example.com", Name = "Alice" };

        var result = ResultOf.Validate<User, string>(user);

        Assert.True(result.IsOk);
    }

    [Fact]
    public void Validate_WithAllPassingValidations_ReturnsOk()
    {
        var user = new User { Age = 25, Email = "test@example.com", Name = "Alice" };

        var result = ResultOf.Validate(user,
            (u => u.Age >= 18, "Must be 18 or older"),
            (u => !string.IsNullOrEmpty(u.Email), "Email is required"),
            (u => u.Name.Length > 2, "Name must be longer than 2 characters")
        );

        Assert.True(result.IsOk);
        Assert.True(result.TryGetOk(out var ok));
        Assert.Equal(user, ok.Value);
    }

    [Fact]
    public void Validate_WithSomeFailingValidations_CollectsAllErrors()
    {
        var user = new User { Age = 15, Email = string.Empty, Name = "A" };

        var result = ResultOf.Validate(user,
            (u => u.Age >= 18, "Must be 18 or older"),
            (u => !string.IsNullOrEmpty(u.Email), "Email is required"),
            (u => u.Name.Length > 2, "Name must be longer than 2 characters")
        );

        Assert.True(result.IsFail);
        Assert.True(result.TryGetFail(out var error));
        Assert.Equal(3, error.ErrorValue.Count);
        Assert.Contains("Must be 18 or older", error.ErrorValue);
        Assert.Contains("Email is required", error.ErrorValue);
        Assert.Contains("Name must be longer than 2 characters", error.ErrorValue);
    }

    [Fact]
    public void Validate_WithOneFailingValidation_ReturnsError()
    {
        var user = new User { Age = 15, Email = "test@example.com", Name = "Alice" };

        var result = ResultOf.Validate(user,
            (u => u.Age >= 18, "Must be 18 or older"),
            (u => !string.IsNullOrEmpty(u.Email), "Email is required")
        );

        Assert.True(result.IsFail);
        Assert.True(result.TryGetFail(out var error));
        Assert.Single(error.ErrorValue);
        Assert.Contains("Must be 18 or older", error.ErrorValue);
    }

    // ============================================
    // WithError
    // ============================================

    [Fact]
    public void WithError_AddsErrorToExistingErrorList()
    {
        var result = ResultOf.Fail<User, List<string>>(["Initial error"]);

        var updated = result.WithError("Second error");

        Assert.True(updated.IsFail);
        Assert.True(updated.TryGetFail(out var error));
        Assert.Equal(2, error.ErrorValue.Count);
        Assert.Contains("Initial error", error.ErrorValue);
        Assert.Contains("Second error", error.ErrorValue);
    }

    [Fact]
    public void WithError_ChainedCalls_AddsMultipleErrors()
    {
        var result = ResultOf.Fail<User, List<string>>(["Error 1"])
            .WithError("Error 2")
            .WithError("Error 3");

        Assert.True(result.IsFail);
        Assert.True(result.TryGetFail(out var error));
        Assert.Equal(3, error.ErrorValue.Count);
        Assert.Contains("Error 1", error.ErrorValue);
        Assert.Contains("Error 2", error.ErrorValue);
        Assert.Contains("Error 3", error.ErrorValue);
    }

    [Fact]
    public void WithError_OnSuccessfulResult_ThrowsException()
    {
        var user = new User { Age = 25, Email = "test@example.com", Name = "Alice" };
        var result = ResultOf.Ok<User, List<string>>(user);

        var ex = Assert.Throws<InvalidOperationException>(() => result.WithError("error"));
        Assert.Contains("Cannot add error to a successful result", ex.Message);
    }

    // Helper class
    private record User
    {
        public int Age { get; init; }
        public string Email { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
    }
}
