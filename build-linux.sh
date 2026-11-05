#!/usr/bin/env bash
# Build one Linux executable containing both the MWin/GTK4 and native GTK3 interfaces.
set -euo pipefail
mnx_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)
mnx_parent=$(dirname -- "$mnx_root")
mnx_managed=false
mnx_test=false
mnx_clean=false
for mnx_argument in "$@"; do
    case "$mnx_argument" in
        --managed) mnx_managed=true ;;
        --test) mnx_test=true ;;
        --clean) mnx_clean=true ;;
        -h|--help)
            printf '%s\n' 'Usage: ./build-linux.sh [--clean] [--test] [--managed]'
            printf '%s\n' 'Default: Linux x64/arm64 NativeAOT, embedded MWin and native GTK3 interfaces.'
            printf '%s\n' '--managed: framework-dependent build with replaceable managed DLLs.'
            printf '%s\n' '--test: offscreen archive, dialogs, cancellation/resume and preview smoke tests.'
            exit 0 ;;
        *) printf 'Unknown option: %s\n' "$mnx_argument" >&2; exit 2 ;;
        esac
    done
case "$(uname -s)/$(uname -m)" in
    Linux/x86_64) mnx_rid=linux-x64 ;;
    Linux/aarch64) mnx_rid=linux-arm64 ;;
    *) printf '%s\n' 'Run this script on Linux x64 or arm64.' >&2; exit 2 ;;
    esac
for mnx_tool in dotnet git python3 flock; do
    command -v "$mnx_tool" >/dev/null || { printf 'Missing build tool: %s\n' "$mnx_tool" >&2; exit 2; }
    done
export DOTNET_PROCESSOR_COUNT=${DOTNET_PROCESSOR_COUNT:-2}
export MNXPortableRoot=${MNXPortableRoot:-"$mnx_parent/mnx_clutter/build/mnx-gui-port"}
export MNXBuildRoot=${MNXBuildRoot:-"$mnx_parent/mnx_clutter/build/mnx-gui-dual"}
export MNXReleaseRoot=${MNXReleaseRoot:-"$mnx_parent/MNX_Builds/mnx-gui"}
# Preparation replaces generated source files. Serialize builds that share either
# compiler intermediates or the pinned source cache, including managed/native modes.
mkdir -p "$MNXBuildRoot" "$MNXPortableRoot"
exec 7>"$MNXBuildRoot/.build.lock"
flock -x 7
exec 8>"$MNXPortableRoot/.build.lock"
flock -x 8
if [[ -z "${MNXBuildNumber:-}" ]]; then
    exec 9>"$mnx_root/BuildNumber.txt.lock"
    flock -x 9
    mnx_previous=$(<"$mnx_root/BuildNumber.txt")
    export MNXBuildNumber=$((mnx_previous + 1))
    if (( MNXBuildNumber < 1 || MNXBuildNumber > 65534 )); then
        printf '%s\n' 'Build number exhausted; bump the release version and reset the counter.' >&2
        exit 2
        fi
    printf '%s\n' "$MNXBuildNumber" > "$mnx_root/BuildNumber.txt.pending"
    mv -- "$mnx_root/BuildNumber.txt.pending" "$mnx_root/BuildNumber.txt"
    flock -u 9
    exec 9>&-
    fi
python3 "$mnx_root/linux/prepare.py" --root "$MNXPortableRoot"
mnx_project="$mnx_root/linux/MNXFile.Launcher.csproj"
mnx_kind=native
if $mnx_managed; then
    mnx_kind=managed
    fi
mnx_output="$MNXBuildRoot/publish/$mnx_rid-$mnx_kind"
mnx_release="$MNXReleaseRoot/$mnx_rid"
if $mnx_managed; then
    mnx_release+="-managed"
    fi
mnx_properties=(-p:EnableMobileHeads=false -p:UseSharedCompilation=false -p:TreatWarningsAsErrors=false -nodeReuse:false)
if $mnx_clean; then
    dotnet clean "$mnx_project" -c Release -r "$mnx_rid" "${mnx_properties[@]}"
    fi
if $mnx_managed; then
    dotnet publish "$mnx_project" -c Release -r "$mnx_rid" --self-contained false -o "$mnx_output" "${mnx_properties[@]}"
else
    dotnet publish "$mnx_project" -c Release -r "$mnx_rid" -p:MNXNativeAot=true -p:StripSymbols=true -o "$mnx_output" "${mnx_properties[@]}"
    fi
if $mnx_test; then
    dotnet run --project "$mnx_root/tests/BackendChecks/BackendChecks.csproj" -c Release -p:UseSharedCompilation=false
    dotnet run --project "$mnx_root/tests/MenuRenderingChecks/MenuRenderingChecks.csproj" -c Release "${mnx_properties[@]}" -- "$MNXBuildRoot/evidence/menu-colors-$MNXBuildNumber"
    "$mnx_output/mnx-gui" --list-backends
    if "$mnx_output/mnx-gui" --probe-backend mwin >/dev/null 2>&1; then
        "$mnx_output/mnx-gui" --backend=mwin --headless-smoke "$MNXBuildRoot/evidence/$mnx_rid-$mnx_kind-$MNXBuildNumber"
    else
        printf '%s\n' 'GTK4/Skia unavailable: MWin offscreen checks skipped. GTK3 desktop checks require a test display.'
        fi
    fi
# Stage a complete release, retaining the previous version until manual cleanup.
mnx_pending="$mnx_release.pending.$$"
mkdir -p "$mnx_pending/THIRD-PARTY"
cp -a "$mnx_output/." "$mnx_pending/"
mkdir -p "$MNXBuildRoot/symbols/$mnx_rid-$mnx_kind"
while IFS= read -r -d '' mnx_symbol; do
    mv -- "$mnx_symbol" "$MNXBuildRoot/symbols/$mnx_rid-$mnx_kind/"
    done < <(find "$mnx_pending" -maxdepth 1 -type f \( -name '*.dbg' -o -name '*.pdb' -o -name '*.xml' \) -print0)
cp -- "$mnx_root/linux/DUAL-DEPENDENCIES.txt" "$mnx_pending/DEPENDENCIES.txt"
cp -- "$MNXPortableRoot/vendor/Majorsilence.Forms/license.md" "$mnx_pending/THIRD-PARTY/Majorsilence.Forms-MIT.txt"
cp -- "$MNXPortableRoot/vendor/Majorsilence.Forms/third-party-licenses.md" "$mnx_pending/THIRD-PARTY/Majorsilence.Forms-notices.md"
cp -- "$MNXPortableRoot/vendor/RichTextKit/license.txt" "$mnx_pending/THIRD-PARTY/RichTextKit-Apache-2.0.txt"
cp -- "$mnx_root/src/Icons/LICENSE.txt" "$mnx_pending/THIRD-PARTY/MNX-icons-MIT.txt"
cp -- "$mnx_root/linux/licenses/"*.txt "$mnx_pending/THIRD-PARTY/"
if [[ -d "$mnx_release" ]]; then
    mnx_history="${MNXArchiveRoot:-$mnx_parent/mnx_clutter/releases/mnx-gui}"
    mkdir -p "$mnx_history"
    mv -- "$mnx_release" "$mnx_history/$(basename -- "$mnx_release").previous.$(date +%Y%m%d%H%M%S)"
    fi
mv -- "$mnx_pending" "$mnx_release"
printf '\nBuilt: %s/mnx-gui\n' "$mnx_release"

if ! $mnx_managed; then
    python3 "$mnx_root/tools/stage-linux.py" mnx-gui "$mnx_release" "${MNXFinalRoot:-$mnx_parent/MNX_Builds/binaries-final}"
    fi
