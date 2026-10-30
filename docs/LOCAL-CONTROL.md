# Local control v1

AmbientLite remains a standalone MIT-licensed application and release. The optional local control endpoint lets a native front end such as EmberRGB operate the existing engine without another capture or strip writer.

## Transport and access

Named pipe `AmbientLite.Control.v1.<current-user-SID>` on the local computer. The server uses a protected DACL granting the current Windows user access and denying the network SID. It opens no TCP port, requires no elevation, account, service or driver, and waits asynchronously when idle.

One request and response per connection. Each message is UTF-8 JSON with a four-byte unsigned little-endian byte count, between 1 and 16,384 bytes. Partial reads are assembled; zero/oversized/truncated messages are rejected. Connections have a five-second I/O budget. Version mismatch returns an error without executing the handler. Clients must not automatically replay mutations after an uncertain response or timeout; refresh state first. An already dispatched UI command may finish after its client disconnects.

The canonical typed schema and framing are in AmbientControlProtocol.cs, under the repository's MIT license. EmberRGB carries a copy of that source. Version 1 requests have `Version`, `Command`, optional `Settings`, and `ExpectedKey`. Replies include `Version`, `Ok`, `Error`, current settings, displays, status, running state, application version, process ID and a diagnostic screen-frame count. The frame count is not a colour stream and may reset when the standalone window's visibility/statistics change.

## Commands

| Command | Behaviour |
|---|---|
| `status` | Read current engine state, settings and available displays |
| `apply` | Validate a complete settings object and matching `ExpectedKey`, then update the existing UI/worker settings |
| `start` | Start the configured mode; response reports current state after a short startup opportunity |
| `pause` | Cancel and await the lighting worker; retain its last physical output |
| `off` | Stop the worker and send black to the configured strip |
| `test` | Send the existing dim-blue strip test; requires paused lighting |
| `show` | Open the standalone window |
| `quit` | Request normal exit, respecting the existing off-on-exit preference |

Unknown commands are rejected. The endpoint cannot execute shell commands, arbitrary file operations, firmware updates or HID packets. `apply` requires a settings fingerprint from the last read. If settings changed through another front end, refresh before applying. Display, LED layout, direction and capture method cannot change while the worker runs.

Settings: `Mode` (`screen` or `static`), six-digit `Colour`, exact display `Monitor` ID, `Brightness` 0–100, `Smoothing` 0–95, `Rate` 5–60, positive `Top`/`Side` with at most 254 LEDs total, `Reverse`, `Compatibility`, `OffOnExit`, `StartInTray`, `StartLighting`, `StartWithWindows`. Windows startup registration changes only when that option changes explicitly. No second profile is stored by the client.

## Companion mode and ownership

`--companion` hides the main window and suppresses AmbientLite's tray icon. It does not itself start lighting. `--companion --startup` uses the existing saved startup-lighting choice, without showing another window or tray icon. `installed.marker` beside the executable selects `%LOCALAPPDATA%\AmbientLite\settings.txt`, as in installed standalone editions; otherwise portable settings remain beside the executable.

The existing single-instance mutex applies to companion and standalone launches, so only one AmbientLite engine runs. If a compatible instance already exists, front ends attach to it. A front end should close only the process it launched, checking the returned process ID, and leave separately launched standalone instances running. Closing AmbientLite's window still leaves the engine running; the existing session/exit handlers and optional blackout apply unchanged.

The bridge reuses AmbientLite's existing worker and LightingFrames object. Static mode parks capture; changing back to screen mode reuses that capture session. It does not implement screen-reactive output for third-party RGB controllers. That future feature should publish bounded colour updates from this worker, rather than start another capture pipeline.

## Verification

ControlTests checks settings bounds, framed roundtrips, invalid/truncated lengths, same-user pipe exchange, unknown-version rejection and recovery after a disconnected client. Existing session, shutdown and 100 capture-mode round-trip tests pass. Hardware integration testing covers status, applying static settings through EmberRGB, pause/resume and restoration to the original screen configuration. Initial automated visual confirmation was inconclusive; the user subsequently confirmed a manual change worked. Software state is not independent proof of visible LED output.

This companion still initializes the native WPF interface internally, even when hidden. It avoids a duplicate capture and extra service, but it is not a headless-core memory optimization. Combined resource measurements belong to the integrating release; no unmeasured memory saving is claimed.
