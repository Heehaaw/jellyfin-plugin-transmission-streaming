namespace Jellyfin.Plugin.HdrPlayback.Player;

using System.Text.RegularExpressions;

internal static class PlayerScriptInjector
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);
    private static readonly Regex Manager = new(@"(?<manager>[\w$]+(?:\.[\w$]+)*)\.getSupportedCommands\((?<player>[\w$]+)\)", RegexOptions.CultureInvariant, Timeout);
    private static readonly Regex Quality = new(
        pattern: "(?<items>[\\w$]+)\\.push\\(\\{name:[^{};]*\\.translate\\(\"Quality\"\\),id:\"quality\",asideText:[^{};]+\\}\\)",
        options: RegexOptions.CultureInvariant,
        matchTimeout: Timeout
    );
    private static readonly Regex Selection = new("switch\\((?<id>[\\w$]+)\\)\\{case\"quality\":", RegexOptions.CultureInvariant, Timeout);

    public static string InjectIndex(string html, string basePath)
    {
        var tag = $"<script defer src=\"{basePath}/HdrPlayback/player.js\"></script>";
        return html.Contains("/HdrPlayback/player.js", StringComparison.Ordinal) ? html : html.Replace("<head>", "<head>" + tag, StringComparison.OrdinalIgnoreCase);
    }

    public static string InjectMenu(string script)
    {
        if (!script.Contains("id:\"quality\"", StringComparison.Ordinal) || script.Contains("JellyfinHdrPlayback.menu", StringComparison.Ordinal))
        {
            return script;
        }

        var manager = Manager.Match(script);
        var quality = Quality.Match(script);
        var selection = Selection.Match(script);
        if (!manager.Success || !quality.Success || !selection.Success)
        {
            return script;
        }

        var instance = manager.Groups["manager"].Value;
        var player = manager.Groups["player"].Value;
        var items = quality.Groups["items"].Value;
        var changed = Quality.Replace(
            input: script,
            evaluator: match => match.Value + $",window.JellyfinHdrPlayback&&{items}.push(window.JellyfinHdrPlayback.menu({instance},{player}))",
            count: 1
        );
        return Selection.Replace(
            input: changed,
            evaluator: match => $"switch({match.Groups["id"].Value}){{case\"hdr-playback\":return window.JellyfinHdrPlayback.toggle({instance},arguments[2]);case\"quality\":",
            count: 1
        );
    }
}
