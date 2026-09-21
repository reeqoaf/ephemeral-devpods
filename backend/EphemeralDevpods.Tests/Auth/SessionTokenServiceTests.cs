using EphemeralDevpods.Functions.Http;

namespace EphemeralDevpods.Tests.Auth;

public class SessionTokenServiceTests
{
    private sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private const string Key = "test-signing-key-that-is-at-least-32-chars-long";

    private static AuthOptions Options(string key = Key) => new() { SessionSigningKey = key };

    private static SessionTokenService Service(TimeProvider? clock = null, string key = Key) =>
        new(Options(key), clock ?? TimeProvider.System);

    [Fact]
    public async Task Session_round_trips_the_user_id()
    {
        var service = Service();

        var userId = await service.ValidateSessionAsync(service.CreateSession("user-1"));

        Assert.Equal("user-1", userId);
    }

    [Fact]
    public async Task Tampered_session_is_rejected()
    {
        var service = Service();
        var token = service.CreateSession("user-1");
        var parts = token.Split('.');
        // Swap the payload for one claiming a different user, keeping the original signature.
        var forgedPayload = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode("""{"sub":"user-2","aud":"session","iss":"ephemeral-devpods"}""");
        var forged = $"{parts[0]}.{forgedPayload}.{parts[2]}";

        Assert.Null(await service.ValidateSessionAsync(forged));
    }

    [Fact]
    public async Task Session_signed_with_a_different_key_is_rejected()
    {
        var token = Service(key: "another-signing-key-that-is-also-32-chars!!").CreateSession("user-1");

        Assert.Null(await Service().ValidateSessionAsync(token));
    }

    [Fact]
    public async Task Session_expires_after_its_lifetime()
    {
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var service = Service(clock);
        var token = service.CreateSession("user-1");

        clock.Now = clock.Now.Add(SessionTokenService.SessionLifetime).AddSeconds(1);

        Assert.Null(await service.ValidateSessionAsync(token));
    }

    [Fact]
    public async Task Session_is_still_valid_just_before_expiry()
    {
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var service = Service(clock);
        var token = service.CreateSession("user-1");

        clock.Now = clock.Now.Add(SessionTokenService.SessionLifetime).AddSeconds(-1);

        Assert.Equal("user-1", await service.ValidateSessionAsync(token));
    }

    [Fact]
    public async Task Garbage_is_rejected_without_throwing()
    {
        var service = Service();

        Assert.Null(await service.ValidateSessionAsync("not-a-jwt"));
        Assert.Null(await service.ValidateSessionAsync(""));
    }

    [Fact]
    public async Task State_round_trips_including_link_user_id()
    {
        var service = Service();
        var state = new OAuthState("s", "verifier", OAuthFlowMode.Link, "user-1", "/settings");

        var loaded = await service.ValidateStateAsync(service.CreateState(state));

        Assert.Equal(state, loaded);
    }

    [Fact]
    public async Task State_for_login_has_no_user_id()
    {
        var service = Service();

        var loaded = await service.ValidateStateAsync(
            service.CreateState(new OAuthState("s", "v", OAuthFlowMode.Login, null, "/")));

        Assert.NotNull(loaded);
        Assert.Null(loaded.UserId);
    }

    [Fact]
    public async Task State_expires_after_ten_minutes()
    {
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var service = Service(clock);
        var token = service.CreateState(new OAuthState("s", "v", OAuthFlowMode.Login, null, "/"));

        clock.Now = clock.Now.Add(SessionTokenService.StateLifetime).AddSeconds(1);

        Assert.Null(await service.ValidateStateAsync(token));
    }

    [Fact]
    public async Task A_state_token_cannot_be_used_as_a_session_and_vice_versa()
    {
        var service = Service();
        var stateToken = service.CreateState(new OAuthState("s", "v", OAuthFlowMode.Login, "user-1", "/"));
        var sessionToken = service.CreateSession("user-1");

        Assert.Null(await service.ValidateSessionAsync(stateToken));
        Assert.Null(await service.ValidateStateAsync(sessionToken));
    }
}
