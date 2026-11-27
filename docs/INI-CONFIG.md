# Editable configuration

The GUI uses UTF-8 INI files, read and written entirely in managed C#.
No ProfileString, registry, Win32, GTK, or desktop service is required by the reader.

On Linux, `mnxarchiver.conf` and `gui.mwin.drawing.conf` are stored under
`$XDG_CONFIG_HOME/mnxarchiver` or `~/.config/mnxarchiver`. On Windows, they are
stored under `%APPDATA%\mnxarchiver`.

Main settings use sections by context:

- `[MNX.Storage]`: temporary-directory selection.
- `[MNX.Display]`: interface backend and opacity.
- `[MNX.Display.MWin]`: use saved desktop colors (`UseDesktopColors=true`).
- `[MNX.Compression]`: default compressor, enable flag, level.
- `[MNX.Encryption]`: default encryption enable flag; no password stored here.
- `[MNX.Previews]`, `[MNX.Previews.Text]`, `[MNX.Previews.Images]`,
  `[MNX.Previews.Display]`: preview activation, limits, types and presentation.

## MWin drawing settings

`gui.mwin.drawing.conf` groups settings by what they draw.

- `[mnx.display.mwin.window]` covers the frame, title and status areas, dividers and caption buttons. `wmTitleScale` multiplies both `wmTitleHeight` (pixels) and `Color.tfSize` (points). The default of 4/3 gives a 35-pixel bar and a 12-point semibold title. Set the scale to `1` for literal sizes.
- `[mnx.display.mwin.buttons]` covers the main toolbar buttons' colors, fonts and rounded borders. Home and Up use the navigation text settings.
- `[mnx.display.mwin.text]` sets the navigation and menu text font, size and color. If `Color` is omitted, the menu palette supplies the normal menu text color.
- `[mnx.display.mwin.files]` covers the folder tree, file list and column headers.
- `[mnx.display.mwin.menus]` overrides menu, context menu and toolbar item colors. `ImageMargin` colors the icon and checkmark gutter, and `Separator` colors the rules between items.

Use `#RRGGBB` for solid colors. The caption highlight and pressed states also
accept two hex colors separated by a comma, with the center color first and
the edge color second. A blank `Color.wmDivs` inherits `Color.wmBorder`. For example:

```ini
[mnx.display.mwin.window]
Color.wmBack=#202326
controlBox.Exit.Colors.Highlight=#DB7093
controlBox.Exit.Colors.Pressed=#8B0000,#000000
```

MNX Archiver creates the drawing file on first use. It carries over colors from an
existing `mnx.mwin-colors.conf` and leaves that file available as a backup.
On a fresh installation, Windows uses its system colors. Linux tries KDE
`kdeglobals`, GTK4 named colors and the built-in defaults, in that order.
Legacy `red,green,blue` values are also accepted for solid colors. Individual
invalid settings use defaults.

Restart MNX after editing the drawing file or changing the colors switch. To
capture new desktop colors, move both files out of the configuration directory
before restarting.

## Portable INI_Config class

Source: `src/Settings/INI_Config.cs`. It uses only .NET base libraries.
The public class can also be copied into another project.

```csharp
INI_Config config = INI_Config.Load("application.conf");
INI_Config.Section compression = config["MNX.Compression"];
bool enabled = compression.GetBool("Enabled", true);
int level = compression.GetInt("Level", 4);
compression.Set("Enabled", true);
compression.Set("Level", 5);
config["MNX.Example"].SetList("Sizes", new int[] { 32, 64, 128 });
IReadOnlyList<int> sizes = config["MNX.Example"].GetList<int>("Sizes");
config.Save("application.conf");
```

Typed access: GetString, GetBool, GetByte, GetShort, GetInt, GetLong,
GetLongLong (Int128), GetFloat, GetDouble, GetDecimal and Get<T> for IParsable types.
GetList<T>/SetList use semicolons; quotes preserve semicolons, quotation marks and
leading/trailing spaces inside a string item. Empty lists and empty strings differ.
Numbers use invariant culture; missing, invalid and overflowing values use the supplied
fallback. Section/key names are case-insensitive. Lines beginning with `;` or `#`
are comments. Scalar strings retain literal semicolons, `#` and `=`; inline comments
are not interpreted by the generic reader. Palette values allow a trailing `;` comment.

Unknown section/key values survive saves, though formatting and comments are regenerated.
Writes use a private sibling file and rename after flushing; failed writes preserve the
previous file. Names/values cannot inject extra sections or lines. The default size limit
is 1 MiB. Unsupported nested KConfig headers and their contents are ignored on import.
Existing MNX settings sections are migrated when next saved; newer categorized keys win.


## Preview choices

In `[MNX.Previews]`, `Trigger` accepts `Single-Click`, `Double-Click` or
`Mouse-Over With Delay` (delay remains bounded to 500–2000 ms).
In `[MNX.Previews.Display]`, `Origin` accepts `Center`, `Top-Left` or
`Right of Main Window` for Dialog/Window presentation. Tooltip `Anchor` remains
`File`, `Cursor` or `Bottom-Right`, with its bounds limited to the main window.
`WindowType=Dialog` uses fixed minimal chrome; `WindowType=Window` retains standard
MWin/window controls. Configured opacity is restored after X11 right-side placement.
