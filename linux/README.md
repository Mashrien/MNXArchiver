# Linux GUI interfaces

The standard build is now `./build-linux.sh --test` from the project root. It
publishes one NativeAOT executable containing MWin/GTK4 and native GTK3 to
`../MNX_Builds/mnx-gui/linux-x64`. Automatic selection prefers MWin; the
`--backend` option or Options → General → Interface selects another preference.
See the root README and `DUAL-DEPENDENCIES.txt` for unified runtime details.

## Standalone MWin development build

`../windows` remains the editable WinForms designer source. This port translates those
forms during its build and runs them through a pinned, patched Majorsilence.Forms GTK4 /
Skia backend. `MWin/` contains the small portable MWin adaptation (0.0.7.2); its platform-specific
changes are commented. The Windows MWin checkout is not modified by this build.

From the MNX-GUI project root:

```sh
./build-linux-mwin.sh --clean --test
```

NativeAOT output: `../MNX_Builds/mnx-gui/linux-mwin-x64/mnx-gui` (arm64 on ARM hosts).
This is separate from the existing GTK3 release and desktop launcher. Keep the two
shipped Skia/HarfBuzz shared libraries beside the executable. MNXFile itself remains a
separately installed native shared library; it is never embedded or copied by this script.

For an uninstalled development engine:

```sh
MNXFILE_NATIVE_LIBRARY=/absolute/path/libmnxfile.so.1 ./build-linux-mwin.sh --test
```

`--test` runs offscreen, without opening any desktop windows. `--smoke-test DIRECTORY`
on the resulting executable instead exercises real windows and owned progress dialogs.
Both create disposable archives under their evidence directory. `--headless-smoke DIRECTORY`
is the standalone offscreen equivalent.

`./build-linux-mwin.sh --managed` builds framework-dependent output with separate managed
DLLs. NativeAOT incorporates MWin and the translation layer into the executable; updating
those components requires rebuilding it. The archive engine's versioned native ABI remains
replaceable independently in either mode.

## Build layout

- `dependencies.json`: pinned upstream commits, independent of the moving branches.
- `patches/forms.patch`: color-key transparency, native GTK file pickers and file drag,
  parent-relative popups, pointer activation, toolbar padding/renderer hooks, and
  top-level bitmap rendering for the inherited smoke suite.
- `patches/richtext.patch`: collection-face/named-instance fix for variable CJK fonts.
- `patches/gui.patch`: small Linux startup, platform event and smoke-test adaptations.
- `overrides/GlobalUsings.cs`: types WinForms normally imports implicitly.
- `prepare.py`: repeatable source preparation, with guards against replacing unmarked folders.
- Generated sources, dependencies, downloads and intermediate files live under sibling
  `mnx_clutter/build`. They are not additional editable GUI projects.

`MNXPortableRoot`, `MNXBuildRoot`, `MNXReleaseRoot`, `MNXPackageFeed`, and `MNXBuildNumber`
override their defaults. Build numbers advance automatically unless explicitly supplied.
The first build downloads the pinned source archives and restores NuGet dependencies.

## Platform caveats under investigation

Menus and non-activating owned previews use native parent-relative popovers, including
bottom-right tooltip anchoring. Wayland still restricts arbitrary placement of independent
top-level windows, so the compositor chooses those windows' screen positions. The unified release uses the existing standard executable location; standalone
MWin development releases remain separate.

The upstream toolkit also contains reflection-based resource/converter paths. NativeAOT
emits warnings for those dependencies; the GUI and MWin metadata are retained explicitly,
and the actual published executable is tested. Raw XML resources avoid Windows image
resource deserialization for these forms. See `DEPENDENCIES.txt` for runtime packages.

Validation results and known limits: [LINUX-MWIN-VALIDATION.md](../docs/LINUX-MWIN-VALIDATION.md).

Unified release validation: [LINUX-DUAL-BACKEND-VALIDATION.md](../docs/LINUX-DUAL-BACKEND-VALIDATION.md).
