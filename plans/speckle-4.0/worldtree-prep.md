# Prep — WorldTree generation for envelope + binary artifacts

> Prep notes for the server-side WorldTree work (speckle-server-internal). Written
> while the server is live for benchmarks — **nothing in speckle-server-internal
> was modified**; this is read-only investigation + a plan. All line refs are from
> a read-only pass and should be re-confirmed at implementation time.
>
> **Update (2026-06-18) — topology source revised.** Decision (with colleague):
> build topology from **`eav.duckdb` pivots + `proxies`** (see
> [objects-duckdb-proxies-sgeo.md §Topology source revision](./objects-duckdb-proxies-sgeo.md)).
> The explicit `parentId` / `layer` / `level` / `group` adjacency in the
> "build-from-proxies" sections below is now the **fallback** behind the open
> `source_tree`/`CollectionProxy` question (2.1), not necessarily the primary
> path; pivot EAV first. The standalone `nodes`-table variant is **not adopted**.

## TL;DR
- **Envelope (viewer.duckdb) WorldTree is already closure-free** for the v2
  duckdb loader. Dropping `__closure` does **not** break it. Expect little/no
  surgery — mostly verification.
- **Binary WorldTree is the real work** (from the `geometries.parquet` +
  `envelope.duckdb` triple — earlier drafts called this `objects.duckdb`): a new
  build-from-proxies path (no `speckle_type`/`id`/`__closure`/collections;
  applicationId-keyed).
- One cross-cutting caveat: the **legacy HTTP object path** (`download.ts` +
  `ObjectLoader2`/`Traverser`) *does* use `__closure` to enumerate a version's
  objects. It is NOT the v2 duckdb path, but anything still hitting it (old 2.0
  viewer over HTTP, connector-receive via object loader) would break on
  closure-less versions. Flag, don't fix yet.

## Two loading paths — they treat `__closure` differently
| Path | Used by | Enumerates children via | Closure-dependent? |
|---|---|---|---|
| **duckdb / v2** | `SpecklePackfileLoader` → `PackfileManager` (duckdb) + `SpeckleConverter.traverse` | `elements` / `displayValue` / `referencedId` resolved by id against the `objects` table | **No** (closure explicitly skipped) |
| **legacy HTTP** | `ObjectLoader2` + `Traverser`, server `download.ts` | `root.__closure` keys | **Yes** |

The 4.0 viewer path is the duckdb one. Evidence:
- `SpecklePackfileLoader.initLoader()` wires `getTotalObjectCount → packfileManager.getObjectCount()` = `SELECT COUNT(*) FROM objects` — table count, not closure.
- `SpecklePackfileLoader.load()` → `getRootObject()` (`SELECT id,data FROM root LIMIT 1`) → `converter.traverse(root, cb)`.
- `SpeckleConverter.traverse()` resolves children by `referencedId` via `loader.getObject(id)` (duckdb lookup) and recurses into `elements`/`@elements`; **`__closure` is skipped** (~SpeckleConverter.ts:241). The `elements`/`displayValue` reference arrays are still serialized by `SerializerV2` — only `__closure` was removed.

## Envelope WorldTree — status & plan (start here)
**Status: works as-is on the v2 path, closure-free.** The envelope changes we made
(no `__closure`, eav category/InstanceProxy exclusions) don't touch the WorldTree:
- WorldTree is built from `viewer.duckdb` **objects** (speckle_type + collections +
  references), not from eav — so the eav exclusions are irrelevant to rendering.
- `traverse()` never read `__closure`, so removing it is a no-op for tree building.

**To verify when we start (the whole "envelope" task is mostly this):**
1. Load a closure-less `viewer.duckdb` version in the viewer → confirm full tree
   (node count ≈ prior), instances placed, materials applied.
2. `traversals/total` progress: `total = COUNT(*) FROM objects` (332,589 incl.
   proxies/blobs) while `traversals` only counts renderable nodes → progress bar
   may not reach 1.0. Cosmetic; decide whether to refine the denominator.
3. Confirm nothing in the **render** path calls the HTTP `download.ts` for these
   versions (it would hit the closure enumeration). If the frontend "raw object"
   / data-tab / receive uses it, note it as a separate follow-up.

**Likely changes: none-to-minimal** (maybe the progress denominator). The envelope
WorldTree essentially already supports closure-less artifacts.

## Binary WorldTree — the real surgery (build-from-proxies)
Target: build the WorldTree from `geometries.parquet` (`geometries(applicationId,
content, id, type)`) + `envelope.duckdb` (`proxies(type, data)`) + `eav` — no
`speckle_type`, no `id`, no `__closure`, no collections. Algorithm already specified in
[objects-duckdb-proxies-sgeo.md](./objects-duckdb-proxies-sgeo.md) (§Server WorldTree
reconstruction). Server/viewer touch points to implement:

| Touch point | File (server) | Change |
|---|---|---|
| Object source | `packages/packfile-manager/src/packfileManager.ts` `getObject`/`getObjectCount`/`getRootObject` | New readers for `geometries`/`proxies` (no `root`/`objects` table); key by applicationId; count = geometries rows or a manifest |
| Tree build | `packages/viewer/.../SpeckleConverter.ts` `traverse` | Replace DOM-walk with build-from-proxies: index proxies → DFS `layer`/`level`/`group` adjacency → attach geometry rows by applicationId → instances from `instanceDef.instances[]` |
| Type dispatch | `SpeckleConverter` `getSpeckleType`/`NodeConverterMapping` | Dispatch on `geometries.type` (mesh/line/…) + proxy `type`, not `speckle_type` |
| Geometry decode | `SpeckleConverter` geometry converters | New **SGEO decoder (TS)** → Three.js `BufferGeometry` (mesh first) |
| Material/colour | `RenderTree.ts` ancestor-walk | O(1) `materialByAppId` / `colourByAppId` from proxy `objects[]` |
| Node identity | `SpeckleConverter`/`WorldTree` (id check ~:131) | Mint node id from proxyId; leaves keyed by applicationId |

No `__closure` consumption to remove on this path — there's none; topology is the
proxy adjacency.

## Open questions / decisions (revisit at start)
1. Envelope progress denominator (use renderable count vs `COUNT(*)`).
2. Does any live consumer still use HTTP `download.ts` for v2 versions? (closure
   enumeration there is closure-dependent; out of scope but must be known.)
3. Binary: where does `getObjectCount`/progress total come from without an objects
   table — geometries row count, or a manifest proxy row?
4. Binary SGEO TS decoder: mesh-only first (matches geometry we emit today), then
   line/polyline/etc.

## Execution checklist (when user says "start", envelope first)
- [ ] Load closure-less viewer.duckdb in viewer; confirm tree/instances/materials.
- [ ] (If needed) fix progress denominator.
- [ ] Confirm no render-path dependency on HTTP download for these versions.
- [ ] Then: binary — PackfileManager readers → SpeckleConverter build-from-proxies
      → SGEO TS decoder (mesh) → material/colour by applicationId.
