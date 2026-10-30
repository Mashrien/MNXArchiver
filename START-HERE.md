# MNX-GUI: start here

Open **`MNX-GUI.Windows.sln`** in Visual Studio for Windows Forms editing (build once, then Shift+F7 on `MainForm.cs`). Open `MNX-GUI.slnx` for both Windows and Linux projects. Read [README.md](README.md) for build and runtime setup.

Run `./build.sh` on Linux or `build.cmd` (or `./build.ps1`) on Windows. Output is `../MNX_Builds/mnx-gui`; intermediates are `../mnx_clutter/build/mnx-gui`. Each repository owns its build counter. Use `MNXBuildNumber=26` to select an explicit counter without advancing it; the version prefix comes from `Directory.Build.props`.

The MNXFile shared engine is installed once. MNX-CLI and MNX-GUI consume its thin client NuGet package and require the installed runtime for archive operations. Their source projects do not reference the engine source.

## One Linux executable, two interfaces

`./build-linux.sh --test` (also `./build.sh --test`) publishes the NativeAOT
`../MNX_Builds/mnx-gui/linux-x64/mnx-gui` executable. It embeds both interfaces,
preferring MWin/GTK4 and falling back to GTK3. Keep its shipped native companion
libraries beside it. No installed .NET runtime is needed.

Use `--backend=mwin`, `--backend=gtk3`, or `--backend=auto` for one launch;
`--list-backends` checks availability. Save a preference in Options → General →
Interface and restart. Options now separates general settings, compression/key
settings, preview behavior, and preview presentation into four tabs.

`./build-linux.sh --managed` is the development alternative. Separate standalone
builds remain available as `build-gtk3.sh` and `build-linux-mwin.sh`.
See [linux/README.md](linux/README.md) for the portable source layout and
[the validation report](docs/LINUX-DUAL-BACKEND-VALIDATION.md) for tested behavior.
