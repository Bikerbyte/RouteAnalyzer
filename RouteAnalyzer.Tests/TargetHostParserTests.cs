using RouteAnalyzer.Services;

namespace RouteAnalyzer.Tests;

public class TargetHostParserTests
{
    [Theory]
    [InlineData("1.1.1.1", "1.1.1.1")]
    [InlineData("https://vpn.example.com/login", "vpn.example.com")]
    [InlineData(" example.com. ", "example.com")]
    public void TryNormalize_AcceptsSupportedTargets(string input, string expected)
    {
        var result = TargetHostParser.TryNormalize(input, out var normalizedTarget);

        Assert.True(result);
        Assert.Equal(expected, normalizedTarget);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bad host name")]
    [InlineData("example.com/path/extra")]
    public void TryNormalize_RejectsInvalidTargets(string input)
    {
        var result = TargetHostParser.TryNormalize(input, out var normalizedTarget);

        Assert.False(result);
        Assert.Equal(string.Empty, normalizedTarget);
    }
}
