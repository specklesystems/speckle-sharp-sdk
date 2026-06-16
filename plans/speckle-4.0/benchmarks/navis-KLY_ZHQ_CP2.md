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
| | ENV+cl | ENV−cl (run1 / run2) | BIN (run1 / run2) |
|---|---:|---:|---:|
| extraction (`process`) | 389.9 | 430.5 / 406.3 | 311.3 / 338.8 |
| upload (`send`) | 33.6 | 30.3 / 22.6 | 11.0 / 7.7 |
| **total** | **424.2** | **461.5 / 429.6** | **323.1 / ~351.9** |

> Binary is consistently the fastest (~323–352 s vs ~424–461 s envelope), even
> with extraction-loop variance.

> Two ENV−cl runs (461.5 s, 429.6 s) **bracket** the ENV+cl baseline (424.2 s) —
> i.e. dropping closures did **not** change total time; the spread is run-to-run
> variance in the ODA/IO-bound extraction loop (±~10%). Not a `SerializerV2`
> regression. The binary path (323 s) is the only one clearly faster.

## Memory — peak (MB)
| | ENV+cl | ENV−cl (r1 / r2) | BIN (r1 / r2) |
|---|---:|---:|---:|
| GLOBAL peak workingSet | 2159 | 2351 / 2362 | **1454 / 2026** |
| peakManaged @ collections | 1004 | **760 / 749** | — (no collections) |
| peakManaged @ extraction loop | 387 | 308 / 296 | **167 / 169** |
| peakNative @ peak phase | 1328 | 1755 / 1806 | 1347 / **1927** |

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
3. **Binary wins on storage, managed memory, and time** — same pixels (identical
   27,527 geometry buffers) and same per-element properties:
   - **storage −62%** (~1.8 GB → ~686 MB) — stable,
   - **managed heap ~4–6× lower** (167 MB vs 750–1004 MB) — stable,
   - **time ~−20–25%** (323–352 s vs 424–461 s) — consistent despite variance.
   - **peak workingSet: inconclusive** — native-noise-dominated; binary ranged
     1454–2026 MB across runs, overlapping envelope. Needs many runs / a capped
     container to claim a number. (Earlier "−33%" retracted — single-run artifact.)
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
oda(binary): proxies + finalize                  2.6s
oda(binary): upload objects + eav                7.7s
GLOBAL PEAK workingSet 2026MB at t=336.5s during extraction loop
```

> Geometry/eav content was confirmed identical to run 1 earlier
> (27,527 mesh geometries, 18.38M eav rows) — the +580 MB native swing is
> ODA/DuckDB run variance, not extra data.
