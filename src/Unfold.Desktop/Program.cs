using Avalonia;

namespace Unfold.Desktop;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--smoke-test") && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR")))
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", Path.Combine(Path.GetTempPath(), "Unfold-smoke-" + Guid.NewGuid().ToString("N")));
        var root = AppPaths.DataRoot;
        Directory.CreateDirectory(root);
        // File locks are released by the OS on crash. Do not allow two tray apps
        // to count time, display pets, or write settings concurrently.
        FileStream instance;
        try { instance = new FileStream(Path.Combine(root, ".instance.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { SingleInstance.RequestActivation(); return 0; }
        using (instance)
        {
            try { return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args); }
            catch (Exception error) { AppPaths.Log(error); return 1; }
        }
    }
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}

public static class AppPaths
{
    public static string DataRoot => Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR") is { Length: > 0 } custom
        ? Path.GetFullPath(custom)
        : OperatingSystem.IsMacOS()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Unfold")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Unfold");
    public static string BuiltInRoot => Path.Combine(AppContext.BaseDirectory, "Assets", "Characters");
    public static void Log(Exception error)
    {
        try { Directory.CreateDirectory(DataRoot); File.AppendAllText(Path.Combine(DataRoot, "unfold.log"), $"{DateTimeOffset.Now:O} {error}\n"); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
