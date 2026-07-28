using DocuRelay.Contracts;
using Xunit;

namespace DocuRelay.Domain.Tests;

public sealed class UploadRetryPolicyTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(4, true)]
    public void IsTerminalFailure_IsTrueOnlyAtOrAfterTheRetryLimit(int attempts, bool expected)
    {
        Assert.Equal(expected, UploadRetryPolicy.IsTerminalFailure(attempts));
    }
}
