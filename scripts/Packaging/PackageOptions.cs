namespace TransmissionStreaming.Packaging;

internal sealed record PackageOptions(Uri? SourceUrl, Uri? PreviousManifestUrl, string? ReleaseTag, string? ArchivePath)
{
    private const string Usage = "Usage: Package.cs [--archive EXISTING_ZIP] [--source-url HTTPS_ZIP_URL] " +
                                 "[--previous-manifest-url HTTPS_JSON_URL] [--release-tag VERSION_TAG]";
    private const string VersionMismatch = "The release tag must match the version in plugin metadata.";

    public static PackageOptions Parse(string[] arguments)
    {
        Uri? source = null;
        Uri? previousManifest = null;
        string? releaseTag = null;
        string? archivePath = null;
        for (var index = 0; index < arguments.Length; index += 2)
        {
            if (index + 1 >= arguments.Length)
            {
                throw new ArgumentException("Each packaging option needs a value.");
            }

            switch (arguments[index])
            {
                case "--archive":
                    archivePath = Path.GetFullPath(arguments[index + 1]);
                    break;
                case "--source-url":
                    source = HttpsUrl(arguments[index + 1]);
                    var hasZipExtension = source.AbsolutePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
                    if (!hasZipExtension || source.Query.Length != 0 || source.Fragment.Length != 0)
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
                    throw new ArgumentException(Usage);
            }
        }

        if (previousManifest is not null && source is null)
        {
            throw new ArgumentException("A previous manifest can only be merged when --source-url is supplied.");
        }

        return new PackageOptions(source, previousManifest, releaseTag, archivePath);
    }

    private static Uri HttpsUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new ArgumentException("Package URLs must be absolute HTTPS URLs without embedded credentials.");
        }

        return uri;
    }

    public Version? ReleaseVersion
    {
        get
        {
            if (ReleaseTag is null)
            {
                return null;
            }

            var value = ReleaseTag.StartsWith('v') ? ReleaseTag[1..] : ReleaseTag;
            if (!Version.TryParse(value, out var version))
            {
                throw new ArgumentException("Release tags must contain a numeric version, optionally prefixed with v.");
            }

            return Normalize(version);
        }
    }

    public void ValidateVersion(string version)
    {
        var expected = ReleaseVersion;
        if (expected is not null && Normalize(Version.Parse(version)) != expected)
        {
            throw new ArgumentException(VersionMismatch);
        }
    }

    private static Version Normalize(Version version)
    {
        var build = Math.Max(0, version.Build);
        var revision = Math.Max(0, version.Revision);
        return new Version(version.Major, version.Minor, build, revision);
    }
}
