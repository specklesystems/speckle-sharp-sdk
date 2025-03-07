using Speckle.Newtonsoft.Json;

namespace Speckle.Sdk.Api.GraphQL.Models.Responses;

/// <summary>
/// With the power of GraphQL Aliasing, we can avoid having to craft individual response classes for each query
/// Instead, we can alias the query object as <c>data</c>, and use either <see cref="RequiredResponse{T}"/> or <see cref="NullableResponse{T}"/>
/// To deserialize the response
/// </summary>
/// <param name="data"></param>
/// <typeparam name="T"></typeparam>
internal record RequiredResponse<T>([property: JsonProperty(Required = Required.Always)] T data);

/// <inheritdoc cref="RequiredResponse{T}"/>
internal record NullableResponse<T>([property: JsonProperty(Required = Required.AllowNull)] T? data);

//TODO: replace with RequiredResponse{T}
internal record ServerInfoResponse([property: JsonProperty(Required = Required.Always)] ServerInfo serverInfo);
