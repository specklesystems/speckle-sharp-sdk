# Context handoff — artefact storage/serving for server-changes-v2-data-endpoints

> ⚠️ **SUPERSEDED / RESOLVED (2026-06-20).** This was the *pre-implementation* "open questions for the server
> thread" doc; everything it asks is now decided and shipped, so the body below is **historical** — do not act
> on its open questions. Final state + canonical record:
> - **Server built + E2E-validated.** New v2 data-endpoints live; 5/5 runs uploaded clean (4 Revit + 1 Navis),
>   `schemaVersion=3`, served via `GET /api/v2/projects/:p/models/:m/versions/:v/artifacts` (presigned GET).
>   Upload is **no longer a stub**.
> - **Naming settled:** files are `{versionId}.<artefact>.<table>.parquet`, versionId **pre-allocated** at
>   ingestion creation, keyed `versions/{versionId}/`. No rename. (The "manifest ↔ filenames" coupling + its 3
>   options below are moot.)
> - **Manifest DROPPED entirely** — never served; consumers build their own `read_parquet` views. The bundle is
>   **12 parquet, 0 `.sql`** (not "12 + 2 manifests" as the body says).
> - **`dataShape` skipped** — `schemaVersion: Int` only.
> - Canonical docs now: **`notes/topology-envelope-SOT.md` §4/§6** (consumer reads + `object_properties`) and
>   **`notes/server-v2-data-endpoints-SOT.md`** (server impl). Consumer view: `notes/handoff-packfileloader2-envelope.md`.

**Audience:** the server-side thread settling artefact naming + data endpoints. **Producer:** Speckle.Sdk
artefact pipeline (`src/Speckle.Sdk/Pipelines/Send/Artifacts/*`), driven by the ODA-Revit POC. As of
**2026-06-20** the producer emits **passive Zstd parquet** for all three artefacts (no DuckDB write engine).
SDK commit `98d381cf`, branch `topology-envelope-poc`. Upload to the server is currently a **stub** — files
land on local disk; wiring the real transport is partly what this thread decides.

This doc is the storage/transport view. The query-semantics view (for the viewer/consumer) is in
`notes/handoff-packfileloader2-envelope.md`; the full design is `notes/topology-envelope-SOT.md`.

---

## What a "send" produces, physically

A model with base name `{base}` produces a **bundle of passive files** — no database, no server-side compute
to generate them, fully streamable ("write and free"):

```
{base}.geometries.parquet            # SGEO mesh/brep blobs (Zstd)

{base}.eav.objects.parquet           # object_index ↔ application_id (the K dictionary)
{base}.eav.paths.parquet             # interned property paths
{base}.eav.eav.parquet               # instance property rows
{base}.eav.types.parquet             # type dictionary
{base}.eav.type_eav.parquet          # deduped type params (once per type)
{base}.eav.object_type.parquet       # object → type weak ref
{base}.eav.manifest.sql              # CREATE VIEWs over the 6 above + object_properties

{base}.envelope.relations.parquet    # typed topology edges
{base}.envelope.nodes.parquet        # value-entities (definition/instance/material/colour/level)
{base}.envelope.meta.parquet         # catalog: schema_version, produced_by
{base}.envelope.rel_types.parquet    # catalog: rel id → name + namespaces
{base}.envelope.node_kinds.parquet   # catalog: kind id → name
{base}.envelope.manifest.sql         # CREATE VIEWs over the 5 envelope tables
```

So per model: **12 parquet files + 2 SQL manifests** (geometries is standalone; eav and envelope each are a
table-set + manifest). All parquet, all Zstd, all append-only at produce time.

**Scale / sizing reference (Trabzon T1, 16,251 objects):** eav 1.53 MB, envelope 0.43 MB, geometries the
bulk. Whole non-geometry set is single-digit MB; ~67% smaller than the old `.duckdb` form (eav −87%,
envelope −81%). T2 (22,425 objects) similar shape. These are small, cache-friendly, range-readable objects.

---

## The one coupling this thread must resolve: manifest ↔ filenames

The two `manifest.sql` files are the consumer's entry point. DuckDB reads parquet natively, so each manifest
is just `CREATE VIEW` statements wrapping `read_parquet('<filename>')`. **Today those filenames are baked in
as RELATIVE paths** (the consumer runs the manifest from the artefact directory). Example (eav):

```sql
CREATE VIEW objects     AS SELECT * FROM read_parquet('{base}.eav.objects.parquet');
CREATE VIEW eav         AS SELECT * FROM read_parquet('{base}.eav.eav.parquet');
-- ... + the flat-read object_properties view (instance ∪ deduped type)
```

**This is the single thing coupled to naming.** Whatever the server chooses — content-addressed hashes,
object-store keys, presigned URLs, a flat vs. nested layout — the `read_parquet(...)` arguments in the
manifests must resolve to it. Options on the table:

1. **Producer learns the scheme** — pass the naming/prefix in, `P(suffix)` + `Manifest()` emit final names.
   Cheapest if names are known at produce time; bad if names are content hashes only known after write.
2. **Server rewrites manifests on ingest** — producer emits provisional names, server rewrites the
   `read_parquet` strings (and/or to absolute/presigned URLs) when it stores the files. Decouples produce
   from storage layout; server owns the rename.
3. **Manifest generated at read time** — server (or client) synthesizes the manifest from a known scheme +
   a file list, so the producer ships data only, no manifest. Most flexible; the view/SQL becomes a
   server/consumer concern, not a producer artefact.

The producer side is naming-agnostic by construction: filenames live **only** in `P(suffix)` and `Manifest()`
in `EavWriter`/`EnvelopeWriter` (+ `GeometriesParquetWriter`). Pick an option and that's the whole change.

> Note for DuckDB-over-HTTP: `read_parquet` can take http(s) URLs (httpfs) and supports range reads, so
> presigned-URL manifests are viable without downloading whole files — relevant if endpoints serve blobs
> directly rather than a mounted directory.

---

## What the endpoint layer needs to expose (consumer's actual access pattern)

Consumers (viewer 2.0/3.0, connector receive, analytical) **attach + cross-file join** rather than download a
monolith. The two hot reads:
- **object set / all applicationIds** → `SELECT application_id FROM objects` (= the whole `eav.objects` file).
- **all properties for an object, flat** → the shipped `object_properties` view (unions instance eav with the
  deduped type params; consumer never sees the dedup).

Implications for endpoints:
- Files are independently useful — a consumer may want only `eav.objects` + `geometries`, or only the
  envelope. Per-file addressability (not just a bundle blob) is valuable.
- The manifests are the "schema" handshake. Whoever owns the final URLs owns the manifest text.
- The catalog tables (`meta`/`rel_types`/`node_kinds`) make the envelope self-describing — a generic endpoint
  can serve the bundle without hardcoding the rel/kind vocabulary; it travels with the data.

---

## Stable vs. in-flux (so the server thread knows what it can lean on)

- **Stable:** the table set, column schemas, the dense-int id model (per-namespace object/geometry/node K),
  the rel/kind enums, and the `object_properties` flat contract. These are validated and committed; build
  the storage/endpoint design against them.
- **In-flux (this thread decides):** filenames + `{base}` prefix; whether manifests ship as-is, are rewritten,
  or are generated; the actual upload transport (currently stubbed); per-file vs bundle addressing; URL form
  (relative path / object key / presigned).
- **Producer commitment:** once this thread picks a naming scheme + manifest strategy, the SDK change is
  localized to `P(suffix)` + `Manifest()` (and the upload stub). No schema or id changes implied.

## Sequencing (agreed 2026-06-20) — prove on Revit first, migrate Navis once

The producer-side consumers of the upload/naming contract migrate **in a fixed order**, so the
multi-file-per-purpose convention is proven exactly once before it's copied:

1. **This thread settles** the naming scheme + manifest strategy + multi-file-per-purpose upload convention.
2. **End-to-end Revit-bound POC** runs against the *real* server/upload (today Revit's `UploadFilesAsync` is a
   stub that just lists paths) — this is where the convention gets validated against a live endpoint.
3. **Navis migrated in a single pass**, copying the proven Revit pattern.

⚠️ **Navis is currently broken against the parquet shape and is intentionally NOT being fixed yet.**
`src/Navis/ODA.DataExtraction.Navis/Extraction/NavisModelBinaryExtractor.cs` still assumes one `.duckdb`
file per artefact: it hands `manifest.sql` (not the data) to `UploadFilesAsync`, and `RenameArtifactToVersion`
matches dead `.envelope.duckdb`/`.eav.duckdb` suffixes. The upload purpose-map + the S3
`{versionId}.{purpose}.duckdb` convention are exactly what this thread is redefining — so Navis waits for the
decision rather than guessing. Don't fix Navis in isolation; it's step 3.

## Pointers
- Writers: `src/Speckle.Sdk/Pipelines/Send/Artifacts/{EavWriter,EnvelopeWriter,ParquetTableWriter,GeometriesParquetWriter}.cs`
- Design SOT: `notes/topology-envelope-SOT.md` (§2 tables, §4 consumer reads, §5 scale, §6 catalog + dedup)
- Consumer-side companion: `notes/handoff-packfileloader2-envelope.md`
