﻿
using System.Runtime.Serialization;
using Speckle.Newtonsoft.Json;

namespace Speckle.Sdk.Models;

[SpeckleType("Speckle.Core.Models.Blob")]
public class Blob : Base
{
  [JsonIgnore]
  public static int LocalHashPrefixLength => 20;

  private string _filePath;
  private string _hash;
  private bool _isHashExpired = true;

  public Blob() { }

  public Blob(string filePath)
  {
    this.filePath = filePath;
  }

  public string filePath
  {
    get => _filePath;
    set
    {
      originalPath ??= value;

      _filePath = value;
      _isHashExpired = true;
    }
  }

  public string originalPath { get; set; }

  /// <summary>
  /// For blobs, the id is the same as the file hash. Please note, when deserialising, the id will be set from the original hash generated on sending.
  /// </summary>
  public override string? id
  {
    get => GetFileHash();
    set => base.id = value;
  }

  public string? GetFileHash()
  {
    if ((_isHashExpired || _hash == null) && filePath != null)
    {
      _hash = HashUtility.HashFile(filePath);
    }

    return _hash;
  }

  [OnDeserialized]
  internal void OnDeserialized(StreamingContext context)
  {
    _isHashExpired = false;
  }

  public string GetLocalDestinationPath(string blobStorageFolder)
  {
    var fileName = Path.GetFileName(filePath);
    var x = id ?? throw new ArgumentException("id is empty");
    return Path.Combine(blobStorageFolder, $"{x[..10]}-{fileName}");
  }
}
