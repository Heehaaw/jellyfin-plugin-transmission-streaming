namespace TransmissionStreaming.Core.Configuration;

/// <summary>Safe local path suggestions; a missing media root means the host's folders were inconclusive.</summary>
/// <param name="ProjectionRoot">The existing projection folder or the plugin-owned default.</param>
/// <param name="LocalMediaRoot">The existing media root, a common library parent, or no suggestion.</param>
public sealed record ConfigurationDefaults(string ProjectionRoot, string? LocalMediaRoot);
