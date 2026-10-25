namespace TransmissionStreaming.TestSupport;

using System.Runtime.InteropServices;

/// <summary>Keeps bind-mounted test data writable and removable by Linux's invoking user.</summary>
internal static class ContainerIdentity
{
    // Colima maps host ownership itself; direct UID mapping is required on native Linux.
    public static string User => OperatingSystem.IsLinux() ? $"{GetEffectiveUserId()}:{GetEffectiveGroupId()}" : "0:0";

    [DllImport("libc", EntryPoint = "geteuid", ExactSpelling = true)]
    private static extern uint GetEffectiveUserId();

    [DllImport("libc", EntryPoint = "getegid", ExactSpelling = true)]
    private static extern uint GetEffectiveGroupId();
}
