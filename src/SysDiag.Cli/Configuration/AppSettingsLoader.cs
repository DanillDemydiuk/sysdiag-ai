using System.Text.Json;
using System.Text.Json.Nodes;

namespace SysDiag.Cli.Configuration;

/// <summary>
/// Reads <see cref="AppSettings"/> from appsettings.json next to the executable
/// and merges the optional appsettings.local.json on top of it.
/// </summary>
/// <remarks>
/// Written against System.Text.Json instead of pulling in the configuration
/// packages: two files and four settings do not justify a dependency, and this
/// way the whole loading path stays readable in one screen.
/// </remarks>
public static class AppSettingsLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Loads the settings. A missing file is not an error: the defaults of
    /// <see cref="AppSettings"/> are a working configuration on their own.
    /// </summary>
    /// <param name="baseDirectory">
    /// Directory that holds the settings files. Defaults to the directory of the
    /// executable, because that is where the build copies appsettings.json.
    /// </param>
    public static AppSettings Load(string? baseDirectory = null)
    {
        string directory = baseDirectory ?? AppContext.BaseDirectory;

        JsonObject merged = ReadObject(Path.Combine(directory, AppSettings.FileName)) ?? [];
        JsonObject? local = ReadObject(Path.Combine(directory, AppSettings.LocalFileName));

        if (local is not null)
        {
            Merge(merged, local);
        }

        return merged.Deserialize<AppSettings>(SerializerOptions) ?? new AppSettings();
    }

    private static JsonObject? ReadObject(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
        }
        catch (JsonException)
        {
            // A broken settings file must not crash the program: fall back to the
            // defaults, the CLI reports the problem separately.
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// Copies values of <paramref name="overrides"/> into <paramref name="target"/>.
    /// Nested objects are merged recursively so a local file can override a single
    /// setting, for example only the model name.
    /// </summary>
    private static void Merge(JsonObject target, JsonObject overrides)
    {
        foreach (KeyValuePair<string, JsonNode?> property in overrides)
        {
            if (property.Value is JsonObject nested && target[property.Key] is JsonObject existing)
            {
                Merge(existing, nested);
                continue;
            }

            target[property.Key] = property.Value?.DeepClone();
        }
    }
}
