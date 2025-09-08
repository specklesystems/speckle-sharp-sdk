using System.Collections;

namespace Speckle.Sdk.Models.GraphTraversal;

public class GraphTraversal : GraphTraversal<TraversalContext>
{
  public GraphTraversal(params ITraversalRule[] traversalRule)
    : base(traversalRule) { }

  public static readonly string traversalContextId = "traversalContextId";

  protected override TraversalContext NewContext(Base current, string? propName, TraversalContext? parent)
  {
    return new TraversalContext<TraversalContext>(current, propName, parent);
  }

  /// <summary>
  /// Traverses supported Collections yielding <see cref="Base"/> objects.
  /// Does not traverse <see cref="Base"/>, only (potentially nested) collections.
  /// </summary>
  /// <param name="value">The value to traverse</param>
  public static IEnumerable<Base> TraverseMember(object? value)
  {
    //TODO we should benchmark this, as yield returning like this could be suboptimal
    switch (value)
    {
      case Base o:
        yield return o;
        break;
      case IList list:
      {
        foreach (object? obj in list)
        {
          foreach (Base o in TraverseMember(obj))
          {
            yield return o;
          }
        }
        break;
      }
      case IDictionary dictionary:
      {
        foreach (object? obj in dictionary.Values)
        {
          foreach (Base o in TraverseMember(obj))
          {
            yield return o;
          }
        }
        break;
      }
    }
  }
}

public abstract class GraphTraversal<T>
  where T : TraversalContext
{
  private readonly ITraversalRule[] _rules;

  protected GraphTraversal(params ITraversalRule[] traversalRule)
  {
    _rules = traversalRule;
  }

  /// <summary>
  /// Given <paramref name="root"/> object, will recursively traverse members according to the provided traversal rules.
  /// </summary>
  /// <param name="root">The object to traverse members</param>
  /// <returns>Lazily returns <see cref="Base"/> objects found during traversal (including <paramref name="root"/>), wrapped within a <see cref="TraversalContext"/></returns>
  public IEnumerable<T> Traverse(Base root)
  {
    var stack = new Queue<T>();
    stack.Enqueue(NewContext(root, null, default));

    while (stack.Count > 0)
    {
      T head = stack.Dequeue();

      Base current = head.Current;
      var activeRule = GetActiveRuleOrDefault(current);

      if (activeRule.ShouldReturn)
      {
        yield return head;
      }

      foreach (string childProp in activeRule.MembersToTraverse(current))
      {
        TraverseMemberToStack(stack, current[childProp], childProp, head);
      }
    }
  }

  private void TraverseMemberToStack(Queue<T> stack, object? value, string? memberName = null, T? parent = default)
  {
    //test
    switch (value)
    {
      case Base o:
        stack.Enqueue(NewContext(o, memberName, parent));
        break;
      case IList list:
      {
        foreach (object? obj in list)
        {
          TraverseMemberToStack(stack, obj, memberName, parent);
        }

        break;
      }
      case IDictionary dictionary:
      {
        foreach (object? obj in dictionary.Values)
        {
          TraverseMemberToStack(stack, obj, memberName, parent);
        }

        break;
      }
    }
  }

  protected abstract T NewContext(Base current, string? propName, T? parent);

  private ITraversalRule GetActiveRuleOrDefault(Base o)
  {
    return GetActiveRule(o) ?? DefaultRule.Instance;
  }

  private ITraversalRule? GetActiveRule(Base o)
  {
    foreach (var rule in _rules)
    {
      if (rule.DoesRuleHold(o))
      {
        return rule;
      }
    }

    return null;
  }
}
