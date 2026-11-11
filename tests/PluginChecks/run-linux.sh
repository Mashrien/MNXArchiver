#!/usr/bin/env bash
set -euo pipefail
checks=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)
module=$(realpath -- "${1:?Pass the built ext.format.RPA.mnxp}")
fixture=$(realpath -- "${2:-$checks/Fixtures/hello.rpa}")
workspace=$(mktemp -d)
trap 'rm -rf -- "$workspace"' EXIT
cp -- "$module" "$workspace/ext.format.RPA.mnxp"
extra_checks=()
if [[ -n "${3:-}" ]]; then
    cp -- "$3" "$workspace/ext.image.WEBP.mnxp"
    extra_checks+=(--check-webp)
    fi
printf 'Not an ELF library.\n' > "$workspace/bad-magic.mnxp"
cc -shared -fPIC -DABI=2 "$checks/RejectedModule.c" -o "$workspace/bad-abi.so"
cc -shared -fPIC '-DRELEASE="1.1.0"' "$checks/RejectedModule.c" -o "$workspace/new-module.so"
cc -shared -fPIC '-DRELEASE="0.9.9"' "$checks/RejectedModule.c" -o "$workspace/old-module.so"
cc -shared -fPIC '-DMIN_HOST="2.2.0"' "$checks/RejectedModule.c" -o "$workspace/new-host-required.so"
cc -shared -fPIC '-DMAX_HOST="2.0.999"' "$checks/RejectedModule.c" -o "$workspace/old-host-only.so"
cc -shared -fPIC "$checks/RejectedModule.c" -o "$workspace/reject-negotiation.so"
DOTNET_PROCESSOR_COUNT=${DOTNET_PROCESSOR_COUNT:-2} MSBUILDDISABLENODEREUSE=1 dotnet run \
    --project "$checks/PluginChecks.csproj" -c Release -p:UseSharedCompilation=false \
    -p:MNXBuildNumber="${MNXBuildNumber:-63}" -- "$workspace" "$fixture" "${extra_checks[@]}"
for bad_config in '' 'relative-plugins'; do
    XDG_CONFIG_HOME="$bad_config" DOTNET_PROCESSOR_COUNT=${DOTNET_PROCESSOR_COUNT:-2} dotnet run \
        --project "$checks/PluginChecks.csproj" -c Release --no-build \
        -p:MNXBuildNumber="${MNXBuildNumber:-63}" -- --check-plugin-path
done
