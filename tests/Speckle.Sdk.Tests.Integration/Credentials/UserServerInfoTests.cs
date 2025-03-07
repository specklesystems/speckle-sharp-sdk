using FluentAssertions;
using GraphQL.Client.Http;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Credentials;


namespace Speckle.Sdk.Tests.Integration.Credentials;

public class UserServerInfoTests : IAsyncLifetime
{
  private Account _acc;

  public Task DisposeAsync() => Task.CompletedTask;

  public async Task InitializeAsync()
  {
    _acc = await Fixtures.SeedUser();
  }

  [Fact]
  public async Task IsFrontEnd2True()
  {
    ServerInfo? result = await Fixtures
      .ServiceProvider.GetRequiredService<IAccountManager>()
      .GetServerInfo(new("https://app.speckle.systems/"));

    result.Should().NotBeNull();
    result.frontend2.Should().BeTrue();
  }

  /// <remarks>
  /// We get ServerInfo from "http://localhost:3000/graphql",
  /// Then we mutate the `frontend2` property of ServerInfo by trying to fetch header from "http://localhost:3000/",
  /// This is not doable in local server because there is no end-point on this to ping.
  /// This is a bad sign for mutation.
  /// </remarks>
  [Fact]
  public async Task GetServerInfo_ExpectFail_CantPing()
  {
    Uri serverUrl = new(_acc.serverInfo.url);

    await FluentActions
      .Invoking(
        async () => await Fixtures.ServiceProvider.GetRequiredService<IAccountManager>().GetServerInfo(serverUrl)
      )
      .Should()
      .ThrowAsync<HttpRequestException>();
  }

  [Fact]
  public async Task GetServerInfo_ExpectFail_NoServer()
  {
    Uri serverUrl = new("http://invalidserver.local");

    await FluentActions
      .Invoking(
        async () => await Fixtures.ServiceProvider.GetRequiredService<IAccountManager>().GetServerInfo(serverUrl)
      )
      .Should()
      .ThrowAsync<HttpRequestException>();
  }

  [Fact]
  public async Task GetUserInfo()
  {
    Uri serverUrl = new(_acc.serverInfo.url);
    UserInfo result = await Fixtures
      .ServiceProvider.GetRequiredService<IAccountManager>()
      .GetUserInfo(_acc.token, serverUrl);

    result.id.Should().Be(_acc.userInfo.id);
    result.name.Should().Be(_acc.userInfo.name);
    result.email.Should().Be(_acc.userInfo.email);
    result.company.Should().Be(_acc.userInfo.company);
    result.avatar.Should().Be(_acc.userInfo.avatar);
  }

  [Fact]
  public async Task GetUserInfo_ExpectFail_NoServer()
  {
    Uri serverUrl = new("http://invalidserver.local");

    await FluentActions
      .Invoking(
        async () => await Fixtures.ServiceProvider.GetRequiredService<IAccountManager>().GetUserInfo("", serverUrl)
      )
      .Should()
      .ThrowAsync<HttpRequestException>();
  }

  [Fact]
  public async Task GetUserInfo_ExpectFail_NoUser()
  {
    Uri serverUrl = new(_acc.serverInfo.url);
    await FluentActions
      .Invoking(
        async () =>
          await Fixtures
            .ServiceProvider.GetRequiredService<IAccountManager>()
            .GetUserInfo("Bearer 08913c3c1e7ac65d779d1e1f11b942a44ad9672ca9", serverUrl)
      )
      .Should()
      .ThrowAsync<GraphQLHttpRequestException>();
  }
}
