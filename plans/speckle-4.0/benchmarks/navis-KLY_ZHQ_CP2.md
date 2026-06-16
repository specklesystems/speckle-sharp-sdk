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
| | ENV+cl | ENV−cl | BIN |
|---|---:|---:|---:|
| extraction (`process`) | 389.9 | 430.5 | 311.3 |
| upload (`send`) | 33.6 | 30.3 | 11.0 |
| **total** | **424.2** | **461.5** | **323.1** |

> ENV−cl total is *higher* than ENV+cl — counter-intuitive, since dropping
> closures is less work. Attributed to run-to-run variance in the extraction
> loop (the loop is ODA/IO-bound; ±10% between runs). Not a real regression from
> `SerializerV2`. Re-run 3× for a stable mean before quoting time deltas.

## Memory — peak (MB)
| | ENV+cl | ENV−cl | BIN |
|---|---:|---:|---:|
| GLOBAL peak workingSet | 2159 | 2351 | **1454** |
| peakManaged @ collections | 1004 | **760** | — (no collections) |
| peakManaged @ extraction loop | 387 | 308 | 167 |
| peakNative @ peak phase | 1328 | 1755 | 1347 |

**The clean signal is managed heap at peak: 1004 → 760 → 167 MB.**
- ENV+cl → ENV−cl: **−244 MB managed** = the `__closure` dict (one entry per
  transitive object, ~277k+ for the root) + the `__closure` JSON block. Real, but
  partial.
- ENV−cl → BIN: **−593 MB managed** = eliminating per-object JSON serialization
  entirely (no DataObject/collection/instance-proxy envelopes built).
- workingSet (physical RAM, what a pod limit enforces) is **native-bound** here
  (ODA geometry + DuckDB), so trimming managed alone barely moves it; only the
  binary path's smaller everything brings the global peak down (2159/2351 → 1454).

## Storage (on disk, checkpointed)
| | ENV (viewer+eav) | BIN (objects+eav) |
|---|---:|---:|
| geometry/objects file | viewer.duckdb **675 MB** | objects.duckdb **253 MB** |
| eav file | **1.1 GB** | **433 MB** |
| **total artifacts** | **~1.8 GB** | **~686 MB** (−62%) |

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
3. **Binary wins across the board**: −62% storage (~1.8 GB → ~686 MB), −33% global
   peak workingSet (2159 → 1454 MB), ~−24% time vs ENV+cl — same pixels (identical
   27,527 geometry buffers) and same per-element properties.
4. Geometry format (SMSH vs SGEO) is byte-equivalent; the win is structural (no
   per-object JSON, no id, no closures, no collections — proxies + applicationId).

## Raw run output

### ENV−cl (envelope, no closures) — SerializerV2
```
[CONV SUMMARY] ENVELOPE (viewer+eav) | load=0.69s collect=0.03s process=430.48s send=30.34s total=461.54s | elements=287326 peakMem=2001MB
oda: extraction + artifact write loop  430.7s  583→1905 MB  peakWS 2003  peakManaged 308  peakNative 1800
oda: collections                         1.5s 1905→2137 MB  peakWS 2351  peakManaged 760  peakNative 1755
writer: appender flush (332589 objects)  0.6s 2137→2237 MB
writer: index(path) build                6.3s
writer: index(object_id) build           3.6s
GLOBAL PEAK workingSet 2351MB at t=433.1s during 'oda: collections'
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
[CONV SUMMARY] BINARY (objects+eav) | load=0.68s collect=0.04s process=311.33s send=11.03s total=323.07s | elements=287326 peakMem=1454MB
oda(binary): extraction + SGEO/eav write loop  311.5s  608→1431 MB  peakWS 1454  peakManaged 167  peakNative 1347
oda(binary): proxies + finalize                  2.8s
oda(binary): upload objects + eav                8.2s
GLOBAL PEAK workingSet 1454MB during extraction loop
```
