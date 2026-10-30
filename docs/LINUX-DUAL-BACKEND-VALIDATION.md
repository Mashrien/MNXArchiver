# Unified Linux GUI validation

Date: 2026-10-24. Target: Linux x64, .NET 10 NativeAOT, version 1.4.0-alpha.26.

## Release and source

- Canonical source: `MNX-GUI/windows/` for Windows and Linux MWin forms;
  `MNX-GUI/src/` for the native GTK3 frontend.
- Build: `./build-linux.sh --test` or `./build.sh --test` from MNX-GUI.
- Release: sibling `MNX_Builds/mnx-gui/linux-x64/`.
- One native `mnx-gui` executable embeds both managed frontend implementations,
  MWin and the translation layer. Shipped Skia/HarfBuzz remain native companions.
- The installed native MNX archive engine remains separate and independently replaceable.
- Generated sources, intermediates and evidence: sibling `mnx_clutter/build/mnx-gui-dual/`
  and pinned portable dependencies in `mnx_clutter/build/mnx-gui-port/`.

## Selection and configuration

Automatic prefers MWin/GTK4. GTK3 is used when MWin dependencies are unavailable.
A saved unavailable preference falls back with a warning and is retained in the INI.
An explicitly requested unavailable backend exits with an error instead of silently changing it.
`--backend=auto|mwin|gtk3` overrides a saved choice without changing it;
`--list-backends` lists detected libraries. GTK4 is an alias for MWin.
A literal `--` protects subsequent filenames from backend option parsing.

The existing INI stores `[Interface] Backend=auto|mwin|gtk3`.
Both options windows expose the choice under General when both are available;
a restart applies it. Cancel preserves the existing file and unrelated keys survive Save.
General → Appearance stores `[Interface] OpacityPercent=10..100` (default100).
Save updates application windows, owned dialogs and previews. Error messages remain
fully opaque. Native system/portal file pickers remain owned by the operating system.

The options window now has General, Compression & Encryption, Previews, and
Preview Display tabs; Windows retains ordinary designer-editable controls.

## Verification

Final build26 verification:

| Check | Result |
| --- | --- |
| NativeAOT publish and 30 policy/INI/opacity checks | PASS |
| MWin offscreen suite, including full Path/Search text fit | PASS |
| Actual MWin GTK4/Wayland suite with fresh config root | PASS |
| Complete native GTK3 Xvfb suite, including error opacity and Save/Cancel | PASS |
| Native GTK3 Wayland popup and browser integration | PASS |
| Seven actual GUI selection/fallback runs, one GTK major mapped per process | PASS |
| Explicit unavailable backend and no-toolkit errors | PASS, exit2 |
| Delayed GTK4 probe, continued GTK3 detection | PASS, 5.09 seconds |
| Windows source/layout compilation | PASS, zero errors |

Host release and guest tested executable SHA256:
`1ae56604fd31c9cebbf948f70a9489da8ac6fc27574ee31b9ae47c484c1cd9ed`.
The release contains one executable, Skia/HarfBuzz native libraries and notices,
with no separate managed frontend DLLs. ELF dependencies contain no eagerly linked GTK.

Manual compositor checks at65% showed visible translucency of main and Options windows.
An actual invalid temporary-path Save raised an opaque error popup above those translucent
windows. The final normal-width and resized screenshots show complete Path/Search labels.
Final review found no concrete correctness gaps. Windows runtime/designer interaction and
ARM64 remain unverified in this pass. Native Windows/system dialogs retain OS behavior.

Earlier real native runs:

- 24 backend policy, argument parsing, INI persistence and fallback assertions passed.
- Build22 native MWin offscreen and actual GTK4/Wayland suites passed: all nine
  designer forms; layout/status region/icons; Unicode metadata; archive add/extract/delete/
  verify/compact; progress ownership; cancellation; encrypted resume and exported-key
  consumption; compression choices; corrupt-archive handling; options and bounded previews.
- Build22 native GTK3 complete suite passed under guest Xvfb at 1920×1080:
  navigation, sorting, previews and cleanup, actual options Save/Cancel, drag/drop,
  creation/extraction, overwrite decisions, progress ownership, cancellation and encrypted resume.
- Build22 native GTK3 Wayland browser integration passed: bottom-right popup placement,
  trimmed image bounds, type grouping, and numeric chunk/size column sorting.
- Build22 probe timeout injection passed: blocked GTK4 load was killed after five seconds,
  GTK3 probe still succeeded, parent exited normally after 5.08 seconds.
- Windows project cross-target compilation passed with zero errors and one existing unused
  CreationForm.components warning. Windows runtime/designer interaction was not tested in this pass.

## Native integration fixes

GtkSharp normally scans every loaded assembly to map native GTypes to managed classes.
In the combined NativeAOT executable, GTK3 and gir.core GTK4 expose identically named
classes such as Gtk.Window. The initial native GTK3 test failed with MissingIntPtrCtorException;
the resolver now restricts mappings to GtkSharp binding assemblies, including native subclass
parent fallback. The complete native GTK3 suite passed afterward.

Manual menu painting with a fresh non-existent XDG_CONFIG_HOME exposed an upstream
settings-path failure: GetFolderPath returned an empty string, producing a relative path
that collided with the executable name. The pinned toolkit patch uses DoNotVerify to
retain the real absolute user path before creating it. The regression smoke check exercises
that lazy path, and final validation uses a fresh profile.

## Limits

GTK3's full synthetic pointer suite requires X11/Xvfb; Wayland integration is tested
separately. The exact-anchor Wayland fixture maximizes its parent so compositor popup sliding
does not confound the test; production preview placement code was unchanged by this task.
Wayland still chooses independent top-level window positions.

Upstream bindings/resource converters emit NativeAOT trimming warnings. Relevant reflection
metadata is retained, and actual published executables are exercised; this is not a claim that
all possible upstream control/API paths are AOT-safe. ARM64 runtime behavior was not tested.
New Windows-only controls or APIs may need a Linux implementation. Standard designer changes
are translated automatically during the Linux build. GTK3 has a separate layout.

No host desktop windows were used. Guest GUI checks ran in the authorized Linux VM.
Git publication and releases await the user's final layout review.

## File-list resize follow-up: 1.4.0-alpha.30

The canonical WinForms layout now gives spare width to Name, preserves metadata
widths, and treats a manually chosen Name width as a minimum. The portable
Details ListView patch adds horizontal scrolling, joint scrollbar geometry,
and matching cell/header drawing and hit testing. Designer controls are unchanged.

Verification:

- Managed27 failed the new scrollbar check before the toolkit fix; managed28/29 passed.
- Native30 passed all30 backend/settings assertions and the complete MWin offscreen suite.
- New control checks cover shrink/grow, both scrollbars without overlap, final-column
  access and header clicks, column reordering, mirrored layout, Scrollable=false,
  empty-list header overflow, and column widths reduced to fit.
- Shared form checks cover Name growing, immediately refilling after manual narrowing,
  and yielding space when a metadata column is widened.
- The same native30 executable passed the complete actual MWin GTK4/Wayland suite
  in the Linux VM. Manual edge drags revealed the horizontal bar; dragging its thumb
  exposed Created; clicking the scrolled Size header sorted numerically. Growing
  the window reset the scroll offset, hid the bar, and expanded Name.
- Windows project cross-target compilation passed with zero errors and one existing
  unused designer-components warning. Windows runtime/designer was not retested.
- Native GTK3 source was unchanged; its full runtime results above are from build26.

Executable SHA256 (host and guest):
`629dc0616eccd3a9bd04f6c3561dd484f4857d94df5cbfaf716845f4cf198795`.
Evidence: `mnx_clutter/build/mnx-gui-dual/evidence/linux-x64-native-30` and
`mnx_clutter/build/mnx-gui-dual/evidence/list-resize-30` in the parent source directory.
The native release is `MNX_Builds/mnx-gui/linux-x64/mnx-gui`; a preview instance of
build30 is open in the Linux VM for review. No host desktop windows were opened.

## Final output and colors: 1.4.0-alpha.42 (2026-10-30)

- Native42 build,30 backend/settings assertions, typed INI/palette tests, and complete
  offscreen MWin suite passed. CLI1.2.0-alpha.4 integration suites passed, including
  interactive terminal/history/completion/cancellation, versions and forwarding.
- Complete actual native MWin Xvfb suite passed twice using a supplied dark palette;
  its SHA256 stayed unchanged. Options screenshots show readable themed controls,
  consistent top-level group widths, visible checked states, and paired title colors.
- Actual native startup created the host's missing mnx.mwin-colors.conf from KDE colors;
  a separate startup with UseDesktopColors=false stayed running and created no palette.
- Native GTK3 full Xvfb suite passed with the shared new INI/settings implementation
  in build39; subsequent42 changes were limited to MWin painting/checks.
- Windows cross-target compilation succeeded with zero errors, one existing unused
  designer-components warning. Windows runtime/designer and Wayland were not retested.
- A build39 themed Xvfb run aborted in GTK/X11 allocation during a show operation.
  Non-themed39 and baseline30 passed. After removing hardcoded light Button/Field/Tab
  painting when a palette is active, final42 passed twice; the underlying native
  allocator failure was not independently localized. GTK generated DRI3 warnings in
  Xvfb, where software rendering is expected.
- Final native GUI SHA256:
  1ac904e9213d66bcd733bda0f9f163fb1b8e5fceef9fd9b429357f87e9d70a87.
- Evidence: parent mnx_clutter/build/mnx-gui-dual/evidence/theme-final and
  linux-x64-native-42. No windows were opened on the host desktop; the Linux VM
  was unreachable. All actual display checks used isolated Xvfb.

Final executables and self-supplied libraries are copied to MNX_Builds/binaries-final
by each successful native component build. Managed/debug output is excluded; per-component
copy tests verify missing input cannot replace working programs and other components
are preserved. Staged CLI located its adjacent engine and round-tripped compressed
Unicode-named input. Rollbacks live in mnx_clutter/releases/mnx-gui rather than the
current release directory. Palette/INI usage is documented in INI-CONFIG.md.
Embedded MWin version is0.0.7.1. No Git publication was performed.


## Preview activation and archive drops — 1.4.1.47 (2026-10-30)

- Single-Click is accepted, persisted and exercised through actual pointer events.
  GTK3 waits for refreshed row allocation in the test before calculating the click;
  otherwise the test hit the column header. Both frontends filter drags/empty rows.
- Right of Main Window is available only for Dialog/Window presentation; tooltip
  anchors remain inside the parent. Dialog has title/X only, zero frame, fixed size,
  no resize grip/min/max or footer Close. MWin zero-border painting and content bounds
  now honor zero thickness. Embedded MWin version0.0.7.2.
- Native X11 MWin Window probe: parent150,150,850,650; preview1000,150,526,433.
  At configured65% opacity, source image RGB45,155,190 rendered29,101,124. This is GTK
  widget opacity, not an X11 window-opacity atom. Image click released the window.
- Native MWin right Dialog probe passed actual adjacent rendering, dismissal on
  clicking the parent, reopening after dismissal, and image-click closure.
- Single-archive drops offer Open Archive / Add to Archive, with close/cancel preserving
  state. Open loads before replacing the old archive. Add stores an ordinary file or
  invokes the normal creation form when no archive is open. The offscreen and real
  MWin suites exercise Open/Add/Cancel and cancellation of the resulting CreationForm.
- `./build-linux.sh --test` passed30 backend/INI checks, palette/typed INI checks,
  dependency probes and the complete native offscreen MWin suite. Actual native MWin
  Xvfb suite passed. Complete native GTK3 suite passed, including single pointer click,
  native right Dialog coordinates, hover/click preview disposal, creation/extraction,
  cancellation, checksummed encrypted resume and key consumption.
- One initial native GTK3 full run failed its synthetic image-tooltip click check.
  Its isolated browser check and the repeated complete suite passed without a code
  change. Retained the failure log; this does not establish its underlying cause.
  A temporary smaller-window preview-only test also exposed a hover fixture failure;
  the supported full initialization passed. The temporary shortcut was removed.
- Windows final source build passed with zero errors and one existing unused designer
  components warning. Windows runtime/designer and native Wayland placement were not
  retested. Wayland independent Window placement remains compositor-controlled;
  owned right Dialog previews use parent-relative surfaces and may be constrained by
  available screen space. X11 uses map-at-zero-opacity, native positioning, then
  configured-opacity restore for right Window previews.
- Pinned toolkit patches apply cleanly during native preparation. Read-only review
  found no additional important defects. Existing upstream AOT/trim warnings remain;
  the runtime suites cover the shipped paths, not every upstream toolkit API.
- Final native GUI SHA256:
  5b4e167107be81baee96707b069111b5916efa89e540aa0d0d00f225af0ca586.
- Final output: `../MNX_Builds/binaries-final/mnx-gui`; matches component release.
  CLI/engine artifacts remain current and unchanged. Evidence under parent
  `mnx_clutter/build/mnx-gui-dual/evidence/`: `linux-x64-native-47`,
  `preview-mwin-native47`, `preview-gtk3-native47-repeat`,
  `preview-pointer-window-native47`, `preview-pointer-dialog-native47`.
  All actual windows used isolated Xvfb. No host desktop window or Git publication.
