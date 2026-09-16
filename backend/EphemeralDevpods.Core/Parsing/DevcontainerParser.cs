using System.Text.Json;
using EphemeralDevpods.Core.Models;

namespace EphemeralDevpods.Core.Parsing;

/// <summary>
/// Parses the raw text of a devcontainer.json (JSONC) file into a normalized EnvironmentSpec.
/// Pure logic, no filesystem/network access — the caller is responsible for fetching the file
/// content (see IDevcontainerFileFetcher) from wherever it lives (Stage 1, docs/spec.md §14).
/// </summary>
public interface IDevcontainerParser
{
    /// <param name="devcontainerJsonContent">The file's raw text.</param>
    /// <param name="baseDirectory">
    /// Folder the file was found in ("" for repo root, ".devcontainer" for the nested location) —
    /// carried through to EnvironmentSpec.DevcontainerBaseDirectory, see DevcontainerFile.
    /// </param>
    /// <exception cref="DevcontainerParseException">
    /// The file uses something v1 doesn't support, or isn't valid JSONC.
    /// </exception>
    EnvironmentSpec Parse(string devcontainerJsonContent, string baseDirectory);
}

public sealed class DevcontainerParser : IDevcontainerParser
{
    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public EnvironmentSpec Parse(string devcontainerJsonContent, string baseDirectory)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(devcontainerJsonContent, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new DevcontainerParseException($"devcontainer.json is not valid JSON(C): {ex.Message}");
        }

        using (document)
        {
            var root = document.RootElement;

            if (root.TryGetProperty("dockerComposeFile", out _))
            {
                throw new DevcontainerParseException(
                    "This repo's devcontainer.json uses dockerComposeFile (multi-container sidecars), " +
                    "which isn't supported yet — v1 only supports image/build-based single-container devcontainers.");
            }

            if (root.TryGetProperty("dockerFile", out _) || root.TryGetProperty("context", out _))
            {
                throw new DevcontainerParseException(
                    "This repo's devcontainer.json uses the legacy top-level dockerFile/context fields, " +
                    "which aren't supported — use the modern nested \"build\": { \"dockerfile\", \"context\" } object instead.");
            }

            var name = GetOptionalString(root, "name");
            var image = GetOptionalString(root, "image");

            string? dockerfilePath = null;
            string? buildContextPath = null;
            var buildArgs = new Dictionary<string, string>();

            if (root.TryGetProperty("build", out var buildElement))
            {
                if (buildElement.ValueKind != JsonValueKind.Object)
                {
                    throw new DevcontainerParseException("\"build\" must be an object.");
                }

                dockerfilePath = GetOptionalString(buildElement, "dockerfile") ?? "Dockerfile";
                buildContextPath = GetOptionalString(buildElement, "context") ?? ".";

                if (buildElement.TryGetProperty("args", out var argsElement))
                {
                    buildArgs = ReadStringDictionary(argsElement, "build.args");
                }
            }

            if (image is null && dockerfilePath is null)
            {
                throw new DevcontainerParseException(
                    "devcontainer.json must specify either \"image\" or \"build\".");
            }

            var forwardPorts = new List<int>();
            if (root.TryGetProperty("forwardPorts", out var portsElement))
            {
                if (portsElement.ValueKind != JsonValueKind.Array)
                {
                    throw new DevcontainerParseException("\"forwardPorts\" must be an array.");
                }

                foreach (var portElement in portsElement.EnumerateArray())
                {
                    if (portElement.ValueKind != JsonValueKind.Number || !portElement.TryGetInt32(out var port))
                    {
                        throw new DevcontainerParseException(
                            "\"forwardPorts\" entries must be plain numbers — host:container mappings " +
                            "and hostname references aren't supported.");
                    }

                    forwardPorts.Add(port);
                }
            }

            var containerEnv = root.TryGetProperty("containerEnv", out var envElement)
                ? ReadStringDictionary(envElement, "containerEnv")
                : new Dictionary<string, string>();

            var postCreateCommand = ReadCommand(root, "postCreateCommand");
            var postAttachCommand = ReadCommand(root, "postAttachCommand");

            return new EnvironmentSpec
            {
                Name = name,
                Image = image,
                DevcontainerBaseDirectory = baseDirectory,
                DockerfilePath = dockerfilePath,
                BuildContextPath = buildContextPath,
                BuildArgs = buildArgs,
                ForwardPorts = forwardPorts,
                ContainerEnv = containerEnv,
                PostCreateCommand = postCreateCommand,
                PostAttachCommand = postAttachCommand,
            };
        }
    }

    /// <summary>postCreateCommand/postAttachCommand both accept a string or array-of-strings form.</summary>
    private static List<string> ReadCommand(JsonElement root, string fieldName)
    {
        if (!root.TryGetProperty(fieldName, out var element))
        {
            return [];
        }

        return element.ValueKind switch
        {
            JsonValueKind.String => [element.GetString()!],
            JsonValueKind.Array => ReadStringArray(element, fieldName),
            _ => throw new DevcontainerParseException($"\"{fieldName}\" must be a string or array of strings."),
        };
    }

    private static string? GetOptionalString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;

    private static Dictionary<string, string> ReadStringDictionary(JsonElement element, string fieldName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new DevcontainerParseException($"\"{fieldName}\" must be an object.");
        }

        var result = new Dictionary<string, string>();
        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String)
            {
                throw new DevcontainerParseException($"\"{fieldName}.{property.Name}\" must be a string.");
            }

            result[property.Name] = property.Value.GetString()!;
        }

        return result;
    }

    private static List<string> ReadStringArray(JsonElement element, string fieldName)
    {
        var result = new List<string>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                throw new DevcontainerParseException($"\"{fieldName}\" array entries must be strings.");
            }

            result.Add(item.GetString()!);
        }

        return result;
    }
}
