/**
 * Map a browser KeyboardEvent.code to a PS/2 set-1 scancode (Windows). Only the most
 * common keys are mapped for v0.1; expand as needed. Extended-key flag is returned
 * alongside so the host can OR in KEYEVENTF_EXTENDEDKEY when sending to SendInput.
 *
 * Source of truth: <https://download.microsoft.com/download/1/6/1/161ba512-40e2-4cc9-843a-923143f3456c/scancode.doc>
 */
export interface ScancodeEntry { code: number; extended: boolean }

const MAP: Record<string, ScancodeEntry> = {
  Escape: { code: 0x01, extended: false },
  Digit1: { code: 0x02, extended: false }, Digit2: { code: 0x03, extended: false },
  Digit3: { code: 0x04, extended: false }, Digit4: { code: 0x05, extended: false },
  Digit5: { code: 0x06, extended: false }, Digit6: { code: 0x07, extended: false },
  Digit7: { code: 0x08, extended: false }, Digit8: { code: 0x09, extended: false },
  Digit9: { code: 0x0a, extended: false }, Digit0: { code: 0x0b, extended: false },
  Minus: { code: 0x0c, extended: false }, Equal: { code: 0x0d, extended: false },
  Backspace: { code: 0x0e, extended: false }, Tab: { code: 0x0f, extended: false },
  KeyQ: { code: 0x10, extended: false }, KeyW: { code: 0x11, extended: false },
  KeyE: { code: 0x12, extended: false }, KeyR: { code: 0x13, extended: false },
  KeyT: { code: 0x14, extended: false }, KeyY: { code: 0x15, extended: false },
  KeyU: { code: 0x16, extended: false }, KeyI: { code: 0x17, extended: false },
  KeyO: { code: 0x18, extended: false }, KeyP: { code: 0x19, extended: false },
  BracketLeft: { code: 0x1a, extended: false }, BracketRight: { code: 0x1b, extended: false },
  Enter: { code: 0x1c, extended: false }, ControlLeft: { code: 0x1d, extended: false },
  KeyA: { code: 0x1e, extended: false }, KeyS: { code: 0x1f, extended: false },
  KeyD: { code: 0x20, extended: false }, KeyF: { code: 0x21, extended: false },
  KeyG: { code: 0x22, extended: false }, KeyH: { code: 0x23, extended: false },
  KeyJ: { code: 0x24, extended: false }, KeyK: { code: 0x25, extended: false },
  KeyL: { code: 0x26, extended: false }, Semicolon: { code: 0x27, extended: false },
  Quote: { code: 0x28, extended: false }, Backquote: { code: 0x29, extended: false },
  ShiftLeft: { code: 0x2a, extended: false }, Backslash: { code: 0x2b, extended: false },
  KeyZ: { code: 0x2c, extended: false }, KeyX: { code: 0x2d, extended: false },
  KeyC: { code: 0x2e, extended: false }, KeyV: { code: 0x2f, extended: false },
  KeyB: { code: 0x30, extended: false }, KeyN: { code: 0x31, extended: false },
  KeyM: { code: 0x32, extended: false }, Comma: { code: 0x33, extended: false },
  Period: { code: 0x34, extended: false }, Slash: { code: 0x35, extended: false },
  ShiftRight: { code: 0x36, extended: false }, NumpadMultiply: { code: 0x37, extended: false },
  AltLeft: { code: 0x38, extended: false }, Space: { code: 0x39, extended: false },
  CapsLock: { code: 0x3a, extended: false },
  F1: { code: 0x3b, extended: false }, F2: { code: 0x3c, extended: false },
  F3: { code: 0x3d, extended: false }, F4: { code: 0x3e, extended: false },
  F5: { code: 0x3f, extended: false }, F6: { code: 0x40, extended: false },
  F7: { code: 0x41, extended: false }, F8: { code: 0x42, extended: false },
  F9: { code: 0x43, extended: false }, F10: { code: 0x44, extended: false },
  NumLock: { code: 0x45, extended: false }, ScrollLock: { code: 0x46, extended: false },
  Numpad7: { code: 0x47, extended: false }, Numpad8: { code: 0x48, extended: false },
  Numpad9: { code: 0x49, extended: false }, NumpadSubtract: { code: 0x4a, extended: false },
  Numpad4: { code: 0x4b, extended: false }, Numpad5: { code: 0x4c, extended: false },
  Numpad6: { code: 0x4d, extended: false }, NumpadAdd: { code: 0x4e, extended: false },
  Numpad1: { code: 0x4f, extended: false }, Numpad2: { code: 0x50, extended: false },
  Numpad3: { code: 0x51, extended: false }, Numpad0: { code: 0x52, extended: false },
  NumpadDecimal: { code: 0x53, extended: false },
  F11: { code: 0x57, extended: false }, F12: { code: 0x58, extended: false },

  // Extended (0xE0-prefixed) — mapped to base code + extended flag.
  NumpadEnter: { code: 0x1c, extended: true },
  ControlRight: { code: 0x1d, extended: true },
  NumpadDivide: { code: 0x35, extended: true },
  AltRight: { code: 0x38, extended: true },
  Home: { code: 0x47, extended: true },
  ArrowUp: { code: 0x48, extended: true },
  PageUp: { code: 0x49, extended: true },
  ArrowLeft: { code: 0x4b, extended: true },
  ArrowRight: { code: 0x4d, extended: true },
  End: { code: 0x4f, extended: true },
  ArrowDown: { code: 0x50, extended: true },
  PageDown: { code: 0x51, extended: true },
  Insert: { code: 0x52, extended: true },
  Delete: { code: 0x53, extended: true },
  MetaLeft: { code: 0x5b, extended: true },
  MetaRight: { code: 0x5c, extended: true },
  ContextMenu: { code: 0x5d, extended: true },
}

export function keyEventToScancode(code: string): ScancodeEntry | undefined {
  return MAP[code]
}
