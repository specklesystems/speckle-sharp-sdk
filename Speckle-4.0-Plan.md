# Speckle 4.0 Plan

> Living design doc for the end-to-end refactor. Contract-first: the goal is to
> nail down what each layer produces and consumes before implementing anything.
> Sections marked **[OPEN]** are undecided; **[TODO: <who>]** needs an input.

---

## Overview

**Core idea:** separation of concerns. Generate **purpose-specific DuckDB files
on the client side** (in the SDK), instead of uploading an interim NDJSON and
having the server materialize one mega DuckDB that contains everything.

The server's job shrinks to **storing the already-built files on S3**. Consumers
(viewer, connector-receive, property queries) each fetch only the file they care
about.

### Repos in scope

| Repo | Role in 4.0 |
|---|---|
| **speckle-oda** | Where we iterate on real objects — the converters that produce atomic Speckle objects + properties. |
| **speckle-sharp-sdk** | Where we serialize objects and write them into the purpose-specific DuckDB files. The heart of this change. |
| **speckle-server-internal** | Where we store the DuckDB files on S3. Materialization moves *off* the server. |
| _(viewer repo — later)_ | Consumes `viewer.duckdb`. Out of scope for the first slices but the read-contract is designed now. |

---

## Why — current state and its problems

From the production pipeline (bottom of the reference diagram):

```
conversion loop
   → produces atomic speckle objects with properties
   → interim NDJSON                         (bandaid)
   → materialize into ONE mega DuckDB        (bandaid)  ← contains everything
   → derive EAV file from properties
```

**Problems:**
- The **mega DuckDB contains everything** — no separation of concerns, large, slow to produce and to query.
- **Server does heavy materialization** — processing latency, server-side memory pressure (we hit the 554 MiB single-blob / oversized-path class of bug).
- The NDJSON → mega-duckdb → derive-EAV chain is **"a bit too much"** — multiple passes over the same data.

**Failure surface (each is a real place it breaks today):**
- **"us" fails** — conversion bugs, bad host-app API usage.
- **Speckle object model fails** — closures, reference integrity, etc.
- **SDK fails** — serialization.
- **Format fails** — the wire/storage format.
- **Infra fails / processing latency** — server-side materialization.

A motivation for 4.0 is to **shrink and isolate this failure surface**: if the
client writes purpose-specific files directly, several of these stages (interim
NDJSON, server materialization, EAV derivation pass) collapse or move.

---

## New approach

**Client side (SDK) produces multiple purpose-specific DuckDB files directly.**
Server stores them on S3. Each consumer reads the one it needs.

### Naming convention

```
{version_id}.{purpose}.duckdb
```

Same filename convention as v1 (`{versionId}.duckdb` / `{versionId}.eav.duckdb`),
stored under the same `versions/` path — the S3 prefix already carries project
and model, so the filename doesn't repeat them. The eav key is byte-identical
to v1's derived eav key, so convention-based lookups keep working.

### The files

| File | Contents | Primary consumer |
|---|---|---|
| `….viewer.duckdb` | Everything needed to render, **self-contained**: base geometry (dedup'd SMSH "binary mesh on steroids"), **instance transforms**, display/material info. | Web viewer, connector-receive (geometry) |
| `….eav.duckdb` | Property values, as today (entity-attribute-value). **[TODO: oguzhan — share sample file]** | Property queries, filtering, dashboards |

**Decision (locked):** instance **transforms live in `viewer.duckdb`**.
Consequence: the viewer file is self-sufficient — base geometry stored once,
placed N times via its transform rows (classic GPU instancing). The viewer never
has to fetch a second file to draw the scene.

**Decision (locked):** there is **no `topology.duckdb`** — the artifact set is
exactly two files: viewer + eav. Whatever connector-receive needs beyond these
is served by the existing v1 path during dual-write, and revisited only if a
concrete receive requirement emerges that the two files can't satisfy.

This mirrors the glTF-style split (structure + binary sidecar) and our own
packfile's `objects` / `blobs` / `eav` table separation — but pushed to the
client and split into independently-fetchable files.

---

## Contracts (the seams)

> This is the part that matters most. Fill one sub-section per file before
> implementing. Each needs: **schema**, **producer** (which SDK component writes
> it), **consumer** (who reads it + how), **keying** (how rows join across files).

### `viewer.duckdb` — self-contained render package

**Finding (from auditing `speckle-viewer-webgpu`): the contract already exists.**
The WebGPU viewer downloads today's mega packfile and **bakes its own viewer
artifacts in the browser** (stored in OPFS, once per model):

| Today's browser-baked artifact | Contents |
|---|---|
| `{vwId}.dat` | binary geometry buffer (Float32 positions + Uint32 triangle indices, pre-triangulated, chunked) |
| `{vwId}.primitives.ndjson` | per unique geometry: byte offset into .dat, chunks, topology (tri/line), bounding sphere |
| `{vwId}.placements.ndjson` | per placement: primitiveId, transform[16], materialId, path[] (ancestor ids, for picking), color |
| `{vwId}.json` (manifest v3) | materials map (baseColor, opacity), topology counts |

This is exactly the diagram's "viewer artefacts" boxes — already implemented,
just **baked client-side in the browser on every user's machine** via a full
bootstrap + DFS traversal of the packfile (`packages/viewer-webgpu/src/packfile-loader/`).
**4.0's move is to relocate this bake from the browser to the SDK at send time**,
producing the same information as one seekable `viewer.duckdb`.

**Draft contract — DDL v1 (decide/veto, then freeze):**

```sql
CREATE TABLE manifest (
  format_version  INTEGER NOT NULL,     -- start at 1; consumers hard-fail on unknown
  project_id      VARCHAR,
  model_id        VARCHAR,
  version_id      VARCHAR,
  units           VARCHAR,
  generator       VARCHAR               -- "speckle-sharp-sdk/x.y.z"
);
CREATE TABLE materials (
  id          VARCHAR PRIMARY KEY,      -- material content hash (dedup)
  base_color  UINTEGER NOT NULL,        -- packed RGBA
  opacity     REAL NOT NULL,
  name        VARCHAR
);
CREATE TABLE primitives (                -- one row per UNIQUE geometry
  id            VARCHAR PRIMARY KEY,    -- geometry content hash
  topology      UTINYINT NOT NULL,      -- 0 = triangle list, 1 = line strip
  vertex_count  UINTEGER NOT NULL,
  index_count   UINTEGER NOT NULL,
  sphere        FLOAT[4],               -- bounding sphere, LOCAL space
  origin        DOUBLE[3]               -- RTC origin (local 0,0,0 in world)
);
CREATE TABLE geometry (
  primitive_id  VARCHAR PRIMARY KEY,    -- → primitives.id
  positions     BLOB NOT NULL,          -- zstd( Float32 xyz, LOCAL/RTC space )
  indices       BLOB NOT NULL           -- zstd( Uint32 )
);
CREATE TABLE placements (                -- one row per rendered instance
  seq           UINTEGER PRIMARY KEY,   -- stable draw/load order
  object_id     VARCHAR NOT NULL,       -- Speckle id (picking)
  primitive_id  VARCHAR NOT NULL,       -- → primitives.id
  transform     DOUBLE[16] NOT NULL,    -- row-major world matrix, RTC folded in
  material_id   VARCHAR,                -- → materials.id
  color         UINTEGER,               -- optional override
  path          VARCHAR[]               -- ancestor ids (picking/visibility)
);
```

Embedded decisions:
1. Geometry is **pre-baked for rendering**: triangulated, Float32, deduped.
   SMSH remains the lossless interchange form in the legacy pipeline.
2. **RTC coordinates (mandatory):** Float32 positions are local to each
   primitive's float64 `origin`; origin folds into placement transforms.
   Survey-coordinate models (Navis X≈5112 m) jitter in Float32 without this.
3. **Per-primitive zstd blobs** — seekable; reuses the `28 B5 2F FD` magic
   convention from 3.x.
4. **No GPU chunking in the file** — chunk classes are a viewer implementation
   detail; loader re-chunks in memory.
5. **Transforms float64 in-file**; viewer downcasts after RTC composition.

- Producer: SDK geometry serializer (extends the MeshBinary encoder + the
  traversal logic the viewer currently runs in JS).
- Consumer: viewer — drops its bootstrap/traverse/bake phase; loads tables directly.
- Keying: `primitives.id` = content hash (dedup); placements join on it.

**Upload/server contract:** SDK requests presigned PUTs for the duckdb files
(same mechanism as today's NDJSON), uploads, then calls a lightweight
**register** endpoint (record object keys against the version — replaces
"process"; no server parsing). Download mirrors the existing
`…/versions/{id}/eav/download` route — add `…/viewer/download`.

**Migration: dual-write.** SDK keeps sending NDJSON (existing pipeline intact
for receive/queries/mega-packfile) AND uploads the new files. Viewer prefers
`viewer.duckdb` when registered, falls back to packfile bake. Cut over
per-consumer; no flag day.

**Gaps found:**
1. The WebGPU viewer does **not** read MeshBinary/SMSH yet — it only parses legacy JSON `Mesh` (with chunk dechunking). Our 3.x blobs are invisible to it today. Slice 1 must close this on one side or the other.
2. Viewer triangulates n-gons at runtime; SMSH stores n-gons. Decide: pre-triangulate at SDK bake time (viewer gets zero-work buffers) — likely yes.
3. Viewer wants Float32 positions; SMSH is float64. Bake-time downcast (with the local-origin trick from the 3.x analysis) fits naturally here.
4. `path[]` (ancestor chain for picking) means a slice of topology lives in the viewer file by design — consistent with the self-contained decision.

### `eav.duckdb` **[TODO: oguzhan — share sample]**
- Schema: as today's EAV artefact (object_id, path, value_text, value_num, type, units, …).
- Producer: SDK property flattener.
- Consumer: property queries / filtering.
- Keying: object_id.

### ~~`topology.duckdb`~~ **[DROPPED]**
- Decision: the artifact set is **viewer + eav only**. The non-render object
  graph (hierarchy, definition↔instance relationships, closures) stays served
  by the existing v1 path during dual-write. Revisit only if a concrete
  connector-receive requirement emerges that the two files can't satisfy.

---

## Open questions to resolve before building

1. **EAV sample** — [TODO: oguzhan] share the current eav file so we match its schema.
2. ~~**Geometry split**~~ — **RESOLVED**: transforms + base geometry both in `viewer.duckdb` (self-contained). Topology parked.
3. **Display info** — separate table inside `viewer.duckdb`, or folded into the `instances` rows? **[OPEN]**
4. **Interim NDJSON** — do we drop it entirely and write DuckDB directly from the SDK, or keep NDJSON as a fallback/debug artefact?
5. **Connector-receive reconstruction** — how does receive stitch the split files back into host-app objects? Which files does it need, in what order?
6. **Server role** — pure storage (presigned PUT of N files), or does it still do *any* validation/indexing?
7. **Failure-surface mapping** — for each current failure stage, does 4.0 eliminate it, move it, or keep it? (Use this to justify the refactor.)
8. **Versioning** — each file format needs a version header (like SMSH's) so consumers can evolve independently.

---

## Cutover plan — single approach, no side-by-side (decided 2026-06-11)

**Strategy: v1 and v2 will NOT run side by side.** Dual-write is a brief
transition state only; the flip to v2-only happens as soon as the gating items
land. Frontend and long-tail dependencies are explicitly NOT blockers.

Gating items, in order:

1. ~~**v2 `complete` creates the version**~~ ✅ DONE (2026-06-11): complete
   verifies etags → finalizes renames → `createCommitByBranchId` (objectKey =
   viewer artifact, eavObjectKey = eav artifact) → marks ingestion Success.
   Reuses the existing version id if one exists (transition runs). The
   Completed-event listener was deleted; complete owns the whole lifecycle.
   v2 is now a SELF-SUFFICIENT ingestion path.
2. **Viewer direct-load of `viewer.duckdb`** — pixels from v2 artifacts.
3. `/eav/download` resolves the v2 eav key by convention.

**Consciously dropped at flip time** (decision, not oversight): current-viewer
rendering of new versions until item 2 lands, Postgres object indexes /
object-level queries, root.json + /single endpoint, connector receive,
fine-grained ingestion progress UI. Version records & frontend version lists
keep working (created by v2 complete).

At flip: oda drops the `sendPipeline` calls (artifact pipeline only), the
double-serialization cost disappears, NDJSON/mega-packfile production stops.

---

## Pre-4.0 pull-forward: kill the server-side re-parse (EAV client-side)

**Finding (from auditing all three repos):** the model is fully traversed three
times today. (1) oda builds structured objects; (2) SDK `Serializer.ExtractProperties`
walks every property to write JSON; (3) the server `JSON.parse`s every envelope
and walks every property *again* (`flattenObjectProperties`, single-threaded JS)
to derive the EAV file. Pass 3 re-derives what the client already had — after a
network round-trip. It is the "processing latency" failure zone, and it's the
root cause of the oversized-object hacks (10 MB `read_text()` dodge, the 554 MiB
blob bug).

**Pull-forward (no format changes, no consumer risk):**
1. SDK emits EAV rows during the serialization walk it already does → sidecar
   TSV next to the NDJSON (same columns as today's `properties` table).
2. Server ingests objects-NDJSON + EAV sidecar via DuckDB-native `read_csv`
   (parallel C++); the JS per-line loop shrinks to blob handling only.
3. Outputs byte-compatible: same packfile, same `eav.duckdb`.

Effort: port of `packages/shared/src/filtering/eavExtraction.ts` (~450 lines,
mechanical; the TS file + its spec ARE the spec) + small server change.
Strategic value: builds the "client writes purpose-specific artifacts" muscle —
a dry run for Slice 2 — behind today's format.

---

## Rollout — vertical slice first

Do **not** rewrite all converters / all of the SDK / the server in parallel.
Drive one representative object end-to-end through every layer, prove the
contracts, then generalize.

**Key sequencing insight:** the WebGPU viewer's bake (`processPackfile`) already
implements the viewer.duckdb *content* in TS, writing through an `OutputSink`
interface. Validate the contract by **repointing its output** before porting
anything to C#.

**Slice 1 — `viewer.duckdb`, in three steps:**

- **Step 0 (baseline, no code):** load a legacy-Mesh version in the sandbox,
  record `BOOTSTRAP` / `TRAVERSAL` timings from the console. This is the
  browser-bake cost 4.0 eliminates — the headline number for the pitch.
- **Step 1 — `DuckDBSink` prototype (TS, in viewer repo):** second `OutputSink`
  that writes `viewer.duckdb` (primitives/placements/materials/geometry tables)
  instead of the 4 OPFS files. Run on real packfiles. Triple payoff:
  (a) validates the schema against real models pre-C#;
  (b) forces the SMSH+zstd JS decoder (new packfiles contain MeshBinary blobs)
      — the viewer gap closes as a side effect;
  (c) produces real `viewer.duckdb` files to build the loader against.
- **Step 2 — viewer direct-load path:** load `viewer.duckdb` straight into
  `scene.loadScene`, skipping bootstrap/traverse. A/B against Step 0 baseline →
  the demo number ("first load Xs → Ys, traversal gone").
- **Step 3 — port the bake to the SDK (C#):** ODA → SDK writes `viewer.duckdb`
  at send time → server stores → viewer fetches. Largest chunk, started only
  once the schema is proven renderable (zero contract risk).

**Parallel track (anytime):** the EAV pull-forward (previous section) — server
-side, orthogonal to the viewer slice, suitable for a second pair of hands.

Then Slice 2 (`eav.duckdb`). (Topology slice dropped — two-file artifact set.)

**Verification harness is a first-class deliverable** — round-trip tests + size
comparisons + "is it actually wired" SQL checks, written alongside Slice 1.

---

## Status log

- _2026-05-…_ — Doc created. Overview, current-state, new-approach captured from
  reference diagram + initial brief. Contracts and open questions scaffolded;
  awaiting EAV sample and the geometry/topology split decision.
- _2026-05-…_ — **Decision:** instance transforms live in `viewer.duckdb` →
  viewer file is self-contained for rendering. `viewer.duckdb` sketched as 3
  tables (geometry / instances / display). `topology.duckdb` parked until
  connector-receive needs are understood. Geometry-split open question resolved.
- _2026-06-11_ — **Decision: one version id, v1 purely untouched.** Artifacts
  upload to deterministic ingestion-keyed STAGING keys (restores the
  one-upload-per-ingestion guard); when v1 processing creates the version it
  emits `ModelIngestionEvents.Completed` (existing contract) and a new v2
  listener renames staging → version-keyed names — the same
  staging→permanent copy pattern v1 uses for its raw NDJSON. The v2 `complete`
  endpoint finalizes instead when processing won the race. No v1 code touched,
  no migrations; artifact keys are derivable from the version by convention.
- _2026-06-11_ — **Decision: geometry bytes leave the v1 wire.** `Blob.content`
  (the base64-in-NDJSON bridge from the 3.x experiments) removed; the artifact
  writer reads bytes from the Blob's local `filePath`. The v1 NDJSON now carries
  only small envelopes; the mega packfile's blobs table stays empty for new
  sends — `viewer.duckdb` is geometry's only home. Server keeps a back-compat
  branch for in-flight uploads that still embed `content`.
- _2026-06-11_ — **Decision: no `topology.duckdb`.** Artifact set is exactly two
  files (viewer + eav); receive stays on v1 during dual-write. v2 purposes enum
  trimmed accordingly. Also: v2 upload endpoints landed (sign/complete) and the
  first convention-named `viewer.duckdb` reached MinIO end-to-end.
- _2026-06-11_ — **Decision: no dual-write — v2 owns the version.** The
  Completed-event listener is gone; the v2 `complete` endpoint itself verifies
  etags, finalizes staging → version-keyed names, creates the version
  (pre-minted id), and marks the ingestion Success. oda (Navis) flipped to
  v2-only: SendPipeline removed from the Importer/ModelConverter/
  NavisModelExtractor chain, `ArtifactPipeline.Process` returns the
  `ObjectReference`s oda builds its collections from, `UploadAsync` replaces
  `WaitForUpload`. Artifacts dir defaults to `%TEMP%/Speckle/artifacts` when
  `SPECKLE_DUCKDB_ARTIFACTS_DIR` is unset. Known gap: no per-object upload
  progress reporting yet (v1's RenderedStreamProgress was sendPipeline-only).
  Dwg/Revit importers still on v1, untouched.
- _2026-06-12_ — **Settled on single-phase direct duckdb writes (+ governance);
  two-phase NDJSON staging prototyped, verified, then removed.** The staged
  variant (extract → local `{ingestionId}.ndjson.gz` → replay into the writer)
  measurably de-stacked extraction from artifact-building: extraction loop hit
  1,027MB — BELOW v1's 1,064MB — and the replay ran flat at 1.1-1.5GB. Removed
  to keep the pipeline simple (one pass, no temp file, no re-parse); resurrect
  it from this session's history if pod peaks ever demand max(phase1, phase2)
  instead of their sum. What ships: direct writes with content-neutral
  governance — per-file memory_limit (append 256MB / index 1024MB via
  SPECKLE_DUCKDB_*_MB env vars), appender recycling every 25K objects, viewer
  connection closed before the eav index builds, and an aggressive GC at
  Complete() start (the giant root-last envelopes leave ~800MB of dead
  committed heap right before the heaviest phases). Full EAV + both indexes —
  artifacts unchanged. The MemoryLog profiler stays active.
- _2026-06-12_ — **Memory benchmarked end-to-end (led to the decision above).**
  Full-run phase-attributed profiling (MemoryLog: SDK + oda phase markers,
  SPECKLE_MEMORY_CSV) on the same model showed v2 peaking ~2.5GB vs v1's
  ~1.1GB client-side. Output-changing mitigations (dropping the eav index
  builds, the server-style >10MB EAV skip) were verified to work (peak →
  2.0GB, eav file 1.07GB → 397MB) but rejected — no regression on the
  artifact contract; the content-neutral governance was kept and folded into
  phase 2 of the two-phase build. Knowledge bank:
  (a) DuckDB fills its memory_limit as cache — budget = floor AND ceiling;
  (b) index builds are the one non-streamable step (~1GB at 29M rows, OOMs at
  512MB) and the ART indexes are ~2/3 of eav file size; (c) the giant
  collection envelope serialization (~0.6-1GB managed, exists in v1 too) is a
  v1 wire-format relic — redesigning element membership as rows instead of
  one JSON array is the real fix, belongs in the 4.0 contract. Pod sizing:
  plan ~3GB limit for current behavior; validate in a memory-capped Linux
  container.
- _2026-06-12_ — **Client EAV validated against server EAV on a real model.**
  Same source file sent through pristine v1 (server-built eav) and v2
  (client-built eav): 29.38M rows each, all property content equivalent.
  Two known differences, both accepted as-is: (1) number→value_text rendering —
  C# `"R"`/Newtonsoft style (`1.0`, `-9.98E-05`) vs JS `String(n)` (`1`,
  `-0.0000998…`); `value_num` doubles are bit-identical, so numeric queries are
  unaffected. A JS-compatible formatter (ECMA-262 Number::toString) was
  prototyped and validated byte-identical against all 557K distinct numbers +
  136K matrix strings the server produced, but **deliberately not shipped** —
  revisit only if a consumer turns out to string-match on value_text.
  (2) v2 extracts the root + element collections that v1 skips via its 10MB
  NDJSON-line limit (closure bloat) — v2 is a superset, kept. Notes: object ids
  are NOT comparable across runs (per-run `Guid.NewGuid()` applicationIds feed
  the content hashes); the two EAV indexes are ~67% of the eav file size and
  the index build peaks at ~800MB RSS for 29M rows (heaviest client-side step).
- _2026-05-…_ — **Direction sharpened:** SDK writes the duckdb files client-side
  and uploads to S3 directly; server post-processing (NDJSON parse, packfile
  build) eliminated for these artifacts. Draft DDL v1 for `viewer.duckdb`
  written into the contract section (RTC float32 geometry, per-primitive zstd
  blobs, float64 transforms, no GPU chunking in-file). Migration = dual-write.
- _2026-05-…_ — **Audited `speckle-viewer-webgpu`.** The viewer already bakes
  viewer artifacts (geometry .dat + primitives + placements + materials manifest)
  — in the browser, per user, by traversing the mega packfile. The viewer.duckdb
  contract is therefore mostly *discovered, not designed*: same information, one
  seekable file, baked once in the SDK instead of in every browser. Schema
  drafted from the viewer's real data model. Gaps: viewer can't read
  MeshBinary/SMSH yet; n-gon triangulation + float64→float32 downcast should
  move to bake time.
