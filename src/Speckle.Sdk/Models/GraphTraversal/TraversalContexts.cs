namespace Speckle.Sdk.Models.GraphTraversal;

public class TraversalContext
{
  public Base Current { get; }
  public TraversalContext? Parent { get; }
  public string? PropName { get; }

  public TraversalContext(Base current, string? propName = null, TraversalContext? parent = default)
    : this(current, propName)
  {
    Parent = parent;
  }

  protected TraversalContext(Base current, string? propName = null)
  {
    Current = current;
    PropName = propName;
  }
}

public class TraversalContext<T> : TraversalContext
  where T : TraversalContext
{
  public new T? Parent => (T?)base.Parent;

  public TraversalContext(Base current, string? propName = null, T? parent = default)
    : base(current, propName, parent) { }
}
