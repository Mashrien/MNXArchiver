# Paged navigation and diagnostics

MNXArchiver 2.1.2 uses MNXFile's independently authenticated front index pages.
Opening a wire8 archive and browsing a folder does not load every indexed file.
Payload streams remain separate; browse operations never read file contents.

The current folder and its parent's immediate child listings are pinned. Recent
history is optional: at most eight folder listings, 4,096 nonpinned entries and an
estimated 4 MiB. The single last-visited folder has priority over older history,
but is evicted if it alone exceeds that budget. Individual ancestry lookups have
a separate 512-entry limit. The active listings themselves must still fit in
memory; opening a single enormous folder can require substantial memory.

Import collision checks read only destination folders. Extraction planning reads
only the selected entries, their ancestors and selected directory subtrees. Plans
and pending edit batches still retain metadata for their own work. Archive modules
may retain their existing full snapshots; the new paged format belongs to MNXFile.

The engine's physical archive limit is 140,737,488,322,560 bytes including the
header and index. Whole-file sizes, record offsets and index lengths are 64-bit;
chunk numbers stay 32-bit. The GUI uses checked signed 64-bit values at .NET
Stream and progress boundaries, safely above this limit. Wire7 remains readable;
its encrypted index needs a transient whole-index authentication buffer during
conversion. New and modified archives use paged wire8 (format 0.1.8).

## Help menus

**About MNX Archiver** lists the actual loaded engine format, built-in functions
and installed providers. **Plugins Folder** opens the provider directory and
watches it for additions or replacements for up to 15 minutes. A change reveals
**Reload**. Restart preserves an existing archive path and reopens About, with
new/updated rows in green. Passwords are not persisted for this restart; encrypted
archives prompt again. Plugin compatibility checks remain unchanged.

**Debug Window** provides a color-coded severity/caller/message list modeled on
PackRat's debug view. A bounded 4,000-record history and batched UI timer keep
logging out of the per-chunk transfer path. Closing hides the view so it can be
reopened; application shutdown disposes it. Errors include useful bounded messages
with common credential assignments redacted. No automatic log files are written.

## Dialog geometry

Dragging an owned MWin dialog by its title bar preserves its offset from the main
window on Windows and X11. On KDE Plasma Wayland, the dialog registers its own
window pair with KWin on the first title press. KWin moves the owner throughout
the gesture and releases the registration when the dialog closes. Other Wayland
compositors control top-level placement independently.

The status divider follows `MenuSeparator.Thickness`. Portable MWin extends the
status strip's top edge through both corner gaps. Fixed dialogs reserve room for
the rounded outline below docked content.
