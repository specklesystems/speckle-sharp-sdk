#nullable disable

using System;

namespace Speckle.Core.Api.GraphQL.Models;

public sealed class Version
{
  public LimitedUser authorUser { get; init; }
  public ResourceCollection<Comment> commentThreads { get; init; }
  public DateTime createdAt { get; init; }
  public string id { get; init; }
  public string message { get; init; }
  public Model model { get; init; }
  public Uri previewUrl { get; init; }
  public string referencedObject { get; init; }
  public string sourceApplication { get; init; }
  
  public System.Version SchemaVersion { get; init; }
  
  // POC: is this the right place for a const?
  public const string EARLIEST_SCHEMA_VERSION_STRING = "0.0.0";
}
