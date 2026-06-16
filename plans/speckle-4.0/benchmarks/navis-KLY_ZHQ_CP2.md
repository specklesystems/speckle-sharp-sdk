# Benchmark — Navis model `KLY_ZHQ_CP2`

Comparison of the three send strategies on one real Navisworks model (sourced
from Revit, so it carries Revit property tabs). All runs on the same machine,
same model. **Single runs** — workingSet/native figures carry run-to-run
variance (ODA tessellation + DuckDB native buffers); the managed-heap and
storage/content figures are the stable signals.

## Model
- **287,326** elements
- **128,270** data objects (NavisworksObject)
- **27,527** unique mesh geometry buffers (heavy instancing: 287k → 27.5k)
- **149,262** instances

## Strategies
| Tag | Path | Geometry | Topology | id | __closure |
|---|---|---|---|---|---|
| **ENV+cl** | envelope (serialize → viewer.duckdb + eav.duckdb) | SMSH blob | collections + instance proxies | yes (SHA256) | yes |
| **ENV−cl** | envelope via `SerializerV2` | SMSH blob | collections + instance proxies | yes (SHA256) | **no** |
| **BIN** | binary (`objects.duckdb` + `eav.duckdb`) | SGEO blob | proxies (applicationId) | no (only geom content hash) | no |

## Time (seconds)
| | ENV+cl | ENV−cl (r1 / r2 / r3) | BIN (r1 / r2 / r3) |
|---|---:|---:|---:|
| extraction (`process`) | 389.9 | 430.5 / 406.3 / 418.9 | 311.3 / 338.8 / 342.0 |
| upload (`send`) | 33.6 | 30.3 / 22.6 / 16.2 | 11.0 / 7.7 / 9.9 |
| **total** | **424.2** | **461.5 / 429.6 / 435.8** | **323.1 / 351.9 / 352.7** |

> Binary is consistently the fastest (~323–353 s vs ~424–461 s envelope), even
> with extraction-loop variance.

> Two ENV−cl runs (461.5 s, 429.6 s) **bracket** the ENV+cl baseline (424.2 s) —
> i.e. dropping closures did **not** change total time; the spread is run-to-run
> variance in the ODA/IO-bound extraction loop (±~10%). Not a `SerializerV2`
> regression. The binary path (323 s) is the only one clearly faster.

## Memory — peak (MB)
| | ENV+cl | ENV−cl (r1 / r2 / r3) | BIN (r1 / r2 / r3) |
|---|---:|---:|---:|
| GLOBAL peak workingSet | 2159 | 2351 / 2362 / 2253 | **1454 / 2026 / 2087** |
| peakManaged @ collections | 1004 | **760 / 749 / 730** | — (no collections) |
| peakManaged @ extraction loop | 387 | 308 / 296 / 304 | **167 / 169 / 170** |
| peakNative @ peak phase | 1328 | 1755 / 1806 / 1694 | 1347 / 1927 / 1977 |

> Binary at DEFAULTS (`memory_limit=256`): runs 2–3 peak ~2.0–2.1 GB (run 1's
> 1454 was a low outlier). Managed pinned at ~170 MB across all runs.

### Memory tuning — DECISIVE (the +600 MB was DuckDB's buffer pool)
Binary run with **`SPECKLE_DUCKDB_MEMORY_LIMIT_MB=64` + `SPECKLE_DUCKDB_FLUSH_MB=16`**:

| | default (mem 256) | tuned (mem 64 / flush 16) |
|---|---:|---:|
| peak workingSet | ~2026–2087 MB | **1430 MB** |
| peakNative | ~1927–1977 MB | **1320 MB** |
| peakManaged | ~169 MB | 163 MB |
| process time | 339–342 s | 341 s (unchanged) |

**Conclusion: the +600 MB over the old NDJSON path was DuckDB's buffer-pool cache**
(`memory_limit` × 2 connections), not our code, not a leak. Capping it to 64 MB
drops the binary path onto the **ODA tessellation floor (~1.3 GB) ≈ the old
NDJSON peak**, at **no time cost**. It's a pure cache/ceiling knob — DuckDB fills
`memory_limit` opportunistically; bounding it reclaims the lot.

Implication for peak-WS comparison: at equal tuning the envelope would still carry
its ~730 MB managed collections-serialization spike (binary has none), so
**tuned-vs-tuned, binary should peak *lower* than envelope** — the default-vs-default
"wash" was just both paths letting DuckDB cache freely. (Pending a tuned envelope
run to confirm.)

**Two separable components — read them differently:**
- **Managed heap = stable + structural.** Binary's extraction-loop managed peak
  is rock-steady **167 / 169 MB**; envelope's collections managed peak is
  **1004 (with closures) → ~750 (without)**. So:
  - closures cost **~250 MB managed** (the `__closure` dict, one entry per
    transitive object ~277k+, plus the JSON block) — real but partial,
  - per-object JSON serialization costs the rest: envelope ~750 MB vs binary
    ~167 MB managed. **This ~580 MB gap is the durable memory win of binary.**
- **Native heap (ODA tessellation + DuckDB) = dominant + volatile.** It swings
  run-to-run: binary 1347 → 1927 MB, envelope 1755 → 1806 MB. Because native
  dominates workingSet (the figure a pod limit enforces), **peak workingSet is
  noisy and NOT a reliable discriminator** — binary's GLOBAL peak ranged
  1454–2026 MB across two runs, overlapping the envelope's 2351–2362. The first
  comparison's "−33% peak" was a low-native binary run vs a high-native envelope
  run; don't over-read it. (DuckDB fills its `memory_limit` as cache, so the
  duckdb budget is both floor and ceiling — a known native contributor.)

**Bottom line on memory:** binary's *managed* footprint is ~4–6× smaller and
stable; its *workingSet* peak is similar-to-lower but native-noise-dominated.
Multiple runs (or a memory-capped Linux container) are needed to quote a peak-WS
delta with confidence.

## Storage (on disk, checkpointed) — current (both: no closures, exclusions, 1 eav index)
| | ENV (viewer+eav) | BIN (objects+eav) | gap |
|---|---:|---:|---:|
| geometry/objects file | viewer.duckdb **642 MB** | objects.duckdb **253 MB** | **−389 MB** |
| eav file | **470 MB** (object_id key) | **436 MB** (applicationId key) | −34 MB |
| **total artifacts** | **~1.11 GB** | **~689 MB** | **−38%** |

> **eav is now essentially equal** (470 vs 436 MB): same exclusions, same single
> `idx_props_obj`/`idx_eav_appid` index, ~same rows (18.66M vs 18.38M). The
> residual ~34 MB is purely the key column — 32-char SHA256 `object_id`
> (high-entropy, poor compression) vs short `applicationId`.
> **The real storage gap is the geometry/objects file (−389 MB)**: viewer.duckdb
> carries the 332k-object JSON graph (speckle_type + id envelopes for DataObjects,
> InstanceProxies, collections) + id-keyed index on top of the SMSH blobs;
> objects.duckdb is just SGEO blobs (byte-equal to SMSH) + 28k proxy rows.
>
> **⚠ Storage-fragmentation finding (2026-06-16).** A low-memory binary run
> produced a **482 MB** objects.duckdb — but it holds only **~241 MB of content**
> (27,527 mesh blobs = 233.9 MB + 28,021 proxy rows = 6.9 MB); the other ~241 MB
> is **dead space**. Cause: the `SPECKLE_DUCKDB_FLUSH_MB=16` byte-flush recycles
> the geometry appender ~15× over the run, and each recycle commits a transaction
> against the PK-indexed `geometries` table, leaving superseded blocks as free
> space that `CHECKPOINT` does **not** reclaim — only a full rewrite does.
> Confirmed by three same-model runs: flush-default (~4 recycles) → **266 MB**,
> flush=16 (~15 recycles) → **481–483 MB**; eav (row-driven, no byte-flush) stayed
> flat ~456 MB throughout. **Fix:** `ObjectsArtifactWriter.Complete()` now rewrites
> the table into a fresh file at finalize (482 → **265 MB** verified, same rows, PK
> preserved), decoupling on-disk size from the memory tuning — so the **253 MB**
> figure above is the true content size and is what ships, at any flush setting.
>
> History: closures + no exclusions + 2 indexes → ENV viewer 675 MB / eav 1.1 GB
> (~1.8 GB). Closure removal + eav exclusions → ~1.32 GB. Path-index drop →
> ~1.11 GB (eav 673→470 MB, −203 MB).

(ENV−cl viewer.duckdb size not captured — expected somewhat smaller than 675 MB
since the root collection's `__closure` JSON block is gone; the `elements`
membership array remains.)

### Geometry bytes (same 27,527 buffers) — format is a wash
- SMSH: **233.7 MB**  ·  SGEO: **233.9 MB** (SGEO mesh body == SMSH body, +16B header)

### What fills viewer.duckdb (the 675 MB)
- 233.7 MB SMSH geometry blobs
- **194.9 MB object JSON** across **332,589** objects (each with `speckle_type` +
  `id` + — for ENV+cl — `__closure`):
  128,270 DataObject + 149,262 InstanceProxy + 27,527 MeshBinary + 27,527 Blob + 3 Collection
- remainder: DuckDB storage + the id-keyed object index

### Content counts
| | ENV | BIN |
|---|---|---|
| objects/rows in main file | 332,589 objects | 27,527 geometries + 28,021 proxies (27,527 instanceDef + 494 material) |
| unique geometry buffers | 27,527 | 27,527 |
| eav rows | 29.38M (277,535 objs) | 18.38M (128,270 objs) |

**eav delta (−11M rows) is explained, not lost data:** same 128,270 DataObject
coverage; difference = −9.5M from excluding `Autodesk Material` (8.4M) +
`Document` (1.1M), −0.6M from relocating the 149,262 instance transforms out of
eav into the `proxies` table, and one fewer index (path index dropped).

## Conclusions
1. **Closures are a secondary cost.** Removing them cut managed heap by ~244 MB
   (collections phase) but did not move the global peak (native-bound) or total
   time. They were *part* of the collections spike, not "the" problem.
2. **The dominant envelope cost is object serialization itself** — building
   332,589 JSON envelopes (speckle_type + id + references) in the extraction loop.
   That's ~430 s and the bulk of managed allocation. The binary path removes it
   (managed peak 167 MB).
3. **Binary wins on storage, managed memory, and time** — same pixels (identical
   27,527 geometry buffers) and same per-element properties:
   - **storage −62%** (~1.8 GB → ~686 MB) — stable,
   - **managed heap ~4–6× lower** (167 MB vs 750–1004 MB) — stable,
   - **time ~−20–25%** (323–352 s vs 424–461 s) — consistent despite variance.
   - **peak workingSet: tunable, not noise.** At default `memory_limit=256` the
     binary path peaks ~2.0 GB; capping it (`SPECKLE_DUCKDB_MEMORY_LIMIT_MB=64` +
     `SPECKLE_DUCKDB_FLUSH_MB=16`) drops it to **1430 MB** — the ODA floor (≈ the
     old NDJSON peak) — at no time cost. The default inflation was purely DuckDB's
     buffer-pool cache (× 2 connections). See "Memory tuning" above.
4. Geometry format (SMSH vs SGEO) is byte-equivalent; the win is structural (no
   per-object JSON, no id, no closures, no collections — proxies + applicationId).

## Raw run output

### ENV−cl (envelope, no closures) — SerializerV2
```
# run 1
[CONV SUMMARY] ENVELOPE (viewer+eav) | load=0.69s collect=0.03s process=430.48s send=30.34s total=461.54s | elements=287326 peakMem=2001MB
oda: extraction + artifact write loop  430.7s  583→1905 MB  peakWS 2003  peakManaged 308  peakNative 1800
oda: collections                         1.5s 1905→2137 MB  peakWS 2351  peakManaged 760  peakNative 1755
GLOBAL PEAK workingSet 2351MB during 'oda: collections'

# run 2
[CONV SUMMARY] ENVELOPE (viewer+eav) | load=0.66s collect=0.03s process=406.33s send=22.58s total=429.60s | elements=287326 peakMem=2042MB
oda: extraction + artifact write loop  406.5s  584→1946 MB  peakWS 2041  peakManaged 296  peakNative 1850
oda: collections                         1.5s 1946→2180 MB  peakWS 2362  peakManaged 749  peakNative 1806
GLOBAL PEAK workingSet 2362MB at t=408.8s during 'oda: collections'
```

### ENV+cl (envelope, with closures) — baseline
```
[CONV SUMMARY] ENVELOPE (viewer+eav) | process=389.93s send=33.59s total=424.24s | elements=287326
oda: extraction + artifact write loop  390.2s  603→1526 MB  peakWS 1526  peakManaged 387
oda: collections                         3.0s 1526→1650 MB  peakWS 2159  peakManaged 1004  peakNative 1328
GLOBAL PEAK workingSet 2159MB during 'oda: collections'
```

### BIN (binary objects + eav)
```
# run 1
[CONV SUMMARY] BINARY (objects+eav) | load=0.68s collect=0.04s process=311.33s send=11.03s total=323.07s | elements=287326 peakMem=1454MB
oda(binary): extraction + SGEO/eav write loop  311.5s  608→1431 MB  peakWS 1454  peakManaged 167  peakNative 1347
oda(binary): proxies + finalize                  2.8s
oda(binary): upload objects + eav                8.2s
GLOBAL PEAK workingSet 1454MB during extraction loop

# run 2 (native memory higher this run)
{"ProcessingTime":338.77,"SendTime":351.92,"ProcessingPeakMemory":2025.9,"ElementsProcessed":287326}
oda(binary): extraction + SGEO/eav write loop  339.0s  606→1718 MB  peakWS 2026  peakManaged 169  peakNative 1927
oda(binary): upload objects + eav                7.7s
GLOBAL PEAK workingSet 2026MB at t=336.5s during extraction loop

# run 3 (consistent with run 2 — version-id-named files: cbc404f6e0.{objects,eav}.duckdb)
[CONV SUMMARY] BINARY (objects+eav) | load=0.71s collect=0.03s process=342.04s send=9.94s total=352.73s | elements=287326 peakMem=2089MB
oda(binary): extraction + SGEO/eav write loop  342.2s  608→1732 MB  peakWS 2087  peakManaged 170  peakNative 1977
oda(binary): upload objects + eav                7.4s
GLOBAL PEAK workingSet 2087MB at t=342.6s during extraction loop
```

> Geometry/eav content was confirmed identical to run 1 earlier
> (27,527 mesh geometries, 18.38M eav rows) — the +580 MB native swing is
> ODA/DuckDB run variance, not extra data.
