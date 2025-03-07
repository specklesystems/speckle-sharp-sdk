using System.Text;
using GraphQL;
using GraphQL.Client.Abstractions;
using GraphQL.Client.Abstractions.Websocket;
using Speckle.Newtonsoft.Json;
using Speckle.Newtonsoft.Json.Serialization;
using Speckle.Sdk.Common;
using Speckle.Sdk.Serialisation;

namespace Speckle.Sdk.Api.GraphQL.Serializer;

internal sealed class NewtonsoftJsonSerializer : IGraphQLWebsocketJsonSerializer
{
  public NewtonsoftJsonSerializer()
    : this(DefaultJsonSerializerSettings) { }

  public NewtonsoftJsonSerializer(Action<JsonSerializerSettings> configure)
    : this(configure.AndReturn(DefaultJsonSerializerSettings)) { }

  public NewtonsoftJsonSerializer(JsonSerializerSettings jsonSerializerSettings)
  {
    JsonSerializerSettings = jsonSerializerSettings;
    ConfigureMandatorySerializerOptions();
  }

  public static JsonSerializerSettings DefaultJsonSerializerSettings =>
    new()
    {
      ContractResolver = new CamelCasePropertyNamesContractResolver { IgnoreIsSpecifiedMembers = true },
      MissingMemberHandling = MissingMemberHandling.Ignore,
      Converters = { new ConstantCaseEnumConverter() },
    };

  public JsonSerializerSettings JsonSerializerSettings { get; }

  public string SerializeToString(GraphQLRequest request)
  {
    return JsonConvert.SerializeObject(request, JsonSerializerSettings);
  }

  public byte[] SerializeToBytes(GraphQLWebSocketRequest request)
  {
    var json = JsonConvert.SerializeObject(request, JsonSerializerSettings);
    return Encoding.UTF8.GetBytes(json);
  }

  public Task<WebsocketMessageWrapper> DeserializeToWebsocketResponseWrapperAsync(System.IO.Stream stream)
  {
    return DeserializeFromUtf8Stream<WebsocketMessageWrapper>(stream, DefaultJsonSerializerSettings); //Ignoring the custom JsonSerializerSettings here, see https://github.com/graphql-dotnet/graphql-client/issues/660
  }

  public GraphQLWebSocketResponse<TResponse> DeserializeToWebsocketResponse<TResponse>(byte[] bytes)
  {
    return JsonConvert
      .DeserializeObject<GraphQLWebSocketResponse<TResponse>>(Encoding.UTF8.GetString(bytes), JsonSerializerSettings)
      .NotNull();
  }

  public Task<GraphQLResponse<TResponse>> DeserializeFromUtf8StreamAsync<TResponse>(
    System.IO.Stream stream,
    CancellationToken cancellationToken
  )
  {
    return DeserializeFromUtf8Stream<GraphQLResponse<TResponse>>(stream, JsonSerializerSettings);
  }

  // deserialize extensions to Dictionary<string, object>
  private void ConfigureMandatorySerializerOptions()
  {
    JsonSerializerSettings.Converters.Insert(0, new MapConverter());
  }

  private static Task<T> DeserializeFromUtf8Stream<T>(
    System.IO.Stream stream,
    JsonSerializerSettings serializerSettings
  )
  {
    using var sr = new StreamReader(stream);
    using JsonReader reader = SpeckleObjectSerializerPool.Instance.GetJsonTextReader(sr);
    var serializer = JsonSerializer.Create(serializerSettings);
    return Task.FromResult(serializer.Deserialize<T>(reader) ?? throw new ArgumentException("Serialized data is null"));
  }
}
