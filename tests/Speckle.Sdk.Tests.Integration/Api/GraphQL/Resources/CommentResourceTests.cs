using FluentAssertions;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Api.GraphQL.Resources;
using Speckle.Sdk.Common;
using Version = Speckle.Sdk.Api.GraphQL.Models.Version;

namespace Speckle.Sdk.Tests.Integration.API.GraphQL.Resources;

public class CommentResourceTests : IAsyncLifetime
{
  private IClient _testUser;
  private CommentResource Sut;
  private Project _project;
  private Model _model;
  private Version _version;
  private Comment _comment;

  // Constructor for setup
  public async Task InitializeAsync()
  {
    // Synchronous operations converted to async Task.Run for constructor
    _testUser = await Fixtures.SeedUserWithClient();
    _project = await _testUser.Project.Create(new("Test project", "", null));
    _model = await _testUser.Model.Create(new("Test Model 1", "", _project.id));
    _version = await Fixtures.CreateVersion(_testUser, _project.id, _model.id);
    _comment = await CreateComment();
    Sut = _testUser.Comment;
  }

  public Task DisposeAsync()
  {
    // No resources to dispose
    return Task.CompletedTask;
  }

  [Fact]
  public async Task Get()
  {
    var comment = await Sut.Get(_comment.id, _project.id);

    comment.Should().NotBeNull();
    comment.id.Should().Be(_comment.id);
    comment.authorId.Should().Be(_testUser.Account.userInfo.id);
  }

  [Fact]
  public async Task GetProjectComments()
  {
    var comments = await Sut.GetProjectComments(_project.id);

    comments.Should().NotBeNull();
    comments.items.Count.Should().Be(1);
    comments.totalCount.Should().Be(1);

    Comment comment = comments.items[0];
    comment.Should().NotBeNull();
    comment.authorId.Should().Be(_testUser.Account.userInfo.id);
    comment.id.Should().Be(_comment.id);
    comment.authorId.Should().Be(_comment.authorId);
    comment.archived.Should().Be(false);
    comment.createdAt.Should().Be(_comment.createdAt);
  }

  [Fact]
  public async Task MarkViewed()
  {
    await Sut.MarkViewed(new(_comment.id, _project.id));

    var res = await Sut.Get(_comment.id, _project.id);
    res.viewedAt.Should().NotBeNull();
  }

  [Fact]
  public async Task Archive()
  {
    await Sut.Archive(new(_comment.id, _project.id, true));
    var archived = await Sut.Get(_comment.id, _project.id);

    archived.archived.Should().BeTrue();

    await Sut.Archive(new(_comment.id, _project.id, false));
    var unarchived = await Sut.Get(_comment.id, _project.id);

    unarchived.archived.Should().BeFalse();
  }

  [Fact]
  public async Task Edit()
  {
    var blobs = await Fixtures.SendBlobData(_testUser.Account, _project.id);
    var blobIds = blobs.Select(b => b.id.NotNull()).ToList();
    var input = new EditCommentInput(new(blobIds, null), _comment.id, _project.id);

    var editedComment = await Sut.Edit(input);

    editedComment.Should().NotBeNull();
    editedComment.id.Should().Be(_comment.id);
    editedComment.authorId.Should().Be(_comment.authorId);
    editedComment.createdAt.Should().Be(_comment.createdAt);
    editedComment.updatedAt.Should().BeOnOrAfter(_comment.updatedAt);
  }

  [Fact]
  public async Task Reply()
  {
    var blobs = await Fixtures.SendBlobData(_testUser.Account, _project.id);
    var blobIds = blobs.Select(b => b.id.NotNull()).ToList();
    var input = new CreateCommentReplyInput(new(blobIds, null), _comment.id, _project.id);

    var editedComment = await Sut.Reply(input);

    editedComment.Should().NotBeNull();
  }

  private async Task<Comment> CreateComment()
  {
    return await Fixtures.CreateComment(_testUser, _project.id, _model.id, _version.id);
  }
}
