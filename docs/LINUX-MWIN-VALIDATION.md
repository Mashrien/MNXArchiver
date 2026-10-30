# Linux MWin port validation

Validated on 24 October 2026, x64 Linux host and Nobara 44 KDE/Wayland guest.
The Windows designer files remain the source of the translated forms. The existing
GTK3 release and desktop launcher are preserved.

## Build and runtime

Run `./build-linux-mwin.sh --test` from the MNX-GUI root. `--clean` requests a clean
build; `--managed` explicitly selects the alternative framework-dependent build.
Verified release: **1.3.0-alpha.17**, MWin portable fork **0.0.8**.

The default produces a real ELF NativeAOT executable with MWin and the managed
translation code incorporated, plus the Skia/HarfBuzz shared libraries. The MNX
archive engine is loaded separately through its native ABI.

Source preparation uses the commits in `linux/dependencies.json` and checked-in
patches, with downloads, generated source, compiler output, symbols and evidence
outside the project under sibling `mnx_clutter/build`. Locks serialize builds
sharing the same compiler output or source cache. Release directories are replaced
only after successful publishing and requested smoke checks; the prior release is
retained with a timestamp.

## Checks completed

- NativeAOT probe startup and pointer input on GTK4/Wayland; pointer enter, move,
  leave, caption highlighting after garbage collection, and resize grip cursor.
- Color-key transparency and shaped input, including rounded window corners.
- Font shaping, including collection faces and named variable-font instances.
- All nine designer forms construct/dispose; populated main layout, 192-pixel tree,
  expanding path field, stable search width, embedded icons, protected status area.
- Archive create/open/add/extract/delete/search/sort/verify/compact, Unicode names,
  byte-for-byte extraction, each compressor and encryption through the native engine.
- Owned progress/cancellation dialogs, hidden main window during operations,
  finish-current-file cancellation, checksummed queue/key export, encrypted resume,
  and immediate abort removing partial output.
- Invalid archive diagnosis retains error-code text and preserves an already-open
  archive. Windows-only window enumeration checks are not claimed as Linux tests.
- Preview options, simultaneous-window limit, repeated disposal, animation,
  plaintext clearing, and actual archived 9.6 MB / 15.5 MB GIF playback checks.
- Actual KDE native file picker, context menu positioning and Dolphin file drag in
  both directions, with output contents verified.
- Actual bottom-right double-click tooltip and cursor-anchored mouseover tooltip;
  image click closes the preview and leaving the item removes the hover preview.
- Six-pixel toolbar experiment: moving the buttons does not uncover the legacy
  parent-drawn separator. The menu's own renderer now draws its bottom divider.
  Toolbar padding is honored; a doubled device rectangle checks scaled coordinates.

## Upstream toolkit suite

The final filtered run passed 8,046 tests, skipped 5 and failed none (8,051 selected).
Two real speech-backend tests were excluded to keep the VM silent. Two font tests
were excluded because they assume the bundled Caladea family is not installed on
that system; fontconfig confirms it is installed in this guest. The initial
unfiltered run exposed these environment failures plus stale feature-gap baselines.
The baselines were updated for implemented `DrawToBitmap`, `CreateParams.ExStyle`
and `Form.TransparencyKey` functionality; no test assertion was weakened.

The exact filter was:

```
FullyQualifiedName!~ThemeCssPrivateFontTests.A_family_registered_after_its_name_was_first_resolved_is_found_afterwards&FullyQualifiedName!~ThemeCssPrivateFontTests.A_disposed_collection_stops_resolving&FullyQualifiedName!~SpeechTests.The_real_desktop_backend
```

Evidence is retained in `../mnx_clutter/build/mnx-gui-mwin/evidence`, including
`toolkit-core-final.log` and `native-final-17/` application smoke results/screenshots.
The final host release and guest-tested executable have the same SHA-256:
`9a6a3e7434e5d0f48ec8859d90ab3809761545035295899efd00e8dbbca75c02`. Build logs also
retain the upstream trimming/AOT warnings rather than suppressing them.

## Remaining platform limits

Wayland controls placement of independent top-level windows. Menus and owned,
non-activating previews use parent-relative native popovers. Arbitrary screen
coordinates for unrelated windows are not guaranteed by the compositor.

The upstream toolkit still has reflection/resource paths that emit NativeAOT
warnings. Explicit metadata retention and testing cover the current forms; this
is not a claim that every upstream control or future designer addition is AOT safe.
ARM64 is a build target selected on ARM64 hosts, but was not executed here.
One guest launch paused for approximately a minute before GTK initialization;
the following manual launch opened normally. The cause of that isolated delay was
not established; functional checks completed successfully after startup.

The requested unified GTK3/MWin executable, toolkit detection and saved backend
choice are a separate follow-up, deferred until this port's validation is complete.
