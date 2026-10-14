using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

public class LocalizationTests
{
    [AvaloniaFact]
    public void ReviewUsesKoreanWeekdaysOnAnEnglishSystemAndPreservesOldExportNames()
    {
        var culture = CultureInfo.CurrentCulture; var uiCulture = CultureInfo.CurrentUICulture;
        BreakReviewWindow? window = null;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            // Simulate a completed routine from the English UI, before this change.
            var session = new BreakSession(new("look-away", "Look away", [new("Rest your eyes", 20)]), "default-cat");
            session.Start(TimeSpan.Zero); session.Tick(TimeSpan.FromSeconds(10)); session.Tick(TimeSpan.FromSeconds(20)); session.Complete();
            var history = new BreakHistory(); history.Add(session, new DateTimeOffset(2026, 9, 14, 10, 0, 0, TimeSpan.FromHours(9)));
            using var temp = new TempDirectory(); var path = Path.Combine(temp.Path, "break-history.json"); history.Save(path);
            var loaded = BreakHistory.Load(path);
            window = new BreakReviewWindow(loaded.Review, currentDay: new DateOnly(2026, 9, 14)) { Width = 560, Height = 600 };
            window.Show(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var text = window.GetVisualDescendants().OfType<TextBlock>().ToArray();
            var monday = Assert.Single(text, block => block.Text == "9월 14일 (월)");
            var row = Assert.IsType<Grid>(monday.Parent);
            var count = row.Children.OfType<TextBlock>().Single(block => Grid.GetColumn(block) == 1 && Grid.GetRow(block) == Grid.GetRow(monday));
            Assert.True(monday.Bounds.Right <= count.Bounds.X);
            Assert.Equal("1회", count.Text);
            Assert.Contains(text, block => block.Name == "ReviewCount" && block.Text == "1");
            Assert.Contains(text, block => block.Name == "ReviewDuration" && block.Text == "0분 20초");
            Assert.Contains(text, block => block.Name == "ReviewActiveDays" && block.Text == "1");
            var csv = Encoding.UTF8.GetString(window.Review.Csv()).TrimStart('\uFEFF');
            Assert.StartsWith("confirmed_at,routine_id,routine_name,planned_seconds,character_id,profile_id,profile_name", csv);
            Assert.Contains(",\"look-away\",\"Look away\",20,\"default-cat\",", csv);
            Assert.Equal("눈 쉬어 주기", BreakRoutines.Find("look-away")!.Name);
        }
        finally { window?.Close(); CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = uiCulture; }
    }


    [Theory]
    [InlineData("End of Central Directory record could not be found.", "파일 형식")]
    [InlineData("A newer version is installed. Choose the same or a newer pack.", "더 최신 버전")]
    [InlineData("The installed companion changed. Reopen the pack preview.", "다시 열어")]
    public void PackErrorsExplainRecoveryInKoreanWithoutLeakingRawDecoderMessages(string raw, string recovery)
    {
        var message = Ui.ErrorText(new InvalidDataException(raw));
        Assert.Contains(recovery, message); Assert.DoesNotContain(raw, message);
    }

    private static readonly AppLanguage[] Translated = [AppLanguage.English, AppLanguage.Japanese, AppLanguage.ChineseSimplified];
    private static readonly Regex Hangul = new("[가-힣]");
    private static readonly Regex TableCall = new(@"L\.(?:T|F|Key)\(\s*""((?:[^""\\]|\\.)*)""|L\.Tx\(\s*""([^""]*)"",\s*""((?:[^""\\]|\\.)*)""");

    [Fact]
    public void EveryKoreanSourceTextHasEnglishJapaneseAndChineseText()
    {
        var root = RepositoryRoot();
        var keys = SourceFiles(root).SelectMany(file => TableCall.Matches(File.ReadAllText(file)))
            .Select(match => match.Groups[1].Success ? Regex.Unescape(match.Groups[1].Value) : match.Groups[2].Value + "|" + Regex.Unescape(match.Groups[3].Value))
            .Where(key => Hangul.IsMatch(key)).ToHashSet();
        // Text that reaches the screen from data instead of a call site.
        using (var account = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "Assets", "Account", "entry-screen.json"))))
        {
            keys.UnionWith(account.RootElement.GetProperty("copy").EnumerateObject().Select(item => item.Value.GetString()!));
            foreach (var market in account.RootElement.GetProperty("markets").EnumerateArray())
                keys.UnionWith([market.GetProperty("label").GetString()!, market.GetProperty("suffix").GetString()!]);
        }
        foreach (var manifest in Directory.EnumerateFiles(Path.Combine(root, "Assets", "Characters"), "character.json", SearchOption.AllDirectories))
            using (var pet = JsonDocument.Parse(File.ReadAllText(manifest))) keys.Add(pet.RootElement.GetProperty("name").GetString()!);
        keys.UnionWith([AppSettings.DefaultAdvanceDialogue, AppSettings.DefaultInvitationDialogue, AppSettings.DefaultRestingDialogue,
            AppSettings.DefaultOvertimeDialogue, AppSettings.DefaultCompletedDialogue]);
        keys.RemoveWhere(key => !Hangul.IsMatch(key));
        Assert.True(keys.Count > 400);
        var placeholder = new Regex(@"\{(\d+)");
        var missing = new List<string>();
        foreach (var key in keys)
            foreach (var language in Translated)
            {
                var text = L.T(key, language);
                var expected = placeholder.Matches(key).Select(m => m.Groups[1].Value).Order();
                // Only the English count unit is intentionally empty: the number stands alone.
                if (Hangul.IsMatch(text) || (text.Length == 0 && !(key == "회" && language == AppLanguage.English)) ||
                    !expected.SequenceEqual(placeholder.Matches(text).Select(m => m.Groups[1].Value).Order()))
                    missing.Add($"{language}: {key} → {text}");
            }
        Assert.Empty(missing);
    }

    [Fact]
    public void KoreanUiTextGoesThroughTheLanguageTable()
    {
        // Diagnostics compare against the Korean copy; routine data, constants and the font path are not UI text.
        string[] allowed = ["const string", "avares://", "Regex.", "new BreakRoutine(", "new BreakStep("];
        var literal = new Regex(@"[$@]*""(?:[^""\\\n]|\\.)*""");
        var bypassed = new List<string>();
        foreach (var file in SourceFiles(RepositoryRoot()).Where(file => !Path.GetFileName(file).StartsWith("SmokeDiagnostics") &&
                     !Path.GetFileName(file).StartsWith("Localization")))
        {
            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index].Trim();
                if (line.StartsWith("//") || allowed.Any(line.Contains)) continue;
                var rest = TableCall.Replace(line, "");
                if (literal.Matches(rest).Any(match => Hangul.IsMatch(match.Value)))
                    bypassed.Add($"{Path.GetFileName(file)}:{index + 1}: {line}");
            }
        }
        Assert.Empty(bypassed);
    }

    [Theory]
    [InlineData(new[] { "ko-KR", "en-US" }, AppLanguage.Korean)]
    [InlineData(new[] { "fr-FR", "ja-JP" }, AppLanguage.Japanese)]
    [InlineData(new[] { "zh-Hans-CN" }, AppLanguage.ChineseSimplified)]
    [InlineData(new[] { "en_GB" }, AppLanguage.English)]
    [InlineData(new[] { "de-DE" }, AppLanguage.English)]
    public void FirstRunUsesTheFirstSupportedSystemLanguage(string[] preferred, AppLanguage expected) =>
        Assert.Equal(expected, L.Preferred(preferred));

    [Fact]
    public void ChangingLanguageMovesOnlyUntouchedDefaultDialogue()
    {
        var edited = new AppSettings { InvitationDialogue = "물 한 잔 마셔요" };
        var english = edited.WithLanguage(AppLanguage.English);
        Assert.Equal("Stretch time in 5 minutes", english.AdvanceDialogue);
        Assert.Equal("물 한 잔 마셔요", english.InvitationDialogue);
        var japanese = english.WithLanguage(AppLanguage.Japanese);
        Assert.Equal(AppLanguage.Japanese, japanese.Language);
        Assert.Equal("ストレッチ完了！", japanese.CompletedDialogue);
        Assert.Equal(AppSettings.DefaultCompletedDialogue, japanese.WithLanguage(AppLanguage.Korean).CompletedDialogue);
    }

    [AvaloniaFact]
    public void NewProfilesFollowTheSystemLanguageAndOlderProfilesStayKorean()
    {
        var previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        try
        {
            using (var fresh = new TempDirectory())
            {
                Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", fresh.Path);
                using var lifetime = new ClassicDesktopStyleApplicationLifetime();
                using var runtime = new AppRuntime(lifetime, AppLanguage.Japanese);
                Assert.Equal(AppLanguage.Japanese, L.Current);
                var saved = AppSettings.Load(Path.Combine(fresh.Path, "settings.json"));
                Assert.Equal(AppLanguage.Japanese, saved.Language); Assert.Equal("ストレッチの時間です", saved.InvitationDialogue);
            }
            using (var existing = new TempDirectory())
            {
                Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", existing.Path);
                var file = Path.Combine(existing.Path, "settings.json");
                new AppSettings { IntervalMinutes = 45 }.Save(file);
                using var lifetime = new ClassicDesktopStyleApplicationLifetime();
                using var runtime = new AppRuntime(lifetime, AppLanguage.English);
                Assert.Equal(AppLanguage.Korean, L.Current); Assert.Null(AppSettings.Load(file).Language);
            }
        }
        finally { Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); UseKorean(); }
    }

    [AvaloniaFact]
    public async Task ChoosingALanguageRedrawsSettingsOnTheSamePage()
    {
        using var temp = new TempDirectory();
        var previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        try
        {
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
            new AppSettings { ShowPet = false, InvitationDialogue = "물 한 잔 마셔요" }.Save(Path.Combine(temp.Path, "settings.json"));
            using var lifetime = new ClassicDesktopStyleApplicationLifetime();
            using var runtime = new AppRuntime(lifetime)
                { AccountServiceFactory = () => new FakeAccountService
                    { RestoreSession = _ => Task.FromResult<AccountSession?>(FakeAccountService.Session), Purchase = PurchaseAccess.Active } };
            await runtime.Start(false);
            await Until(() => runtime.AccessAllowed && lifetime.MainWindow is { IsVisible: true });
            var korean = (SettingsWindow)lifetime.MainWindow!;
            Press(korean, "SettingsNavSettings");
            Find<ComboBox>(korean, "SettingsLanguage").SelectedIndex = 1;
            await Until(() => !ReferenceEquals(lifetime.MainWindow, korean));
            var english = (SettingsWindow)lifetime.MainWindow!;
            Layout();
            Assert.False(korean.IsVisible); Assert.True(english.IsVisible); Assert.Equal(AppLanguage.English, L.Current);
            Assert.NotNull(Find<ComboBox>(english, "SettingsLanguage"));
            Assert.Equal("Settings", Find<TextBlock>(english, "SettingsNavSettingsLabel").Text);
            Assert.Equal("English", Find<ComboBox>(english, "SettingsLanguage").SelectedItem);
            var saved = AppSettings.Load(Path.Combine(temp.Path, "settings.json"));
            Assert.Equal(AppLanguage.English, saved.Language);
            Assert.Equal("Stretch time in 5 minutes", saved.AdvanceDialogue); Assert.Equal("물 한 잔 마셔요", saved.InvitationDialogue);

            Press(english, "SettingsNavTimer");
            Find<ComboBox>(english, "HomeLanguage").SelectedIndex = 3;
            await Until(() => !ReferenceEquals(lifetime.MainWindow, english));
            var chinese = (SettingsWindow)lifetime.MainWindow!;
            Layout();
            Assert.Equal(AppLanguage.ChineseSimplified, L.Current);
            Assert.NotNull(Find<ComboBox>(chinese, "HomeLanguage"));
            Assert.Equal("主页", Find<TextBlock>(chinese, "SettingsNavTimerLabel").Text);
            Assert.Equal(DesignSystem.TextFont(AppLanguage.ChineseSimplified).Name, Find<TextBlock>(chinese, "SettingsNavTimerLabel").FontFamily.Name);
            Assert.Equal("企鹅", runtime.Characters.Single(pet => pet.Manifest.Id == "penguin").DisplayName);
        }
        finally { Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); UseKorean(); }
    }

    [AvaloniaFact]
    public async Task TheFirstScreenChangesLanguageBeforeSignIn()
    {
        using var temp = new TempDirectory();
        var previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        try
        {
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
            using var lifetime = new ClassicDesktopStyleApplicationLifetime();
            using var runtime = new AppRuntime(lifetime) { AccountServiceFactory = () => new FakeAccountService { CanSignIn = false } };
            await runtime.Start(false);
            await Until(() => runtime.ActiveAccount is { IsVisible: true });
            var account = runtime.ActiveAccount!; var hiddenSettings = lifetime.MainWindow;
            Assert.False(runtime.AccessAllowed);
            Assert.Equal("로그인 연결을 확인해 주세요.", account.FindControl<TextBlock>("AccountStatus")!.Text);
            var picker = account.FindControl<ComboBox>("AccountLanguage")!;
            Assert.True(picker.IsVisible); Assert.Equal("한국어", picker.SelectedItem);
            picker.SelectedIndex = 2;
            await Until(() => account.FindControl<TextBlock>("AccountHeading")!.Text == "Unfoldへ\nようこそ。");
            Assert.Equal(AppLanguage.Japanese, L.Current); Assert.False(runtime.AccessAllowed);
            Assert.Equal("ログインの接続を確認してください。", account.FindControl<TextBlock>("AccountStatus")!.Text);
            Assert.Equal("日本語", picker.SelectedItem);
            Assert.Equal(AppLanguage.Japanese, AppSettings.Load(Path.Combine(temp.Path, "settings.json")).Language);
            Assert.NotSame(hiddenSettings, lifetime.MainWindow); Assert.False(lifetime.MainWindow!.IsVisible);
            Assert.Equal("Unfold・休憩のお知らせ・" + AppRelease.DisplayVersion, lifetime.MainWindow.Title);
        }
        finally { Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); UseKorean(); }
    }

    [AvaloniaFact]
    public async Task TranslatedDialogButtonsKeepTheirCancelAndDangerRoles()
    {
        L.Use(AppLanguage.Japanese);
        var owner = new Window { Width = 600, Height = 500 };
        owner.Show(); Layout();
        try
        {
            var choice = Ui.Confirm(owner, L.T("저장한 펫을 삭제할까요?"), "-", L.T("펫 삭제"), L.T("취소")); Layout();
            var dialog = Assert.Single(owner.OwnedWindows); Layout();
            var buttons = dialog.GetVisualDescendants().OfType<Button>().ToArray();
            var delete = Assert.Single(buttons, button => Equals(button.Content, "ペットを削除"));
            var cancel = Assert.Single(buttons, button => Equals(button.Content, "キャンセル"));
            Assert.Contains("danger", delete.Classes); Assert.True(cancel.IsCancel); Assert.Contains("quiet", cancel.Classes);
            Assert.Contains(dialog.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "UNFOLD / 確認");
            cancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert.Equal(1, await choice);
        }
        finally { foreach (var owned in owner.OwnedWindows.ToArray()) owned.Close(); owner.Close(); UseKorean(); }
    }

    [AvaloniaFact]
    public void ReviewDatesAndDurationsFollowTheAppLanguage()
    {
        BreakReviewWindow? window = null;
        try
        {
            L.Use(AppLanguage.English);
            var session = new BreakSession(BreakRoutines.Find("look-away")!, "default-cat");
            session.Start(TimeSpan.Zero); session.Tick(TimeSpan.FromSeconds(10)); session.Tick(TimeSpan.FromSeconds(20)); session.Complete();
            var history = new BreakHistory(); history.Add(session, new DateTimeOffset(2026, 9, 14, 10, 0, 0, TimeSpan.FromHours(9)));
            window = new BreakReviewWindow(history.Review, currentDay: new DateOnly(2026, 9, 14)) { Width = 560, Height = 600 };
            window.Show(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var text = window.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text).ToArray();
            Assert.Contains("Mon, Sep 14", text); Assert.Contains("0 min 20 sec", text);
            Assert.Equal("9:05 PM", PetTimerWindow.FormatLocalTime(new DateTimeOffset(2026, 9, 14, 21, 5, 0, TimeSpan.Zero)));
        }
        finally { window?.Close(); UseKorean(); }
    }

    private static void UseKorean() { L.Use(AppLanguage.Korean); DesignSystem.ApplyLanguage(AppLanguage.Korean); }
    private static T Find<T>(Control root, string name) where T : Control => root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
    private static void Layout() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
    private static void Press(Window window, string name) { Find<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Layout(); }
    private static async Task Until(Func<bool> ready)
    {
        for (var i = 0; i < 300; i++) { Layout(); if (ready()) return; await Task.Delay(10, TestContext.Current.CancellationToken); }
        Assert.True(ready());
    }
    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Unfold.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException("Unfold.slnx");
    }
    private static IEnumerable<string> SourceFiles(string root) =>
        Directory.EnumerateFiles(Path.Combine(root, "src"), "*.*", SearchOption.AllDirectories)
            .Where(file => (file.EndsWith(".cs") || file.EndsWith(".axaml")) &&
                !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
}
