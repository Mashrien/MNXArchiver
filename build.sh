#!/usr/bin/env bash
# Standard Linux build: one executable, two selectable interfaces.
set -euo pipefail
mnx_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)
exec "$mnx_root/build-linux.sh" "$@"
