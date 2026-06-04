// using System.Data.Common;
// using System.Text.Json;
// using System.Text.Json.Serialization;
// using DuckDB.NET.Data;
//
// namespace Speckle.Sdk.Pipelines.Receive;
//
// public sealed class PackFileDepthMigration
// {
//   private readonly FileInfo _file;
//
//   public PackFileDepthMigration(FileInfo file)
//   {
//     _file = file;
//   }
//
//   public void Migrate()
//   {
//     var connectionString = $"Data Source={_file.FullName}";
//
//     var nodes = new Dictionary<string, NodeRecord>(StringComparer.Ordinal);
//
//     using var connection = new DuckDBConnection(connectionString);
//     connection.Open();
//
//     //
//     // 1. LOAD ALL NODES
//     //
//     using (var command = connection.CreateCommand())
//     {
//       command.CommandText = """
//         SELECT id, data
//         FROM objects
//         """;
//
//       using var reader = command.ExecuteReader();
//
//       while (reader.Read())
//       {
//         var id = reader.GetString(0);
//         var json = reader.GetString(1);
//
//         var node = JsonSerializer.Deserialize(json, PackFileJsonContext.Default.NodeDto);
//
//         // IMPORTANT:
//         // Missing __closure => leaf node
//         var children = node?.Closure?.Keys.ToArray() ?? Array.Empty<string>();
//
//         nodes[id] = new NodeRecord(id, children);
//       }
//     }
//
//     string rootId = GetRootObjectId(connection);
//     //
//     // 2. COMPUTE DEPTHS FROM SINGLE ROOT
//     //
//     var maxDepths = new Dictionary<string, int>(StringComparer.Ordinal);
//
//     void Traverse(string nodeId, int depth)
//     {
//       if (!nodes.TryGetValue(nodeId, out var node))
//       {
//         return;
//       }
//
//       if (maxDepths.TryGetValue(nodeId, out var existingDepth) && existingDepth >= depth)
//       {
//         return;
//       }
//
//       maxDepths[nodeId] = depth;
//
//       foreach (var child in node.Children)
//       {
//         Traverse(child, depth + 1);
//       }
//     }
//
//     Traverse(rootId, 0);
//
//     //
//     // 3. WRITE BACK TO DUCKDB (NO JSON MODIFICATION)
//     //
//     using var transaction = connection.BeginTransaction();
//
//     using (var setup = connection.CreateCommand())
//     {
//       setup.Transaction = transaction;
//
//       setup.CommandText = """
//         ALTER TABLE objects
//         ADD COLUMN IF NOT EXISTS depth INTEGER;
//
//         CREATE TEMP TABLE depth_updates
//         (
//             id VARCHAR,
//             depth INTEGER
//         );
//         """;
//
//       setup.ExecuteNonQuery();
//     }
//
//     using (var insert = connection.CreateCommand())
//     {
//       insert.Transaction = transaction;
//
//       insert.CommandText = """
//         INSERT INTO depth_updates (id, depth)
//         VALUES ($id, $depth)
//         """;
//
//       var idParam = insert.CreateParameter();
//       idParam.ParameterName = "id";
//       insert.Parameters.Add(idParam);
//
//       var depthParam = insert.CreateParameter();
//       depthParam.ParameterName = "depth";
//       insert.Parameters.Add(depthParam);
//
//       foreach (var (id, depth) in maxDepths)
//       {
//         idParam.Value = id;
//         depthParam.Value = depth;
//
//         insert.ExecuteNonQuery();
//       }
//     }
//
//     using (var update = connection.CreateCommand())
//     {
//       update.Transaction = transaction;
//
//       update.CommandText = """
//         UPDATE objects
//         SET depth = d.depth
//         FROM depth_updates d
//         WHERE objects.id = d.id
//         """;
//
//       update.ExecuteNonQuery();
//     }
//
//     transaction.Commit();
//   }
//
//   private string GetRootObjectId(DuckDBConnection connection)
//   {
//     //language=PostgreSQL
//     const string QUERY = """
//       SELECT id FROM root LIMIT 1
//       """;
//
//     using DuckDBCommand command = connection.CreateCommand();
//     command.CommandText = QUERY;
//
//     using DbDataReader reader = command.ExecuteReader();
//
//     if (!reader.Read())
//     {
//       throw new KeyNotFoundException();
//     }
//
//     string id = reader.GetString(0);
//     return id;
//   }
//
//   private sealed record NodeRecord(string Id, string[] Children);
// }
//
// public sealed class NodeDto
// {
//   [JsonPropertyName("__closure")]
//   public Dictionary<string, string>? Closure { get; init; }
// }
//
// [JsonSerializable(typeof(NodeDto))]
// internal partial class PackFileJsonContext : JsonSerializerContext { }
