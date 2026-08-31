import Foundation

/// What the Desktop Pet is currently doing in reaction to the mouse.
///
/// Deliberately its own type, kept separate from any app-level event (e.g.
/// `StretchEvent`) — this is purely about pointer interaction with the pet
/// on screen, and has nothing to do with the Stretch Reminder timer/overlay.
enum PetInteractionState: Equatable {
    /// Default state: the current character's idle animation, looping.
    case idle
    /// The pet is being held down (mouseDown, button still down). Plays its
    /// reaction once, then holds the last frame until mouseUp — it does not
    /// return to idle on its own.
    case pointerDown
    /// A long press was released. Plays a landing reaction once, then
    /// returns to idle.
    case pointerUp
    /// A short press was released. Plays a quick reaction once, then
    /// returns to idle.
    case click
}
