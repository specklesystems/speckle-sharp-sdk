using Shouldly;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Api.GraphQL.Resources;
using Speckle.Sdk.Common;
using Xunit;

namespace Speckle.Sdk.Tests.Integration.API.GraphQL.Resources;

public class CommentResourceTests
{
  private readonly Client _testUser;
  private readonly CommentResource Sut;
  private readonly Project _project;
  private readonly Model _model;
  private readonly string _versionId;
  private readonly Comment _comment;

  // Constructor for setup
  public CommentResourceTests()
  {
    // Synchronous operations converted to async Task.Run for constructor
    _testUser = Task.Run(async () => await Fixtures.SeedUserWithClient()).Result!;
    _project = Task.Run(async () => await _testUser.Project.Create(new("Test project", "", null))).Result!;
    _model = Task.Run(async () => await _testUser.Model.Create(new("Test Model 1", "", _project.id))).Result!;
    _versionId = Task.Run(async () => await Fixtures.CreateVersion(_testUser, _project.id, _model.id)).Result!;
    _comment = Task.Run(CreateComment).Result!;
    Sut = _testUser.Comment;
  }

  [Fact]
  public async Task Get()
  {
    var comment = await Sut.Get(_comment.id, _project.id);

    comment.ShouldNotBeNull();
    comment.id.ShouldBe(_comment.id);
    comment.authorId.ShouldBe(_testUser.Account.userInfo.id);
  }

  [Fact]
  public async Task GetProjectComments()
  {
    var comments = await Sut.GetProjectComments(_project.id);

    comments.ShouldNotBeNull();
    comments.items.Count.ShouldBe(1);
    comments.totalCount.ShouldBe(1);

    Comment comment = comments.items[0];
    comment.ShouldNotBeNull();
    comment.authorId.ShouldBe(_testUser.Account.userInfo.id);
    comment.ShouldSatisfyAllConditions(
      () => comment.id.ShouldBe(_comment.id),
      () => comment.authorId.ShouldBe(_comment.authorId),
      () => comment.archived.ShouldBe(false),
      () => comment.createdAt.ShouldBe(_comment.createdAt)
    );
  }

  [Fact]
  public async Task MarkViewed()
  {
    await Sut.MarkViewed(new(_comment.id, _project.id));

    var res = await Sut.Get(_comment.id, _project.id);
    res.viewedAt.ShouldNotBeNull();
  }

  [Fact]
  public async Task Archive()
  {
    await Sut.Archive(new(_comment.id, _project.id, true));
    var archived = await Sut.Get(_comment.id, _project.id);

    archived.archived.ShouldBeTrue();

    await Sut.Archive(new(_comment.id, _project.id, false));
    var unarchived = await Sut.Get(_comment.id, _project.id);

    unarchived.archived.ShouldBeFalse();
  }

  [Fact]
  public async Task Edit()
  {
    var blobs = await Fixtures.SendBlobData(_testUser.Account, _project.id);
    var blobIds = blobs.Select(b => b.id.NotNull()).ToList();
    var input = new EditCommentInput(new(blobIds, null), _comment.id, _project.id);

    var editedComment = await Sut.Edit(input);

    editedComment.ShouldNotBeNull();
    editedComment.ShouldSatisfyAllConditions(
      () => editedComment.id.ShouldBe(_comment.id),
      () => editedComment.authorId.ShouldBe(_comment.authorId),
      () => editedComment.createdAt.ShouldBe(_comment.createdAt),
      () => editedComment.updatedAt.ShouldBeGreaterThanOrEqualTo(_comment.updatedAt)
    );
  }

  [Fact]
  public async Task Reply()
  {
    var blobs = await Fixtures.SendBlobData(_testUser.Account, _project.id);
    var blobIds = blobs.Select(b => b.id.NotNull()).ToList();
    var input = new CreateCommentReplyInput(new(blobIds, null), _comment.id, _project.id);

    var editedComment = await Sut.Reply(input);

    editedComment.ShouldNotBeNull();
  }

  private async Task<Comment> CreateComment()
  {
    return await Fixtures.CreateComment(_testUser, _project.id, _model.id, _versionId);
  }
}
