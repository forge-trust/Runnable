using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Config;

public class FileBasedConfigProvider : IConfigProvider
{
    private readonly IConfigFileLocationProvider _configFileLocationProvider;
    private readonly ILogger<FileBasedConfigProvider> _logger;

    private readonly Lazy<Dictionary<string, JsonNode>> _environmentsLazy;

    public int Priority { get; } = 1;
    public string Name { get; } = nameof(FileBasedConfigProvider);

    public FileBasedConfigProvider(
        IConfigFileLocationProvider configFileLocationProvider,
        ILogger<FileBasedConfigProvider> logger)
    {
        _configFileLocationProvider = configFileLocationProvider;
        _logger = logger;

        _environmentsLazy = new Lazy<Dictionary<string, JsonNode>>(InitializeEnvironments, true);
    }

    public T? GetValue<T>(string environment, string key)
    {
        if (_environmentsLazy.Value.TryGetValue(environment, out var envConfig))
        {
            return GetValue<T>(envConfig, key);
        }

        return default;
    }

    private Dictionary<string, JsonNode> InitializeEnvironments()
    {
        var environments = new Dictionary<string, JsonNode>(StringComparer.OrdinalIgnoreCase);

        var directory = _configFileLocationProvider.Directory;
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return environments;
        }

        // Collect matching files
        string[] files =
        [
            ..Directory.EnumerateFiles(directory, "appsettings*.json", SearchOption.TopDirectoryOnly),
            ..Directory.EnumerateFiles(directory, "config_*.json", SearchOption.TopDirectoryOnly)
        ];

        // Deterministic order so merges are predictable; later files override earlier ones when keys collide
        foreach (var file in files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            JsonNode? root;
            try
            {
                var text = File.ReadAllText(file);
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                root = JsonNode.Parse(text);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping malformed config file {FileName}", Path.GetFileName(file));

                continue;
            }

            if (root is not JsonObject obj)
            {
                continue; // Only merge JSON objects at the root
            }

            var fileName = Path.GetFileNameWithoutExtension(file);
            var environment = ExtractEnvironment(fileName);

            if (!environments.TryGetValue(environment, out var existing))
            {
                existing = new JsonObject();
                environments[environment] = existing;
            }

            if (existing is JsonObject targetObj)
            {
                MergeJsonObjects(targetObj, obj);
            }
        }

        return environments;
    }

    private static string ExtractEnvironment(string fileName)
    {
        // Patterns supported:
        // appsettings.json => production
        // appsettings.Development.json => Development
        // config_Foo.Development.json => Development
        // config_Foo.json or config.json (if it appears) => production
        // Any other unexpected pattern falls back to production

        if (fileName.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith("config_", StringComparison.OrdinalIgnoreCase))
        {
            var parts = fileName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length > 1)
            {
                // second segment is environment (appsettings.{Env})
                return parts[1];
            }
        }

        return Environments.Production;
    }

    private T? GetValue<T>(JsonNode node, string key)
    {
        var keys = key.Split('.');
        JsonNode? currentNode = node;
        foreach (var k in keys)
        {
            if (currentNode is JsonObject obj && obj.TryGetPropertyValue(k, out var nextNode))
            {
                currentNode = nextNode;
            }
            else
            {
                return default;
            }
        }

        if (currentNode != null)
        {
            try
            {
                return currentNode.Deserialize<T>();
            }
            catch
            {
                return default;
            }
        }

        return default;
    }

    private void MergeJsonObjects(JsonObject target, JsonObject source)
    {
        foreach (var kvp in source)
        {
            if (kvp.Value == null)
            {
                // Skip null values in source
                continue;
            }

            if (target.ContainsKey(kvp.Key))
            {
                if (target[kvp.Key] is JsonObject targetObj && kvp.Value is JsonObject sourceObj)
                {
                    MergeJsonObjects(targetObj, sourceObj);
                }
                else
                {
                    target[kvp.Key] = kvp.Value?.DeepClone();
                }
            }
            else
            {
                target[kvp.Key] = kvp.Value?.DeepClone();
            }
        }
    }
}
