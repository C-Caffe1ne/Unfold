using Avalonia.Controls;
using Unfold.Core;

namespace Unfold.Desktop;

public sealed partial class AppRuntime
{
    private readonly List<(NativeMenuItem Item, string Korean)> trayLabels = [];

    /// <returns>Whether the settings file needs to record the resolved language.</returns>
    private bool ApplyStartupLanguage(AppLanguage? firstRunLanguage)
    {
        var language = Settings.Language ?? firstRunLanguage ?? AppLanguage.Korean;
        L.Use(language); DesignSystem.ApplyLanguage(language);
        // A Korean profile without a stored language resolves the same way next time, so it needs no write.
        if (Settings.Language is null && language == AppLanguage.Korean) return false;
        var localized = Settings.WithLanguage(language);
        if (localized == Settings) return false;
        Settings = localized;
        return true;
    }

    /// <summary>Saves the language and redraws every open surface in it. Edited pet dialogue is kept.
    /// Presentation only, so it is allowed before sign-in and purchase.</summary>
    internal void SetLanguage(AppLanguage language)
    {
        if (disposed || quitting) return;
        if (language == L.Current) return;
        var value = Settings.WithLanguage(language);
        value.Save(settingsFile);
        Settings = value;
        L.Use(language); DesignSystem.ApplyLanguage(language);
        RefreshTrayLabels(); RefreshUpdateMenuText();
        accountWindow?.Model.UseContent(AccountContent);
        ReplaceSettingsWindow();
        RefreshPetNotice(); Changed?.Invoke();
    }

    private void RefreshTrayLabels()
    {
        foreach (var (item, korean) in trayLabels) item.Header = L.T(korean);
    }

    // Settings text is built with the window, so a new window replaces it at the same place and page.
    private void ReplaceSettingsWindow()
    {
        if (settingsWindow is not { } current) return;
        var replacement = new SettingsWindow(this);
        replacement.Activated += (_, _) => TryShowStartupUpdate();
        if (DiagnosticMode) PrepareDiagnosticWindow(replacement);
        settingsWindow = replacement; desktop.MainWindow = replacement;
        current.HandOff(replacement);
    }
}
