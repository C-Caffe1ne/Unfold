using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class PetTimerHoverTests
{
    [Theory]
    [InlineData(0, 4, "오전 12:04")]
    [InlineData(1, 9, "오전 01:09")]
    [InlineData(11, 59, "오전 11:59")]
    [InlineData(12, 0, "오후 12:00")]
    [InlineData(13, 5, "오후 01:05")]
    [InlineData(23, 59, "오후 11:59")]
    public void LocalTimeKeepsKoreanMeridiemAndTwelveHourDigits(int hour, int minute, string expected)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            // The caption remains Korean even when macOS uses another locale.
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            Assert.Equal(expected, PetTimerWindow.FormatLocalTime(new(2026, 10, 13, hour, minute, 0, TimeSpan.FromHours(9))));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void PetHoverStaysVisibleUntilThePointerLeavesBothAreas()
    {
        var reveal = new PetTimerReveal();
        reveal.Update(false, false, At(0)); Assert.Equal(0, reveal.Opacity);
        reveal.Update(true, false, At(10)); Assert.Equal(0, reveal.Opacity);
        reveal.Update(true, false, At(100)); Assert.InRange(reveal.Opacity, .1, .9);
        reveal.Update(true, false, At(190)); Assert.Equal(1, reveal.Opacity);
        reveal.Update(true, false, At(2189)); Assert.Equal(1, reveal.Opacity);
        reveal.Update(true, false, At(8000)); Assert.Equal(1, reveal.Opacity);
        reveal.Update(false, false, At(8001)); Assert.Equal(1, reveal.Opacity);
        reveal.Update(false, false, At(8091)); Assert.InRange(reveal.Opacity, .1, .9);
        reveal.Update(false, false, At(8181)); Assert.Equal(0, reveal.Opacity);
    }

    [Fact]
    public void DirectTransferFromPetToTimerKeepsItVisibleAndExitStartsFadingImmediately()
    {
        var reveal = new PetTimerReveal();
        reveal.Update(true, false, At(0)); reveal.Update(true, false, At(180));
        reveal.Update(false, true, At(500)); reveal.Update(false, true, At(15000));
        Assert.Equal(1, reveal.Opacity);
        reveal.Update(false, false, At(15001)); Assert.Equal(1, reveal.Opacity);
        reveal.Update(false, false, At(15002)); Assert.True(reveal.Opacity < 1);
        reveal.Update(false, false, At(15091)); Assert.InRange(reveal.Opacity, .1, .9);
        reveal.Update(false, false, At(15181)); Assert.Equal(0, reveal.Opacity);
    }

    [Fact]
    public void EnteringTheTimerDuringFadeRestoresItWithoutAnOpacityJump()
    {
        var reveal = new PetTimerReveal();
        reveal.Update(true, false, At(0)); reveal.Update(true, false, At(180));
        reveal.Update(false, false, At(1000)); reveal.Update(false, false, At(1090)); var fading = reveal.Opacity;
        Assert.InRange(fading, .1, .9);
        reveal.Update(false, true, At(1090)); Assert.Equal(fading, reveal.Opacity);
        reveal.Update(false, true, At(1180)); Assert.InRange(reveal.Opacity, fading, 1);
        reveal.Update(false, true, At(1270)); Assert.Equal(1, reveal.Opacity);
        reveal.Update(false, true, At(20000)); Assert.Equal(1, reveal.Opacity);
    }

    [Fact]
    public void ExitingDuringFadeInKeepsOpacityContinuousAndCancelsTheReveal()
    {
        var reveal = new PetTimerReveal();
        reveal.Update(true, false, At(0)); reveal.Update(true, false, At(90));
        var halfway = reveal.Opacity; Assert.InRange(halfway, .1, .9);
        reveal.Update(false, false, At(90)); Assert.Equal(halfway, reveal.Opacity);
        reveal.Update(false, false, At(180)); Assert.InRange(reveal.Opacity, .01, halfway - .01);
        reveal.Update(false, false, At(270)); Assert.Equal(0, reveal.Opacity);
        reveal.Update(true, false, At(400)); reveal.Update(true, false, At(580)); Assert.Equal(1, reveal.Opacity);
    }

    [Fact]
    public void ResetCancelsAnActiveRevealAndAllowsACompletelyNewPetEntry()
    {
        var reveal = new PetTimerReveal();
        reveal.Update(true, true, At(0)); reveal.Update(true, true, At(1000));
        Assert.Equal(1, reveal.Opacity);
        reveal.Reset(); Assert.Equal(0, reveal.Opacity);
        reveal.Update(false, false, At(5000)); Assert.Equal(0, reveal.Opacity);
        reveal.Update(true, false, At(6000)); reveal.Update(true, false, At(6180));
        Assert.Equal(1, reveal.Opacity);
    }

    [Theory]
    [InlineData(true, false)] [InlineData(false, true)]
    public void GestureSuppressionBlocksReleaseInsideEitherAreaUntilExitAndReentry(bool petHovered, bool timerHovered)
    {
        var reveal = new PetTimerReveal();
        reveal.Update(petHovered, timerHovered, At(0)); reveal.Update(petHovered, timerHovered, At(180));
        Assert.Equal(1, reveal.Opacity);
        reveal.SuppressUntilPointerExit(); Assert.Equal(0, reveal.Opacity);
        reveal.Update(petHovered, timerHovered, At(1000)); reveal.Update(petHovered, timerHovered, At(8000));
        Assert.Equal(0, reveal.Opacity);
        reveal.Update(false, false, At(9000)); Assert.Equal(0, reveal.Opacity);
        reveal.Update(petHovered, timerHovered, At(10000)); reveal.Update(petHovered, timerHovered, At(10180));
        Assert.Equal(1, reveal.Opacity);
    }

    [Fact]
    public void PressedPointerOutsideCannotClearSuppressionBeforeAReleasedPointerExit()
    {
        var reveal = new PetTimerReveal();
        reveal.Update(true, false, At(0)); reveal.Update(true, false, At(180));
        reveal.Update(false, false, At(500), petInteracting: true); Assert.Equal(0, reveal.Opacity);
        reveal.Update(false, false, At(1500), petInteracting: true); Assert.Equal(0, reveal.Opacity);
        reveal.Update(true, false, At(2000)); reveal.Update(true, false, At(3000));
        Assert.Equal(0, reveal.Opacity);
        reveal.Update(false, false, At(4000));
        reveal.Update(true, false, At(5000)); reveal.Update(true, false, At(5180));
        Assert.Equal(1, reveal.Opacity);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void APressImmediatelyHidesTheTimerDuringEitherFade(bool fadingOut)
    {
        var reveal = new PetTimerReveal();
        reveal.Update(true, false, At(0));
        if (fadingOut)
        {
            reveal.Update(true, false, At(180)); reveal.Update(false, false, At(1000));
            reveal.Update(false, false, At(1090));
        }
        else reveal.Update(true, false, At(90));
        Assert.InRange(reveal.Opacity, .1, .9);
        reveal.SuppressUntilPointerExit(); Assert.Equal(0, reveal.Opacity);
    }

    [Theory]
    [InlineData(1)] [InlineData(1.5)] [InlineData(2)]
    public void TimerCentersBelowVisibleFeetWithAnEightLogicalPixelGap(double scale)
    {
        var body = new PixelRect(700, 300, (int)(120 * scale), (int)(160 * scale));
        var area = new PixelRect(0, 25, 3840, 2135);
        var point = PetTimerPlacement.Calculate(body, scale, area);
        var width = (int)Math.Ceiling(PetTimerWindow.TimerWidth * scale);
        var height = (int)Math.Ceiling(PetTimerWindow.TimerHeight * scale);
        Assert.Equal(body.Bottom + (int)Math.Ceiling(8 * scale), point.Y);
        Assert.InRange(Math.Abs(point.X + width / 2d - (body.X + body.Width / 2d)), 0, 1);
        Assert.True(area.Contains(new PixelRect(point, new PixelSize(width, height))));
        Assert.False(body.Intersects(new PixelRect(point, new PixelSize(width, height))));
    }

    [Theory]
    [InlineData(0, 1920, 1)] [InlineData(-1920, 1920, 1)] [InlineData(-3840, 3840, 2)]
    public void TimerAtDockAndMonitorEdgesStaysOnScreenWithoutCoveringThePet(int origin, int width, double scale)
    {
        var area = new PixelRect(origin, 25, width, (int)(1055 * scale));
        var bodyWidth = (int)(120 * scale); var bodyHeight = (int)(160 * scale);
        foreach (var x in new[] { area.X, area.X + area.Width / 2, area.Right - bodyWidth })
        foreach (var y in new[] { area.Y, area.Bottom - bodyHeight })
        foreach (var side in new[] { BubbleDirection.Left, BubbleDirection.Right })
        {
            var body = new PixelRect(x, y, bodyWidth, bodyHeight);
            var point = PetTimerPlacement.Calculate(body, scale, area, side);
            var timer = new PixelRect(point, new PixelSize((int)Math.Ceiling(PetTimerWindow.TimerWidth * scale),
                (int)Math.Ceiling(PetTimerWindow.TimerHeight * scale)));
            Assert.True(area.Contains(timer), $"Timer {timer} must fit working area {area} for body {body}.");
            Assert.False(body.Intersects(timer), $"Timer {timer} must not cover body {body}.");
        }
    }

    [Fact]
    public void ALongPetAtTheDockOnANarrowScreenMovesOnlyTheTimerAboveItsBody()
    {
        var area = new PixelRect(0, 0, 640, 1000);
        var body = new PixelRect(176, 700, 288, 260);
        var position = PetTimerPlacement.Calculate(body, 1, area);
        var timer = new PixelRect(position, new PixelSize((int)PetTimerWindow.TimerWidth, (int)PetTimerWindow.TimerHeight));
        Assert.True(area.Contains(timer)); Assert.False(body.Intersects(timer));
        Assert.Equal(body.Y, timer.Bottom + PetTimerWindow.PetGap);
    }

    [Fact]
    public void ATopEdgeReminderOnANarrowScreenShrinksBesideTheWiderTimer()
    {
        var area = new PixelRect(0, 0, 640, 1000);
        var body = new PixelRect(320, 25, 20, 20);
        var timer = new PixelRect(226, 53, 208, 78);
        var placement = PetOverlayPlacement.Calculate(body, 1, area, BubbleDirection.Left, timer);
        var bubble = new PixelRect(placement.Position, new PixelSize((int)Math.Ceiling(placement.Width),
            (int)Math.Ceiling(PetOverlayWindow.SurfaceHeight)));
        Assert.True(placement.Width > 0); Assert.True(area.Contains(bubble));
        Assert.False(bubble.Intersects(timer)); Assert.False(bubble.Intersects(body));
    }

    [AvaloniaFact]
    public void InteractiveEnvelopeIgnoresTransparentPaddingAndUsesAllClipFrames()
    {
        using var view = new AnimationView();
        view.Measure(new Size(200, 100)); view.Arrange(new Rect(0, 0, 200, 100)); view.SetRunning(false);
        var firstPixels = new uint[100]; var secondPixels = new uint[100];
        firstPixels[3 * 10 + 2] = 0xffffffff;
        firstPixels[0] = 0x19ffffff; // Alpha 25 is below the native input threshold.
        secondPixels[7 * 10 + 6] = 0x1affffff; // Alpha 26 is interactive.
        var first = new AnimationFrame(new PixelImage(10, 10, firstPixels), TimeSpan.FromSeconds(1));
        var second = new AnimationFrame(new PixelImage(10, 10, secondPixels), TimeSpan.FromSeconds(1));
        var expected = new Rect(70, 30, 50, 50);
        view.SetFrames([first, second], true);
        AssertRect(expected, view.InteractiveBounds);
        view.SetFrames([second, first], true);
        AssertRect(expected, view.InteractiveBounds);
    }

    [AvaloniaFact]
    public async Task AlignedFramesAndPoseTransformsKeepTheOverlayAtTheDrawnBodyWithoutMovingThePet()
    {
        using var scope = new Scope(); await scope.Load(); var pet = scope.Pet; var view = pet.PetView;
        var firstPixels = new uint[100]; var secondPixels = new uint[100];
        firstPixels[4 * 10 + 1] = secondPixels[6 * 10 + 7] = 0xffffffff;
        var first = new AnimationFrame(new PixelImage(10, 10, firstPixels), TimeSpan.FromSeconds(1));
        var second = new AnimationFrame(new PixelImage(10, 10, secondPixels), TimeSpan.FromSeconds(1));
        var anchor = pet.Position; var clientSize = pet.ClientSize; var size = view.Width;
        view.SetRunning(false); view.SetPose(PetPose.Neutral);
        view.SetFrames([first, second], true, alignCompanion: true);
        AssertRect(new(size * .45, size * .8, size * .1, size * .1), view.InteractiveBounds);
        view.SetFrames([second, first], true, alignCompanion: true);
        AssertRect(new(size * .45, size * .8, size * .1, size * .1), view.InteractiveBounds);
        var scale = pet.DesktopScaling;
        var expected = new PixelRect(anchor.X + (int)Math.Floor(size * .44 * scale),
            anchor.Y + (int)Math.Floor(size * .77 * scale),
            (int)Math.Ceiling(size * .12 * scale), (int)Math.Ceiling(size * .08 * scale));
        foreach (var mirrored in new[] { false, true })
        {
            view.SetPose(new PetPose(1.2, .8, .05), mirrored);
            Assert.Equal(expected, pet.InteractiveScreenBounds);
            Assert.Equal(anchor, pet.Position); Assert.Equal(clientSize, pet.ClientSize);
        }
    }

    [AvaloniaFact]
    public async Task TimeCaptionAndControlsUseTheirOwnWindowWithoutArrangingThePet()
    {
        using var scope = new Scope(); await scope.Load(); var pet = scope.Pet; var timer = pet.Overlay.Timer;
        Assert.NotSame(pet, timer); Assert.NotSame(pet.Overlay, timer);
        Assert.Empty(pet.GetVisualDescendants().OfType<AnimatedCountdown>());
        // No notice has opened the hidden overlay's visual tree yet.
        Assert.Equal("PetBreakTimer", Assert.Single(pet.Overlay.Bubble.GetLogicalDescendants().OfType<AnimatedCountdown>()).Name);
        Assert.DoesNotContain(pet.Overlay.GetLogicalDescendants().OfType<AnimatedCountdown>(), control => control.Name == "PetTimerCountdown");
        var caption = Find<TextBlock>(timer, "PetCurrentTime");
        var countdown = Find<AnimatedCountdown>(timer, "PetTimerCountdown");
        Assert.Matches("^(오전|오후) [0-9]{2}:[0-9]{2}$", caption.Text!);
        var captionPoint = caption.TranslatePoint(default, timer)!.Value;
        var countdownPoint = countdown.TranslatePoint(default, timer)!.Value;
        Assert.True(captionPoint.Y + caption.Bounds.Height <= countdownPoint.Y);
        var anchor = pet.Position; var size = pet.ClientSize; var bounds = pet.PetView.Bounds;
        var moves = 0; var sizes = 0; var arrangements = 0;
        pet.PositionChanged += (_, _) => moves++; pet.SizeChanged += (_, _) => sizes++;
        pet.PetView.PropertyChanged += (_, e) => { if (e.Property == Visual.BoundsProperty) arrangements++; };
        timer.UpdateHover(true, false, At(0)); timer.UpdateHover(true, false, At(180));
        timer.Reposition(); Layout(timer);
        Assert.Equal(1, timer.HoverOpacity); Assert.Equal(1, timer.Opacity);
        Assert.Equal(anchor, pet.Position); Assert.Equal(size, pet.ClientSize); Assert.Equal(bounds, pet.PetView.Bounds);
        Assert.Equal(0, moves); Assert.Equal(0, sizes); Assert.Equal(0, arrangements);
    }

    [AvaloniaFact]
    public async Task InvisibleButtonsPassInputAndRevealedButtonsCaptureOnlyTheirOwnSurfaces()
    {
        using var scope = new Scope(); await scope.Load(); var timer = scope.Pet.Overlay.Timer;
        var toggle = Find<Button>(timer, "PetTimerToggle");
        var point = toggle.TranslatePoint(new Point(10, 10), timer)!.Value;
        Assert.Equal(0, timer.HoverOpacity); Assert.False(timer.AcceptsPointerAt(point));
        timer.UpdateHover(true, false, At(0)); timer.UpdateHover(true, false, At(180));
        Assert.True(timer.AcceptsPointerAt(point)); Assert.False(timer.AcceptsPointerAt(new(-1, -1)));
        foreach (var caption in new Control[] { Find<TextBlock>(timer, "PetCurrentTime"), Find<AnimatedCountdown>(timer, "PetTimerCountdown") })
            Assert.False(timer.AcceptsPointerAt(caption.TranslatePoint(new Point(5, 5), timer)!.Value));
        timer.UpdateHover(false, false, At(3000)); timer.UpdateHover(false, false, At(3180));
        Assert.Equal(0, timer.HoverOpacity);
        Assert.False(timer.AcceptsPointerAt(point));
    }

    [AvaloniaFact]
    public async Task HiddenCountdownSuppressesTickAndResetMotionAndDoesNotReplayItWhenRevealed()
    {
        using var scope = new Scope(); await scope.Load(); var timer = scope.Pet.Overlay.Timer;
        var countdown = Find<AnimatedCountdown>(timer, "PetTimerCountdown"); var clock = scope.Runtime.Clock;
        clock.Start(TimeSpan.Zero); timer.Refresh();
        timer.UpdateHover(true, false, At(0)); timer.UpdateHover(true, false, At(180));
        clock.Tick(TimeSpan.FromSeconds(1), TimeSpan.Zero, TimeSpan.FromMinutes(5)); timer.Refresh();
        Assert.Equal("59:59", countdown.Text); Assert.True(countdown.HasDigitMotion);
        Assert.Equal(-1, countdown.MotionDirection);
        timer.UpdateHover(false, false, At(3000)); timer.UpdateHover(false, false, At(3180));
        Assert.Equal(0, timer.HoverOpacity); Assert.False(countdown.HasDigitMotion);
        Assert.False(countdown.AnimationsEnabled); Assert.Empty(countdown.AnimatedDigitIndices);
        clock.Tick(TimeSpan.FromSeconds(2), TimeSpan.Zero, TimeSpan.FromMinutes(5)); timer.Refresh();
        Assert.Equal("59:58", countdown.Text); Assert.False(countdown.HasDigitMotion);
        clock.Reset(TimeSpan.FromSeconds(2)); timer.Refresh();
        Assert.Equal("60:00", countdown.Text); Assert.Equal(1, countdown.MotionDirection);
        Assert.False(countdown.HasDigitMotion); Assert.Empty(countdown.AnimatedDigitIndices);
        clock.SetInterval(TimeSpan.FromMinutes(5)); timer.Refresh();
        Assert.Equal("05:00", countdown.Text); Assert.Equal(-1, countdown.MotionDirection);
        Assert.False(countdown.HasDigitMotion); Assert.Empty(countdown.AnimatedDigitIndices);
        clock.Start(TimeSpan.FromSeconds(3));
        clock.Tick(TimeSpan.FromSeconds(4), TimeSpan.Zero, TimeSpan.FromMinutes(5)); timer.Refresh();
        Assert.Equal("04:59", countdown.Text); Assert.False(countdown.HasDigitMotion);
        timer.UpdateHover(true, false, At(4000)); timer.UpdateHover(true, false, At(4180));
        Assert.True(countdown.AnimationsEnabled); Assert.False(countdown.HasDigitMotion);
        clock.Tick(TimeSpan.FromSeconds(5), TimeSpan.Zero, TimeSpan.FromMinutes(5)); timer.Refresh();
        Assert.Equal("04:58", countdown.Text); Assert.True(countdown.HasDigitMotion);
        Assert.Equal(-1, countdown.MotionDirection);
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task PetClickOrPickupImmediatelyHidesTheTimerAndRequiresExitAfterRelease(bool pickup)
    {
        using var scope = new Scope(); await scope.Load(bori: true); var pet = scope.Pet; var timer = pet.Overlay.Timer;
        Assert.True(pet.HasOriginalBehavior); Assert.True(pet.HasPointerArt);
        timer.UpdateHover(true, false, At(0)); timer.UpdateHover(true, false, At(180));
        Assert.Equal(1, timer.HoverOpacity);
        pet.BeginCompanionPress(); Assert.True(pet.HasPointerInteraction); Assert.Equal(0, timer.HoverOpacity);
        if (pickup)
        {
            pet.AdvanceCompanion(PetPose.LiftDelay);
            Assert.Equal(PetPointerPhase.Pickup, pet.PointerPhase);
        }
        timer.UpdateHover(true, false, At(1000)); Assert.Equal(0, timer.HoverOpacity);
        // A captured drag can leave the visible areas while its button remains down.
        timer.UpdateHover(false, false, At(2000)); timer.UpdateHover(false, false, At(2180));
        Assert.Equal(0, timer.HoverOpacity);
        pet.ReleaseCompanionPress(clicked: !pickup);
        await Until(() => !pet.HasPointerInteraction && pet.PointerPhase == PetPointerPhase.None);
        timer.UpdateHover(true, false, At(3000)); timer.UpdateHover(false, true, At(4000));
        timer.UpdateHover(false, true, At(8000)); Assert.Equal(0, timer.HoverOpacity);
        timer.UpdateHover(false, false, At(9000));
        timer.UpdateHover(true, false, At(10000)); timer.UpdateHover(true, false, At(10180));
        Assert.Equal(1, timer.HoverOpacity);
    }

    [AvaloniaFact]
    public async Task SuppressionKeepsThePressedBodyAreaUntilARealExitDespiteFrameAndWindowMovement()
    {
        using var scope = new Scope(); await scope.Load(); var pet = scope.Pet; var view = pet.PetView; var timer = pet.Overlay.Timer;
        var pressedPoint = view.TranslatePoint(new Point(view.Width / 2, view.Height / 2), pet)!.Value;
        Assert.True(pet.PointerOverBody(pressedPoint));
        timer.UpdateHover(true, false, At(0)); timer.UpdateHover(true, false, At(180));
        timer.SuppressForPetInteraction(); Assert.Equal(0, timer.HoverOpacity);
        Assert.True(timer.PointerOverPetForReveal(pressedPoint));
        // The pickup/release frame can leave the cursor over a newly transparent pixel.
        var shiftedPixels = new uint[16 * 16]; shiftedPixels[2 * 16 + 2] = 0xffffffff;
        view.SetRunning(false);
        view.SetFrames([new AnimationFrame(new PixelImage(16, 16, shiftedPixels), TimeSpan.FromSeconds(1))], true);
        var latestBodyPoint = view.TranslatePoint(new Point(view.Width * 2.5 / 16, view.Height * 2.5 / 16), pet)!.Value;
        Assert.False(pet.PointerOverBody(pressedPoint)); Assert.True(pet.PointerOverBody(latestBodyPoint));
        Assert.True(timer.PointerOverPetForReveal(pressedPoint)); Assert.False(timer.PointerOverPetForReveal(latestBodyPoint));
        pet.Position = new(pet.Position.X + 150, pet.Position.Y + 80);
        Assert.True(timer.PointerOverPetForReveal(pressedPoint));
        timer.UpdateHover(timer.PointerOverPetForReveal(pressedPoint), false, At(1000));
        Assert.Equal(0, timer.HoverOpacity);
        var outside = new Point(-10, -10);
        Assert.False(timer.PointerOverPetForReveal(outside));
        timer.UpdateHover(timer.PointerOverPetForReveal(outside), false, At(2000));
        Assert.False(timer.PointerOverPetForReveal(pressedPoint)); Assert.True(timer.PointerOverPetForReveal(latestBodyPoint));
        timer.UpdateHover(timer.PointerOverPetForReveal(latestBodyPoint), false, At(3000));
        timer.UpdateHover(timer.PointerOverPetForReveal(latestBodyPoint), false, At(3180));
        Assert.Equal(1, timer.HoverOpacity);
    }

    [AvaloniaFact]
    public async Task HidingThePetOrDisablingTheTimerClearsTheRevealAndKeepsReminderInputAvailable()
    {
        using var scope = new Scope(); await scope.Load(); var pet = scope.Pet; var timer = pet.Overlay.Timer;
        timer.UpdateHover(true, true, At(0)); timer.UpdateHover(true, true, At(180)); Assert.Equal(1, timer.HoverOpacity);
        await scope.Runtime.UpdateSettings(scope.Runtime.Settings with { ShowPetTimer = false });
        Assert.False(timer.IsVisible); Assert.Equal(0, timer.HoverOpacity);
        timer.UpdateHover(true, true, At(1000)); timer.UpdateHover(true, true, At(2000));
        Assert.Equal(0, timer.HoverOpacity);
        await scope.Runtime.ShowReminder(); Layout(pet.Overlay);
        var reminderButton = Find<Button>(pet.Overlay, "PetBreakStart");
        Assert.True(pet.Overlay.AcceptsPointerAt(reminderButton.TranslatePoint(new Point(10, 10), pet.Overlay)!.Value));
        Assert.False(timer.IsVisible);
        await scope.Runtime.UpdateSettings(scope.Runtime.Settings with { ShowPetTimer = true }); Layout(timer);
        Assert.Equal(0, timer.HoverOpacity);
        timer.UpdateHover(true, true, At(3000)); timer.UpdateHover(true, true, At(3180)); Assert.Equal(1, timer.HoverOpacity);
        await scope.Runtime.HidePet(); Assert.False(timer.IsVisible); Assert.Equal(0, timer.HoverOpacity);
        timer.UpdateHover(true, true, At(4000)); timer.UpdateHover(true, true, At(4180)); Assert.Equal(0, timer.HoverOpacity);
        await scope.Runtime.UpdateSettings(scope.Runtime.Settings with { ShowPet = true }); Layout(timer);
        Assert.True(pet.IsVisible); Assert.Equal(0, timer.HoverOpacity);
    }

    [AvaloniaFact]
    public async Task ReminderTailUsesTheVisibleBodyEdgeRatherThanTheTransparentCanvas()
    {
        using var scope = new Scope(); await scope.Load(); var pet = scope.Pet;
        await scope.Runtime.ShowReminder(); Layout(pet.Overlay);
        var body = pet.InteractiveScreenBounds;
        var scale = pet.DesktopScaling;
        Assert.True(body.X > pet.Position.X); Assert.True(body.Right < pet.Position.X + pet.PetView.Width * scale);
        var edge = pet.Overlay.Direction == BubbleDirection.Right ? pet.Overlay.Position.X
            : pet.Overlay.Position.X + pet.Overlay.Width * scale;
        var distance = pet.Overlay.Direction == BubbleDirection.Right ? edge - body.Right : body.X - edge;
        Assert.InRange(distance, 0, Math.Ceiling(DesignSystem.PetBubbleGap * scale));
    }

    private static T Find<T>(Window window, string name) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);
    private static void AssertRect(Rect expected, Rect actual)
    {
        Assert.Equal(expected.X, actual.X, 6); Assert.Equal(expected.Y, actual.Y, 6);
        Assert.Equal(expected.Width, actual.Width, 6); Assert.Equal(expected.Height, actual.Height, 6);
    }
    private static void Layout(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static async Task Until(Func<bool> ready)
    {
        for (var i = 0; i < 300 && !ready(); i++)
        { Dispatcher.UIThread.RunJobs(); await Task.Delay(10, TestContext.Current.CancellationToken); }
        Assert.True(ready(), "The pet gesture did not finish its assigned release animation.");
    }
    private sealed class Scope : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        private readonly ClassicDesktopStyleApplicationLifetime lifetime = new();
        public AppRuntime Runtime { get; }
        public PetWindow Pet => Runtime.ActivePet!;
        public Scope() { Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path); Runtime = new(lifetime); }
        public async Task Load(bool bori = false)
        {
            string id;
            if (bori)
            {
                await Runtime.Start(true, true); id = "bori-rabbit";
                Assert.Contains(Runtime.Characters, character => character.Manifest.Id == id);
            }
            else
            {
                var fixture = new SpriteFixture(16, 16) { Name = "Timer hover fixture" };
                for (var y = 5; y < 11; y++) for (var x = 5; x < 11; x++) fixture.Pixels[y * 16 + x] = 0xffffffff;
                id = Runtime.Library.InstallFixture(fixture).Manifest.Id;
            }
            await Runtime.Reload(); Runtime.Stop();
            await Runtime.UpdateSettings(Runtime.Settings with { SelectedCharacterId = id, ShowPet = true,
                ShowPetTimer = true, ReminderSoundsEnabled = false });
            Pet.Position = new(500, 300); Pet.RefreshSpeech(); Layout(Pet); Layout(Pet.Overlay); Layout(Pet.Overlay.Timer);
        }
        public void Dispose()
        {
            Runtime.Dispose(); lifetime.Dispose(); Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose();
        }
    }

    private static TimeSpan At(double milliseconds) => TimeSpan.FromMilliseconds(milliseconds);
}
