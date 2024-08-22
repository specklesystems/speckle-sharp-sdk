namespace Speckle.Sdk.Serialisation.Utilities;

internal readonly struct OperationTask<T>
  where T : struct
{
  public readonly T OperationType;
  public readonly object? InputValue;
  public readonly TaskCompletionSource<object?>? Tcs;

  public OperationTask(T operationType, object? inputValue = null, TaskCompletionSource<object?>? tcs = null)
  {
    OperationType = operationType;
    InputValue = inputValue;
    Tcs = tcs;
  }

  public void Deconstruct(out T operationType, out object? inputValue, out TaskCompletionSource<object?>? tcs)
  {
    operationType = OperationType;
    inputValue = InputValue;
    tcs = Tcs;
  }
}
