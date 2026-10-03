using System.Reflection;

namespace Unfold.Desktop;

internal static class AppRelease
{
    public static string Version { get; } = typeof(AppRelease).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
    public static string DisplayVersion { get; } = Version.EndsWith("-beta", StringComparison.Ordinal)
        ? $"Beta v{Version.Split('-')[0]}" : $"v{Version}";
}
