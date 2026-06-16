# SGEO — Speckle binary geometry family format (v1)

> Companion to [objects-duckdb-proxies-sgeo.md](./objects-duckdb-proxies-sgeo.md).
> This is the implementable spec: an encoder/decoder author should need nothing
> else. **The SDK owns this format** (`speckle-sharp-sdk`); the Three.js viewer
> (speckle-server-internal) and connectors mirror the decode.
>
> Every primitive type in `Speckle.Objects/Geometry` was reviewed. The renderable
> ones get an SGEO body below. The fidelity types (`Brep`, `BrepX`, `ExtrusionX`,
> `SubDX`, `SolidX`) are **not** SGEO primitives — see *Fidelity types* at the end.
> `Surface` (NURBS) and `Region` are **out of scope** for SGEO v1.

## Conventions (apply to every primitive)

- **Endianness:** little-endian throughout. `f64` = IEEE-754 double (8 B),
  `i32` = signed int32 (4 B), `u32` = unsigned int32 (4 B), `u8` = byte.
- **Alignment:** the 16-byte header is 8-byte aligned, so the body starts
  8-aligned at `0x10`. Every `f64` array stays 8-aligned — `u32` scalars are
  written in pairs (or padded with a `reserved` u32) so a JS `Float64Array` can
  view the buffer with zero copy.
- **What is stored:** only the *authoritative* definition. Derived/cached fields
  are **omitted** and recomputed on read: `length`, `area`, `volume`, `bbox`,
  Arc `radius`/`measure`, Circle `area`. (Matches the existing `[JsonIgnore]`
  derived getters on each class.)
- **Units:** one unit per blob, from the header `units_code`
  (`Units.GetEncodingFromUnit`, `speckle-sharp-sdk/src/Speckle.Sdk/Common/Units.cs:293`).
  Composite sub-objects' own `units` fields are dropped.
- **Colors:** `i32` packed ARGB (matches `Mesh.colors` / `Pointcloud.colors`).
- **`applicationId`** is NOT in the blob — it is the `geometries.applicationId`
  column. The blob is pure geometry.

## Header (16 bytes) — shared by all primitives

```
0x00  4  magic           "SGEO"  (0x53 0x47 0x45 0x4F)
0x04  1  version         = 1
0x05  1  primitive_type  see table below
0x06  2  flags           uint16 bitfield (see table below)
0x08  2  units_code      uint16, Units.GetEncodingFromUnit  (mm=1 cm=2 m=3 km=4 in=5 ft=6 yd=7 mi=8 none=0)
0x0A  2  reserved        = 0
0x0C  4  crc             CRC32 of body bytes only (0x10..end); header excluded so a units edit doesn't invalidate
0x10  …  body            per primitive_type
```

### `primitive_type` codes (0–7 are the board's; 8+ extend it)

| code | type | SDK class |
|---|---|---|
| 0 | mesh | `Mesh` |
| 1 | line | `Line` |
| 2 | polyline | `Polyline` |
| 3 | polycurve | `Polycurve` |
| 4 | curve (NURBS) | `Curve` |
| 5 | arc | `Arc` |
| 6 | circle | `Circle` |
| 7 | points | `Point` (count=1) and `Pointcloud` |
| 8 | ellipse | `Ellipse` |
| 9 | spiral | `Spiral` |
| 10 | box | `Box` |

`Vector`, `Plane`, `Interval`, `ControlPoint` are building blocks embedded in the
bodies above, never top-level blobs.

### `flags` bitfield (uint16)

| bit | meaning | applies to |
|---|---|---|
| 0 | quantized | reserved (v1 unused) |
| 1 | closed | polyline, curve, polycurve |
| 2 | rational | curve |
| 3 | periodic | curve |
| 4 | has normals | mesh |
| 5 | has uvs | mesh |
| 6 | has colors | mesh, points |
| 7 | has sizes | points |
| 8 | has trimDomain | ellipse |
| others | reserved = 0 | |

## Primitive bodies (one by one)

### 0 — mesh (`Mesh`)

Reuses the existing SMSH body verbatim under the SGEO header; standalone `"SMSH"`
magic is deprecated (decoder reads it one more release). Keep the body writer in
`speckle-sharp-sdk/src/Speckle.Objects/Utils/MeshBinaryEncoder.cs`, drop SMSH's
own header. `faces` keeps the SDK n-gon encoding `[n, i0..i(n-1)]…`.

```
0x10  u32  vertex_count            (= vertices.Count / 3)
0x14  u32  face_index_count        (= faces.Count, the n-gon-encoded int list)
0x18  f64[vertex_count*3]  vertices            x,y,z …
   +  i32[face_index_count] faces              [n,i0,i1,i2]…  (pad to 8B before next f64 block)
   +  f64[vertex_count*3]  normals    if flag bit4
   +  f64[vertex_count*2]  uvs        if flag bit5   (u,v …)
   +  i32[vertex_count]    colors     if flag bit6   (ARGB)
```

### 1 — line (`Line`)

8 doubles = 64-byte body (matches the board's worked example exactly).

```
0x10  f64  domain.start
0x18  f64  domain.end
0x20  f64  start.x   0x28 f64 start.y   0x30 f64 start.z
0x38  f64  end.x     0x40 f64 end.y     0x48 f64 end.z
```

### 2 — polyline (`Polyline`)

`closed` = flag bit1.

```
0x10  u32  point_count             (= value.Count / 3)
0x14  u32  reserved = 0            (8-byte alignment)
0x18  f64[point_count*3]  points   x,y,z …
```

### 3 — polycurve (`Polycurve`)

Recursive: each segment is a complete nested SGEO blob (it already carries its own
type/flags/units), length-prefixed so a decoder can skip an unknown segment type.
`closed` = flag bit1.

```
0x10  u32  segment_count
0x14  u32  reserved = 0
   repeat segment_count times, each record 8-byte aligned:
     u32  blob_len
     u32  reserved = 0
     u8[blob_len]  nested SGEO blob (header + body)
     pad to next 8-byte boundary
```

### 4 — curve / NURBS (`Curve`)

`weights` present only when `rational` (flag bit2). `closed` = bit1, `periodic` =
bit3. The derived `displayValue` polyline is NOT stored — the server/viewer
tessellates, or reads a sibling polyline row.

```
0x10  u32  degree
0x14  u32  control_point_count     (= points.Count / 3)
0x18  u32  knot_count
0x1C  u32  reserved = 0
0x20  f64  domain.start
0x28  f64  domain.end
0x30  f64[control_point_count*3]  control_points   x,y,z …
   +  f64[control_point_count]    weights           if flag bit2 (rational)
   +  f64[knot_count]             knots
```

### 5 — arc (`Arc`)

Authoritative = plane + 3 points (radius/measure/length derived). 23 doubles =
184-byte body.

```
0x10  f64[3]  plane.origin   (x,y,z)
0x28  f64[3]  plane.normal
0x40  f64[3]  plane.xdir
0x58  f64[3]  plane.ydir
0x70  f64[3]  startPoint
0x88  f64[3]  midPoint
0xA0  f64[3]  endPoint
0xB8  f64     domain.start
0xC0  f64     domain.end
```

### 6 — circle (`Circle`)

15 doubles = 120-byte body (matches the board's worked example exactly).

```
0x10  f64  radius
0x18  f64  domain.start
0x20  f64  domain.end
0x28  f64[3]  plane.origin
0x40  f64[3]  plane.normal
0x58  f64[3]  plane.xdir
0x70  f64[3]  plane.ydir
```

### 7 — points (`Point` with count=1, or `Pointcloud`)

`colors` = flag bit6, `sizes` = flag bit7 (pointcloud only).

```
0x10  u32  point_count
0x14  u32  reserved = 0
0x18  f64[point_count*3]  positions   x,y,z …
   +  i32[point_count]    colors      if flag bit6 (ARGB)   (pad to 8B if sizes follow)
   +  f64[point_count]    sizes       if flag bit7
```

### 8 — ellipse (`Ellipse`)

`trimDomain` present only when flag bit8.

```
0x10  f64  firstRadius
0x18  f64  secondRadius
0x20  f64  domain.start
0x28  f64  domain.end
0x30  f64[3]  plane.origin
0x48  f64[3]  plane.normal
0x60  f64[3]  plane.xdir
0x78  f64[3]  plane.ydir
   +  f64  trimDomain.start, f64 trimDomain.end   if flag bit8
```

### 9 — spiral (`Spiral`)

`spiralType` is the enum ordinal (Biquadratic=0 … Unknown). `displayValue`
derived, not stored.

```
0x10  u32  spiral_type            (enum ordinal)
0x14  u32  reserved = 0
0x18  f64[3]  startPoint
0x30  f64[3]  endPoint
0x48  f64[3]  plane.origin
0x60  f64[3]  plane.normal
0x78  f64[3]  plane.xdir
0x90  f64[3]  plane.ydir
0xA8  f64     turns
0xB0  f64[3]  pitchAxis
0xC8  f64     pitch
0xD0  f64     domain.start
0xD8  f64     domain.end
```

### 10 — box (`Box`)

`area`/`volume` derived.

```
0x10  f64[3]  plane.origin
0x28  f64[3]  plane.normal
0x40  f64[3]  plane.xdir
0x58  f64[3]  plane.ydir
0x70  f64  xSize.start  0x78 f64 xSize.end
0x80  f64  ySize.start  0x88 f64 ySize.end
0x90  f64  zSize.start  0x98 f64 zSize.end
```

## Fidelity types — route to `blobs.parquet`, not SGEO

`Brep`, `BrepX`, `ExtrusionX`, `SubDX`, `SolidX` carry an authoritative
non-renderable definition (NURBS topology / opaque `RawEncoding`). Per the board,
these go to **`blobs.parquet`** (`format` from `RawEncoding.format`, e.g. `"3dm"`)
for connector-receive fidelity. Their **`displayValue` meshes** are written as
ordinary SGEO **mesh (type 0)** rows in `geometries` under the same
`applicationId`, so the viewer renders them with no brep evaluator. (`blobs.parquet`
itself is parked, but the SGEO/displayValue split is fixed now.)

## Size comparison — current JSON vs SGEO (per primitive)

**Assumptions** (so the numbers are reproducible, not magic):
- A round-trippable `double` in Speckle JSON averages **~15 B** inside a flat
  numeric array (`"123.45678901234,"`) and **~22 B** as a named scalar
  (`"radius":5.0,`). SGEO stores every double in **8 B**, every int in **4 B**.
- Each Speckle object carries a JSON **envelope** ≈ **150 B**: `speckle_type`
  (~40), `id` 32-hex hash (~45), `applicationId`, `units`, `totalChildrenCount`,
  braces/keys. SGEO replaces this with the **16 B header** — and `id`/`__closure`
  vanish entirely (the systemic win, see note below).
- Nested inline sub-objects cost their own envelope: `Point`/`Vector` ≈ **100 B**,
  `Interval` ≈ **60 B**, `Plane` (origin + 3 vectors) ≈ **450 B** in JSON; in SGEO
  they are just their raw doubles (Point 24 B, Interval 16 B, Plane 96 B).

**Fixed-size primitives** (envelope-dominated — the big wins):

| Primitive | JSON ≈ | SGEO (exact) | Reduction |
|---|---:|---:|---:|
| line | ~410 B | **80 B** | ~5.1× (80%) |
| circle | ~690 B | **136 B** | ~5.1× (80%) |
| ellipse | ~720 B | **144 B** | ~5.0× (80%) |
| arc | ~960 B | **200 B** | ~4.8× (79%) |
| box | ~780 B | **160 B** | ~4.9× (79%) |
| spiral | ~980 B | **224 B** | ~4.4× (77%) |
| point (single) | ~95 B | **48 B** | ~2.0× (50%) |

**Bulk / array primitives** (number-dominated — ~2× on data, plus envelope &
per-chunk reference & `__closure` elimination on top):

| Primitive | JSON / element | SGEO / element | Reduction |
|---|---:|---:|---:|
| polyline (per vertex) | ~45 B (3 × 15) | **24 B** | ~1.9× (47%) |
| pointcloud (per point, xyz) | ~45 B | **24 B** | ~1.9× (47%) |
| pointcloud (+ ARGB colour) | ~57 B | **28 B** | ~2.0× (51%) |
| mesh (per vertex, xyz) | ~45 B | **24 B** | ~1.9× (47%) |
| mesh (per triangle, `3,i,j,k`) | ~32 B (4 ints) | **16 B** | ~2.0× (50%) |
| curve NURBS (per control pt) | ~45 B | **24 B** | ~1.9× (47%) |

*Worked totals:* a 10k-vertex / 20k-triangle mesh ≈ **770 KB** JSON (positions
450 KB + faces 320 KB, before chunk-reference & closure overhead) → **~560 KB**
SGEO (240 KB + 320 KB) — ~1.4–1.9× depending on optional channels, and that is
*before* counting the chunk-wrapper objects and `__closure` entries JSON adds for
every 31,250-element chunk.

> **The headline isn't per-blob bytes.** Fixed primitives shrink ~5× because the
> JSON envelope (`speckle_type` strings, the 32-char `id` hash, nested sub-object
> type tags, property names) dwarfs the actual numbers. Bulk primitives shrink
> ~2×. But the real, super-linear win is what these tables *can't* show: deleting
> `id` hashing and `__closure` removes work that scales with the **whole object
> graph**, not the single object — the exact cost the brief calls "the massive
> headache" when a root has millions of transitive references.

## Worked hex examples (golden test vectors)

These double as the first SDK unit tests (encode → compare bytes; decode → equal).

**Line** from (0,0,0)→(3,3,6), meters, domain [0,1] — the board's example:
```
HEADER:  53 47 45 4F | 01 | 01 | 00 00 | 03 00 | 00 00 | 00 00 00 00
         "SGEO"        v1   line  flags    m       rsv     crc
BODY:    00…00 (0.0)  …F0 3F (1.0)  | 00…(0,0,0) | …08 40 …08 40 …18 40  (3,3,6)
```

**Circle** r=5 at origin, Z-up, meters — the board's 120-byte example:
```
HEADER:  53 47 45 4F | 01 | 06 | 00 00 | 03 00 | 00 00 | crc
BODY:    …14 40 (radius 5) | 0.0,1.0 (domain) | origin 0,0,0 | normal 0,0,1 | xdir 1,0,0 | ydir 0,1,0
```

**Point** at (1,2,3), mm:
```
HEADER:  …| 07 | 00 00 | 01 00 | …                 (primitive 7, flags 0, units mm=1)
BODY:    01 00 00 00 | 00 00 00 00 | 1.0 2.0 3.0   (count=1, reserved, x,y,z)
```

## Implementation notes

- **Encoder (C#, SDK):** extend `MeshBinaryEncoder` into an SGEO writer that emits
  the 16-byte header (`Units.GetEncodingFromUnit` for `units_code`, CRC32 over the
  body) and dispatches the body by `primitive_type`. Mesh (type 0) reuses the
  existing SMSH body writer.
- **Decoder (TS, Viewer 2.0):** one decode path per `primitive_type` producing a
  Three.js `BufferGeometry`; the SDK owns the format, the viewer mirrors decode.
- **Tests:** round-trip per primitive (encode→decode equality), header/CRC/units
  assertions, and byte-equality against the golden vectors above. Mirror
  `speckle-sharp-sdk/tests/Speckle.Objects.Tests.Unit/Geometry/MeshBinaryTests.cs`.
