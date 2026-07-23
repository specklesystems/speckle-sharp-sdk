# Speckle Artefact-Bundle Harness

End-to-end harness for the Speckle artefact-bundle (parquet) migration: it derives a parquet
bundle from a Speckle object graph and can upload it to a server via the v2 model-ingestion flow.

Pipeline: source graph → `Base` → `GraphArtifactProducer.Produce` → parquet bundle on disk →
(optional) upload.

## Commands

Run from this directory as `dotnet run --project . -- <command> …`, or invoke the built dll
directly (see [Build note](#build-note)).

| Command | Purpose |
| --- | --- |
| `remote <serverUrl> <projectId> <modelId> [versionId]` | Migrate a server version, source → destination |
| `ndjson <ndjsonPath>` | Migrate a local NDJSON dump |
| `packfile <packfilePath>` | Migrate a local DuckDB packfile |
| `selftest` | SGEO encoder byte-layout self-test |

### `remote`

Fetches the source version (`versionId` optional, default: latest) and **always uploads** the
produced bundle to the destination. The destination defaults to the source, so with no `--dest-*`
it writes a new version back onto the source model; override with all three of `--dest-server`,
`--dest-project`, `--dest-model` (all-or-nothing). By default the source is fetched by downloading
its DuckDB packfile; `--legacy-api` fetches via the REST deserialize API instead. `--out <dir>`
sets the staging directory (default: a temp dir).

Tokens: `SPECKLE_SRC_TOKEN` (or `SPECKLE_TOKEN`) to read, `SPECKLE_DST_TOKEN` to upload.

### `ndjson` / `packfile`

Migrate a local file — an NDJSON dump (`.ndjson`/`.gz`/`.zip`) or a `.duckdb` packfile, e.g. an
artefact pulled from a server's object storage. Options: `--root <id>` (the `ndjson` default
`auto` detects the root; `packfile` uses its root table), `--out <dir>` (default: a temp dir), and
`--upload <serverUrl> <projectId> <modelId>` to additionally upload (`SPECKLE_DST_TOKEN`).

`ndjson` loads the whole object closure into memory, so it is memory-hungry on large models;
`packfile` (and `remote`) stream from DuckDB instead.

## Environment variables

- `SPECKLE_SRC_TOKEN` (or `SPECKLE_TOKEN`) — read token for the source server.
- `SPECKLE_DST_TOKEN` — write token, required for any upload.

Tokens are read only from the environment — never hardcoded, written to a file, or echoed.

## Examples

```bash
export SPECKLE_SRC_TOKEN=<src>  SPECKLE_DST_TOKEN=<dst>

# Migrate a server model (latest version) to a destination model
dotnet run --project . -- remote https://app.speckle.systems srcProj srcModel \
  --dest-server http://localhost:3000 --dest-project dstProj --dest-model dstModel

# Pin a version and use the legacy REST fetch instead of the packfile download
dotnet run --project . -- remote https://app.speckle.systems srcProj srcModel 9f8e7d6c5b \
  --dest-server http://localhost:3000 --dest-project dstProj --dest-model dstModel --legacy-api

# Local NDJSON → bundle on disk
dotnet run --project . -- ndjson ~/Downloads/model.ndjson.gz --out /tmp/bundle

# Local DuckDB packfile → derive + upload
dotnet run --project . -- packfile ./version.duckdb --upload http://localhost:3000 dstProj dstModel
```
