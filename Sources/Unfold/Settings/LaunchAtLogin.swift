import Foundation
import ServiceManagement

/// Thin wrapper around `SMAppService.mainApp` (the current, non-deprecated
/// Launch at Login API — no separate helper-app target needed for a simple
/// "launch this app at login" registration).
///
/// Deliberately stateless: the real system registration status is always
/// the source of truth, never a cached `UserDefaults` flag, so there is
/// exactly one place this can disagree with reality.
enum LaunchAtLogin {

    static var status: SMAppService.Status {
        SMAppService.mainApp.status
    }

    static var isEnabled: Bool {
        status == .enabled
    }

    /// `true` once registered but not yet approved by the user in System
    /// Settings → General → Login Items. Worth a short, non-blocking note
    /// in the UI; not worth forcing System Settings open over.
    static var requiresApproval: Bool {
        status == .requiresApproval
    }

    /// Registers or unregisters the app as a login item. Never throws past
    /// this boundary — a failure is logged and left for the caller to
    /// notice by re-reading `status` afterward, rather than crashing a
    /// menu-bar utility over a login-item registration hiccup.
    static func setEnabled(_ enabled: Bool) {
        do {
            if enabled {
                try SMAppService.mainApp.register()
            } else {
                try SMAppService.mainApp.unregister()
            }
        } catch {
            NSLog("Unfold: Launch at Login \(enabled ? "register" : "unregister") failed — \(error.localizedDescription)")
        }
    }
}
