﻿using System.Net.Http;
using GraphQL.Client.Http;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Credentials;
using Xunit;

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

    result.ShouldNotBeNull();
    result.frontend2.ShouldBeTrue();
  }

  [Fact]
  public async Task IsFrontEnd2False()
  {
    ServerInfo? result = await Fixtures
      .ServiceProvider.GetRequiredService<IAccountManager>()
      .GetServerInfo(new("https://speckle.xyz/"));

    result.ShouldNotBeNull();
    result.frontend2.ShouldBeFalse();
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

    await Should.ThrowAsync<HttpRequestException>(
      async () => await Fixtures.ServiceProvider.GetRequiredService<IAccountManager>().GetServerInfo(serverUrl)
    );
  }

  [Fact]
  public async Task GetServerInfo_ExpectFail_NoServer()
  {
    Uri serverUrl = new("http://invalidserver.local");

    await Should.ThrowAsync<HttpRequestException>(
      async () => await Fixtures.ServiceProvider.GetRequiredService<IAccountManager>().GetServerInfo(serverUrl)
    );
  }

  [Fact]
  public async Task GetUserInfo()
  {
    Uri serverUrl = new(_acc.serverInfo.url);
    UserInfo result = await Fixtures
      .ServiceProvider.GetRequiredService<IAccountManager>()
      .GetUserInfo(_acc.token, serverUrl);

    result.id.ShouldBe(_acc.userInfo.id);
    result.name.ShouldBe(_acc.userInfo.name);
    result.email.ShouldBe(_acc.userInfo.email);
    result.company.ShouldBe(_acc.userInfo.company);
    result.avatar.ShouldBe(_acc.userInfo.avatar);
  }

  [Fact]
  public async Task GetUserInfo_ExpectFail_NoServer()
  {
    Uri serverUrl = new("http://invalidserver.local");

    await Should.ThrowAsync<HttpRequestException>(
      async () => await Fixtures.ServiceProvider.GetRequiredService<IAccountManager>().GetUserInfo("", serverUrl)
    );
  }

  [Fact]
  public async Task GetUserInfo_ExpectFail_NoUser()
  {
    Uri serverUrl = new(_acc.serverInfo.url);

    await Should.ThrowAsync<GraphQLHttpRequestException>(
      async () =>
        await Fixtures
          .ServiceProvider.GetRequiredService<IAccountManager>()
          .GetUserInfo("Bearer 08913c3c1e7ac65d779d1e1f11b942a44ad9672ca9", serverUrl)
    );
  }
}
