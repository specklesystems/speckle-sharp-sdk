[`Speckle.Objects`](https://www.nuget.org/packages/Speckle.Objects) has been incorporated into [`Speckle.Sdk`](https://www.nuget.org/packages/Speckle.Sdk)
to more easily accommodate the new bundle spec.

[`Speckle.Objects`](https://www.nuget.org/packages/Speckle.Objects) will continue to be published as an empty (no code)
assembly that provides type forwarding.
This should reduce friction in some cases where consumers have `Speckle.Objects` as a transitive dependency or are relying on binary compatibility,
and would benefit from a grace period for migration.

To migrate: update all Speckle packages together to one matching version, then reference [`Speckle.Sdk`](https://www.nuget.org/packages/Speckle.Sdk) directly
and remove any references to [`Speckle.Objects`](https://www.nuget.org/packages/Speckle.Objects).
Namespaces are unchanged (`Speckle.Objects.*`), so no code edits are needed.
