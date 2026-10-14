#!/usr/bin/env bash
set -euo pipefail

# clean-locks must be able to recover from a broken build/packages.lock.json, so it restores unlocked
locked_mode=true
for arg in "$@"; do
  if [[ "$arg" == "clean-locks" ]]; then
    locked_mode=false
  fi
done

if [[ "$locked_mode" == false ]]; then
  rm -f build/packages.lock.json
fi

dotnet run --project build/build.csproj --property:RestoreLockedMode=$locked_mode -- "$@"
