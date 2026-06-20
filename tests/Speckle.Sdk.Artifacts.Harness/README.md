# Speckle Artefact-Bundle Harness

End-to-end validation harness for the Speckle artefact-bundle (envelope-triple / parquet)
migration. It derives a 14-file parquet bundle from a Speckle object graph and can upload
it to a server via the **v2 envelope-bundle upload flow**.

Pipeline: object graph (local NDJSON **or** remote server) → `Base` graph
→ `GraphArtifactProducer.Produce` → parquet bundle on disk → (optional) v2 upload.

## CLI

Pick **one** input and any combination of outputs.

### Input

| Mode | Args | Token (env) |
| --- | --- | --- |
| Local NDJSON | `--local <ndjsonPath> [--root <id\|auto>]` | — |
| Remote server | `--remote <serverUrl> <projectId> <modelId> [--version <versionId>]` | `SPECKLE_SRC_TOKEN` |

- `--local` reads a tab-separated NDJSON file (`id\tjson`, also tolerates `id\ttype\tjson`;
  `.gz` / `.zip` aware). `--root auto` (default) auto-detects the root collection.
- `--remote` pulls the graph directly from the server via the SDK's server-backed
  deserialize process (no file download, no local account store — the token is passed
  explicitly). When `--version` is omitted it resolves the model's **latest** version's
  `id` + `referencedObject` (rootId) via GraphQL first.

### Output

| Flag | Effect | Token (env) |
| --- | --- | --- |
| `--out <dir>` | Write the bundle to `<dir>` (default: a temp dir) | — |
| `--upload <serverUrl> <projectId> <modelId>` | Additionally upload the bundle via the v2 flow | `SPECKLE_DST_TOKEN` |

Both may apply. `--upload` implies a temp dir if `--out` is absent.

### Legacy positional form (still supported)

```
dotnet run -c Release --project . -- <ndjsonPath> [rootId|auto] [outDir]
```

## Environment variables

- `SPECKLE_SRC_TOKEN` — bearer token for the SRC (read) server. Required for `--remote`.
- `SPECKLE_DST_TOKEN` — bearer token for the DST (write) server. Required for `--upload`.

Tokens are read **only** from these env vars. They are never hardcoded, written to any
file, or echoed. A missing token for the chosen mode prints an error and exits non-zero.

## Examples

```bash
# Local file → bundle on disk
dotnet run -c Release --project . -- --local ~/Downloads/model.ndjson --out /tmp/bundle

# Remote latest version → bundle on disk
export SPECKLE_SRC_TOKEN="<src-token>"
dotnet run -c Release --project . -- \
  --remote https://app.speckle.systems abc123proj def456model --out /tmp/bundle

# Remote → migrate (derive + upload) to a local server
export SPECKLE_SRC_TOKEN="<src-token>" SPECKLE_DST_TOKEN="<dst-token>"
dotnet run -c Release --project . -- \
  --remote https://app.speckle.systems abc123proj def456model \
  --upload http://localhost:3000 dstProj dstModel

# Pin a specific source version
dotnet run -c Release --project . -- \
  --remote https://app.speckle.systems abc123proj def456model --version 9f8e7d6c5b \
  --upload http://localhost:3000 dstProj dstModel
```

On a successful upload the harness prints the resulting `versionId` and a viewer URL.

## v2 envelope-bundle upload flow

The harness implements, against the DST server (paths derived from
`packages/server/modules/data/rest/upload.ts`, router mounted at `API_PATH = '/api'`):

1. **GraphQL** `projectMutations.modelIngestionMutations.create(input)` → returns the
   `ModelIngestion { id, versionId }`. The `versionId` is **reserved at creation** (it lives
   on the ingestion, not on `statusData.Success`).
2. `POST /api/v2/projects/{projectId}/modelingestion/{ingestionId}/uploads/sign`
   with `{ "files": [<basenames>] }` → `{ "uploads": { "<name>": { "url", "key" } } }`.
3. `PUT` each local bundle file to its presigned URL with
   `Content-Type: application/octet-stream`, capturing the returned `ETag` header.
4. `POST /api/v2/projects/{projectId}/modelingestion/{ingestionId}/uploads/complete`
   with `{ "etags": { "<name>": "<etag>" }, "rootId", "totalChildrenCount"? }` →
   `{ "versionId", "files" }`. This creates the commit (schemaVersion 3, `artifactFiles`).

## Fan-out runner

`run-validation.sh` fans the harness across many source models with bounded parallelism
(default 4 via `xargs -P`), pulling each via `--remote` and uploading via `--upload`, then
prints a per-model PASS/FAIL summary. Tokens come from the environment; no secrets in the
script. See the header comment in the script for all flags/env vars.

```bash
export SPECKLE_SRC_TOKEN=... SPECKLE_DST_TOKEN=...
SRC_SERVER=https://app.speckle.systems DST_SERVER=http://localhost:3000 \
DST_PROJECT=dstProj DST_MODEL=dstModel \
./run-validation.sh srcProj1/srcModelA srcProj2/srcModelB
# or: ./run-validation.sh --refs refs.txt
```

## BUILD GOTCHA (read before running)

`Speckle.Sdk.Dependencies` is ILRepacked by a run-once MSBuild target. A plain
`dotnet build -c Release` after an incremental build can clobber it, producing
`BadImageFormatException: Duplicate type ... Speckle.Sdk.Dependencies` at runtime.

For a guaranteed-clean build (one that actually runs):

```bash
HARNESS=tests/Speckle.Sdk.Artifacts.Harness
rm -rf "$HARNESS/bin" "$HARNESS/obj" \
       src/Speckle.Sdk.Dependencies/bin src/Speckle.Sdk.Dependencies/obj
dotnet build -c Release "$HARNESS/Speckle.Sdk.Artifacts.Harness.csproj"
```

Target framework is `net8.0`.
```
