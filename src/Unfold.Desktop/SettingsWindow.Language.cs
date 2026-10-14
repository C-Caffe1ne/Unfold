using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Unfold.Core;
using static Unfold.Desktop.DesignSystem;

namespace Unfold.Desktop;

public sealed partial class SettingsWindow
{
    private bool handedOff;
    private string? pendingFocusName;
    private bool pendingFocusByKeyboard;

    private ComboBox LanguagePicker(string name)
    {
        var languages = L.Languages.ToList();
        var picker = new ComboBox { Name = name, ItemsSource = languages.Select(L.NativeName).ToArray(),
            SelectedIndex = languages.IndexOf(L.Current), MinWidth = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        picker.Classes.Add("home-choice");
        picker.ContainerPrepared += (_, e) => e.Container.Classes.Add("home-choice-item");
        AutomationProperties.SetName(picker, L.T("언어"));
        picker.SelectionChanged += async (_, _) =>
        {
            if (handedOff || picker.SelectedIndex < 0 || languages[picker.SelectedIndex] == L.Current) return;
            var language = languages[picker.SelectedIndex];
            var keyboard = picker.Classes.Contains(":focus-visible");
            // Let the drop-down finish closing before this window is replaced.
            await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);
            try
            {
                if (await CanReplaceForLanguage())
                {
                    pendingFocusByKeyboard = keyboard;
                    runtime.SetLanguage(language);
                    if (handedOff) return;
                }
            }
            catch (Exception error) { if (!handedOff) await Ui.Error(this, error); }
            if (!handedOff) picker.SelectedIndex = languages.IndexOf(L.Current);
        };
        return picker;
    }

    private Border BuildHomeLanguageCard()
    {
        var picker = LanguagePicker("HomeLanguage");
        picker.Width = HomeNumberWidth; picker.Height = HomeControlHeight; picker.HorizontalAlignment = HorizontalAlignment.Left;
        var heading = HomeHeading(L.T("언어"));
        Ui.KeyboardFocusLabel(picker, heading); AutomationProperties.SetLabeledBy(picker, heading);
        var body = Ui.Column(heading, picker);
        body.Spacing = 16; body.Margin = new(Inset);
        return Card("SettingsHomeLanguageCard", body, Surface, CardRadius);
    }

    private Control BuildSettingsLanguageRow()
    {
        var label = SettingsLabel(L.T("언어"));
        var picker = LanguagePicker("SettingsLanguage");
        picker.Width = SettingsChoiceWidth; picker.Height = SettingsControlHeight;
        Ui.KeyboardFocusLabel(picker, label); AutomationProperties.SetLabeledBy(picker, label);
        return NotificationSettingRow("SettingsLanguageRow", label, picker);
    }

    private async Task<bool> CanReplaceForLanguage()
    {
        if (petPage is null) return true;
        if (petPage.IsBusy)
        {
            await Ui.Confirm(this, L.T("파일 작업 중이에요"), L.T("파일을 확인하거나 저장하고 있어요. 작업이 끝난 뒤 언어를 바꿔 주세요."), L.T("확인"));
            return false;
        }
        return !petPage.HasUnsavedDraft || await Ui.Confirm(this, L.T("저장하지 않은 펫이 있어요"),
            L.T("언어를 바꾸면 저장하지 않은 변경사항이 사라져요."), L.T("언어 바꾸기"), L.T("취소")) == 0;
    }

    /// <summary>Puts <paramref name="next"/> where this window is, on the same page, and closes this one.</summary>
    internal void HandOff(SettingsWindow next)
    {
        next.WindowStartupLocation = WindowStartupLocation.Manual;
        next.Position = Position;
        if (ClientSize.Width > 0 && ClientSize.Height > 0) { next.Width = ClientSize.Width; next.Height = ClientSize.Height; }
        if (IsVisible)
        {
            next.pendingFocusName = selectedPage switch { "dashboard" => "HomeLanguage", "settings" => "SettingsLanguage", _ => null };
            next.pendingFocusByKeyboard = pendingFocusByKeyboard;
            next.Show();
            if (WindowState == WindowState.Maximized) next.WindowState = WindowState.Maximized;
            _ = selectedPage switch
            {
                "settings" => next.OpenPreferences(),
                "review" => next.OpenReview(),
                "pets" => next.OpenPetPacks(),
                _ => next.OpenDashboard()
            };
            next.Activate();
        }
        handedOff = true;
        Close();
    }

    private Control? TakePendingFocus(IEnumerable<Control> controls, ref bool keyboard)
    {
        if (pendingFocusName is not { } name) return null;
        pendingFocusName = null; keyboard |= pendingFocusByKeyboard;
        return controls.FirstOrDefault(control => control.Name == name);
    }
}
