# Benchmark — Navis `KLY_ZHQ_ALL_without MEP.nwd` (binary 4.0 path)

Large federated Navis model. **1,744,221 elements.** Counterpart to the smaller
`KLY_ZHQ_CP2` (287k) benchmark.

## Run 1 — parquet geometry + DuckDB fat-string eav (COMPLETED)
First end-to-end completion of this model. Geometry on parquet; eav still the old
single-table `eav(applicationId, path, …)` DuckDB writer with the per-object +
(at the time) ART index, built at `SPECKLE_DUCKDB_INDEX_MEMORY_LIMIT_MB=8192`.

Env: `SPECKLE_DUCKDB_MEMORY_LIMIT_MB=64`, `SPECKLE_DUCKDB_FLUSH_MB=16`,
`SPECKLE_DUCKDB_INDEX_MEMORY_LIMIT_MB=8192`. macOS arm64.

**Artifacts**

| file | size |
|---|---:|
| `geometries.parquet` | 2.90 GB |
| `envelope.duckdb` | 42.7 MB |
| `eav.duckdb` (fat strings) | 2.34 GB |

**Timing**

| phase | s |
|---|---:|
| model load | 3.65 |
| object id scan | 0.33 |
| process (extraction loop) | 2381.09 |
| send (upload) | 62.79 |
| **total** | **2447.86** (~41 min) |

**Memory (phase-attributed; workingSet = native + gcCommitted)**

| phase | peakWS | peakManaged | peakNative | note |
|---|---:|---:|---:|---|
| init + model load | 2316 | 8 | 2307 | ODA loads the model (~2.3 GB, inherent) |
| object id scan | 2360 | 45 | 2306 | |
| extraction loop | 4184 | 2455 | 3199 | ODA + in-memory proxy/dedup accumulation (managed) + bounded parquet buffer |
| **proxies + finalize** | **5291** | 736 | 4311 | ← GLOBAL PEAK: ODA-retained + the eav ART index build (8 GB budget) |
| upload | 4983 | 488 | 4003 | |

**GLOBAL PEAK workingSet 5291 MB**, during `proxies + finalize`.

### Read
- Completed only after: geometry → parquet (no DuckDB-geometry crashes) + the eav
  index given an 8 GB build budget. Earlier attempts on this model crashed 6 ways
  (geometry commit OOM, compaction OOM, geometry checkpoint assertion at 93%, eav
  index OOM, eav checkpoint assertion).
- Peak is **finalize**, dominated by ODA-retained native + the eav ART index build
  over ~110M fat-string rows. The next-largest term is the **extraction-loop
  managed 2.45 GB** — the in-memory proxy/topology accumulation + dedup set, which
  grows with model size (not the parquet buffer, which stays bounded).

## Run 2 — compact interned eav + native (no-JObject) feed (COMPLETED)
Same model; geometry parquet unchanged; eav swapped to compact interned
`objects`/`paths`/`eav` (int32 refs, `objects.application_id` index only, no eav ART
index); properties streamed straight from the `Dictionary` (no `JObject.FromObject`).

| | Run 1 (fat eav) | **Run 2 (compact)** | Δ |
|---|---:|---:|---:|
| geometries.parquet | 2.90 GB | 2.90 GB | — |
| envelope.duckdb | 42.7 MB | 44.3 MB | — |
| **eav.duckdb** | 2.34 GB | **959 MB** | **−59%** |
| total | 2448 s | 2202 s | −10% |
| finalize | heavy eav ART build (needed 8 GB budget) | **1.4 s** | cliff gone |
| peak WS | 5291 MB (finalize spike) | 5431 MB (loop/upload) | ~flat |

Loop phase: peakManaged **2718 MB**, peakNative **4058 MB**.

### Read
- **eav −59%** (interning), consistent with CP2's −57%. **Finalize cliff eliminated**:
  Run 1 only completed because the eav ART index got an 8 GB build budget; compact
  eav needs none (finalize 1.4 s). That's the robustness win, plus −10% time.
- **Peak ~flat (~5.4 GB)** — at 1.7M the eav was never the peak driver; **ODA + the
  in-loop managed accumulation are**. Native ~4 GB = ODA holding the 1.7M-element
  model resident (~2.3 GB) + tessellation. Managed ~2.7 GB = proxy/topology
  accumulation (`InstanceDefinitionProxies` held to finalize) + SGEO encode-buffer
  churn + interning dicts. Compact eav removed the *finalize* spike (the old peak),
  but the loop's ODA+managed floor remains.
- Next levers for the peak (not eav): the in-loop proxy/topology accumulation and
  SGEO buffer churn (`ArrayPool`), and ODA itself (hard floor).
