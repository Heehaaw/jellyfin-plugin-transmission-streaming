namespace TransmissionStreaming.Core.Configuration;

/// <summary>Reports service directories and editable local translations without changing saved settings.</summary>
public sealed record ServicePathSuggestions(DiscoveredServicePaths Paths, IReadOnlyList<ServicePathMapping> Mappings);
