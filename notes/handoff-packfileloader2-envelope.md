# Context handoff — consuming the 3-artefact envelope in PackfileLoader2 (viewer 2.0)

**Audience:** the PackfileLoader2 consumer chat. **Producer:** Speckle.Sdk artefact pipeline
(`src/Speckle.Sdk/Pipelines/Send/Artifacts/*`), driven by the ODA-Revit POC. As of **2026-06-20** all
three artefacts are **direct Zstd parquet** (no DuckDB write engine). Validated data-identical to the prior
DuckDB form on Trabzon T1/T2. SDK commit `98d381cf` on branch `topology-envelope-poc`.

> ⚠️ **Filenames below are PROVISIONAL.** The `{base}.eav.*` / `{base}.envelope.*` scheme is what the
> producer emits *today*. A different naming scheme is being settled in **server-changes-v2-data-endpoints**.
> Treat the **table shapes + view contract as stable**; treat **filenames as a variable** you'll rebind once
> that lands. The producer's only naming touch-points are `P(suffix)` + `Manifest()` in each writer.

---

## The three artefacts

For a model with base name `{base}`:

### 1. `geometries` — the renderable blobs
- `{base}.geometries.parquet` — int-keyed (`geometry_index`), SGEO-encoded mesh/brep blobs, `CompressionMethod.Zstd`.
- This is the **only** place vertex/index/transform-baked geometry lives.

### 2. `eav` — object set + identity dictionary + per-object labels
Six parquet files + a SQL manifest:
| file | columns | meaning |
|---|---|---|
| `{base}.eav.objects.parquet`     | `object_index:int, application_id:string` | **the K dictionary**: dense int ↔ applicationId, one row per atomic object |
| `{base}.eav.paths.parquet`       | `path_index:int, path:string` | shared property-path vocabulary (interned) |
| `{base}.eav.eav.parquet`         | `object_index:int, path_index:int, value_string:string?, value_double:double?, value_boolean:bool?, unit:string?, internal_definition_name:string?` | **instance**-scoped property rows |
| `{base}.eav.types.parquet`       | `type_index:int, type_key:string` | type dictionary |
| `{base}.eav.type_eav.parquet`    | same value shape, keyed by `type_index` | **type**-scoped params, written **once per type** (deduped) |
| `{base}.eav.object_type.parquet` | `object_index:int, type_index:int` | weak ref: object → its type |
| `{base}.eav.manifest.sql`        | — | `CREATE VIEW` over all of the above + the flat-read `object_properties` view |

### 3. `envelope` — the topology (relations + nodes + self-describing catalog)
| file | columns | meaning |
|---|---|---|
| `{base}.envelope.relations.parquet` | `rel:int, src:int, dst:int, ord:int` | typed edges; `src`/`dst` namespace is fixed by `rel` |
| `{base}.envelope.nodes.parquet`     | `id:int, kind:int, name:string?, def_ref:int?, transform:string?, units:string?, argb:int?, opacity:double?, metalness:double?, roughness:double?, elevation:double?` | shared value-entities |
| `{base}.envelope.meta.parquet`      | `schema_version:int, produced_by:string` | catalog |
| `{base}.envelope.rel_types.parquet` | `rel:int, name:string, src_ns:string, dst_ns:string` | catalog: what each `rel` means + its namespaces |
| `{base}.envelope.node_kinds.parquet`| `kind:int, name:string` | catalog |
| `{base}.envelope.manifest.sql`      | — | `CREATE VIEW` over the five tables |

---

## Identity model (read this before joining anything)

- **Per-namespace dense int32 K.** Three independent id spaces: **object**, **geometry**, **node**.
  ids overlap *across* namespaces (object 5 ≠ geometry 5 ≠ node 5). The `rel` column is what tells you which
  namespace `src`/`dst` live in — never assume.
- **eav owns the object↔applicationId dictionary** (`eav.objects`). The envelope only ever stores ints; to
  get an applicationId for an object id you join through `eav.objects`. Geometry ids index into
  `geometries.parquet`; node ids index into `envelope.nodes`.

### `rel` enum (relations.rel) — also self-described in `rel_types`
| rel | name | src ns → dst ns | meaning |
|---|---|---|---|
| 1 | DISPLAY | object → geometry | direct world-coord renderable mesh; `ord` = fragment index |
| 2 | SOLID | object → geometry | authoritative Brep/Solid |
| 3 | SUBELEMENT | object → object | host→hosted nesting (curtain wall → panels) |
| 4 | DEFINES | node(DEFINITION) → geometry \| node(nested INSTANCE) | definition membership |
| 5 | HAS_MATERIAL | geometry → node(MATERIAL) | **per-mesh** render material |
| 6 | HAS_COLOR | geometry\|object → node(COLOR) | display colour |
| 7 | ON_LEVEL | object → node(LEVEL) | level membership |
| 8 | DISPLAY_INSTANCE | object → node(INSTANCE) | renderable via a placement (transform + definition) |

> **DISPLAY (1) vs DISPLAY_INSTANCE (8) are split on purpose.** Direct geometry vs instanced placement use
> different dst namespaces (geometry vs node), and per-namespace ids overlap — a single rel would be
> ambiguous. If you want "everything renderable for object X": union rel 1 (→ geometry blobs) with rel 8
> (→ INSTANCE node → its transform + DEFINES geometries).

### `kind` enum (nodes.kind) — also self-described in `node_kinds`
| kind | name | populated columns |
|---|---|---|
| 1 | DEFINITION | `name` |
| 2 | INSTANCE | `def_ref` (→ DEFINITION node id), `transform` (16 row-major doubles, comma-sep), `units` |
| 3 | MATERIAL | `argb` (packed int), `opacity`, `metalness`, `roughness` |
| 4 | COLOR | `argb` |
| 5 | LEVEL | `name`, `elevation` |

---

## Consumer read model — ⚠️ CORRECTED 2026-06-20 (validated E2E): manifests are NOT served

**The earlier draft of this doc said "run the two `manifest.sql` files" as the entry point. That is WRONG for
anything fetching from the server.** End-to-end validation proved the producer uploads **only the parquet
files**; the two `manifest.sql` are **producer-local only — never uploaded, never served.** So a server-fed
consumer (PackfileLoader2) never sees a `.sql` file and **must build its own views.**

How the server actually hands you a version's bundle:

1. `GET /api/v2/projects/{p}/models/{m}/versions/{v}/artifacts` → `{ files: [{ name, url, expiresAt }] }`,
   where `url` is a **presigned S3/MinIO GET** (3600s) and `name` is the bare basename
   (`{versionId}.eav.objects.parquet`, … — exactly the 12 you'll load: 6 eav + 5 envelope + 1 geometries).
   The new shape is flagged by the version's **`schemaVersion = 3`** (GraphQL `Version.schemaVersion`;
   there is **no `dataShape` field** yet — branch on `schemaVersion`).
2. Fetch each parquet (to OPFS, or range-read the presigned URL via DuckDB httpfs), then **create the views
   yourself** — one `CREATE VIEW <table> AS SELECT * FROM read_parquet('<local-or-url>')` per file, plus the
   `object_properties` union below. The view SQL is NOT shipped; **embed it in the loader.** (The producer no
   longer emits any `manifest.sql` — the canonical read recipe + the `object_properties` view live in
   `notes/topology-envelope-SOT.md` §4/§6; copy from there into code.)
3. Join across the now-registered tables on the dense ids.

**Two reads PackfileLoader2 specifically needs:**

- **"All unique applicationIds in the model"** → `SELECT application_id FROM objects` (that's the whole
  `eav.objects` dictionary; it IS the atomic-object set). No scan of property rows needed.

- **"All properties for an object" (instance ∪ type, flat)** → the shipped `object_properties` view already
  unions instance eav with the deduped type_eav via `object_type`:
  ```sql
  -- Embed this in the loader (it's the object_properties view from EavWriter.Manifest() — NOT a fetched file):
  CREATE VIEW object_properties AS
    SELECT object_index, path_index, value_string, value_double, value_boolean, unit, internal_definition_name
      FROM eav
    UNION ALL
    SELECT ot.object_index, te.path_index, te.value_string, te.value_double, te.value_boolean, te.unit, te.internal_definition_name
      FROM object_type ot JOIN type_eav te ON te.type_index = ot.type_index;
  ```
  So a consumer never has to know type dedup happened — query `object_properties JOIN paths` and you get the
  full flat property set per object. Type params live once on disk, re-expand in the view.

**Instancing contract (the bit PackfileLoader2 cares most about):** an instanced object carries no baked
geometry under DISPLAY. Instead: object —rel 8→ INSTANCE node; that node's `transform`+`units` give the
placement, `def_ref` points at a DEFINITION node; DEFINITION —rel 4→ the shared geometry blobs (and/or nested
INSTANCE nodes for nested instancing). So the shared mesh is stored **once** in `geometries.parquet` and
referenced by N instances — exactly the "don't pollute geometry with transforms; share raw + place via node"
design. Material is per-mesh (rel 5 off the geometry id), not per-object.

---

## What changed vs. what didn't (for the consumer)

- **Changed:** storage is now N small parquet files per artefact (eav 6 + envelope 5 + geometries 1 = the 12
  served), instead of one `.duckdb`. Sizes ~67% smaller (eav ~87%, envelope ~81%). The producer also writes a
  `manifest.sql` per artefact, but those are **local-only / not served** — see the corrected read model above.
- **Unchanged:** table/column names, the id model, the rel/kind enums, and the `object_properties` flat
  contract. A consumer written against the DuckDB views ports by swapping "open db" for "create these views
  over `read_parquet`" (the SQL is the same; you just run it yourself instead of opening a db or a `.sql`).
- **Settled (was provisional):** filenames are `{versionId}.<artefact>.<table>.parquet`, keyed server-side
  under `versions/{versionId}/`. `{versionId}` is the server pre-allocated id (== the commit id). Validated E2E.

## Pointers
- Producer writers: `EavWriter.cs`, `EnvelopeWriter.cs`, `ParquetTableWriter.cs`, `GeometriesParquetWriter.cs`
  (all under `src/Speckle.Sdk/Pipelines/Send/Artifacts/`).
- Canonical design: `notes/topology-envelope-SOT.md` (§2 tables, §3 encoding/producer facts, §4 consumer
  reads, §6 catalog + type-param dedup, §7 invariants).
- Round-trip examples (read parquet → assert shape): `tests/Speckle.Sdk.Tests.Unit/Pipelines/
  EnvelopeWriterTests.cs`, `EavTypeDedupTests.cs` — both show the exact attach/view/query pattern a consumer uses.
