using FluentAssertions;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Common;

namespace Speckle.Sdk.Tests.Integration.API.GraphQL.Resources;

public class ProjectInviteResourceTests : IAsyncLifetime
{
  private IClient _inviter,
    _invitee;
  private Project _project;
  private PendingStreamCollaborator _createdInvite;

  public async Task InitializeAsync()
  {
    _inviter = await Fixtures.SeedUserWithClient();
    _invitee = await Fixtures.SeedUserWithClient();
    _project = await _inviter.Project.Create(new("test", null, null));
    _createdInvite = await SeedInvite();
  }

  public Task DisposeAsync() => Task.CompletedTask;

  private async Task<PendingStreamCollaborator> SeedInvite()
  {
    ProjectInviteCreateInput input = new(_invitee.Account.userInfo.email, null, null, null);
    var res = await _inviter.ProjectInvite.Create(_project.id, input);
    var invites = await _invitee.ActiveUser.GetProjectInvites();
    return invites.First(i => i.projectId == res.id);
  }

  [Fact]
  public async Task ProjectInviteCreate_ByEmail()
  {
    ProjectInviteCreateInput input = new(_invitee.Account.userInfo.email, null, null, null);
    var res = await _inviter.ProjectInvite.Create(_project.id, input);

    var invites = await _invitee.ActiveUser.GetProjectInvites();
    var invite = invites.First(i => i.projectId == res.id);

    res.id.Should().Be(_project.id);
    res.invitedTeam.Should().ContainSingle();
    invite.user!.id.Should().Be(_invitee.Account.userInfo.id);
    invite.token.Should().NotBeNull();
  }

  [Fact]
  public async Task ProjectInviteCreate_ByUserId()
  {
    ProjectInviteCreateInput input = new(null, null, null, _invitee.Account.userInfo.id);
    var res = await _inviter.ProjectInvite.Create(_project.id, input);

    res.id.Should().Be(_project.id);
    res.invitedTeam.Should().ContainSingle();
    res.invitedTeam[0].user!.id.Should().Be(_invitee.Account.userInfo.id);
  }

  [Fact]
  public async Task ProjectInviteGet()
  {
    var collaborator = await _invitee.ProjectInvite.Get(_project.id, _createdInvite.token).NotNull();

    collaborator.inviteId.Should().Be(_createdInvite.inviteId);
    collaborator.user!.id.Should().Be(_createdInvite.user!.id);
  }

  [Fact]
  public async Task ProjectInviteGet_NonExisting()
  {
    var collaborator = await _invitee.ProjectInvite.Get(_project.id, "this is not a real token");
    collaborator.Should().BeNull();
  }

  [Fact]
  public async Task ProjectInviteUse_MemberAdded()
  {
    ProjectInviteUseInput input = new(true, _createdInvite.projectId, _createdInvite.token.NotNull());
    await _invitee.ProjectInvite.Use(input);

    var project = await _inviter.Project.GetWithTeam(_project.id);
    var teamMembers = project.team.Select(c => c.user.id).ToArray();
    var expectedTeamMembers = new[] { _inviter.Account.userInfo.id, _invitee.Account.userInfo.id };

    teamMembers.Should().BeEquivalentTo(expectedTeamMembers);
  }

  [Fact]
  public async Task ProjectInviteCancel_MemberNotAdded()
  {
    var res = await _inviter.ProjectInvite.Cancel(_createdInvite.projectId, _createdInvite.inviteId);
    res.invitedTeam.Should().BeEmpty();
  }

  [Theory]
  [InlineData(StreamRoles.STREAM_OWNER)]
  [InlineData(StreamRoles.STREAM_CONTRIBUTOR)]
  [InlineData(StreamRoles.STREAM_REVIEWER)]
  [InlineData(StreamRoles.REVOKE)]
  public async Task ProjectUpdateRole(string? newRole)
  {
    await ProjectInviteUse_MemberAdded();

    ProjectUpdateRoleInput input = new(_invitee.Account.userInfo.id, _project.id, newRole);
    await _inviter.Project.UpdateRole(input);

    var finalProject = await _invitee.Project.Get(_project.id);
    finalProject.role.Should().Be(newRole);
  }
}
