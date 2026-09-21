using System.Net;
using EphemeralDevpods.Core.Auth;
using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Infrastructure.Auth;

namespace EphemeralDevpods.Tests.Auth;

public class GitHubOAuthProviderTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request, request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));
            return respond(request);
        }
    }

    private static GitHubOAuthProvider Provider(StubHandler handler) =>
        new(new HttpClient(handler), new GitHubOAuthOptions { ClientId = "cid", ClientSecret = "secret" });

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json) };

    [Fact]
    public async Task Exchanges_code_then_reads_the_numeric_id_as_the_subject()
    {
        var handler = new StubHandler(req => req.RequestUri!.Host switch
        {
            "github.com" => Json("""{ "access_token": "tok", "token_type": "bearer" }"""),
            "api.github.com" => Json("""{ "id": 583231, "login": "octocat", "email": null }"""),
            _ => throw new InvalidOperationException(req.RequestUri!.ToString()),
        });

        var identity = await Provider(handler).ExchangeAsync("the-code", "the-verifier", "http://localhost/cb", CancellationToken.None);

        Assert.Equal(new ExternalIdentity(IdentityProvider.GitHub, "583231", "octocat", null), identity);

        var tokenBody = System.Web.HttpUtility.ParseQueryString(handler.Requests[0].Body);
        Assert.Equal("the-code", tokenBody["code"]);
        Assert.Equal("the-verifier", tokenBody["code_verifier"]);
        Assert.Equal("cid", tokenBody["client_id"]);
        Assert.Equal("secret", tokenBody["client_secret"]);
        Assert.Equal("Bearer", handler.Requests[1].Request.Headers.Authorization?.Scheme);
        Assert.Equal("tok", handler.Requests[1].Request.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task A_200_with_an_error_body_is_a_failed_exchange()
    {
        var handler = new StubHandler(_ => Json("""{ "error": "bad_verification_code" }"""));

        await Assert.ThrowsAsync<OAuthExchangeException>(() =>
            Provider(handler).ExchangeAsync("c", "v", "http://localhost/cb", CancellationToken.None));

        Assert.Single(handler.Requests); // never went on to call /user
    }

    [Fact]
    public async Task A_failing_user_lookup_is_a_failed_exchange()
    {
        var handler = new StubHandler(req => req.RequestUri!.Host == "github.com"
            ? Json("""{ "access_token": "tok" }""")
            : Json("{}", HttpStatusCode.Unauthorized));

        await Assert.ThrowsAsync<OAuthExchangeException>(() =>
            Provider(handler).ExchangeAsync("c", "v", "http://localhost/cb", CancellationToken.None));
    }

    [Fact]
    public void Authorize_url_carries_pkce_state_and_no_scope()
    {
        var url = Provider(new StubHandler(_ => Json("{}"))).BuildAuthorizeUrl("st", "chal", "http://localhost/cb");
        var query = System.Web.HttpUtility.ParseQueryString(url.Query);

        Assert.Equal("github.com", url.Host);
        Assert.Equal("st", query["state"]);
        Assert.Equal("chal", query["code_challenge"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.Null(query["scope"]);
    }
}
