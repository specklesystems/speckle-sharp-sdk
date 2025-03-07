using FluentAssertions;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Resources;
using Speckle.Sdk.Credentials;


namespace Speckle.Sdk.Tests.Integration.API.GraphQL.Resources;

public class OtherUserResourceTests
{
  private readonly Client _testUser;
  private readonly Account _testData;
  private OtherUserResource Sut => _testUser.OtherUser;

  public OtherUserResourceTests()
  {
    _testUser = Fixtures.SeedUserWithClient().GetAwaiter().GetResult();
    _testData = Fixtures.SeedUser().GetAwaiter().GetResult();
  }

  [Fact]
  public async Task OtherUserGet_Should_ReturnCorrectUser()
  {
    var res = await Sut.Get(_testData.userInfo.id);

    res.Should().NotBeNull();
    res!.name.Should().Be(_testData.userInfo.name);
  }

  [Fact]
  public async Task OtherUserGet_NonExistentUser_Should_ReturnNull()
  {
    var result = await Sut.Get("AnIdThatDoesntExist");

    result.Should().BeNull();
  }

  [Fact]
  public async Task UserSearch_Should_ReturnMatchingUser()
  {
    var res = await Sut.UserSearch(_testData.userInfo.email, 25);

    res.items.Should().ContainSingle();
    res.items[0].id.Should().Be(_testData.userInfo.id);
  }

  [Fact]
  public async Task UserSearch_NonExistentUser_Should_ReturnEmptyList()
  {
    var res = await Sut.UserSearch("idontexist@example.com", 25);

    res.items.Should().BeEmpty();
  }
}
