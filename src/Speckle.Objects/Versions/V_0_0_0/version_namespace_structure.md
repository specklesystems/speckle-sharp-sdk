# Namespace Structure
At present the namespace of an object is used both to identify it's type and to identify if it is
a version of some other object.

i.e. we may have the following:

> `Objects.Versions.V_0_1_0.Geometry.Point`

Which would be version 0.1.0 of a Point as below: 

> `Objects.Versions.Point`

It is critical that any version of a class, i.e. Point lives within the namespace following this schema:

> `<prefix>.Versions.V_<mj_mi_bf>.<SuffixAndType>`

The prefix can be anything and can be multi-level, i.e. `Speckle.Objects` or `Objects` as it is right now.
'Versions' namespace must follow the prefix which must then be followed be `V_` and the version
Major.Minor.Bigfix specified as digits seperated by underscores, i.e. `V_0_1_0` for version 0.1.0.

The 'SuffixAndType' represents everything in the original type that came after the prefix, so for the type 
with `Objects.Geometry.Point` the prefix is `Objects` and the suffix is `Geometry.Point` and any
Versions of `Objects.Geometry.Point` will therefore be found within
`Objects.Version.V_<mj_mi_bf>.Geometry.Point`.

## Tight Coupling
There is some tight coupling then of the namesapce to the type and to each version. It is absolutely the
case that this could be broken down some by other mechanisms, perhaps by attributing each type so we know it's
Speckle Type name and verison etc..., however, pre-existing Versions within Speckle already use the
fully qualified typename, i.e. `Objects.Geometry.Point`, so breaking away from this will be harder to do - hence
why existing objects are not in the `Speckle.Objects namespace`, as they really ought to be.

It's also worth recognising that while the namespace is used, there is a very clear and solid pattern here,
some of which is being checked and easily checked at that, when upgraders are being discovered. The Type of something
is already a strong name, so `Objects.Geometry.Point` is a distinct type and so is `Objects.Versions.V_0_1_0.Geometry.Point`
if there is any weakness here, it is in the inferred relatonship between the two.

# Upgraders
Each type is upgraded by running one or more upgraders. Some info on these can be find in the
[Upraders Article by following this link.](Upgraders/upgraders.md)

