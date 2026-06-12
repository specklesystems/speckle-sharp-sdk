using System.CommandLine;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Schema;
using Newtonsoft.Json.Schema.Generation;
using Newtonsoft.Json.Serialization;
using Speckle.Automate.Sdk.DataAnnotations;
using Speckle.Automate.Sdk.Schema;
using Speckle.Connectors.Logging;
using Speckle.InterfaceGenerator;
using Speckle.Sdk;
using Speckle.Sdk.Logging;

namespace Speckle.Automate.Sdk;

/// <summary>
/// Provides mechanisms to execute any function that conforms to the AutomateFunction "interface"
/// </summary>
[GenerateAutoInterface(VisibilityModifier = "public")]
internal sealed class AutomationRunner(
  IAutomationContextFactory contextFactory,
  ISdkActivityFactory activityFactory,
  ILogger<AutomationRunner> logger
) : IAutomationRunner
{
  public async Task<IAutomationContext> RunFunction<TInput>(
    Func<IAutomationContext, TInput, Task> automateFunction,
    AutomationRunData automationRunData,
    string speckleToken,
    TInput inputs
  )
    where TInput : struct
  {
    var automationContext = await contextFactory.Initialize(automationRunData, speckleToken).ConfigureAwait(false);
    using ISdkActivity? activity = activityFactory.Start();
    try
    {
      await automateFunction.Invoke(automationContext, inputs).ConfigureAwait(false);
      if (automationContext.RunStatus is not ("FAILED" or "SUCCEEDED"))
      {
        automationContext.MarkRunSuccess(
          "WARNING: Automate assumed a success status, but it was not marked as so by the function."
        );
        activity?.SetStatus(SdkActivityStatusCode.Ok);
      }
    }
    catch (Exception ex)
    {
      logger.LogCritical(ex, "Function error: {ExceptionMessage}", ex.ToString());
      automationContext.MarkRunException("Function error. Check the automation run logs for details.");
      activity?.SetStatus(SdkActivityStatusCode.Error);
      activity?.RecordException(ex);

      if (ex.IsFatal())
      {
        throw;
      }
    }
    finally
    {
      if (automationContext.ContextView is null)
      {
        automationContext.SetContextView();
      }

      await automationContext.ReportRunStatus().ConfigureAwait(false);
    }
    return automationContext;
  }

  public async Task<IAutomationContext> RunFunction(
    Func<IAutomationContext, Task> automateFunction,
    AutomationRunData automationRunData,
    string speckleToken
  ) =>
    await RunFunction(
        async (context, _) => await automateFunction(context).ConfigureAwait(false),
        automationRunData,
        speckleToken,
        new Fake()
      )
      .ConfigureAwait(false);

  private struct Fake { }

  /// <summary>
  /// Main entrypoint to execute an Automate function with no input data
  /// </summary>
  /// <param name="args">The command line arguments passed into the function by automate</param>
  /// <param name="automateFunction">The automate function to execute</param>
  /// <remarks>This should always be called in your own functions, as it contains the logic to trigger the function automatically.</remarks>
  public async Task<int> Main(string[] args, Func<IAutomationContext, Task> automateFunction)
  {
    return await Main(
        args,
        async (IAutomationContext context, Fake _) => await automateFunction(context).ConfigureAwait(false)
      )
      .ConfigureAwait(false);
  }

  /// <summary>
  /// Main entrypoint to execute an Automate function with input data of type <typeparamref name="TInput"/>.
  /// </summary>
  /// <param name="args">The command line arguments passed into the function by automate</param>
  /// <param name="automateFunction">The automate function to execute</param>
  /// <typeparam name="TInput">The provided input data</typeparam>
  /// <remarks>This should always be called in your own functions, as it contains the logic to trigger the function automatically.</remarks>
  public async Task<int> Main<TInput>(string[] args, Func<IAutomationContext, TInput, Task> automateFunction)
    where TInput : struct
  {
    using ISdkActivity? activity = StartActivityFromEnv();
    Argument<string> pathArg = new(name: "Input Path", description: "A file path to retrieve function inputs");
    RootCommand rootCommand = new();

    // a stupid hack to be able to exit with a specific integer exit code
    // read more at https://github.com/dotnet/command-line-api/issues/1570
    var exitCode = 0;

    rootCommand.AddArgument(pathArg);
    rootCommand.SetHandler(
      async inputPath =>
      {
        try
        {
          FunctionRunData<TInput> data = FunctionRunDataParser.FromPath<TInput>(inputPath);

          var context = await RunFunction(
              automateFunction,
              data.AutomationRunData,
              data.SpeckleToken,
              data.FunctionInputs
            )
            .ConfigureAwait(false);

          if (context.RunStatus is "EXCEPTION")
          {
            exitCode = 1;
          }
        }
        catch (Exception)
        {
          exitCode = 1;
          throw;
        }
      },
      pathArg
    );

    Argument<string> schemaFilePathArg = new(
      name: "Function inputs file path",
      description: "A token to talk to the Speckle server with"
    );

    Command generateSchemaCommand = new("generate-schema", "Generate JSON schema for the function inputs");
    generateSchemaCommand.AddArgument(schemaFilePathArg);
    generateSchemaCommand.SetHandler(
      schemaFilePath =>
      {
        try
        {
          JSchemaGenerator generator = new() { ContractResolver = new CamelCasePropertyNamesContractResolver() };
          generator.GenerationProviders.Add(new SpeckleSecretProvider());
          JSchema schema = generator.Generate(typeof(TInput));
          schema.ToString(SchemaVersion.Draft2019_09);
          File.WriteAllText(schemaFilePath, schema.ToString());
        }
        catch (Exception)
        {
          exitCode = 1;
          throw;
        }
      },
      schemaFilePathArg
    );
    rootCommand.Add(generateSchemaCommand);

    await rootCommand.InvokeAsync(args).ConfigureAwait(false);

    activity?.SetStatus(SdkActivityStatusCode.Ok);
    // if we've gotten this far, the execution should technically be completed as expected
    // thus exiting with 0 is the semantically correct thing to do
    return exitCode;
  }

  /// <summary>
  /// Propagates trace context from server via environment variables
  /// </summary>
  /// <returns></returns>
  private ISdkActivity? StartActivityFromEnv()
  {
    var traceParent = Environment.GetEnvironmentVariable("SPECKLE_OTEL_TRACEPARENT");
    var traceState = Environment.GetEnvironmentVariable("SPECKLE_OTEL_TRACESTATE");
    return activityFactory.StartRemote(traceParent, traceState, SdkActivityKind.Server, "Automation Runner");
  }
}

internal sealed class SpeckleSecretProvider : JSchemaGenerationProvider
{
  public override JSchema? GetSchema(JSchemaTypeGenerationContext context)
  {
    var attributes = context.MemberProperty?.AttributeProvider?.GetAttributes(false) ?? new List<Attribute>();
    var isSecretString = attributes.Any(att => att is SecretAttribute);

    if (isSecretString)
    {
      return CreateSchemaWithWriteOnly(context.ObjectType, context.Required);
    }

    return null;
  }

  private static JSchema CreateSchemaWithWriteOnly(Type type, Required required)
  {
    JSchemaGenerator generator = new();
    JSchema schema = generator.Generate(type, required != Required.Always);

    schema.WriteOnly = true;

    return schema;
  }
}
