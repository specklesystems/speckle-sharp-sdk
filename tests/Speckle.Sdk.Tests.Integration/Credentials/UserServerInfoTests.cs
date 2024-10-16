﻿using GraphQL.Client.Http;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Credentials;

namespace Speckle.Sdk.Tests.Integration.Credentials;

public class UserServerInfoTests
{
  private Account _acc;

  [SetUp]
  public async Task Setup()
  {
    _acc = await Fixtures.SeedUser();
  }

  [Test]
  public async Task IsFrontEnd2True()
  {
    ServerInfo? result = await Fixtures
      .ServiceProvider.GetRequiredService<IAccountManager>()
      .GetServerInfo(new("https://app.speckle.systems/"));

    Assert.That(result, Is.Not.Null);
    Assert.That(result!.frontend2, Is.True);
  }

  [Test]
  public async Task IsFrontEnd2False()
  {
    ServerInfo? result = await Fixtures
      .ServiceProvider.GetRequiredService<IAccountManager>()
      .GetServerInfo(new("https://speckle.xyz/"));

    Assert.That(result, Is.Not.Null);
    Assert.That(result!.frontend2, Is.False);
  }

  /// <remarks>
  /// We get ServerInfo from "http://localhost:3000/graphql",
  /// Then we mutate the `frontend2` property of ServerInfo by trying to fetch header from "http://localhost:3000/",
  /// This is not doable in local server because there is no end-point on this to ping.
  /// This is a bad sign for mutation.
  /// </remarks>
  [Test]
  public void GetServerInfo_ExpectFail_CantPing()
  {
    Uri serverUrl = new(_acc.serverInfo.url);

    Assert.ThrowsAsync<HttpRequestException>(
      async () => await Fixtures.ServiceProvider.GetRequiredService<IAccountManager>().GetServerInfo(serverUrl)
    );
  }

  [Test]
  public void GetServerInfo_ExpectFail_NoServer()
  {
    Uri serverUrl = new("http://invalidserver.local");

    Assert.ThrowsAsync<HttpRequestException>(
      async () => await Fixtures.ServiceProvider.GetRequiredService<IAccountManager>().GetServerInfo(serverUrl)
    );
  }

  [Test]
  public async Task GetUserInfo()
  {
    Uri serverUrl = new(_acc.serverInfo.url);
    UserInfo result = await Fixtures
      .ServiceProvider.GetRequiredService<IAccountManager>()
      .GetUserInfo(_acc.token, serverUrl);

    Assert.That(result.id, Is.EqualTo(_acc.userInfo.id));
    Assert.That(result.name, Is.EqualTo(_acc.userInfo.name));
    Assert.That(result.email, Is.EqualTo(_acc.userInfo.email));
    Assert.That(result.company, Is.EqualTo(_acc.userInfo.company));
    Assert.That(result.avatar, Is.EqualTo(_acc.userInfo.avatar));
  }

  [Test]
  public void GetUserInfo_ExpectFail_NoServer()
  {
    Uri serverUrl = new("http://invalidserver.local");

    Assert.ThrowsAsync<HttpRequestException>(
      async () => await Fixtures.ServiceProvider.GetRequiredService<IAccountManager>().GetUserInfo("", serverUrl)
    );
  }

  [Test]
  public void GetUserInfo_ExpectFail_NoUser()
  {
    Uri serverUrl = new(_acc.serverInfo.url);

    Assert.ThrowsAsync<GraphQLHttpRequestException>(
      async () =>
        await Fixtures
          .ServiceProvider.GetRequiredService<IAccountManager>()
          .GetUserInfo("Bearer 08913c3c1e7ac65d779d1e1f11b942a44ad9672ca9", serverUrl)
    );
  }
}
