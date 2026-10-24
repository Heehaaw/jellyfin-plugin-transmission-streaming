namespace TransmissionStreaming.Packaging;

internal sealed record PackageOptions(Uri? SourceUrl, Uri? PreviousManifestUrl, string? ReleaseTag)
{
    public static PackageOptions Parse(string[] arguments)
    {
        Uri? source = null;
        Uri? previousManifest = null;
        string? releaseTag = null;
        for (var index = 0; index < arguments.Length; index += 2)
        {
            if (index + 1 >= arguments.Length)
            {
                throw new ArgumentException("Each packaging option needs a value.");
            }
            switch (arguments[index])
            {
                case "--source-url":
                    source = HttpsUrl(arguments[index + 1]);
                    if (!source.AbsolutePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || source.Query.Length != 0 || source.Fragment.Length != 0)
                    {
                        throw new ArgumentException("The source URL must end in .zip, without a query or fragment.");
                    }
                    break;
                case "--previous-manifest-url":
                    previousManifest = HttpsUrl(arguments[index + 1]);
                    break;
                case "--release-tag":
                    releaseTag = arguments[index + 1];
                    break;
                default:
                    throw new ArgumentException("Usage: Package.cs [--source-url HTTPS_ZIP_URL] [--previous-manifest-url HTTPS_JSON_URL] [--release-tag VERSION_TAG]");
            }
        }
        if (previousManifest is not null && source is null)
        {
            throw new ArgumentException("A previous manifest can only be merged when --source-url is supplied.");
        }
        return new PackageOptions(source, previousManifest, releaseTag);
    }

    public void ValidateVersion(string version)
    {
        if (ReleaseTag is null)
        {
            return;
        }
        var tagVersion = ReleaseTag.StartsWith('v') ? ReleaseTag[1..] : ReleaseTag;
        if (!Version.TryParse(tagVersion, out var parsed) || Normalize(parsed) != Normalize(Version.Parse(version)))
        {
            throw new ArgumentException("The release tag must match the version in plugin metadata.");
        }
    }

    private static Version Normalize(Version version)
    {
        return new Version(version.Major, version.Minor, Math.Max(0, version.Build), Math.Max(0, version.Revision));
    }

    private static Uri HttpsUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new ArgumentException("Package URLs must be absolute HTTPS URLs without embedded credentials.");
        }
        return uri;
    }
}
