# Speckle 4.0 — `objects.duckdb`, proxies-as-topology, SGEO

> Extension of [Speckle-4.0-Plan.md](../../Speckle-4.0-Plan.md). Captures the
> Integrations Board model: the artifact set is reframed, and the
> "no collection — all topology via proxies", "no speckle id / no `__closure` /
> applicationId-keyed", "opaque SGEO geometry blobs" direction is locked.
>
> The full per-primitive SGEO byte spec lives in its companion,
> [sgeo-binary-format.md](./sgeo-binary-format.md).

## Decisions locked (Q&A, 2026-06-16)

1. Topology = **adjacency list** (`id` + `parentId` per proxy row).
2. `objects.duckdb` is a **rename / reorganization** of the existing
   `viewer.duckdb` content model, not a greenfield rewrite.
3. `blobs.parquet` + connector-receive = **documented, implementation parked**
   (receive stays on v1 for the first slices).
4. Cutover = **no side-by-side** (v2 owns the version; flip per-consumer as
   gating items land).

**Two viewers, named:**
- **Viewer 2.0** — the **Three.js** viewer in
  `speckle-server-internal/packages/viewer` (`WorldTree`/`RenderTree`/
  `SpeckleConverter`/`PackfileManager`). **This is the target of this plan** — it
  is where the WorldTree is generated and where all viewer-side 4.0 work happens.
  Per the board's consumer questions, viewer-2.0 compatibility needs a new
  packfile loader (the build-from-proxies path below).
- **Viewer 3.0** — `speckle-viewer-webgpu`. Per the board, "compatibility with
  viewer 3.0 → work needed." We **will** work on it later: generating viewer-3.0
  artefacts **server-side** (instead of the browser-side bake it does today). This
  is explicitly **out of scope for this initial plan** — a much later stage,
  noted only so the arc is on record (see *Later — viewer 3.0*).

## Context — why this revision

The master plan settled the *mechanism* (client writes purpose-specific files,
server stores them) and shipped a working `viewer.duckdb` + `eav.duckdb` path
with validated client-side EAV. The board pushes the *content model* one step
further, attacking the costs the master plan only named:

- The serializer hashes every object to a speckle `id` and builds `__closure`
  tables; this bubbles object size from the leaves up to the root and is "the
  massive headache" on models where the root has millions of transitive refs.
- Topology is carried by `Collection.elements` nesting **plus** `speckle_type`
  **plus** proxies — three overlapping sources, with three divergent client
  builders (Revit `CollectionBuilder`, DWG `LayerUnpacker`, Navis flat).
- The server re-derives the scene tree by a recursive DOM walk that hard-fails
  without `obj.id` and dispatches on `speckle_type`.

4.0 deletes all four (`id`, `__closure`, Collections, `speckle_type`). The only
surviving object identity is the host **`applicationId`**. Topology becomes a
single flat `proxies` table. Geometry becomes an opaque binary blob (SGEO).

## The artifact set (revised — final shape 2026-06-16)

> **Update:** the original single `objects.duckdb` (geometries + proxies) is now
> **split into two files** — `geometries.duckdb` (the heavy binary blobs) and a
> lean `envelope.duckdb` (proxies only). This keeps the topology file small and
> independently fetchable from the geometry payload. eav is unchanged.

| File | Purpose (board) | Tables |
|---|---|---|
| `{versionId}.geometries.duckdb` | *the renderable geometry* | `geometries` |
| `{versionId}.envelope.duckdb` | *relationships between objects (no collection tree)* | `proxies` |
| `{versionId}.eav.duckdb` | *what the viewer doesn't need but all others do* | `eav` |
| `{versionId}.blobs.parquet` | *what connectors need* (receive fidelity) | `blobs` — **parked** |

### Relationship to the existing `viewer.duckdb` (rename / reorg — Decision 2)

This is a **reorganization of content that already exists**, plus three genuine
model changes. The render content (RTC float32 geometry, instance transforms,
materials) the current `viewer.duckdb` already carries is preserved; it is
re-keyed and re-shaped, not re-derived.

| Existing `viewer.duckdb` (master plan DDL) | → New `objects.duckdb` |
|---|---|
| `geometry(primitive_id, positions, indices)` + `primitives(...)` | `geometries(applicationId, content, id, type)` — content = one SGEO blob per buffer |
| `placements(seq, object_id, primitive_id, transform, material_id, color, path[])` | `proxies` rows: `instanceDef.instances[]` (transforms), `material`/`colour` (assignment), `layer`/`level`/`group` (the `path[]` becomes adjacency) |
| `materials(id, base_color, opacity, name)` | `proxies` rows of `type='material'` |
| `manifest(...)` | a `manifest` proxy row (or DB-level metadata) |
| `eav.duckdb` | unchanged |

**Genuinely new (more than a rename), called out so scope is honest:**
1. Primary key flips from content-hash `primitive_id`/`object_id` to host
   **`applicationId`**.
2. Topology moves from `placements.path[]` (ancestor-id array) to the
   `proxies` **adjacency list**.
3. Geometry blob gains the **SGEO family header** (was raw per-primitive zstd).

## Conceptual shifts (the substance)

- **No speckle `id`.** Delete `IdGenerator.ComputeId` + `SerializeBaseWithClosures`
  on the send path
  (`speckle-sharp-sdk/src/Speckle.Sdk/Serialisation/V2/Send/ObjectSerializer.cs`).
  Objects are addressed by `applicationId`.
- **No `__closure`.** Nothing builds or consumes closure tables on the new path.
- **No Collections.** `proxies` adjacency replaces `CollectionBuilder` /
  `LayerUnpacker` / Navis flat collections.
- **No `speckle_type`.** Dispatch on `geometries.type` (geometry) and proxy
  `type` (topology), not a type-name chain.
- **Geometry is an opaque SGEO blob** — no JSON, no per-type columns.

## Schemas

```sql
-- objects.duckdb
CREATE TABLE geometries (
  applicationId VARCHAR NOT NULL,   -- host object this buffer is displayed under (NOT unique)
  content       BLOB,               -- one SGEO buffer (NULL allowed if dedup'd by id — see §dedup)
  id            VARCHAR NOT NULL,    -- content hash of the SGEO bytes (geometry dedup key)
  type          VARCHAR NOT NULL,    -- mesh | line | polyline | point | polycurve | curve | arc | points
  PRIMARY KEY (applicationId, id)
);
CREATE TABLE proxies (
  type VARCHAR NOT NULL,             -- instanceDef | layer | material | colour | group | level
  data JSON   NOT NULL               -- per-type envelope (see §proxy-as-topology)
);

-- eav.duckdb (unchanged content model; columns per board + existing EavExtraction)
CREATE TABLE eav (
  applicationId            VARCHAR NOT NULL,
  path                     VARCHAR NOT NULL,
  value_string             VARCHAR,
  value_double             DOUBLE,
  value_boolean            BOOLEAN,
  unit                     VARCHAR,
  internal_definition_name VARCHAR,
  PRIMARY KEY (applicationId, path)
);

-- blobs.parquet (PARKED — schema only)
-- blobs(applicationId VARCHAR PK, content BLOB, format VARCHAR)  -- e.g. format='3dm' (breps)
```

## Proxy-as-topology (adjacency list — Decision 1)

All proxy `data` share an envelope. `objects` is always a list of
**applicationId** strings (matches today's `List<string>` on every proxy class).
`id` is the proxy's own stable id (its applicationId when it has one, else a
deterministic synthetic). `parentId` gives nesting; `null` = root.

Container types (`layer`, `level`, `group`) carry `parentId` and form the tree.
Attribute types (`material`, `colour`) and the instancing type (`instanceDef`)
carry `objects[]` reference edges, not tree edges.

```jsonc
// layer  — DWG layers + Revit Category/Type container tiers (the topology backbone)
{ "id":"L1/Walls/Basic Wall", "parentId":"L1/Walls", "name":"Basic Wall - 200mm",
  "objects":["wall-app-1","wall-app-2"], "color":-16777216, "visible":true }

// level  — Revit level container; value (elevation/name) also flattened to EAV
{ "id":"level:L1", "parentId":null, "name":"Level 1", "objects":[], "elevation":3000.0, "units":"mm" }

// instanceDef  — definition members + placements folded in (keeps the 6-value type enum fixed)
{ "id":"<defAppId>", "name":"Block A", "maxDepth":0,
  "objects":["<memberAppId>", ...],
  "instances":[ { "applicationId":"<instAppId>", "parentId":"layer:Blocks",
                  "transform":[1,0,0,0, 0,1,0,0, 0,0,1,0, tx,ty,tz,1], "units":"mm" } ] }

// material — RenderMaterialProxy shape; objects = applicationIds it paints
{ "id":"mat:steel", "objects":["<appId>", ...],
  "value":{ "diffuse":-8355712, "opacity":1.0, "metalness":0.9, "roughness":0.3, "name":"Steel" } }

// colour — ColorProxy; preserve `source` (object|layer|block) the inheritance logic needs
{ "id":"col:1", "objects":["<appId>", ...], "value":-65536, "source":"object" }

// group — selection/attribute grouping; optional parentId if groups nest
{ "id":"grp:1", "name":"Group 1", "objects":["<appId>", ...], "parentId":null }
```

**Worked example — Revit `Level → Category → ElementName`** (replaces
`speckle-oda/src/Revit/ODA.DataExtraction.Revit/Extraction/CollectionBuilder.cs`):
container tiers carry empty `objects`; only the leaf tier lists element
applicationIds. Family/category/level also go to EAV (already indexed by
`EavExtraction`), so explorer labels and filterable attributes stay in sync
without duplicating structure.

**Worked example — DWG layer tree** (replaces
`LayerUnpacker.BuildLayerCollectionHierarchy`): one `layer` proxy per layer-path
segment, `parentId` = previous segment, leaf `objects` = entities, `color` from
the DWG layer. Revit + DWG + Navis now emit the **same** adjacency structure —
one proxy emitter replaces three divergent builders.

**Single-parent enforcement:** adjacency is DAG-capable, but the explorer is a
tree. Enforce single-parent at SDK write time; express sharing via
groups/instances, not multi-parenting. (Model stays DAG-capable for the future.)

## SGEO family format (summary — full spec in companion)

The SDK **owns** the SGEO binary geometry family. Full byte-level spec, every
primitive body, size comparison, and golden hex vectors are in
[sgeo-binary-format.md](./sgeo-binary-format.md). Summary here:

**Header (16 bytes), shared by all primitives:**

```
0x00  4  magic           "SGEO"
0x04  1  version         = 1
0x05  1  primitive_type  see table
0x06  2  flags           uint16 bitfield (closed/rational/periodic; mesh normals/uvs/colors; points colors/sizes)
0x08  2  units_code      uint16, Units.GetEncodingFromUnit
0x0A  2  reserved        = 0
0x0C  4  crc             CRC32 of body bytes only
0x10  …  body            per primitive_type
```

**`primitive_type` codes:**

| code | type | | code | type |
|---|---|---|---|---|
| 0 | mesh | | 6 | circle |
| 1 | line | | 7 | points (Point/Pointcloud) |
| 2 | polyline | | 8 | ellipse |
| 3 | polycurve | | 9 | spiral |
| 4 | curve (NURBS) | | 10 | box |
| 5 | arc | | | |

`Surface` (NURBS) and `Region` are **out of scope** for SGEO v1. `Brep`/`BrepX`/
`ExtrusionX`/`SubDX`/`SolidX` are **not** SGEO primitives — their authoritative
form goes to `blobs.parquet` (e.g. `3dm`) and their `displayValue` meshes are
written as SGEO **mesh** rows.

## `geometries` keying & dedup

- **`applicationId`** = the host object the buffer is displayed under. Not unique
  (multi-geometry objects → multiple rows). Join key to proxies + EAV.
- **`id`** = content hash of the SGEO bytes only. The **geometry dedup key**, NOT
  the old speckle object id. This reconciles "eliminate id hashing": we delete
  graph-hashing + closure building, and keep a cheap O(bytes) hash of a flat blob.
- **Instancing dedup** (block placed 10k times) is via proxies, not the PK:
  unique buffers live under `instanceDef.objects`; placements are
  `instanceDef.instances[]` rows of one 16-float transform each. Server attaches
  the same geometry rows as `instanced` children per placement.
- **Multi-geometry object:** N rows, same `applicationId`, distinct `id`/`type`.
  The grouping-by-applicationId *is* the old `displayValue` array.
- **Cross-object storage dedup** (two distinct objects, identical geometry):
  start one-blob-per-row (simplest); add `content=NULL` + `id`-join dedup only if
  a real model shows the win. *(Open decision — see below.)*

## Server WorldTree reconstruction (proxies + geometries + eav)

Replace the recursive DOM walk in
`speckle-server-internal/packages/viewer/src/modules/loaders/Speckle/SpeckleConverter.ts`
with a **build-from-proxies** pass:

1. **Build proxy indices** (one linear scan): `nodeById`, `childrenByParent`
   (from `parentId`), `materialByAppId`/`colourByAppId` (from `objects[]`),
   `defByAppId`/`defMembers`/`placements`, and `objectParent` (inverted from each
   container's `objects[]`).
2. **Materialize container tree:** DFS from each `parentId==null` proxy over
   `childrenByParent`, minting one TreeNode per proxy with
   `node.model.id = proxyId` (synthetic, deterministic — this is where "id is
   gone" is handled). Existing dedup-by-id guard still works (proxyIds unique).
3. **Attach leaf objects:** for each container's `objects[]`, look up geometry
   rows by `applicationId`; one atomic container node + N non-atomic geometry
   children (multi-geometry). Properties/labels come from EAV joined on
   `applicationId`, not a `raw` DOM.
4. **Instances:** per `instanceDef.instances[]` placement, create a Transform
   node under `placement.parentId`, attach def member geometries as `instanced`
   children. The current two-phase `convertInstances` consume/remove dance is
   **deleted** — def geometry was never in the container tree.
5. **Material/colour:** resolve O(1) from `materialByAppId` with
   `objectParent` fallback — replaces the `RenderTree` ancestor-walk and the
   "bake parentLayerApplicationId onto the node" hack.

Dependency → replacement:

| Current hard-dependency | Location | Replacement |
|---|---|---|
| `if (!obj.id) return` | SpeckleConverter.ts:131 | node id minted from proxyId; leaves keyed by applicationId |
| `speckle_type` dispatch | :127,:156,:364 | dispatch on `geometries.type` / proxy `type` |
| `elements` recursion | :220-235 | `parentId` adjacency |
| material inheritance ancestor-walk | RenderTree.ts:117-154 | O(1) `materialByAppId` + `objectParent` fallback |
| `convertInstances` consume/remove | :722-783 | deleted; members fetched by applicationId |

Transform compounding for nested instances (RenderTree.ts:156-175) is retained.

## Per-repo work

**speckle-oda (client — parse + write directly):**
- Replace `CollectionBuilder` / `LayerUnpacker` / Navis flat collections with one
  **proxy emitter** producing `layer`/`level`/`group` adjacency.
- During conversion, write SGEO blobs → `geometries`, flattened props → `eav`,
  proxy relationships accumulated in memory → `proxies` as the last step.
- No more C# wrapper objects for serialization; no calls into the id/closure path.
- Generalize the `ArtifactPipeline` path already used by Navis
  (`speckle-oda/src/Navis/Speckle.ODA.Navis/Importer.cs`) to Revit/DWG.

**speckle-sharp-sdk (client — owns format + row inserts):**
- Extend
  `speckle-sharp-sdk/src/Speckle.Sdk/Pipelines/Send/Artifacts/DuckDbArtifactWriter.cs`:
  `objects`/`root`/`blobs` → `geometries(applicationId,content,id,type)` +
  `proxies(type,data)`; keep `eav`; add `blobs.parquet` writer (parked behind the
  schema).
- Add the **SGEO encoder** (family header + per-primitive bodies) extending
  `MeshBinaryEncoder`; `Units.GetEncodingFromUnit` for `units_code`; CRC32 of body.
  See [sgeo-binary-format.md](./sgeo-binary-format.md).
- **Tests** (first-class): round-trip per primitive type (encode→decode equality),
  header/CRC/units assertions, golden hex vectors matching the board's line &
  circle examples — under
  `speckle-sharp-sdk/tests/Speckle.Objects.Tests.Unit/Geometry/` (mirror
  `MeshBinaryTests.cs`).

**speckle-server-internal (Viewer 2.0 / Three.js — build WorldTree + render):**
This is where the WorldTree for the viewer is generated; the Three.js viewer
lives in `packages/viewer` (`WorldTree.ts`, `RenderTree.ts`, `SpeckleConverter.ts`,
`SpecklePackfileLoader.ts`, `PackfileManager`). All viewer-side 4.0 work happens
here — there is no separate viewer repo in scope for this plan.
- Rebuild the scene tree from proxies + geometries + eav (algorithm above) inside
  the loader/`SpeckleConverter` path; delete the
  id/`speckle_type`/`elements`/`convertInstances` dependencies.
- Add an **SGEO decoder (TS)** feeding the geometry converter — one decode path
  per `primitive_type`, producing the Three.js `BufferGeometry` the `RenderTree`
  expects. (Mirrors the C# encoder; the SDK owns the format, the viewer mirrors
  the decode.)
- Validate the viewer loads all geometries with correct topology (explorer tree,
  instancing, materials).

## `blobs.parquet` + connector-receive (documented, parked — Decision 3)

Schema specified above. CNX-receive flow (from the board), to implement later:
`eav → all unique applicationIds → per applicationId get type → (optional
topology check) → fetch geometry from blob store or geometry store → convert &
place in host app`. Receive stays on the v1 path for the first slices.

## Later — viewer 3.0 (`speckle-viewer-webgpu`), server-side artefacts

**Explicitly out of scope for this initial plan — a much later stage.** Viewer 3.0
(`speckle-viewer-webgpu`) today bakes its render artefacts in the browser
(per-user DFS traversal of the packfile into OPFS `.dat`/primitives/placements/
manifest). The future move — mirroring the SDK→server direction of this plan — is
to **generate viewer-3.0 artefacts on the server side** (from `objects.duckdb`:
proxies + SGEO geometries) and serve them, so the browser skips the bake. This is
the board's "compatibility with viewer 3.0 → work needed" item. It is sequenced
*after* viewer 2.0 (Three.js) renders the slice end-to-end; it does not block
anything in this plan, and is recorded here only so the direction is on file.

## Cutover — no side-by-side (Decision 4)

Keep the master plan's stance: v2 owns the version; flip per-consumer as gating
items land, no production dual-run. Gating items (restated for the new model):
1. ✅ v2 `complete` creates the version (done per master plan).
2. Viewer direct-load of `objects.duckdb` (proxies + geometries + SGEO).
3. `/eav/download` resolves the v2 eav key by convention.

## Rollout — vertical slice first

One representative Revit model exercising the hard cases (levels + categories,
instanced families, multi-geometry walls, render materials), end-to-end, before
widening to DWG / Navis / federated multi-unit:
- **Stage 0** — hand-author a small `objects.duckdb` (proxies + a few SGEO blobs)
  for the slice model and drive the new build-from-proxies path in
  speckle-server-internal's Three.js viewer; validate the proxy schema + SGEO
  decode actually render before any C# is written.
- **Stage 1** — SDK: `geometries`/`proxies` tables + SGEO encoder + tests.
- **Stage 2** — oda: proxy emitter replacing the collection builders.
- **Stage 3** — server/Three.js viewer: build-from-proxies + SGEO decode; renders.
- **Stage 4** — delete `__closure`, `speckle_type` emission, Collections,
  `IdGenerator.ComputeId` for speckle objects. Geometry content-hash stays.

## Open decisions / TODO

1. **Cross-object geometry storage dedup** — one-blob-per-row vs `content=NULL` +
   `id`-join. (Recommend defer until a model proves the win.)
2. **Geometry `id` hash** — SHA256 (cross-compat) vs xxh3 (client speed).
3. **`manifest`** — a `proxies` row vs DB-level metadata.
4. Confirm the 6-value `proxies.type` enum is fixed (drove folding instance
   placements into `instanceDef.instances[]`).

## Verification

- **SGEO unit tests** (SDK): round-trip + golden hex vectors vs the board examples.
- **Schema "is it wired" SQL checks**: every `geometries.applicationId` resolves
  in `eav`; every proxy `objects[]`/`parentId` resolves; no orphan placements.
- **Render parity**: the Three.js viewer (speckle-server-internal) renders the
  slice model from `objects.duckdb` and visually matches the current v1 render (A/B).
- **End-to-end**: oda send → server store → Three.js viewer direct-load of the
  slice model, topology (explorer tree), instancing, and materials correct.
