#!/usr/bin/env bash
#
# run-validation.sh — fan the artefact-bundle harness across a list of source models,
# pulling each via --remote from a SRC server and uploading via --upload to a DST server.
#
# Tokens are read from the environment ONLY and are never written to disk or echoed:
#   SPECKLE_SRC_TOKEN  — auth for the SRC (read) server
#   SPECKLE_DST_TOKEN  — auth for the DST (write) server
#
# Server URLs + the DST project/model are configured via env or flags:
#   SRC_SERVER   (e.g. https://app.speckle.systems)        — or --src-server
#   DST_SERVER   (e.g. http://localhost:3000)              — or --dst-server
#   DST_PROJECT  destination projectId                      — or --dst-project
#   DST_MODEL    destination modelId                        — or --dst-model
#   PARALLEL     max concurrent harness runs (default 4)    — or --parallel
#
# Source refs are `srcProjectId/srcModelId` tokens, supplied as:
#   - positional args:        run-validation.sh proj1/modelA proj2/modelB
#   - or a file (one per line): run-validation.sh --refs refs.txt
#
# NOTE: every source model is migrated INTO the single DST project/model given. The DST
# server creates a NEW version per upload, so repeated runs stack versions on that model.
# (Per-source destination mapping is intentionally out of scope — adjust if you need it.)
#
# Example:
#   export SPECKLE_SRC_TOKEN=... SPECKLE_DST_TOKEN=...
#   SRC_SERVER=https://app.speckle.systems DST_SERVER=http://localhost:3000 \
#   DST_PROJECT=abc123 DST_MODEL=def456 \
#   ./run-validation.sh p1/m1 p2/m2

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CSPROJ="$SCRIPT_DIR/Speckle.Sdk.Artifacts.Harness.csproj"

SRC_SERVER="${SRC_SERVER:-}"
DST_SERVER="${DST_SERVER:-}"
DST_PROJECT="${DST_PROJECT:-}"
DST_MODEL="${DST_MODEL:-}"
PARALLEL="${PARALLEL:-4}"
REFS_FILE=""
REFS=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    --src-server)  SRC_SERVER="$2"; shift 2 ;;
    --dst-server)  DST_SERVER="$2"; shift 2 ;;
    --dst-project) DST_PROJECT="$2"; shift 2 ;;
    --dst-model)   DST_MODEL="$2"; shift 2 ;;
    --parallel)    PARALLEL="$2"; shift 2 ;;
    --refs)        REFS_FILE="$2"; shift 2 ;;
    -h|--help)
      sed -n '2,40p' "${BASH_SOURCE[0]}"; exit 0 ;;
    *)             REFS+=("$1"); shift ;;
  esac
done

# ── validate config (tokens stay in env; we only check presence) ─────────────────────────
err=0
[[ -z "${SPECKLE_SRC_TOKEN:-}" ]] && { echo "error: SPECKLE_SRC_TOKEN not set" >&2; err=1; }
[[ -z "${SPECKLE_DST_TOKEN:-}" ]] && { echo "error: SPECKLE_DST_TOKEN not set" >&2; err=1; }
[[ -z "$SRC_SERVER"  ]] && { echo "error: SRC_SERVER not set (env or --src-server)" >&2; err=1; }
[[ -z "$DST_SERVER"  ]] && { echo "error: DST_SERVER not set (env or --dst-server)" >&2; err=1; }
[[ -z "$DST_PROJECT" ]] && { echo "error: DST_PROJECT not set (env or --dst-project)" >&2; err=1; }
[[ -z "$DST_MODEL"   ]] && { echo "error: DST_MODEL not set (env or --dst-model)" >&2; err=1; }
[[ $err -ne 0 ]] && exit 2

if [[ -n "$REFS_FILE" ]]; then
  while IFS= read -r line; do
    line="${line%%#*}"; line="$(echo "$line" | xargs)"
    [[ -n "$line" ]] && REFS+=("$line")
  done < "$REFS_FILE"
fi

if [[ ${#REFS[@]} -eq 0 ]]; then
  echo "error: no source refs given (positional 'proj/model' tokens or --refs <file>)" >&2
  exit 2
fi

# Build once up-front so parallel workers don't race the ILRepack target.
echo "Building harness (Release) …"
if ! dotnet build -c Release "$CSPROJ" >/dev/null 2>&1; then
  echo "error: build failed; run 'dotnet build -c Release $CSPROJ' to see why" >&2
  exit 1
fi

RESULTS_DIR="$(mktemp -d)"
trap 'rm -rf "$RESULTS_DIR"' EXIT

run_one() {
  local ref="$1"
  local src_project="${ref%%/*}"
  local src_model="${ref##*/}"
  local safe="${ref//\//_}"
  local logf="$RESULTS_DIR/$safe.log"

  if [[ -z "$src_project" || -z "$src_model" || "$src_project" == "$src_model" && "$ref" != *"/"* ]]; then
    echo "FAIL  $ref  (malformed ref, expected projectId/modelId)" | tee "$RESULTS_DIR/$safe.status"
    return 0
  fi

  if dotnet run -c Release --no-build --project "$CSPROJ" -- \
      --remote "$SRC_SERVER" "$src_project" "$src_model" \
      --upload "$DST_SERVER" "$DST_PROJECT" "$DST_MODEL" \
      >"$logf" 2>&1; then
    local vid
    vid="$(grep -oE 'versionId=[A-Za-z0-9]+' "$logf" | head -1 | cut -d= -f2)"
    echo "PASS  $ref  -> version ${vid:-?}" | tee "$RESULTS_DIR/$safe.status"
  else
    echo "FAIL  $ref  (see log: $logf)" | tee "$RESULTS_DIR/$safe.status"
    tail -5 "$logf" | sed 's/^/      /'
  fi
}
export -f run_one
export SRC_SERVER DST_SERVER DST_PROJECT DST_MODEL RESULTS_DIR CSPROJ

echo "Running ${#REFS[@]} model(s) with parallelism $PARALLEL …"
printf '%s\n' "${REFS[@]}" | xargs -P "$PARALLEL" -I{} bash -c 'run_one "$@"' _ {}

echo
echo "──────── SUMMARY ────────"
pass=0; fail=0
for s in "$RESULTS_DIR"/*.status; do
  [[ -e "$s" ]] || continue
  line="$(cat "$s")"
  echo "$line"
  [[ "$line" == PASS* ]] && pass=$((pass+1)) || fail=$((fail+1))
done
echo "─────────────────────────"
echo "PASS=$pass  FAIL=$fail"
[[ $fail -eq 0 ]]
