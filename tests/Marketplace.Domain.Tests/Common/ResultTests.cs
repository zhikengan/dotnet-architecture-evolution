using Marketplace.Domain.Common;

namespace Marketplace.Domain.Tests.Common;

public class ResultTests
{
    private static readonly Error SomeError = new("Test.Error", "boom");

    [Fact]
    public void Success_is_success_and_carries_no_error()
    {
        var result = Result.Success();
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void Failure_is_failure_and_carries_the_given_error()
    {
        var result = Result.Failure(SomeError);
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SomeError);
    }

    [Fact]
    public void Generic_success_exposes_the_value()
    {
        var result = Result.Success(42);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Generic_failure_throws_on_Value_access()
    {
        var result = Result.Failure<int>(SomeError);
        result.IsFailure.Should().BeTrue();
        var act = () => _ = result.Value;
        act.Should().Throw<InvalidOperationException>();
    }
}
