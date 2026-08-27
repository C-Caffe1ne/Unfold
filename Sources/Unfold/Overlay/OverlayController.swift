import Foundation

/// Presents and dismisses the stretch-reminder overlay.
///
/// This is the only thing `StretchCoordinator` talks to for on-screen
/// display; it doesn't know about `Character` internals beyond passing one
/// through to the view, and it doesn't know about the timer at all.
@MainActor
final class OverlayController {

    private var windowController: OverlayWindowController?
    private var autoDismissTask: Task<Void, Never>?

    func presentStretchReminder(for character: Character) {
        autoDismissTask?.cancel()
        windowController?.dismiss()

        let view = StretchOverlayView(
            character: character,
            onDismiss: { [weak self] in self?.dismiss() }
        )
        let controller = OverlayWindowController(rootView: view)
        windowController = controller
        controller.showCentered()

        // V1 default is manual-dismiss-only (Constants.overlayAutoDismissDelay
        // is nil). Setting a delay there — or scheduling this from
        // `SpriteAnimator.isFinished` instead of on presentation — is the
        // extension point for a future timed auto-dismiss.
        if let delay = Constants.overlayAutoDismissDelay {
            autoDismissTask = Task { [weak self] in
                try? await Task.sleep(for: .seconds(delay))
                guard !Task.isCancelled else { return }
                self?.dismiss()
            }
        }
    }

    func dismiss() {
        autoDismissTask?.cancel()
        autoDismissTask = nil
        windowController?.dismiss()
        windowController = nil
    }
}
