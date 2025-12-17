using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using Spectre.Console;

namespace Infra.AgentDeployment;

internal interface IAgentDefinitionLoader
{
    AgentDefinition[] LoadDefinitions();
}

internal sealed class JsonAgentDefinitionLoader : IAgentDefinitionLoader
{
    private readonly string _configPath;
    public JsonAgentDefinitionLoader(string configPath) => _configPath = configPath;

    public AgentDefinition[] LoadDefinitions()
    {
        if (!File.Exists(_configPath))
        {
            AnsiConsole.MarkupLine($"[red]Error: Agent configuration file not found at {_configPath}.[/]");
            return Array.Empty<AgentDefinition>();
        }

        try
        {
            var json = File.ReadAllText(_configPath);
            var sanitized = SanitizeJsonForParsing(json);
            var list = JsonSerializer.Deserialize(sanitized, AgentDefinitionJsonContext.Default.ListAgentDefinition) ?? new List<AgentDefinition>();
            AnsiConsole.MarkupLine($"[green]✓[/] Loaded {list.Count} agent definition(s).\n");
            return list.ToArray();
        }
        catch (JsonException jex)
        {
            // Preserve original parsing error details for user diagnostics
            AnsiConsole.MarkupLine($"[red]Error: Failed to parse agent configuration: {jex.Message}[/]");
            return Array.Empty<AgentDefinition>();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: Failed to load agent configuration: {ex.Message}[/]");
            return Array.Empty<AgentDefinition>();
        }
    }

    /// <summary>
    /// Performs minimal, targeted sanitization to correct common invalid JSON escapes
    /// introduced by hand-edits or non-JSON producers (for example, replacing "\'" with
    /// "'" and escaping lone backslashes that are not part of a valid JSON escape).
    /// This keeps the sanitizer conservative and avoids masking other JSON errors.
    /// </summary>
    private static string SanitizeJsonForParsing(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        // 1) Remove invalid single-quote escape sequences like \' which are not valid JSON escapes
        var result = Regex.Replace(input, "\\'", "'");

        // 2) Escape single backslashes that are not part of a valid JSON escape sequence.
        // Valid escapes: \" \\ \/ \b \f \n \r \t \uXXXX
        // The negative lookahead ensures we don't touch valid escapes or Unicode sequences.
        result = Regex.Replace(
            result,
            @"\\(?!(u[0-9A-Fa-f]{4}|[\""""\\/bfnrt]))",
            "\\\\");

        return result;
    }
}
