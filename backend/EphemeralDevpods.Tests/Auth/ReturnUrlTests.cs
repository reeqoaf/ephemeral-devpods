using EphemeralDevpods.Functions.Http;

namespace EphemeralDevpods.Tests.Auth;

public class ReturnUrlTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/new")]
    [InlineData("/settings?tab=accounts")]
    [InlineData("/a/b#frag")]
    public void Accepts_same_origin_relative_paths(string candidate) =>
        Assert.Equal(candidate, ReturnUrl.Sanitize(candidate));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("new")]
    [InlineData("https://evil.example/")]
    [InlineData("http://evil.example/")]
    [InlineData("//evil.example")]
    [InlineData("/\\evil.example")]
    [InlineData("\\\\evil.example")]
    [InlineData("javascript:alert(1)")]
    [InlineData("/ok\r\nSet-Cookie: x=y")]
    public void Rejects_everything_else_with_the_fallback(string? candidate) =>
        Assert.Equal("/", ReturnUrl.Sanitize(candidate));

    [Fact]
    public void Uses_the_supplied_fallback() =>
        Assert.Equal("/settings", ReturnUrl.Sanitize("//evil.example", fallback: "/settings"));
}
