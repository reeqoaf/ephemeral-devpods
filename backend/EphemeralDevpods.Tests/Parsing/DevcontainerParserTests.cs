using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Core.Parsing;

namespace EphemeralDevpods.Tests.Parsing;

public class DevcontainerParserTests
{
    private readonly DevcontainerParser _parser = new();

    // Most tests don't care about base-directory threading — default to the common
    // ".devcontainer" location so call sites below stay uncluttered.
    private EnvironmentSpec Parse(string content) => _parser.Parse(content, ".devcontainer");

    [Fact]
    public void Parses_image_and_forward_ports_and_name()
    {
        var spec = Parse("""
            {
              "name": "My Dev Env",
              "image": "node:20",
              "forwardPorts": [3000, 5173]
            }
            """);

        Assert.Equal("My Dev Env", spec.Name);
        Assert.Equal("node:20", spec.Image);
        Assert.Equal([3000, 5173], spec.ForwardPorts);
        Assert.Null(spec.DockerfilePath);
    }

    [Fact]
    public void Parses_build_with_defaults_when_dockerfile_and_context_omitted()
    {
        var spec = Parse("""{ "build": {} }""");

        Assert.Equal("Dockerfile", spec.DockerfilePath);
        Assert.Equal(".", spec.BuildContextPath);
    }

    [Fact]
    public void Parses_build_dockerfile_context_and_args_as_relative_paths()
    {
        var spec = Parse("""
            {
              "build": {
                "dockerfile": "docker/Dockerfile.dev",
                "context": "..",
                "args": { "NODE_VERSION": "20" }
              }
            }
            """);

        Assert.Equal("docker/Dockerfile.dev", spec.DockerfilePath);
        Assert.Equal("..", spec.BuildContextPath);
        Assert.Equal("20", spec.BuildArgs["NODE_VERSION"]);
    }

    [Fact]
    public void Tolerates_comments_and_trailing_commas_jsonc()
    {
        var spec = Parse("""
            {
              // a comment
              "image": "node:20",
              "forwardPorts": [3000,],
            }
            """);

        Assert.Equal("node:20", spec.Image);
        Assert.Equal([3000], spec.ForwardPorts);
    }

    [Fact]
    public void Throws_on_invalid_json()
    {
        Assert.Throws<DevcontainerParseException>(() => Parse("{ not json "));
    }

    [Fact]
    public void Throws_when_neither_image_nor_build_present()
    {
        Assert.Throws<DevcontainerParseException>(() => Parse("""{ "name": "no image" }"""));
    }

    [Fact]
    public void Throws_on_dockerComposeFile()
    {
        var ex = Assert.Throws<DevcontainerParseException>(() =>
            Parse("""{ "dockerComposeFile": "docker-compose.yml" }"""));

        Assert.Contains("dockerComposeFile", ex.Message);
    }

    [Fact]
    public void Throws_on_legacy_top_level_dockerFile_field()
    {
        Assert.Throws<DevcontainerParseException>(() =>
            Parse("""{ "dockerFile": "Dockerfile", "context": "." }"""));
    }

    [Fact]
    public void Throws_on_forwardPorts_string_entry()
    {
        Assert.Throws<DevcontainerParseException>(() =>
            Parse("""{ "image": "node:20", "forwardPorts": ["3000:3000"] }"""));
    }

    [Fact]
    public void Parses_containerEnv_and_ignores_remoteEnv()
    {
        var spec = Parse("""
            {
              "image": "node:20",
              "containerEnv": { "FOO": "bar" },
              "remoteEnv": { "SHOULD_BE_IGNORED": "yes" }
            }
            """);

        Assert.Equal("bar", spec.ContainerEnv["FOO"]);
        Assert.False(spec.ContainerEnv.ContainsKey("SHOULD_BE_IGNORED"));
    }

    [Fact]
    public void Parses_postCreateCommand_string_form()
    {
        var spec = Parse("""{ "image": "node:20", "postCreateCommand": "npm install" }""");

        Assert.Equal(["npm install"], spec.PostCreateCommand);
    }

    [Fact]
    public void Parses_postCreateCommand_array_form()
    {
        var spec = Parse(
            """{ "image": "node:20", "postCreateCommand": ["npm", "install"] }""");

        Assert.Equal(["npm", "install"], spec.PostCreateCommand);
    }

    [Fact]
    public void Parses_postAttachCommand_string_form()
    {
        var spec = Parse("""{ "image": "node:20", "postAttachCommand": "npm start" }""");

        Assert.Equal(["npm start"], spec.PostAttachCommand);
    }

    [Fact]
    public void Parses_postAttachCommand_array_form()
    {
        var spec = Parse("""{ "image": "node:20", "postAttachCommand": ["npm", "start"] }""");

        Assert.Equal(["npm", "start"], spec.PostAttachCommand);
    }

    [Fact]
    public void Ignores_postStartCommand_and_other_lifecycle_hooks()
    {
        var spec = Parse("""
            {
              "image": "node:20",
              "postStartCommand": "should be ignored",
              "onCreateCommand": "should be ignored too"
            }
            """);

        Assert.Empty(spec.PostCreateCommand);
        Assert.Empty(spec.PostAttachCommand);
    }

    [Fact]
    public void Ignores_workspaceFolder_remoteUser_features_and_customizations()
    {
        var spec = Parse("""
            {
              "image": "node:20",
              "workspaceFolder": "/workspaces/repo",
              "remoteUser": "node",
              "features": { "ghcr.io/devcontainers/features/docker-in-docker:2": {} },
              "customizations": { "vscode": { "extensions": ["foo.bar"] } }
            }
            """);

        Assert.Equal("node:20", spec.Image);
    }

    [Fact]
    public void Passes_through_variable_substitution_syntax_literally()
    {
        var spec = Parse("""
            {
              "image": "node:20",
              "containerEnv": { "HOME_VAR": "${localEnv:FOO}" }
            }
            """);

        Assert.Equal("${localEnv:FOO}", spec.ContainerEnv["HOME_VAR"]);
    }

    [Fact]
    public void Carries_base_directory_through_for_context_resolution()
    {
        var spec = _parser.Parse("""{ "image": "node:20" }""", ".devcontainer");
        Assert.Equal(".devcontainer", spec.DevcontainerBaseDirectory);

        var rootSpec = _parser.Parse("""{ "image": "node:20" }""", "");
        Assert.Equal("", rootSpec.DevcontainerBaseDirectory);
    }
}
