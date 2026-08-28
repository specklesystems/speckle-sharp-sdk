using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Speckle.Sdk.Logging;

namespace Speckle.Sdk.BundleMigrator.Logging;

internal static class LoggingConfiguration
{
  public const string DEPLOYMENT_ENVIRONMENT = "deployment.environment.name";
  public const string SERVICE_NAME = "connector.name";
  public const string SERVICE_SLUG = "connector.slug";
  public const string OS_NAME = "os.name";
  public const string OS_TYPE = "os.type";
  public const string OS_SLUG = "os.slug";
  public const string RUNTIME_NAME = "process.runtime.name";
  public const string RUNTIME_VERSION = "process.runtime.version";
  public const string USER_ID = "user.id";
  public const string USER_DISTINCT_ID = "user.distinctId";
  public const string USER_SERVER_URL = "user.server_url";
  public const string TRACING_SOURCE = "file_importer";

  public static IDisposable AddOTelLogging(this IServiceCollection serviceCollection, string slug)
  {
    string deploymentEnvironment =
      Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
      ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
      ?? "Development";

    serviceCollection.AddTransient<ISdkActivityFactory, SdkActivityFactory>();
    string serviceName =
      Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME") ?? AppDomain.CurrentDomain.FriendlyName;

    string? assemblyVersion = GetPackageVersion(Assembly.GetExecutingAssembly());

    var resourceBuilder = ResourceBuilder
      .CreateDefault()
      .AddService(serviceName, serviceVersion: assemblyVersion)
      .AddAttributes([
        new(DEPLOYMENT_ENVIRONMENT, deploymentEnvironment.ToLowerInvariant()),
        new(SERVICE_NAME, serviceName),
        new(SERVICE_SLUG, slug),
        new(OS_NAME, Environment.OSVersion.ToString()),
        new(OS_TYPE, RuntimeInformation.ProcessArchitecture.ToString()),
        new(OS_SLUG, DetermineHostOsSlug()),
        new(RUNTIME_NAME, ".NET"),
        new(RUNTIME_VERSION, RuntimeInformation.FrameworkDescription),
      ]);

    var tracerProvider = OpenTelemetry
      .Sdk.CreateTracerProviderBuilder()
      .AddSource(TRACING_SOURCE)
      .AddHttpClientInstrumentation()
      .AddOtlpExporter(x =>
      {
        x.Protocol = OtlpExportProtocol.HttpProtobuf;
        x.Endpoint = new Uri(
          Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT")
            ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
            ?? "http://localhost:4318"
        );
        x.Headers =
          Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_HEADERS")
          ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS")
          ?? null;
        x.TimeoutMilliseconds = int.Parse(
          Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_TIMEOUT")
            ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_TIMEOUT")
            ?? "10000"
        );
      })
      .SetResourceBuilder(resourceBuilder)
      .SetSampler<AlwaysOnSampler>()
      .Build();

    serviceCollection.AddLogging(logging =>
    {
      logging.ClearProviders();
      logging.AddOpenTelemetry(options =>
      {
        options.IncludeFormattedMessage = true;
        options.IncludeScopes = true;
        options.ParseStateValues = true;

        options.SetResourceBuilder(resourceBuilder);

        options.AddOtlpExporter(x =>
        {
          x.Protocol = OtlpExportProtocol.HttpProtobuf;
          x.Endpoint = new Uri(
            Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_LOGS_ENDPOINT")
              ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
              ?? "http://localhost:4318"
          );
          x.Headers =
            Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_LOGS_HEADERS")
            ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS")
            ?? null;
          x.TimeoutMilliseconds = int.Parse(
            Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_LOGS_TIMEOUT")
              ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_TIMEOUT")
              ?? "10000"
          );
        });
      });
      logging.AddConsole();
    });

    return tracerProvider;
  }

  private static string DetermineHostOsSlug()
  {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
      return "Windows";
    }

    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
      return "MacOS";
    }

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
      return "Linux";
    }

    return RuntimeInformation.OSDescription;
  }

  public static string? GetPackageVersion(Assembly assembly)
  {
    // MinVer https://github.com/adamralph/minver?tab=readme-ov-file#version-numbers
    // together with Microsoft.SourceLink.GitHub https://github.com/dotnet/sourcelink
    // fills AssemblyInformationalVersionAttribute by
    // {majorVersion}.{minorVersion}.{patchVersion}.{pre-release label}.{pre-release version}.{gitHeight}+{Git SHA of current commit}
    // Ex: 1.5.0-alpha.1.40+807f703e1b4d9874a92bd86d9f2d4ebe5b5d52e4
    // The following parts are optional: pre-release label, pre-release version, git height, Git SHA of current commit
    // For package version, value of AssemblyInformationalVersionAttribute without commit hash is returned.

    var informationalVersion = assembly
      .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
      ?.InformationalVersion;
    if (informationalVersion is null)
    {
      return null;
    }

    var indexOfPlusSign = informationalVersion.IndexOf('+');
    return indexOfPlusSign > 0 ? informationalVersion[..indexOfPlusSign] : informationalVersion;
  }
}
