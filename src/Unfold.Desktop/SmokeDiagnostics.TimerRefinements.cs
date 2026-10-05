using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Unfold.Core;

namespace Unfold.Desktop;

internal static partial class SmokeDiagnostics
{
    private static async Task<object> VerifyTimerRefinements(AppRuntime runtime, Window settings, string directory)
    {
        var original = runtime.Settings;
        var width = settings.Width; var height = settings.Height;
        try
        {
            runtime.ShowSettings(); runtime.Stop(); Press(settings, "SettingsNavTimer");
            await Task.Delay(100);
            var interval = settings.GetVisualDescendants().OfType<NumericUpDown>().Single(c => c.Name == "ReminderInterval");
            interval.Value = 1; interval.Text = "1"; Press(settings, "ApplyHomeTimingSettings");
            await Until(() => runtime.Settings.IntervalMinutes == 1);
            if (AppSettings.Load(Path.Combine(AppPaths.DataRoot, "settings.json")).IntervalMinutes != 1 ||
                runtime.Clock.Remaining != TimeSpan.FromMinutes(1))
                throw new InvalidOperationException("One-minute interval did not persist or update the clock.");

            foreach (var size in new[] { new Size(1120, 800), new Size(640, 560) })
            {
                settings.Width = size.Width; settings.Height = size.Height; await Task.Delay(120); settings.UpdateLayout();
                var version = settings.GetVisualDescendants().OfType<TextBlock>().Single(c => c.Name == "SidebarVersion");
                var position = version.TranslatePoint(default, settings)!.Value;
                if (version.Text?.Replace("\n", " ") != AppRelease.DisplayVersion || position.X < 0 || position.Y < 0 ||
                    position.Y + version.Bounds.Height > settings.ClientSize.Height || version.Bounds.Width <= 0)
                    throw new InvalidOperationException("Sidebar version is missing or clipped.");
                foreach (var name in new[] { "SettingsQuit", "TimerStop" })
                {
                    var button = settings.GetVisualDescendants().OfType<Button>().Single(c => c.Name == name);
                    if (!button.Classes.Contains("danger") || button.Foreground is not ISolidColorBrush color || color.Color != DesignSystem.Error.Color)
                        throw new InvalidOperationException("Destructive action lost its danger color.");
                }
                Capture(settings, Path.Combine(directory, $"timer-refinement-home-{size.Width}.png"));
                Press(settings, "SettingsNavSettings"); await Task.Delay(100);
                var update = settings.GetVisualDescendants().OfType<Button>().Single(c => c.Name == "SettingsCheckUpdates");
                update.BringIntoView(); await Task.Delay(100); settings.UpdateLayout();
                var appVersion = settings.GetVisualDescendants().OfType<TextBlock>().Single(c => c.Name == "SettingsAppVersion");
                if (appVersion.Text != AppRelease.DisplayVersion || !update.IsEffectivelyVisible)
                    throw new InvalidOperationException("Settings app version/update action is missing.");
                Capture(settings, Path.Combine(directory, $"timer-refinement-settings-{size.Width}.png"));
                Press(settings, "SettingsCheckUpdates"); await Task.Delay(100);
                var updater = runtime.ActiveUpdate ?? throw new InvalidOperationException("Settings did not open updates.");
                Press(settings, "SettingsCheckUpdates");
                if (runtime.ActiveUpdate != updater) throw new InvalidOperationException("Settings opened duplicate update windows.");
                Capture(updater, Path.Combine(directory, "timer-refinement-updater.png")); updater.Close();
                Press(settings, "SettingsNavTimer");
            }

            var textMotion = await VerifyBubbleTimeMotion(directory);
            await runtime.UpdateSettings(runtime.Settings with { ShowPet = false, SnoozeMinutes = 3, ReminderSoundsEnabled = false });
            runtime.Stop(); runtime.TogglePause(); runtime.TogglePause();
            var completions = runtime.BreakHistory.Completions.Count;
            var snoozes = runtime.Reminder.ConsecutiveSnoozes;
            await runtime.ShowReminder();
            var waiting = Stopwatch.StartNew();
            var deadline = runtime.Reminder.NoticeExpiresAt;
            if (deadline is null || runtime.ActivePet?.IsVisible != true)
                throw new InvalidOperationException("Visible invitation did not arm its timeout.");
            Capture(runtime.ActivePet, Path.Combine(directory, "timer-refinement-auto-snooze-before.png"));
            await Task.Delay(TimeSpan.FromSeconds(29));
            if (runtime.Reminder.Notice != PetNotice.Invitation || runtime.Reminder.NoticeExpiresAt != deadline)
                throw new InvalidOperationException("Invitation expired early or its deadline drifted on refresh.");
            await Until(() => runtime.Reminder.Session is null);
            var autoSnoozeSeconds = waiting.Elapsed.TotalSeconds;
            if (autoSnoozeSeconds < 29.8 || autoSnoozeSeconds > 32 || runtime.ActivePet.IsVisible ||
                !runtime.Clock.Paused || runtime.Clock.Remaining != TimeSpan.FromMinutes(3) ||
                runtime.BreakHistory.Completions.Count != completions || runtime.Reminder.ConsecutiveSnoozes != snoozes + 1)
                throw new InvalidOperationException("Automatic snooze changed pause, duration, visibility or completion history.");
            Capture(settings, Path.Combine(directory, "timer-refinement-auto-snooze-after.png"));
            return new { oneMinutePersisted = true, version = AppRelease.DisplayVersion, sidebarSizes = new[] { "1120x800", "640x560" },
                settingsUpdaterOpened = true, dangerColors = true, textMotion, autoSnoozeSeconds,
                configuredSnoozeMinutes = 3, pausedStatePreserved = true, temporaryPetHidden = true, historyUnchanged = true };
        }
        finally
        {
            runtime.Stop(); await runtime.UpdateSettings(original);
            settings.Width = width; settings.Height = height;
        }
    }

    private static async Task<object> VerifyBubbleTimeMotion(string directory)
    {
        var bubble = new PetSpeechBubble(() => { }, () => { }, () => { })
            { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var clock = new StretchClock(TimeSpan.FromMinutes(1)); clock.Start(TimeSpan.Zero);
        var now = new DateTime(2026, 10, 5, 13, 7, 0); bubble.RefreshHover(now, clock);
        var window = new Window { Width = 380, Height = 260, Content = bubble, Background = DesignSystem.Surface };
        AppRuntime.PrepareDiagnosticWindow(window); window.Show();
        try
        {
            await Task.Delay(100); window.UpdateLayout();
            var wallClock = bubble.GetVisualDescendants().OfType<AnimatedTimeText>().Single(c => c.Name == "PetHoverTime");
            var countdown = bubble.GetVisualDescendants().OfType<AnimatedTimeText>().Single(c => c.Name == "PetHoverRemaining");
            var resting = bubble.GetVisualDescendants().OfType<AnimatedTimeText>().Single(c => c.Name == "PetBreakTimer");
            if (wallClock.Text != "오후 01:07") throw new InvalidOperationException("Hover clock has the wrong time format.");
            var bounds = bubble.Bounds; var position = window.Position; var clientSize = window.ClientSize;
            clock.Tick(TimeSpan.FromSeconds(1), TimeSpan.Zero, TimeSpan.FromMinutes(5));
            bubble.RefreshHover(now.AddMinutes(1), clock);
            if (!wallClock.HasMotion || !countdown.HasMotion || wallClock.Opacity != .55)
                throw new InvalidOperationException("Bubble time text did not start its transition.");
            Capture(window, Path.Combine(directory, "timer-refinement-hover-start.png"));
            await Task.Delay(65);
            if (wallClock.Opacity <= .55 || wallClock.Opacity >= 1)
                throw new InvalidOperationException("Bubble time text did not advance its transition.");
            Capture(window, Path.Combine(directory, "timer-refinement-hover-mid.png"));
            await Task.Delay(180);
            if (wallClock.HasMotion || countdown.HasMotion || wallClock.Opacity != 1 || bubble.Bounds != bounds ||
                window.Position != position || window.ClientSize != clientSize)
                throw new InvalidOperationException("Text motion did not settle or moved the bubble/window.");
            Capture(window, Path.Combine(directory, "timer-refinement-hover-settled.png"));
            var reminder = new PetReminder(); reminder.Invite(new(BreakRoutines.All[0], "default-cat")); reminder.Start(TimeSpan.Zero);
            bubble.Refresh(reminder, 3); await Task.Delay(220);
            reminder.Tick(TimeSpan.FromSeconds(1)); bubble.Refresh(reminder, 3);
            if (!resting.HasMotion || resting.Text != "00:59") throw new InvalidOperationException("Rest countdown did not animate.");
            await Task.Delay(65); Capture(window, Path.Combine(directory, "timer-refinement-rest-mid.png"));
            await Task.Delay(180);
            if (resting.HasMotion || resting.Opacity != 1) throw new InvalidOperationException("Rest countdown did not settle.");
            Capture(window, Path.Combine(directory, "timer-refinement-rest-settled.png"));
            return new { hoverFormat = wallClock.Text, hoverAnimated = true, restAnimated = true, durationMs = 180, windowGeometryStable = true };
        }
        finally { window.Close(); }
    }
}
