# MNX icon assets

`Source/` contains 22 editable SVGs. `Rendered/` contains their PNGs at **16, 24,
32 and 48 px only**. The GUI embeds the PNGs and caches decoded pixels; it does
not interpret SVGs or look up its icons in a desktop theme. The application's
existing `archive_100x100.png` and Windows `archive.ico` remain unchanged.

## Coverage

| Area | Icons |
| --- | --- |
| Toolbar | new, open, add, extract, test, delete, compact, info |
| Menus and navigation | recheck, options, up, home |
| File and folder rows | file, folder, existing archive icon |
| Entry context menu | extract, search (preview), delete, info (properties) |
| Messages and drag indicator | question, warning, delete (error), info, save |
| Additional supplied artwork | alert, extract-plain, new-folder, sliders |

The supplied definitions are kept as SVGs; `sources.json` records their
original filenames and modifications. Extract replaces the supplied fast-forward
with the new-folder artwork and a green Up arrow at 75 percent size, rotated 90 degrees
clockwise at the top-right, following the project owner's design. A white halo
and dark green edge keep it distinct from the blue papers and gold folder. The plain file
is derived from the supplied open-file icon; folder, Up and Home are matching additions using its blue/gold palette and rounded
shapes. The supplied `test` and `recheck` artwork intentionally share a design.

## Regeneration

From the project root:

```sh
python3 scripts/render-icons.py
python3 scripts/render-icons.py --check
```

Regeneration needs Python, CairoSVG and Pillow. The hash check needs only Python's
standard library. `rendered.json` records the original and PNG hashes. Normal
.NET builds use the committed PNGs, without these rendering tools. Sizes are
16 px for menus/rows, 24 px for navigation, 32 px for drag images and 48 px for
the toolbar and messages.

## License

See `LICENSE.txt` for MIT attribution. The supplied artwork includes Microsoft
Fluent-style assets; the upstream MIT notice is retained:
https://github.com/microsoft/fluentui-system-icons/blob/main/LICENSE

The license is embedded in the executable and also copied into native GUI
release output as `MNXFile.Icons.LICENSE.txt`.
