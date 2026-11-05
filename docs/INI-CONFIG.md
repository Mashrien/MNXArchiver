# Editable configuration

The GUI uses UTF-8 INI files, read and written entirely in managed C#.
No ProfileString, registry, Win32, GTK, or desktop service is required by the reader.

Normal Linux directory: `$XDG_CONFIG_HOME/mnxarchiver`, or `~/.config/mnxarchiver`.
Main settings: `mnxarchiver.conf`. MWin palette: `mnx.mwin-colors.conf`.

Main settings use sections by context:

- `[MNX.Storage]`: temporary-directory selection.
- `[MNX.Display]`: interface backend and opacity.
- `[MNX.Display.MWin]`: use saved desktop colors (`UseDesktopColors=true`).
- `[MNX.Compression]`: default compressor, enable flag, level.
- `[MNX.Encryption]`: default encryption enable flag; no password stored here.
- `[MNX.Previews]`, `[MNX.Previews.Text]`, `[MNX.Previews.Images]`,
  `[MNX.Previews.Display]`: preview activation, limits, types and presentation.

The palette file has its own `[MNX.Display.MWin]` section, for example:

```ini
[MNX.Display.MWin]
WindowBackground=#202326
WindowText=#FCFCFC
ViewBackground=#141618
ViewText=#FCFCFC
ButtonBackground=#292C30
ButtonText=#FCFCFC
SelectionBackground=#3DAEE9
SelectionText=#FCFCFC
Accent=#3DAEE9
Border=#808080
DisabledText=#A1A9B1
TitleBackground=#272C31
TitleText=#FCFCFC

[MNX.Display.MWin.Menus]
BarBackground=#202326
Background=#202326
Text=#FCFCFC
DisabledText=#A1A9B1
HoverBackground=#3DAEE9
HoverText=#FCFCFC
PressedBackground=#3DAEE9
PressedText=#FCFCFC
CheckedBackground=#3DAEE9
CheckedText=#FCFCFC
ImageMargin=#202326
Border=#808080
Separator=#808080
```

`#RRGGBB` or `red,green,blue` is accepted. MWin creates the palette from the desktop
only when the file is missing. KDE's kdeglobals takes precedence on KDE; GTK4 named
colors are the fallback, then readable built-in colors. Manual edits are preserved.
The `Menus` section controls the menu bar, its popup and nested menus, context
menus and the shared toolbar item states. Missing or invalid menu values inherit
the corresponding window, selection or border color from the main palette, so an
older dark palette stays dark without being rewritten. New palette files include
all menu keys. `ImageMargin` is the icon/checkmark gutter; `Separator` controls the
rules between menu groups.

Restart after editing the palette or changing the colors switch. Delete the palette
file to recapture a new desktop theme. Unreadable/malformed individual colors use
fallback values; reading the file never overwrites its contents.

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
