namespace Corsinvest.Fx.Functional.Tests;

public class ResultOfCombineTests
{
    // ============================================
    // Combine (array variant)
    // ============================================

    [Fact]
    public void Combine_WithNoResults_ReturnsEmptyArray()
    {
        var result = ResultOf.Combine<int, string>();

        Assert.True(result.IsOk);
        Assert.True(result.TryGetOk(out var ok));
        Assert.Empty(ok.Value);
    }

    [Fact]
    public void Combine_WithAllSuccessResults_ReturnsArrayOfValues()
    {
        var results = new[]
        {
            ResultOf.Ok<int, string>(1),
            ResultOf.Ok<int, string>(2),
            ResultOf.Ok<int, string>(3)
        };

        var combined = ResultOf.Combine(results);

        Assert.True(combined.IsOk);
        Assert.True(combined.TryGetOk(out var ok));
        Assert.Equal(3, ok.Value.Length);
        Assert.Equal(1, ok.Value[0]);
        Assert.Equal(2, ok.Value[1]);
        Assert.Equal(3, ok.Value[2]);
    }

    [Fact]
    public void Combine_WithSomeErrors_ReturnsAllErrors()
    {
        var results = new[]
        {
            ResultOf.Ok<int, string>(1),
            ResultOf.Fail<int, string>("error2"),
            ResultOf.Fail<int, string>("error3")
        };

        var combined = ResultOf.Combine(results);

        Assert.True(combined.IsFail);
        Assert.True(combined.TryGetFail(out var error));
        Assert.Equal(2, error.ErrorValue.Count);
        Assert.Contains("error2", error.ErrorValue);
        Assert.Contains("error3", error.ErrorValue);
    }

    [Fact]
    public void Combine_WithAllErrors_ReturnsAllErrors()
    {
        var results = new[]
        {
            ResultOf.Fail<int, string>("error1"),
            ResultOf.Fail<int, string>("error2"),
            ResultOf.Fail<int, string>("error3")
        };

        var combined = ResultOf.Combine(results);

        Assert.True(combined.IsFail);
        Assert.True(combined.TryGetFail(out var error));
        Assert.Equal(3, error.ErrorValue.Count);
        Assert.Contains("error1", error.ErrorValue);
        Assert.Contains("error2", error.ErrorValue);
        Assert.Contains("error3", error.ErrorValue);
    }

    // ============================================
    // Combine (2 results)
    // ============================================

    [Fact]
    public void Combine2_WithBothSuccess_ReturnsTuple()
    {
        var result1 = ResultOf.Ok<int, string>(42);
        var result2 = ResultOf.Ok<string, string>("hello");

        var combined = ResultOf.Combine(result1, result2);

        Assert.True(combined.IsOk);
        Assert.True(combined.TryGetOk(out var ok));
        Assert.Equal((42, "hello"), ok.Value);
    }

    [Fact]
    public void Combine2_WithFirstError_ReturnsError()
    {
        var result1 = ResultOf.Fail<int, string>("error1");
        var result2 = ResultOf.Ok<string, string>("hello");

        var combined = ResultOf.Combine(result1, result2);

        Assert.True(combined.IsFail);
        Assert.True(combined.TryGetFail(out var error));
        Assert.Single(error.ErrorValue);
        Assert.Contains("error1", error.ErrorValue);
    }

    [Fact]
    public void Combine2_WithSecondError_ReturnsError()
    {
        var result1 = ResultOf.Ok<int, string>(42);
        var result2 = ResultOf.Fail<string, string>("error2");

        var combined = ResultOf.Combine(result1, result2);

        Assert.True(combined.IsFail);
        Assert.True(combined.TryGetFail(out var error));
        Assert.Single(error.ErrorValue);
        Assert.Contains("error2", error.ErrorValue);
    }

    [Fact]
    public void Combine2_WithBothErrors_ReturnsBothErrors()
    {
        var result1 = ResultOf.Fail<int, string>("error1");
        var result2 = ResultOf.Fail<string, string>("error2");

        var combined = ResultOf.Combine(result1, result2);

        Assert.True(combined.IsFail);
        Assert.True(combined.TryGetFail(out var error));
        Assert.Equal(2, error.ErrorValue.Count);
        Assert.Contains("error1", error.ErrorValue);
        Assert.Contains("error2", error.ErrorValue);
    }

    // ============================================
    // Combine (3 results)
    // ============================================

    [Fact]
    public void Combine3_WithAllSuccess_ReturnsTuple()
    {
        var result1 = ResultOf.Ok<int, string>(42);
        var result2 = ResultOf.Ok<string, string>("hello");
        var result3 = ResultOf.Ok<bool, string>(true);

        var combined = ResultOf.Combine(result1, result2, result3);

        Assert.True(combined.IsOk);
        Assert.True(combined.TryGetOk(out var ok));
        Assert.Equal((42, "hello", true), ok.Value);
    }

    [Fact]
    public void Combine3_WithErrors_CollectsAllErrors()
    {
        var result1 = ResultOf.Fail<int, string>("error1");
        var result2 = ResultOf.Ok<string, string>("hello");
        var result3 = ResultOf.Fail<bool, string>("error3");

        var combined = ResultOf.Combine(result1, result2, result3);

        Assert.True(combined.IsFail);
        Assert.True(combined.TryGetFail(out var error));
        Assert.Equal(2, error.ErrorValue.Count);
        Assert.Contains("error1", error.ErrorValue);
        Assert.Contains("error3", error.ErrorValue);
    }

    // ============================================
    // Combine (4 results)
    // ============================================

    [Fact]
    public void Combine4_WithAllSuccess_ReturnsTuple()
    {
        var result1 = ResultOf.Ok<int, string>(42);
        var result2 = ResultOf.Ok<string, string>("hello");
        var result3 = ResultOf.Ok<bool, string>(true);
        var result4 = ResultOf.Ok<double, string>(3.14);

        var combined = ResultOf.Combine(result1, result2, result3, result4);

        Assert.True(combined.IsOk);
        Assert.True(combined.TryGetOk(out var ok));
        Assert.Equal((42, "hello", true, 3.14), ok.Value);
    }

    [Fact]
    public void Combine4_WithErrors_CollectsAllErrors()
    {
        var result1 = ResultOf.Fail<int, string>("error1");
        var result2 = ResultOf.Ok<string, string>("hello");
        var result3 = ResultOf.Fail<bool, string>("error3");
        var result4 = ResultOf.Fail<double, string>("error4");

        var combined = ResultOf.Combine(result1, result2, result3, result4);

        Assert.True(combined.IsFail);
        Assert.True(combined.TryGetFail(out var error));
        Assert.Equal(3, error.ErrorValue.Count);
        Assert.Contains("error1", error.ErrorValue);
        Assert.Contains("error3", error.ErrorValue);
        Assert.Contains("error4", error.ErrorValue);
    }

    // ============================================
    // Real-World Example
    // ============================================

    [Fact]
    public void RealWorld_FormValidation_CombinesMultipleFields()
    {
        var emailResult = ValidateEmail("test@example.com");
        var ageResult = ValidateAge(25);
        var nameResult = ValidateName("Alice");

        var combined = ResultOf.Combine(emailResult, ageResult, nameResult);

        Assert.True(combined.IsOk);
        Assert.True(combined.TryGetOk(out var ok));
        Assert.Equal(("test@example.com", 25, "Alice"), ok.Value);
    }

    [Fact]
    public void RealWorld_FormValidation_CollectsMultipleErrors()
    {
        var emailResult = ValidateEmail("invalid");
        var ageResult = ValidateAge(15);
        var nameResult = ValidateName("A");

        var combined = ResultOf.Combine(emailResult, ageResult, nameResult);

        Assert.True(combined.IsFail);
        Assert.True(combined.TryGetFail(out var error));
        Assert.Equal(3, error.ErrorValue.Count);
        Assert.Contains("Invalid email format", error.ErrorValue);
        Assert.Contains("Age must be 18 or older", error.ErrorValue);
        Assert.Contains("Name must be longer than 2 characters", error.ErrorValue);
    }

    // Helper methods
    private static ResultOf<string, string> ValidateEmail(string email)
        => email.Contains('@')
            ? ResultOf.Ok<string, string>(email)
            : ResultOf.Fail<string, string>("Invalid email format");

    private static ResultOf<int, string> ValidateAge(int age)
        => age >= 18
            ? ResultOf.Ok<int, string>(age)
            : ResultOf.Fail<int, string>("Age must be 18 or older");

    private static ResultOf<string, string> ValidateName(string name)
        => name.Length > 2
            ? ResultOf.Ok<string, string>(name)
            : ResultOf.Fail<string, string>("Name must be longer than 2 characters");
}
