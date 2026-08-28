# Speckle Bundle Migrator

End-to-end migrator for the Speckle artefact-bundle (parquet) migration: it derives a parquet
bundle from a Speckle object graph and can upload it to a server via the v2 model-ingestion flow.

Pipeline: source graph → `Base` → `GraphArtifactProducer.Produce` → parquet bundle on disk →
(optional) upload.

## Commands

Run from this directory as `dotnet run --project . -- <command> …`, or invoke the built dll
directly (see [Build note](#build-note)).

| Command | Purpose |
| --- | --- |
| `remote <serverUrl> <projectId> <modelId> [versionId]` | Migrate a server version, in place or to a destination |
| `packfile <packfilePath>` | Migrate a DuckDB packfile on disk |
| `selftest` | SGEO encoder byte-layout self-test |

### `remote`

Two modes, chosen by whether a destination is given. `--out <dir>` sets the staging directory
(default: a temp dir) in both.

**In place (no `--dest-*`)** — migrates the given version onto itself via the bundle-migration API:
the produced bundle is uploaded to that version's artifact prefix and **no new version is created**.
`versionId` is required (there is no way to resolve "latest" with a migration token). This is the mode
the server's bundle-migration service invokes.

```
remote <serverUrl> <projectId> <modelId> <versionId>
```

The only credential is the per-job **migration JWT** in `SPECKLE_TOKEN`, which authorises the migration
endpoints and nothing else — so the source packfile is fetched from the presigned URL those endpoints
return, straight out of object storage. The service owns the surrounding lifecycle (`start` / `complete` /
`fail`); the migrator only signs and uploads, and signals failure by exiting non-zero.

**New version (all three `--dest-*`)** — the previous behaviour: produce, then upload as a NEW version on
the destination via the v2 model-ingestion flow. `versionId` is optional here (default: latest), and
`--legacy-api` (REST deserialize instead of the packfile download) applies only to this mode.

```
remote <serverUrl> <projectId> <modelId> [versionId] --dest-server <url> --dest-project <id> --dest-model <id>
```

Tokens: `SPECKLE_TOKEN` (migration JWT) for in place; `SPECKLE_SRC_TOKEN`/`SPECKLE_TOKEN` to read and
`SPECKLE_DST_TOKEN` to upload for the new-version mode.

### `packfile`

Migrate a `.duckdb` packfile already on disk — e.g. one pulled from a server's object storage. Options:
`--root <id>` (default: the packfile's own root table), `--out <dir>` (default: a temp dir), and
`--upload <serverUrl> <projectId> <modelId>` to additionally upload as a new version (`SPECKLE_DST_TOKEN`).

## Environment variables

- `SPECKLE_TOKEN` — the per-job migration JWT for an in-place `remote` migration; also the fallback for
  the two below.
- `SPECKLE_SRC_TOKEN` (or `SPECKLE_TOKEN`) — read token for the source server.
- `SPECKLE_DST_TOKEN` — write token, required for any new-version upload.

Tokens are read only from the environment — never hardcoded, written to a file, or echoed.

## Examples

```bash
# Migrate a version IN PLACE (no new version) — the mode the migration service uses
export SPECKLE_TOKEN=<per-job migration JWT>
dotnet run --project . -- remote https://app.speckle.systems srcProj srcModel 9f8e7d6c5b

export SPECKLE_SRC_TOKEN=<src>  SPECKLE_DST_TOKEN=<dst>

# Migrate a server model (latest version) to a destination model as a NEW version
dotnet run --project . -- remote https://app.speckle.systems srcProj srcModel \
  --dest-server http://localhost:3000 --dest-project dstProj --dest-model dstModel

# Pin a version and use the legacy REST fetch instead of the packfile download
dotnet run --project . -- remote https://app.speckle.systems srcProj srcModel 9f8e7d6c5b \
  --dest-server http://localhost:3000 --dest-project dstProj --dest-model dstModel --legacy-api

# Local DuckDB packfile → bundle on disk
dotnet run --project . -- packfile ./version.duckdb --out /tmp/bundle

# Local DuckDB packfile → derive + upload as a new version
dotnet run --project . -- packfile ./version.duckdb --upload http://localhost:3000 dstProj dstModel
```
