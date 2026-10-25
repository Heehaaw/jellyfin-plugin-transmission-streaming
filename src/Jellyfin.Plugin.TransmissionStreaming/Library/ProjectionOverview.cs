namespace Jellyfin.Plugin.TransmissionStreaming.Library;

using System.Net;

using global::TransmissionStreaming.Core.Projection;

/// <summary>Formats the Jellyfin-specific activity link without replacing metadata-provider descriptions.</summary>
internal sealed class ProjectionOverview : IProjectionOverview
{
    private const string SectionStart = "\n\n<!-- transmission-streaming:activity -->";
    private const string SectionEnd = "<!-- /transmission-streaming:activity -->";

    public string? Format(string? overview, string? publicUrl)
    {
        if (string.IsNullOrWhiteSpace(publicUrl) ||
            !Uri.TryCreate(publicUrl.TrimEnd('/') + "/", UriKind.Absolute, out var origin) ||
            origin.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(origin.UserInfo) ||
            !string.IsNullOrEmpty(origin.Query) || !string.IsNullOrEmpty(origin.Fragment))
        {
            return overview;
        }

        var description = overview ?? "";
        var start = description.IndexOf(SectionStart, StringComparison.Ordinal);
        if (start >= 0)
        {
            var end = description.IndexOf(SectionEnd, start + SectionStart.Length, StringComparison.Ordinal);
            if (end >= 0)
            {
                // Remove only our section, preserving any text another editor appended after it.
                description = description.Remove(start, end + SectionEnd.Length - start);
            }
        }

        var address = new Uri(origin, "web/#/configurationpage?name=" + PluginConstants.SettingsPage);
        return description + SectionStart + "<p><a href=\"" + WebUtility.HtmlEncode(address.AbsoluteUri)
               + "\">View download status</a></p>" + SectionEnd;
    }
}
