using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Api.GraphQL.Resources;
using Speckle.Sdk.Common;

namespace Speckle.Sdk.Tests.Integration.API.GraphQL.Resources;

[TestOf(typeof(CommentResource))]
public class CommentResourceTests
{
  private Client _testUser;
  private CommentResource Sut => _testUser.Comment;
  private Project _project;
  private Model _model;
  private string _versionId;
  private Comment _comment;

  [SetUp]
  public async Task Setup()
  {
    _testUser = await Fixtures.SeedUserWithClient();
    _project = await _testUser.Project.Create(new("Test project", "", null));
    _model = await _testUser.Model.Create(new("Test Model 1", "", _project.id));
    _versionId = await Fixtures.CreateVersion(_testUser, _project.id, _model.id);
    _comment = await CreateComment();
  }

  [Test]
  public async Task Get()
  {
    var comment = await Sut.Get(_comment.id, _project.id);
    Assert.That(comment.id, Is.EqualTo(_comment.id));
    Assert.That(comment.authorId, Is.EqualTo(_testUser.Account.userInfo.id));
  }

  [Test]
  public async Task GetProjectComments()
  {
    var comments = await Sut.GetProjectComments(_project.id);
    Assert.That(comments.items.Count, Is.EqualTo(1));
    Assert.That(comments.totalCount, Is.EqualTo(1));

    Comment comment = comments.items[0];
    Assert.That(comment, Is.Not.Null);
    Assert.That(comment, Has.Property(nameof(Comment.authorId)).EqualTo(_testUser.Account.userInfo.id));

    Assert.That(comment, Has.Property(nameof(Comment.id)).EqualTo(_comment.id));
    Assert.That(comment, Has.Property(nameof(Comment.authorId)).EqualTo(_comment.authorId));
    Assert.That(comment, Has.Property(nameof(Comment.archived)).EqualTo(_comment.archived));
    Assert.That(comment, Has.Property(nameof(Comment.archived)).EqualTo(false));
    Assert.That(comment, Has.Property(nameof(Comment.createdAt)).EqualTo(_comment.createdAt));
  }

  [Test]
  public async Task MarkViewed()
  {
    await Sut.MarkViewed(new(_comment.id, _project.id));
    var res = await Sut.Get(_comment.id, _project.id);

    Assert.That(res.viewedAt, Is.Not.Null);
  }

  [Test]
  public async Task Archive()
  {
    await Sut.Archive(new(_comment.id, _project.id, true));
    var archived = await Sut.Get(_comment.id, _project.id);
    Assert.That(archived.archived, Is.True);

    await Sut.Archive(new(_comment.id, _project.id, false));
    var unarchived = await Sut.Get(_comment.id, _project.id);
    Assert.That(unarchived.archived, Is.False);
  }

  [Test]
  public async Task Edit()
  {
    var blobs = await Fixtures.SendBlobData(_testUser.Account, _project.id);
    var blobIds = blobs.Select(b => b.id.NotNull()).ToList();
    EditCommentInput input = new(new(blobIds, null), _comment.id, _project.id);

    var editedComment = await Sut.Edit(input);

    Assert.That(editedComment, Is.Not.Null);
    Assert.That(editedComment, Has.Property(nameof(Comment.id)).EqualTo(_comment.id));
    Assert.That(editedComment, Has.Property(nameof(Comment.authorId)).EqualTo(_comment.authorId));
    Assert.That(editedComment, Has.Property(nameof(Comment.createdAt)).EqualTo(_comment.createdAt));
    Assert.That(editedComment, Has.Property(nameof(Comment.updatedAt)).GreaterThanOrEqualTo(_comment.updatedAt));
  }

  [Test]
  public async Task Reply()
  {
    var blobs = await Fixtures.SendBlobData(_testUser.Account, _project.id);
    var blobIds = blobs.Select(b => b.id.NotNull()).ToList();
    CreateCommentReplyInput input = new(new(blobIds, null), _comment.id, _project.id);

    var editedComment = await Sut.Reply(input);

    Assert.That(editedComment, Is.Not.Null);
  }

  private async Task<Comment> CreateComment()
  {
    return await Fixtures.CreateComment(_testUser, _project.id, _model.id, _versionId);
  }
}
