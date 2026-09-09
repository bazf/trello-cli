using System.Text;

namespace TrelloCli;

/// <summary>
/// Renders the human-readable views of <see cref="CommandCatalog"/>: the full help
/// page and the detail page for a single command.
/// </summary>
public static class HelpRenderer
{
    private const int WrapWidth = 96;

    public static string RenderOverview(string version)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"{CommandCatalog.ToolName} v{version}");
        builder.AppendLine(CommandCatalog.Tagline);
        builder.AppendLine();
        builder.AppendLine("USAGE:");
        builder.AppendLine($"  {CommandCatalog.ToolName} <command> [arguments] [options]");
        builder.AppendLine();
        builder.AppendLine("DISCOVERY:");
        foreach (var line in CommandCatalog.Discovery)
            builder.AppendLine($"  {line}");
        builder.AppendLine();
        AppendAuthentication(builder);
        AppendCommands(builder);
        AppendOutput(builder);
        AppendRestrictions(builder);
        AppendErrorCodes(builder);
        builder.AppendLine("EXAMPLES:");
        builder.AppendLine($"  {CommandCatalog.ToolName} --get-boards");
        builder.AppendLine($"  {CommandCatalog.ToolName} --get-all-cards 5f2c3d4e5f6a7b8c9d0e1f2a");
        builder.AppendLine($"  {CommandCatalog.ToolName} --create-card 5f2c...1f2a \"My Task\" --desc \"Details\"");
        builder.AppendLine($"  {CommandCatalog.ToolName} --move-card 5f2c...1f2a 5f2c...1f2b");
        return builder.ToString();
    }

    public static string RenderCommand(string version, CommandDefinition command)
    {
        var builder = new StringBuilder();
        var names = string.Join(", ", new[] { command.Name }.Concat(command.Aliases));
        builder.AppendLine($"{CommandCatalog.ToolName} v{version}");
        builder.AppendLine();
        builder.AppendLine($"{names} — {command.Summary}");
        builder.AppendLine($"Group: {command.Group}    Requires credentials: {(command.RequiresAuth ? "yes" : "no")}" +
            (command.Destructive ? "    Irreversible: yes" : string.Empty));
        builder.AppendLine();
        builder.AppendLine("USAGE:");
        builder.AppendLine($"  {command.Usage}");

        if (command.Arguments.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("ARGUMENTS:");
            var width = command.Arguments.Max(argument => Token(argument).Length);
            foreach (var argument in command.Arguments)
                AppendWrapped(builder, $"  {Token(argument).PadRight(width)}  ", argument.Description);
        }

        if (command.Options.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("OPTIONS:");
            var width = command.Options.Max(option => option.Display.Length);
            foreach (var option in command.Options)
                AppendWrapped(builder, $"  {option.Display.PadRight(width)}  ", option.Description);
        }

        if (command.Notes.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("LIMITS AND BEHAVIOR:");
            foreach (var note in command.Notes)
                AppendWrapped(builder, "  - ", note);
        }

        if (command.Examples.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("EXAMPLES:");
            foreach (var example in command.Examples)
                builder.AppendLine($"  {example}");
        }

        builder.AppendLine();
        builder.AppendLine($"See also: {CommandCatalog.ToolName} --help (all commands and global limits), " +
            $"{CommandCatalog.ToolName} --commands {command.Name} (JSON)");
        return builder.ToString();
    }

    private static void AppendAuthentication(StringBuilder builder)
    {
        // Kept as literal prose: these guarantees are security-relevant and are asserted verbatim.
        builder.AppendLine("AUTHENTICATION:");
        builder.AppendLine("  Option 1 - CLI (recommended):");
        builder.AppendLine($"    {CommandCatalog.ToolName} --set-auth <api-key>");
        builder.AppendLine("    You will be securely prompted for your Trello token.");
        builder.AppendLine();
        builder.AppendLine("  Option 2 - Environment variables:");
        builder.AppendLine("    TRELLO_API_KEY  - Your Trello API key");
        builder.AppendLine("    TRELLO_TOKEN    - Your Trello token");
        builder.AppendLine();
        builder.AppendLine("  Storage:");
        builder.AppendLine("    Windows Credential Manager; macOS Keychain; Linux Secret Service.");
        builder.AppendLine("    Linux requires secret-tool (libsecret-tools) and a running D-Bus Secret Service.");
        builder.AppendLine();
        builder.AppendLine("  Semantics:");
        builder.AppendLine("    Environment credentials override persisted credentials.");
        builder.AppendLine("    Legacy plaintext tokens are removed only after secure-store verification.");
        builder.AppendLine("    Migration failure preserves the legacy file and reports a safe warning.");
        builder.AppendLine("    Environment variables remain active after --clear-auth; it does not revoke tokens.");
        builder.AppendLine("    For headless use, set both TRELLO_API_KEY and TRELLO_TOKEN.");
        builder.AppendLine();
        builder.AppendLine($"  Get credentials: {CommandCatalog.Authentication.CredentialsUrl}");
        builder.AppendLine();
    }

    private static void AppendCommands(StringBuilder builder)
    {
        builder.AppendLine("COMMANDS:");
        var signatures = CommandCatalog.Commands.ToDictionary(command => command.Name, Signature);
        var width = Math.Min(signatures.Values.Max(signature => signature.Length), 44);

        foreach (var group in CommandCatalog.GroupOrder)
        {
            var commands = CommandCatalog.Commands.Where(command => command.Group == group).ToArray();
            if (commands.Length == 0) continue;

            builder.AppendLine();
            builder.AppendLine($"  {group}:");
            foreach (var command in commands)
            {
                var summary = command.Destructive ? $"{command.Summary} (irreversible)" : command.Summary;
                var signature = signatures[command.Name];

                // A signature wider than the column gets its own line so summaries stay aligned.
                if (signature.Length > width)
                {
                    builder.AppendLine($"    {signature}");
                    AppendWrapped(builder, $"    {new string(' ', width)}  ", summary);
                }
                else
                {
                    AppendWrapped(builder, $"    {signature.PadRight(width)}  ", summary);
                }

                foreach (var option in command.Options)
                    AppendWrapped(builder, $"      {$"[{option.Display}]".PadRight(width - 2)}  ", option.Description);
            }
        }

        builder.AppendLine();
        builder.AppendLine($"  Per-command detail, including its own limits: {CommandCatalog.ToolName} --help <command>");
        builder.AppendLine();
    }

    private static void AppendOutput(StringBuilder builder)
    {
        var output = CommandCatalog.Output;
        builder.AppendLine("OUTPUT:");
        builder.AppendLine($"  Success: {output.Success}");
        builder.AppendLine($"  Error:   {output.Error}");
        AppendWrapped(builder, "  ", output.Stdout);
        AppendWrapped(builder, "  ", output.ExitCode);
        AppendWrapped(builder, "  ", output.Stderr);
        builder.AppendLine();
    }

    private static void AppendRestrictions(StringBuilder builder)
    {
        builder.AppendLine("LIMITS AND RESTRICTIONS:");
        foreach (var group in CommandCatalog.Restrictions)
        {
            builder.AppendLine();
            builder.AppendLine($"  {group.Title}:");
            foreach (var item in group.Items)
                AppendWrapped(builder, "    - ", item);
        }

        builder.AppendLine();
    }

    private static void AppendErrorCodes(StringBuilder builder)
    {
        builder.AppendLine("ERROR CODES:");
        var width = CommandCatalog.ErrorCodes.Max(code => code.Code.Length);
        foreach (var code in CommandCatalog.ErrorCodes)
            AppendWrapped(builder, $"  {code.Code.PadRight(width)}  ", code.Meaning);
        builder.AppendLine();
    }

    private static string Signature(CommandDefinition command)
    {
        var parts = new List<string> { string.Join(", ", new[] { command.Name }.Concat(command.Aliases)) };
        parts.AddRange(command.Arguments.Select(Token));
        return string.Join(' ', parts);
    }

    private static string Token(CommandArgument argument)
    {
        var token = $"<{argument.Name}>";
        if (argument.Repeatable) token += "...";
        return argument.Required ? token : $"[{token}]";
    }

    /// <summary>Writes <paramref name="text"/> after <paramref name="prefix"/>, wrapping onto aligned continuation lines.</summary>
    private static void AppendWrapped(StringBuilder builder, string prefix, string text)
    {
        var indent = new string(' ', prefix.Length);
        var lineStart = prefix;
        var length = 0;

        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (length > 0 && prefix.Length + length + 1 + word.Length > WrapWidth)
            {
                builder.AppendLine();
                lineStart = indent;
                length = 0;
            }

            if (length == 0)
            {
                builder.Append(lineStart).Append(word);
                length = word.Length;
            }
            else
            {
                builder.Append(' ').Append(word);
                length += word.Length + 1;
            }
        }

        builder.AppendLine();
    }
}
