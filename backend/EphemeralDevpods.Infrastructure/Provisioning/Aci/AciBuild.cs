using Azure.ResourceManager.ContainerRegistry.Models;
using EphemeralDevpods.Core.Git;
using EphemeralDevpods.Core.Models;

namespace EphemeralDevpods.Infrastructure.Provisioning.Aci;

/// <summary>
/// ACI can only pull images, so a devcontainer that builds from a Dockerfile is built by ACR Tasks straight from the
/// GitHub URL (no clone on our side, spec section 4) and pushed to our registry. Pure request-building, no Azure calls.
/// </summary>
public static class AciBuild
{
    /// <summary>Image name inside the registry, tagged per environment (spec section 4).</summary>
    public static string ImageName(string environmentId) => $"epd/{environmentId}:latest";

    /// <summary>The fully qualified reference a container group pulls.</summary>
    public static string ImageReference(AciOptions options, string environmentId) =>
        $"{options.RegistryLoginServer}/{ImageName(environmentId)}";

    /// <summary>
    /// ACR git context: <c>url#branch:folder</c>. The branch is left out (repo default branch) and the folder is the
    /// resolved build context, so only that subtree is the build's root.
    /// </summary>
    public static string SourceLocation(string owner, string repo, string resolvedContext) =>
        $"https://github.com/{owner}/{repo}.git" + (resolvedContext.Length > 0 ? $"#:{resolvedContext}" : "");

    /// <exception cref="BuildContextNotFoundException">The Dockerfile isn't inside the build context.</exception>
    public static ContainerRegistryDockerBuildContent Content(
        string environmentId, string repoUrl, EnvironmentSpec spec)
    {
        var (owner, repo) = GitHubRepoUrl.Parse(repoUrl);
        var contextPath = spec.BuildContextPath ?? ".";
        var resolvedContext = DevcontainerPaths.Resolve(spec.DevcontainerBaseDirectory, contextPath);
        var resolvedDockerfile = DevcontainerPaths.Resolve(spec.DevcontainerBaseDirectory, spec.DockerfilePath!);

        var dockerfileInContext = DevcontainerPaths.ReparentUnderContext(resolvedDockerfile, resolvedContext)
            ?? throw new BuildContextNotFoundException(
                $"Dockerfile \"{spec.DockerfilePath}\" is not located within build context \"{contextPath}\" in {repoUrl}.");

        var content = new ContainerRegistryDockerBuildContent(
            dockerfileInContext,
            new ContainerRegistryPlatformProperties(ContainerRegistryOS.Linux)
            {
                Architecture = ContainerRegistryOSArchitecture.Amd64, // ACI only runs x64 images
            })
        {
            SourceLocation = SourceLocation(owner, repo, resolvedContext),
            IsPushEnabled = true,
        };
        content.ImageNames.Add(ImageName(environmentId));
        foreach (var (name, value) in spec.BuildArgs)
        {
            content.Arguments.Add(new ContainerRegistryRunArgument(name, value));
        }

        return content;
    }

    public static bool IsTerminal(ContainerRegistryRunStatus? status) =>
        status == ContainerRegistryRunStatus.Succeeded
        || status == ContainerRegistryRunStatus.Failed
        || status == ContainerRegistryRunStatus.Canceled
        || status == ContainerRegistryRunStatus.Error
        || status == ContainerRegistryRunStatus.Timeout;
}
