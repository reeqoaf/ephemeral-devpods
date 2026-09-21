using EphemeralDevpods.Infrastructure.Auth;

namespace EphemeralDevpods.Tests.Auth;

public class MicrosoftOAuthProviderTests
{
    [Theory]
    [InlineData("https://login.microsoftonline.com/72f988bf-86f1-41af-91ab-2d7cd011db47/v2.0")]
    // The personal-account (consumer) tenant id.
    [InlineData("https://login.microsoftonline.com/9188040d-6c67-4c5b-b112-36a304b66dad/v2.0")]
    public void Accepts_tenant_issuers(string issuer) => Assert.True(MicrosoftOAuthProvider.IsValidIssuer(issuer));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://login.microsoftonline.com/common/v2.0")]
    [InlineData("https://login.microsoftonline.com/72f988bf-86f1-41af-91ab-2d7cd011db47/v2.0/extra")]
    [InlineData("https://login.microsoftonline.com/72f988bf-86f1-41af-91ab-2d7cd011db47/")]
    [InlineData("https://sts.windows.net/72f988bf-86f1-41af-91ab-2d7cd011db47/")]
    [InlineData("https://evil.example/login.microsoftonline.com/72f988bf-86f1-41af-91ab-2d7cd011db47/v2.0")]
    [InlineData("https://login.microsoftonline.com.evil.example/72f988bf-86f1-41af-91ab-2d7cd011db47/v2.0")]
    public void Rejects_everything_else(string? issuer) => Assert.False(MicrosoftOAuthProvider.IsValidIssuer(issuer));

    [Fact]
    public void Authorize_url_carries_pkce_state_and_redirect()
    {
        var provider = new MicrosoftOAuthProvider(new HttpClient(), new MicrosoftOAuthOptions { ClientId = "cid", ClientSecret = "s" });

        var url = provider.BuildAuthorizeUrl("st", "chal", "http://localhost:5173/api/auth/callback/microsoft");
        var query = System.Web.HttpUtility.ParseQueryString(url.Query);

        Assert.Equal("login.microsoftonline.com", url.Host);
        Assert.Equal("/common/oauth2/v2.0/authorize", url.AbsolutePath);
        Assert.Equal("cid", query["client_id"]);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("st", query["state"]);
        Assert.Equal("chal", query["code_challenge"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.Equal("http://localhost:5173/api/auth/callback/microsoft", query["redirect_uri"]);
        Assert.Contains("openid", query["scope"]);
    }
}
