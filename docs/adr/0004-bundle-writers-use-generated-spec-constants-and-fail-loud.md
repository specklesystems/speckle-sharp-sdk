# ADR-0004: Bundle writers address columns via generated spec constants; any dropped row fails the job

- **Status**: pointer — the canonical text lives in the speckle-atlas layer:
  [`atlas/adr/0004`](../../../atlas/adr/0004-bundle-writers-use-generated-spec-constants-and-fail-loud.md)
  (standing stack ADR, no owning spec)
- **Date**: 2026-08-04

Summary: after the 2026-08 empty-`envelope.nodes` incident (a bundle-spec
column insert drifted from the C++ writers' hand-written ordinals; six days
of green jobs shipping viewer-blank versions — this repo's writer was safe
by construction but hardened anyway), the stack rule is: bundle writers
address columns **only** via the spec's generated column-index constants —
a hand-written ordinal is a defect; writes are type-checked and any dropped
row fails the job loudly; relations referencing missing/empty nodes are a
hard error in the spec validator; and CI round-trips each writer against
its pinned spec on every PR.

## What this binds in this repo

- **`src/Speckle.Sdk.Parquet/Pipelines/Send/Artifacts/`**
  (`ParquetTableWriter`, `EavWriter`, `EnvelopeWriter`,
  `StructuralResultsWriter`): row/schema arity guards fail fast, and
  columns are addressed via the generated `BundleCols` constants (#525).
- **CI**: the bundle-spec sibling checkout is provisioned so the writer
  round-trip runs against the pinned spec on every PR (#525).
