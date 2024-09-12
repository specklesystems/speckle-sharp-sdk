using System.Diagnostics;
using System.Reflection;
using Speckle.Sdk.Api;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Transports;

TypeLoader.Reset();
TypeLoader.Initialize(typeof(Base).Assembly, Assembly.GetExecutingAssembly());

var url = "https://latest.speckle.systems/projects/a3ac1b2706/models/59d3b0f3c6"; //small?

//var url = "https://latest.speckle.systems/projects/2099ac4b5f/models/da511c4d1e"; //perf?

StreamWrapper sw = new(url);
var acc = await sw.GetAccount().ConfigureAwait(false);
using var client = new Client(acc);
var branch = await client.BranchGet(sw.StreamId, sw.BranchName!, 1).ConfigureAwait(false);
var objectId = branch.commits.items[0].referencedObject;

Console.WriteLine(url);
Console.ReadLine();
Stopwatch stopwatch = new();
long lastMs = 0;
const int UPDATE_INTERVAL = 200;
stopwatch.Start();
var rootObject = await Operations
  .Receive2(
    acc,
    sw.StreamId,
    objectId,
    args =>
    {
      if (stopwatch.ElapsedMilliseconds < lastMs + UPDATE_INTERVAL)
      {
        return;
      }
      lastMs = stopwatch.ElapsedMilliseconds;
      string message = "Preparing...";
      if (args.Length != 0)
      {
        message = string.Empty;
        foreach (var arg in args)
        {
          switch (arg.ProgressEvent)
          {
            case ProgressEvent.DownloadBytes:
              message += $" B ({arg.Count})";
              break;
            case ProgressEvent.DownloadObject:
              message += $" O ({arg.Count})";
              break;
            case ProgressEvent.DeserializeObject:
              message += $" S ({arg.Count})";
              break;
          }
        }
      }
      Console.WriteLine(message);
    }
  )
  .ConfigureAwait(false);
Console.WriteLine($"Root: {rootObject.id}");
Console.ReadLine();
