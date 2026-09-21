using EphemeralDevpods.Core.Auth;
using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Functions.Http;

namespace EphemeralDevpods.Tests.Auth;

/// <summary>The `?error=` codes the OAuth callback redirects with; the SPA's authErrors.ts has a message for each.</summary>
public class AuthErrorCodeTests
{
    [Fact]
    public void Unlinked_github_maps_to_github_not_linked() =>
        Assert.Equal("github_not_linked",
            ExceptionHandlingMiddleware.ExpectedAuthErrorCode(new IdentityNotLinkedException(IdentityProvider.GitHub)));

    [Fact]
    public void Identity_owned_by_another_user_maps_to_already_linked() =>
        Assert.Equal("already_linked",
            ExceptionHandlingMiddleware.ExpectedAuthErrorCode(new IdentityAlreadyLinkedException(IdentityProvider.GitHub)));

    [Fact]
    public void Second_account_of_the_same_provider_maps_to_provider_already_linked() =>
        Assert.Equal("provider_already_linked",
            ExceptionHandlingMiddleware.ExpectedAuthErrorCode(new AccountRuleException("x")));

    [Fact]
    public void Anything_else_has_no_expected_code_and_falls_back_to_provider_error()
    {
        Assert.Null(ExceptionHandlingMiddleware.ExpectedAuthErrorCode(new OAuthExchangeException("boom")));
        Assert.Null(ExceptionHandlingMiddleware.ExpectedAuthErrorCode(new InvalidOperationException()));
        Assert.Equal("provider_error", ExceptionHandlingMiddleware.ProviderErrorCode);
    }
}
