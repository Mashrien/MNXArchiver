# MNX GTK interface scaffold

A GTK3 `.ui` form, loaded by C# with GtkSharp. The main window follows the familiar
archive manager layout: menus, large icon buttons, a path bar, an expandable folder
tree on the left, the current folder's contents on the right, and a status footer.
The initial divider is 30/70; it can be dragged, and both panes expand when resized.

## Run

From the MNX-GUI repository directory:

```sh
dotnet run --project src/MNXFile.Gui.csproj
dotnet run --project src/MNXFile.Gui.csproj -- --preview
dotnet run --project src/MNXFile.Gui.csproj -- /path/to/archive.mnx
```

`--preview` uses sample metadata held in memory. It does not create an archive or
duplicate files. Normal startup displays an empty browser until an archive is opened.

## What is connected

- Open/Close, encrypted archive passphrase prompt, and archive information.
- Folder tree expansion and selection; list double-click opens folders.
- Parent/root buttons and an editable archive path bar (Enter navigates).
- Multiple selection, sortable columns, selection count/size, and button sensitivity.
- Ctrl+O opens an archive, Ctrl+L focuses its internal path, and Alt+Up goes to its parent.
- Startup accepts paths (including spaces), local `file://` URIs, or `-- PATH...`.
  A single archive is identified by its four-byte MNX header signature, even without
  an extension; ordinary files/folders open an archive configuration dialog. Its prefilled output field
  has a Browse button; compression can be disabled or set to Snappy, LZ4, or Brotli
  (with an effort level), and encryption requires two matching passwords.
  Multiple inputs are scanned together for duplicate names and read access before writing.
  New archives use version 7 and standard 32 KB chunks. Names are preserved, including
  long Unicode names and OS-valid punctuation. Output must be outside the input folders.
  Replacing an existing output requires confirmation and uses a sibling temporary file
  so failure or force-cancellation leaves the previous output intact.
- **File → Resume from queue…**, or opening an MNQ1 queue as a startup argument,
  validates and continues the unfinished archive using its existing compression and
  encryption settings. Encrypted archives ask for their original password. Completed
  queue files are retained under a completed filename.
- Right-click a file/folder row in either pane for **Extract…**. One file opens a Save
  dialog (so it can be renamed); folders/multiple entries use a destination-folder dialog.
  Folder extraction includes the selected folder and all descendants. The Extract toolbar
  and main menu use the same dialogs. Existing file replacement requires confirmation,
  including when extracting folders or multiple entries; declining keeps existing files.
- Drop a single signature-identified MNX archive onto the browser to open it.
- Drop files/folders onto the list background to add them to the current folder;
  drop onto a folder row in either pane to add them there. Folder contents are recursive.
- Drag selected files/folders from the list, or a folder from the tree, into a file manager.
  These are copy operations: source files and archived entries are retained.
- Transfers use one I/O worker and display an owned progress dialog while hiding the
  main window. It shows current-file and overall bytes, percentages, file counts, and
  input-piece counts. Pieces are chunk-sized input units; compression means they are
  not an advance prediction of physical output chunks.
- Cancel opens two choices: **Keep current progress** finishes the current file,
  stops before the next one, and keeps a valid archive of completed entries. It writes
  `<archive basename>.mnq` alongside the archive with the remaining file/folder list,
  destination IDs, source paths/sizes/times, and archive/queue checksums. See
  [MNQ_FORMAT.md](../MNQ_FORMAT.md) for the resume interface.
  The unchecked **Export key-file** option exports a matching .mnx-key for encrypted archives only when keeping progress. Resume uses the password automatically and randomly overwrites, flushes and deletes the key after validation, before adding remaining files.
  **Delete partial archive** cancels at the next bounded I/O checkpoint and removes
  the partial archive. During an add to an existing archive, this explicitly deletes
  the entire archive, including preexisting entries. Closing the progress window opens
  the same question. There is no double-click cancellation gesture.
- Directory browsing uses only the library's front index; no file payloads are read.
- Extraction shows an owned **MNX Extraction Progress** dialog with current-file and
  whole-selection totals. The main window is hidden and disabled until the operation
  returns, including for file-manager actions and outbound drags. Its Cancel button
  offers **Stop after current file** or **Stop now**. The first finishes the current
  output and stops before the next file; the second deletes only the unfinished output.
  Earlier completed files and the source archive are kept. Extraction writes directly
  to final output paths, with no temporary staging files. If replacing an existing file,
  stopping now deletes that current output rather than restoring its previous contents.

New/Add/Test/Delete/Compact raise `MainWindow.ActionRequested` with the
current folder ID and selected entry IDs. Their archive operations are intentionally
unconnected in this interface pass. With no subscriber, the status footer says so.
Drag-and-drop, startup creation, and extraction are connected directly to MNX's methods.
The remaining toolbar hooks are available for your later dialogs. The progress bar is hidden while idle.
Archive changes are serialized, and closing during a transfer waits for its completion.
A multi-file import may have committed earlier entries if a later entry fails; the view
refreshes to show what was actually added. Existing names are never replaced by a drop.
Internal rearrangement between this GUI's panes is not implemented.

## File-manager integration

Register a published native executable with Linux application menus and **Open With**:

```sh
python3 scripts/install-gui-desktop.py /absolute/path/to/mnx-gui
```

This installs a per-user `MNX Archive Manager` desktop entry, the MNX MIME type,
and a Dolphin **MNX Archive** submenu. Archive files offer **Open MNX Archive**,
**Extract Here**, and **Extract To…**; other files and folders offer **Add to MNX Archive**.
Extract Here places the root contents beside the archive; Extract To opens a folder
chooser. These actions verify the file signature before opening an archive and ask
for its passphrase if encrypted.
The launcher passes the selected filenames using `%F`; existing default applications are preserved.
Windows can pass an archive filename through an Open With registration as well.

The extraction actions can also be called directly:

```sh
mnx-gui --extract-here -- /path/to/archive.mnx
mnx-gui --extract-to -- /path/to/archive.mnx
```

Outbound drag uses GTK's `text/uri-list` protocol, supported by file managers on Linux
and Windows. Since the destination needs filesystem paths, entries are streamed to a
private disk directory under the user's local application data (`mnx-gui/drag-files`).
No file payload is loaded wholesale into RAM. Large selections need enough disk space
for the extracted selection and take time to prepare; desktop DnD timeouts may limit
very long preparations. Cancelled drags with prepared files can leave temporary data.
Files survive drag completion and GUI exit so asynchronous destination copies can finish.
A later GUI export cleans up abandoned transfers older than 24 hours; live GUI sessions
hold leases that protect their files. Exporting an encrypted archive produces plaintext
files, as ordinary extraction does.

## Application and archive icon

`archive_100x100.png` in the project root is the shared icon source. The GUI embeds
this original PNG, so it needs no external icon file beside the executable. Linux
registration installs the same PNG for the launcher and `application/x-mnx` files;
GTK and the desktop scale it as needed. `archive.ico` wraps the original PNG for
Windows executable resources, with no changes to its pixels. Windows build/runtime
verification remains pending.

## Customize

- **MainWindow.ui:** controls, labels, menus, toolbar icons, packing, and resizing.
  Open with Glade for GTK3 visual editing. Stable IDs connect controls to C#.
- **MainWindow.cs:** signal hookups, navigation, archive opening, and action routing.
- **ArchiveBrowser.cs:** metadata adapter and the sample layout preview.
- **style.css:** modest toolbar/footer styling. Native GTK styling handles the rest.
- **Program.cs:** application startup.

The right-hand list uses GTK's `TreeView` with a flat `ListStore` (GTK3's list view).
The left-hand tree lazily creates rows for expanded folders. Size and date columns
sort by numeric metadata rather than displayed strings. `[CES]` uses fixed indicator
positions; spanned storage is reserved and currently blank.

## Windows and Linux

This uses platform-neutral .NET 10 and GtkSharp, with no WinForms dependency. Both
platforms need the **GTK3** native runtime, not GTK4. Linux can use its distribution's
GTK3 installation. A Windows release must bundle matching GTK3 DLLs and dependencies,
including icon/theme assets, as described in the GTK Windows packaging guide:
https://www.gtk.org/docs/installations/windows/

```sh
dotnet publish src/MNXFile.Gui.csproj -c Release -r linux-x64 --self-contained true
dotnet publish src/MNXFile.Gui.csproj -c Release -r win-x64 --self-contained true
```

Self-contained publishing includes .NET; it does not supply GTK's native libraries.

## Native AOT

From the project root, `./scripts/publish-gui.sh` (Linux) or
`./scripts/publish-gui.ps1` (Windows) publishes the current native GUI.
Build outputs are redirected by `Directory.Build.props` into `../mnx_clutter/build/`;
published programs are in `../MNX_Builds/`.

The GUI can also be published as a native executable with no installed .NET runtime:

```sh
dotnet publish src/MNXFile.Gui.csproj -p:PublishProfile=NativeLinux -o ../MNX_Builds
# Run on a Windows build machine with the Visual Studio C++ build tools installed:
dotnet publish src/MNXFile.Gui.csproj -p:PublishProfile=NativeWindows -o ../MNX_Builds
```

The `.ui` and CSS are embedded in published executables. Development builds still
copy editable sidecars; if sidecars are present they override embedded assets.

GtkSharp discovers widget constructors and signal arguments through reflection.
The project preserves the seven GTK binding assemblies when `PublishAot=true`.
Without that preservation, a successful native compilation crashes at startup.
The Linux native build passes the layout/navigation/encrypted-archive check;
Windows AOT and GTK runtime packaging still need a Windows run check.
GtkSharp/GLibSharp report trim/AOT analysis warnings, so new controls and handlers
also need verification with the native executable.

GTK's native runtime remains external. See **DEPENDENCIES.txt** for package names,
Windows DLL/data requirements, and this host's complete linked-library list.
No separate native Snappy/LZ4 packages are needed. Native AOT compiles the managed
archive codecs into the executable.

## Integration check

```sh
xvfb-run -a -s "-screen 0 1920x1080x24" dotnet run --project src/MNXFile.Gui.csproj -- --smoke-test /tmp/mnx-gui-shots
```

This exercises the actual GTK form and navigation signals, verifies the initial split
and resize behavior, opens a disposable encrypted Snappy archive, uses a second GTK window and synthetic
mouse gestures to test imports/exports, folder targeting, and right-click extraction.
It also drives actual save/folder dialogs to test creation, cancellation, and byte-exact
extraction, and writes two PNG
screenshots. The tiny temporary archive is removed after the check.

## Options and in-memory previews

Open **Help → Options…** for temporary-directory overrides, new-archive
compression/encryption defaults and experimental previews. Saving creates
`~/.config/mnxarchiver/mnxarchiver.conf` on Linux (`XDG_CONFIG_HOME` is honored),
or `%APPDATA%\mnxarchiver\mnxarchiver.conf` on Windows. It is a plain INI file;
see `Settings/mnxarchiver.conf.example` for keys and permitted values.
Unknown keys survive saves. Defaults never change an existing archive's codec
or encryption. Output staging remains alongside the target archive for atomic
replacement; the temporary-directory option controls file-manager drag exports.

Saved passwords are opt-in, with the warning shown in Options. **STORE DEFAULT**
writes AES-256-GCM ciphertext to `.mnxdefaultkey`, using a random 256-bit key
held in the Linux desktop keyring, or wrapped with Windows current-user DPAPI.
On Linux this feature requires `secret-tool` (Fedora: `libsecret`, Debian/Ubuntu:
`libsecret-tools`) and an unlocked Secret Service wallet. There is no plaintext
or machine-ID fallback. Copying the ciphertext alone to another account/machine
will not recover the password. Store and Clear are explicit immediate actions;
Clear disables default encryption even when Options is subsequently cancelled.
Clear overwrites the key file with random bytes, flushes and deletes it, and
removes its keyring entry. Filesystem snapshots and flash storage can retain
older blocks; this operation is not a guarantee of physical erasure.

Previews are disabled initially. Enable them, choose double-click or delayed
hover, and select the text/image categories. Folder double-click still navigates.
Right-click entries for **Preview**, **Extract**, **Delete**, or **Properties**.
Delete confirms and removes only archived entries; compact to reclaim holes.
Text is read-only, without markup execution. Common source-code extensions and
extensionless README/LICENSE/Makefile/Dockerfile names are recognized. Additional
extensions can be entered as comma-separated tokens. External source viewing is
visible but deliberately disabled until implemented.

Image decoding uses installed GTK loaders: PNG, JPEG, BMP, TIFF, ICO, PNM, TGA,
static WebP when available, and experimental GIF animation. There are no extra
image packages required for PNG/JPEG/GIF. Unsupported decoder/format errors leave
the archive untouched. The input buffer is configurable from **16–128 MiB per file**, default **32 MiB**, text display is
limited to **2 MiB**, decoded image/animation memory to **64 MiB**, combined open
previews to **256 MiB**, and the window count to **10**. GIF frame counts and
canvas sizes are checked before decoding. A preview never extracts to a temporary
file. Buffers, decoders and animation timers are released when closed. Pending
reads cancel before archive mutation/replacement; long reads run off the GTK thread.

A Tooltip is an undecorated, control-free window whose top-left is placed at
File, Cursor or Bottom-Right. With delayed hover it disappears when the cursor
leaves that exact item; with double-click it closes on any click. Dialog previews
close on any click or focus loss. Clicking an image closes its owning preview.
Window previews positioned relative to Current Screen are independent, permitting
multiple simultaneous windows; all are tracked and closed on archive replacement
or application exit. GTK/Wayland compositor policies can constrain absolute
window placement. Scale fits with aspect preserved, Zoom fills/crops with aspect
preserved, Stretch fills both axes, and Center keeps original pixels centered.


When opening a single `.mnx` argument, startup always attempts real archive parsing.
An invalid or unreadable archive displays the filename, exception message and any
named parser return flags, then exits after dismissal. It never becomes an implicit
"add this file" request. Signatures still recognize valid archives without `.mnx`.

Release build numbers increment automatically; see `../START-HERE.md` and
`../BuildNumber.txt`. The About dialog includes the compiled build number.

## Owned icons

The toolbar, navigation, file rows, menus, drag indicator and message dialogs
use embedded PNG resources at **16, 24, 32 and 48 px**. No desktop icon theme or
runtime SVG renderer is required for MNX's icons. The existing application and
archive-file artwork is retained. Standard GTK file choosers can still use the
system's theme. Desktop integration installs the extraction action as PNGs too.

Editable SVG originals, the supplied-to-project name mapping and MIT attribution
are in [Icons/](Icons/README.md). Run `python3 scripts/render-icons.py` from the
project root to regenerate PNGs (developer tools: CairoSVG and Pillow), or add
`--check` to validate their hashes using only Python's standard library. Normal
builds embed the committed PNGs and do not run an SVG renderer. Native GUI
publish scripts copy `MNXFile.Icons.LICENSE.txt` alongside the release.

## Globalization and native cryptography

Standalone GUI, CLI and shared-library builds use `InvariantGlobalization` and
need no ICU runtime. Unicode names, Unicode case-insensitive search and text
previews remain supported. File-list byte counts use comma grouping; dates use
`dd-MM-yyyy`. These are fixed formats, independent of the desktop's region.
.NET named-culture data is unavailable in the standalone builds. GTK still uses
its own locale support for native controls. The managed archive library leaves
globalization policy to its host application.

Linux retains `openssl-libs` for the `libcrypto` implementation of AES-256-GCM,
PBKDF2/HKDF and SHA-256. Integrity checks also use it for unencrypted archives.
Windows uses its operating-system cryptography instead of the Linux package.
