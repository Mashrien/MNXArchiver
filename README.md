# MNXArchiver

**A Windows and Linux archive manager built for large network archives and NAS backups.**

MNX's primary purpose is managing hundreds-of-gigabytes or terabytes of backups on
network storage with minimal overhead. A complete front index makes browsing and
searching independent of payload size; file transfers stream through bounded buffers,
while contiguous chunk extents reduce unnecessary seeking and network reads.

Large action buttons, an expandable folder tree and a sortable file list provide a
traditional archive-manager interface. Archive handling comes from the separately
installed **[MNXFile](https://github.com/Mashrien/MNXFile)** engine. For terminal use,
see **[mnx-cli](https://github.com/Mashrien/mnx-cli)**.

## Downloads and installation

Recent source changes include Linux MWin menu colors, portable library lookup,
Options layout fixes and the testing-only preview overrides described below.

[Releases](https://github.com/Mashrien/MNXArchiver/releases) provide **mnx-gui**
RPM/DEB/portable Linux packages and a Windows x64 ZIP. Install the separate
[MNXFile engine](https://github.com/Mashrien/MNXFile/releases) first. Linux requires
glibc2.39+, OpenSSL3 and GTK4 >=4.12 for MWin or GTK3 >=3.24 for the native frontend.
The RPM/DEB includes desktop/MIME registration and Dolphin's **MNX Archive** submenu.
Optional saved-password support also needs `secret-tool` and an unlocked desktop
Secret Service. Windows10 x64 and later need no GTK. Neither platform needs an
installed .NET runtime. See [packaging instructions](packaging/README.md).

## Optional archive formats

MNXArchiver 2.0 supports native archive modules. The first module adds Ren’Py
RPA 3.0 browsing, extraction, add, replace, delete, and creation to the same GUI.
See the [module installation and ABI guide](docs/ARCHIVE-PLUGINS.md).

## Features

- Create/open archives; add, extract, delete, search, verify and compact entries.
- Compression choices None, Brotli, LZ4 and Snappy; optional encrypted contents/index.
- File-manager launch arguments and drag/drop; dropping an archive offers **Open
  Archive** or **Add to Archive**. Creating from inputs opens the full destination,
  compression and encryption form.
- Owned progress/cancel dialogs: finish the current file and keep a resume queue,
  or stop immediately and remove incomplete output. Resume honors the archive's
  stored compression/encryption settings.
- In-memory text and image previews, with single-click, double-click or delayed
  hover activation; tooltip, minimal Dialog or normal Window presentation.
- Editable INI settings, configurable memory limits/opacity and desktop color matching.
- Embedded PNG icons; no runtime SVG renderer or desktop icon theme dependency.
- Desktop/MIME resources and Dolphin **MNX Archive** context menu resources.

## Interfaces and runtime

| Platform | Interface | Published program |
| --- | --- | --- |
| Linux | Customized **MWin/GTK4** (preferred), native GTK3 fallback | One Native AOT `mnx-gui` executable |
| Windows | Designer-editable WinForms on MWin | Self-contained `mnx-gui.exe` |

Linux includes both frontends in one binary. At startup it probes dependencies in
short-lived processes, then loads only the selected frontend. Keep the shipped
`libSkiaSharp.so` and `libHarfBuzzSharp.so` beside the program for MWin.
Install GTK4 for MWin, GTK3 for the native frontend, or both.

```sh
mnx-gui backup.mnx
mnx-gui --list-backends
mnx-gui --backend=mwin backup.mnx
mnx-gui --backend=gtk3 backup.mnx
```

Automatic selection prefers MWin. Options → General → Interface saves the next
launch's preference; `--backend=auto|mwin|gtk3` overrides it for one launch.
An explicitly requested unavailable backend reports an error.

Both platforms require the separately installed MNXFile engine (`libmnxfile.so.1`
or `MNXFile.Native.dll`). No engine/codecs are embedded in the GUI. No installed .NET
runtime is needed by either release. **Windows WinForms is self-contained, not
Native AOT**; the Linux GUI and native engine use AOT.
See [Linux runtime dependencies](linux/DUAL-DEPENDENCIES.txt) for toolkit, OpenSSL and
optional desktop-keyring requirements.

## Build from source

Install the **.NET 10 SDK**. Linux builds need Python3.12+, Git, `flock`, `clang`,
a C/C++ toolchain and zlib development headers. Windows builds need Visual Studio
C++ build tools and a Windows SDK for the independent engine build.

The client package is pinned to **`Mash.MNXFile.Client` `1.2.0-alpha.3`**, which is
not yet published on NuGet.org. Build the matching engine/client dependency first:

```sh
git clone https://github.com/Mashrien/MNXFile.git
git clone https://github.com/Mashrien/MNXArchiver.git
(cd MNXFile && MNXBuildNumber=3 ./build.sh --test)
cd MNXArchiver
export MNXFILE_NATIVE_LIBRARY="$(realpath ../MNX_Builds/mnxfile/linux-x64/libmnxfile.so.1)"
./build.sh --test
```

`MNXPackageFeed=/absolute/path/to/feed ./build.sh --test` selects another feed;
the default is `../MNX_Builds/mnxfile/packages`. Once the client package is available,
this repository builds without the engine source tree. The first Linux build
fetches the pinned toolkit sources in [linux/dependencies.json](linux/dependencies.json)
and applies the checked-in patches. MWin source is included as ordinary source files;
**no submodule initialization is required**. See [MWin source notes](windows/MASHWIN-SOURCE.md).

```sh
./build.sh --clean --test    # Native Linux build with offscreen checks
./build-linux.sh --managed  # Framework-dependent development build
```

On Windows:

```powershell
$env:MNXFILE_NATIVE_LIBRARY = (Resolve-Path "..\MNX_Builds\mnxfile\win-x64\MNXFile.Native.dll").Path
.\build.cmd -Clean -Test -Runtime win-x64
```

For Visual Studio, add the client package feed to NuGet sources, open
**`MNX-GUI.Windows.sln`**, build once, then open `windows/MainForm.cs` with
**View Designer** (Shift+F7). Each form has normal `.cs`, `.Designer.cs` and `.resx`
files. Open `MNX-GUI.slnx` when working on both platform frontends.

### Designer and source layout

| Path | Purpose |
| --- | --- |
| `windows/` | Canonical WinForms/MWin forms, actions and designer resources |
| `windows/MashWin/` | Included MWin source snapshot, version0.0.7.2 |
| `linux/MWin/` | Portable MWin adaptation for the GTK4/Skia backend |
| `linux/patches/`, `linux/overrides/` | Pinned toolkit patches and portable GUI adjustments |
| `linux/Backends/`, `linux/Launcher/` | Dependency probes, backend policy and entry point |
| `src/` | Native GTK3 frontend/UI file, shared settings, preview/transfer logic and icons |
| `tests/` | Backend/INI checks; frontend runtime smoke modes live with each frontend |
| `packaging/` | Desktop entry, MIME data and Dolphin menu resources |

A Linux build translates current Windows designer sources automatically. New
Windows-only controls/APIs may need portable support. Native GTK3 has its own layout.
Do not edit generated sources under `mnx_clutter/build/`.

## Outputs and versions

Current GUI version is **`1.4.1.N`**; `BuildNumber.txt` supplies an automatic build
counter. Set `MNXBuildNumber=N` to reproduce a specific counter. Output remains:

- `../MNX_Builds/mnx-gui/linux-x64/` or `win-x64/`: component release.
- `../MNX_Builds/binaries-final/`: flat Linux executables/companion-library staging.
- `../mnx_clutter/build/mnx-gui-dual/`: unified Linux intermediates/evidence.
- `../mnx_clutter/build/mnx-gui-port/`: downloaded toolkit/generated portable sources.

`MNXReleaseRoot`, `MNXBuildRoot`, `MNXPortableRoot` and `MNXFinalRoot` override paths.
Builds do not install programs or register context menus.

## Settings and previews

Settings are plain INI under `~/.config/mnxarchiver/` on Linux or
`%APPDATA%\mnxarchiver\` on Windows. The main file is `mnxarchiver.conf`; MWin's
editable palette is `mnx.mwin-colors.conf`. See [INI-CONFIG.md](docs/INI-CONFIG.md).

For example:

```ini
[MNX.Display]
Backend=auto
OpacityPercent=100

[MNX.Previews]
Enabled=true
Trigger=Single-Click
BufferMiB=32
```

Preview input buffers default to32 MiB (16–128 configurable). Decoded-image memory
has separate limits:512 MiB per image and1024 MiB across previews by default. At most
10 previews can be open concurrently; closing one releases its decoder/stream and
slot. Hover delay is500–2000ms, default750ms. Previews stay in memory; external source
viewing is disabled. GIF/animation preview is experimental and requires opt-in.

Tooltips stay inside the manager. Dialog previews have fixed minimal title/X chrome;
Window previews retain normal window controls. Right of Main Window attaches previews
to its right edge. Wayland can constrain popup placement and controls independent
window positions; X11 right Window previews position before becoming visible.
Opacity applies to owned forms/previews; errors remain fully opaque. System file
pickers remain OS-managed. Optional saved defaults/queue key export are explicitly
opt-in convenience features; consult [MNXFile's queue specification](https://github.com/Mashrien/MNXFile/blob/master/MNQ_FORMAT.md).

### Unsupported development/testing settings

For development or controlled testing only, manually add this section to
`mnxarchiver.conf`. The three memory limits below are the **hard maximum values**, in MiB:

```ini
[MNX.Previews.OVERRIDE]
BufferMiB=512
DecodedMiB=5120
TotalMiB=8192
DebugFlags=1
```

`DebugFlags` is a byte of bitflags (0–255); bit 0 (`1 << 0`, decimal `1`)
requests half-width, half-height image decoding, rounding odd dimensions up.
Unset/zero keeps full resolution; other bits are reserved. GIFs decode directly
to the selected size and cache every composited frame in RAM, so later loops do
not decode again. Half resolution uses roughly one-quarter of the pixel cache.
Static formats that cannot decode at reduced size use a temporary full-size
raster; that allocation is included in the memory check. No preview temp files
are created. GIF caches are released when their preview closes.

These unsupported overrides can substantially increase memory use. Options keeps
the ordinary 128/1024/2048 MiB ranges, disables overridden controls and shows the
effective values in red. **revert** removes the entire override section and exits
the program. This setting is intentionally absent from shipped user documentation
and cannot be enabled or edited through the GUI.

## Desktop integration and tests

Package resources use the installed `mnx-gui` name, not a developer's shell alias.
Install the desktop file to `share/applications`, MIME XML to `share/mime/packages`,
the PNG to `share/pixmaps/org.mash.MNXFile.png`, and service menus
to `share/kio/servicemenus`. Refresh the distribution's desktop/MIME caches.
[packaging/OWNERSHIP.md](packaging/OWNERSHIP.md) documents package ownership.
[Packaging scripts](packaging/README.md) create RPM/DEB and portable releases with
these resources and desktop-cache hooks.

`./build.sh --test` runs backend/settings and offscreen MWin checks. Actual frontend
checks need a display session or Xvfb:

```sh
MNXFILE_NATIVE_LIBRARY=/absolute/path/libmnxfile.so.1 \
  mnx-gui --backend=mwin --smoke-test /tmp/mnx-mwin-check
MNXFILE_NATIVE_LIBRARY=/absolute/path/libmnxfile.so.1 \
  mnx-gui --backend=gtk3 --smoke-test /tmp/mnx-gtk3-check
```

[Validation notes](docs/LINUX-DUAL-BACKEND-VALIDATION.md) separate tested behavior,
known limits and unverified platforms. Linux x64 is the primary test target;
The initial Windows x64 release was built and exercised in Windows10, including
engine/client/C API/CLI and all nine WinForms smoke sections. Native ARM64 runtime
behavior is not validated here.

## License

Copyright Mashrien. This project's original code is licensed under
[GNU GPL version 3](LICENSE) (`GPL-3.0-only`). Bundled and downloaded dependencies
retain their own copyright and license notices.
