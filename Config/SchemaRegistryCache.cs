// ────────────────────────────────────────────────────────────────────
// SchemaRegistryCache.cs
// Created:  2026-02-11
// Author:   Ayu Admassu
// Purpose:  Downloads Avro schemas from Confluent Schema Registry and
//           caches them locally as .avsc files under schema-cache/.
//           Program.cs populates the cache at startup; SpecificRecord
//           classes read from the cache synchronously via the static
//           GetSchemaJsonFromCache method.
// ────────────────────────────────────────────────────────────────────

using Confluent.SchemaRegistry;
using Spectre.Console;

namespace ConfluentThroughputTestHarness.Config;

public class SchemaRegistryCache
{
    private static readonly string CacheDir =
        Path.Combine(AppContext.BaseDirectory, "schema-cache");

    private readonly ISchemaRegistryClient _client;
    private readonly bool _forceRefresh;

    public SchemaRegistryCache(SchemaRegistrySettings srSettings, bool forceRefresh = false)
    {
        _forceRefresh = forceRefresh;

        var config = new SchemaRegistryConfig
        {
            Url = srSettings.Url,
            BasicAuthCredentialsSource = AuthCredentialsSource.UserInfo,
            BasicAuthUserInfo = srSettings.BasicAuthUserInfo
        };
        _client = new CachedSchemaRegistryClient(config);

        Directory.CreateDirectory(CacheDir);
    }

    /// <summary>
    /// Returns the schema JSON for the given subject. Downloads from Schema
    /// Registry and writes to the local cache if the file is missing or
    /// --refresh-schemas was specified.
    /// </summary>
    public async Task<string> GetSchemaJsonAsync(string subject)
    {
        var cachePath = GetCachePath(subject);

        if (!_forceRefresh && File.Exists(cachePath))
        {
            AnsiConsole.MarkupLine($"  [grey]Schema cached:[/] {subject}");
            return await File.ReadAllTextAsync(cachePath);
        }

        AnsiConsole.MarkupLine($"  [grey]Downloading schema:[/] {subject}");
        var registeredSchema = await _client.GetLatestSchemaAsync(subject);
        var json = registeredSchema.SchemaString;

        await File.WriteAllTextAsync(cachePath, json);
        AnsiConsole.MarkupLine($"  [grey]Cached to:[/] {cachePath}");

        return json;
    }

    /// <summary>
    /// Reads schema JSON synchronously from the local cache. Safe to call
    /// from Lazy initializers because Program.cs always populates the cache
    /// before any SpecificRecord is accessed.
    /// </summary>
    public static string GetSchemaJsonFromCache(string subject)
    {
        var cachePath = GetCachePath(subject);
        if (!File.Exists(cachePath))
            throw new FileNotFoundException(
                $"Schema cache file not found for subject '{subject}'. " +
                "Ensure Program.cs has populated the cache before accessing SpecificRecord types.",
                cachePath);

        return File.ReadAllText(cachePath);
    }

    private static string GetCachePath(string subject) =>
        Path.Combine(CacheDir, $"{subject}.avsc");
}
