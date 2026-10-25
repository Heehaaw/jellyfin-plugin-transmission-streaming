namespace TransmissionStreaming.Core.Projection;

/// <summary>Formats a host-specific activity link while retaining the existing description.</summary>
public interface IProjectionOverview
{
    /// <summary>Adds or refreshes the owned activity link without replacing the synopsis or unrelated links.</summary>
    string? Format(string? overview, string? publicUrl);
}
