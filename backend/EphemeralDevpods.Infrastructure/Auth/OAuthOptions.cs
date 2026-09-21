namespace EphemeralDevpods.Infrastructure.Auth;

public sealed class MicrosoftOAuthOptions
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
}

public sealed class GitHubOAuthOptions
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
}
