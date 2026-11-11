# Native archive modules

MNXArchiver 2.0 uses a shared browser and a versioned native interface for optional archive formats. A module supplies metadata and performs archive I/O. The GUI provides the folder tree, search, file picker, operation progress and capability controls.

## Install and use the RPA module

Place the library for your platform in the per-user module directory and restart MNXArchiver:

| Platform | Module | Directory |
|---|---|---|
| Linux | `MnxRpa.so` | `${XDG_CONFIG_HOME:-$HOME/.config}/mnxarchiver/plugins/` |
| Windows | `MnxRpa.dll` | `%APPDATA%\mnxarchiver\plugins\` |

Install modules you trust: native modules run inside the GUI process with your permissions. The library header and version handshake check compatibility.

Open an RPA archive with **Open**, a command line path, or drag and drop. Use **Add** to add files or folders, **Extract** to extract, and **Replace file…** in the entry menu to replace one selected file while preserving its archive path. **New Ren'Py RPA archive…** creates an archive from selected files or folders. The module supports the stock **RPA 3.0** format. Delete removes selected files or folder contents. Edits preserve the original index protocol (2–5); new archives use protocol 2. Add, Replace and Delete stream a sibling staging archive, so the destination needs space for the rewritten archive. A failed or cancelled rewrite keeps the original. Extraction publishes completed files individually. RPA derives folders from file paths, so empty folders are not stored.

RPA module 1.0.1 adds in-memory text and image previews in MNXArchiver 2.0.0.70
and later. Enable previews in Options. Ren’Py `.rpy` scripts use the source-code
preview setting, and images use the same decoder and memory limits as MNX.
The module reads a bounded subset of pickle as data and requires no Python installation. RPA 2.0 and unofficial variants are outside this module's supported range.

### Build the module

On Linux with the .NET 10 SDK and NativeAOT compiler prerequisites:

```sh
dotnet publish plugins/Rpa/MnxRpa.csproj -c Release -r linux-x64 -o /tmp/mnx-rpa-module
```

Use `linux-arm64` for an ARM64 host. On Windows with the .NET 10 SDK and Visual C++ NativeAOT toolchain:

```powershell
dotnet publish plugins/Rpa/MnxRpa.csproj -c Release -r win-x64 -o .\rpa-module
```

Copy the resulting shared library to the module directory above. Python is only needed for the independent format checks:

```sh
python3 plugins/Rpa/verify_rpa.py /tmp/mnx-rpa-module/MnxRpa.so -v
tests/PluginChecks/run-linux.sh /tmp/mnx-rpa-module/MnxRpa.so
```

The host checks use a small independently generated protocol 4 fixture under
`tests/PluginChecks/Fixtures`.

## ABI 1

The declarations are in [mnx_plugin.h](../plugins/include/mnx_plugin.h). Export the three required C functions with C calling convention and unmangled symbol names. Use UTF-8 JSON and explicit byte lengths, capped at 64 MiB in each direction. The module allocates the response; the host releases it once using `mnx_plugin_release`. A zero transport status returns a response, including application errors and cancellations. Do not retain request memory or the optional cancellation callback/state after the call. The host keeps loaded libraries until process exit.

### Discovery and compatibility

1. The host checks native ELF/PE headers, architecture, and `mnx_plugin_abi_version`.
2. `describe` returns a `format` object. It does not perform archive I/O and remains available for version diagnostics.
3. The host checks the module release against its own range, and checks the host release against the module's range. It then calls `negotiate`; the module independently checks both ranges. All archive operations repeat the module-side checks before I/O.

Every request carries `hostVersion`, `minPluginVersion`, and `maxPluginVersion`. The host release is the first three components of its assembly version, excluding the build counter. Ranges are inclusive. The 2.0 host supports module releases **1.0.0–1.0.999**; RPA module 1.0.1 supports host releases **2.0.0–2.0.999**. The archive format versions are independent:

```json
{
  "format": {
    "id": "renpy.rpa",
    "name": "Ren'Py RPA",
    "version": "1.0.1",
    "minHostVersion": "2.0.0",
    "maxHostVersion": "2.0.999",
    "extensions": [".rpa"],
    "currentVersion": "3.0",
    "versions": [
      { "version": "3.0", "magicHex": "5250412D332E3020", "actions": 63 }
    ]
  }
}
```

Signatures are a prefix of 1–64 bytes at offset zero, encoded as hex. Extensions help the picker and identify files that should receive an unsupported-format diagnostic. A signature match takes precedence over filename extension. The longest matching signature wins; an ambiguous tie is rejected. Built-in MNX signatures and the `.mnx` / `.mnq` extensions are reserved. Modules advertise optional operations per format version using the capability bits in the header.

### Requests

| `operation` | Extra request fields | Success response |
|---|---|---|
| `describe` | — | `format` descriptor |
| `negotiate` | — | Empty object |
| `open` | `archivePath` | `entries` |
| `create` | `archivePath`, `sources`, `overwrite` | `entries` |
| `add` | `archivePath`, `sources`, `folder` | `entries` |
| `replace` | `archivePath`, `entryPath`, one item in `sources` | `entries` |
| `delete` | `archivePath`, `entryPaths` | `entries` |
| `extract` | `archivePath`, `outputs`, `overwrite` | Empty object |
| `read` | `archivePath`, `entryPath`, `entrySize` | `bytesRead` and entry bytes in the supplied buffer |

Archive and source disk paths are absolute. `sources` may contain files or directories for Add/Create. `folder` and `entryPath` are slash-separated archive paths, with an empty `folder` denoting the root. Return file entries as `{ "path": "folder/name", "size": 42 }`, with nonnegative 64-bit byte sizes; the host derives directories. Each output maps an archive `entryPath` to an absolute disk `destination`. An error response uses `error` with a readable message; cancellation uses `cancelled: true`.

The module owns path validation and all payload I/O. Poll the optional cancellation callback during long work and before committing a mutation; a nonzero return requests cancellation. Return the updated listing after a successful mutation. Add must reject duplicate archive paths; Replace keeps the selected path. Delete takes a list of exact file paths; the host expands selected directories. Stage writes before publishing and reject malformed lengths, unsafe paths and destination links.

### Optional in-memory entry reads

Advertise `MNX_PLUGIN_READ_ENTRY` for each format version that supports previews
and implement `mnx_plugin_read_entry`. The three required ABI 1 exports and
their request and response shapes still work with older hosts and modules.

The host sends a `read` request to this export with the full indexed byte count
in `entrySize` and a separate writable buffer. Check the indexed byte count
against the current entry and fill exactly `destination_length` bytes from the
start of that entry. The host may request a prefix for text, or the full entry for
images. Reject a buffer length larger than the entry or 512 MiB. A zero-length
buffer may be null. Return `bytesRead` only on success. Report errors and
cancellation through the normal JSON response. The host owns the entry buffer
for the duration of the call and releases the JSON response through
`mnx_plugin_release`. A module must never retain either pointer.
