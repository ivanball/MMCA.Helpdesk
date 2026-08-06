using AwesomeAssertions;
// template:begin child
using MMCA.Common.Domain.Enums;
// template:end child
using MMCA.Helpdesk.Tickets.Domain.Tickets;
// template:begin child
using MMCA.Helpdesk.Tickets.Domain.Tickets.DomainEvents;
// template:end child
// template:begin status
using MMCA.Helpdesk.Tickets.Shared.Tickets;

// template:end status
namespace MMCA.Helpdesk.Tickets.Domain.Tests.Tickets;

public class TicketTests
{
    [Fact]
    public void Create_WithValidData_ReturnsSuccess()
    {
        var result = Ticket.Create(id: null, "Cannot log in", "The login page returns a 500.", requesterUserId: 42);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Title.Should().Be("Cannot log in");
        // template:begin status
        result.Value.Status.Should().Be(TicketStatus.Open);
        // template:end status
        result.Value.RequesterUserId.Should().Be(42);
    }

    [Fact]
    public void Create_DoesNotRaiseDomainEvent_CreationIsSignalledByIntegrationEvent()
    {
        var result = Ticket.Create(id: null, "Title", "Description", requesterUserId: 1);

        result.IsSuccess.Should().BeTrue();
        // The aggregate omits an "Added" domain event because the Id is DB-generated; the create
        // handler publishes TicketOpenedIntegrationEvent (with the real id) after commit instead.
        result.Value!.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithEmptyTitle_ReturnsFailure()
    {
        var result = Ticket.Create(id: null, "   ", "Description", requesterUserId: 1);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Code == "Ticket.Title.Empty");
    }

    // template:begin child
    [Fact]
    public void AddComment_OnOpenTicket_AddsCommentAndRaisesEvent()
    {
        var ticket = CreateOpenTicket();

        var result = ticket.AddComment(id: null, "Looking into it.", authorUserId: 7);

        result.IsSuccess.Should().BeTrue();
        ticket.Comments.Should().ContainSingle();
        ticket.DomainEvents.OfType<TicketChanged>()
            .Should().Contain(e => e.State == DomainEntityState.Updated);
    }

    // template:end child
    // template:begin childStatus
    [Fact]
    public void AddComment_OnClosedTicket_ReturnsFailure()
    {
        var ticket = CreateOpenTicket();
        ticket.ChangeStatus(TicketStatus.Closed);

        var result = ticket.AddComment(id: null, "Late comment", authorUserId: 7);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Code == "Ticket.Closed");
    }

    // template:end childStatus
    // template:begin status
    [Fact]
    public void ChangeStatus_UpdatesStatus()
    {
        var ticket = CreateOpenTicket();

        var result = ticket.ChangeStatus(TicketStatus.Resolved);

        result.IsSuccess.Should().BeTrue();
        ticket.Status.Should().Be(TicketStatus.Resolved);
    }

    // template:end status
    // template:begin child
    [Fact]
    public void Delete_SoftDeletesTicketAndCascadesToComments()
    {
        var ticket = CreateOpenTicket();
        ticket.AddComment(id: null, "A comment", authorUserId: 7);

        var result = ticket.Delete();

        result.IsSuccess.Should().BeTrue();
        ticket.IsDeleted.Should().BeTrue();
        ticket.Comments.Should().OnlyContain(c => c.IsDeleted);
    }

    // template:end child
    [Fact]
    public void UpdateDetails_WithValidData_UpdatesTitleAndDescription()
    {
        var ticket = CreateOpenTicket();

        var result = ticket.UpdateDetails("New title", "New description");

        result.IsSuccess.Should().BeTrue();
        ticket.Title.Should().Be("New title");
        ticket.Description.Should().Be("New description");
    }

    [Fact]
    public void UpdateDetails_WithEmptyTitle_ReturnsFailure()
    {
        var ticket = CreateOpenTicket();

        var result = ticket.UpdateDetails("   ", "New description");

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Code == "Ticket.Title.Empty");
    }

    // template:begin child
    [Fact]
    public void EditComment_UpdatesBody()
    {
        var ticket = CreateOpenTicket();
        var comment = ticket.AddComment(id: null, "Original", authorUserId: 7).Value!;

        var result = ticket.EditComment(comment.Id, "Edited");

        result.IsSuccess.Should().BeTrue();
        comment.Body.Should().Be("Edited");
    }

    [Fact]
    public void EditComment_UnknownId_ReturnsFailure()
    {
        var ticket = CreateOpenTicket();

        var result = ticket.EditComment(commentId: 999, "Edited");

        result.IsFailure.Should().BeTrue();
    }

    // template:end child
    // template:begin childStatus
    [Fact]
    public void EditComment_OnClosedTicket_ReturnsFailure()
    {
        var ticket = CreateOpenTicket();
        var comment = ticket.AddComment(id: null, "Original", authorUserId: 7).Value!;
        ticket.ChangeStatus(TicketStatus.Closed);

        var result = ticket.EditComment(comment.Id, "Edited");

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Code == "Ticket.Closed");
    }

    [Fact]
    public void RemoveComment_OnClosedTicket_ReturnsFailure()
    {
        var ticket = CreateOpenTicket();
        var comment = ticket.AddComment(id: null, "To remove", authorUserId: 7).Value!;
        ticket.ChangeStatus(TicketStatus.Closed);

        var result = ticket.RemoveComment(comment.Id);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Code == "Ticket.Closed");
    }

    // template:end childStatus
    // template:begin child
    [Fact]
    public void RemoveComment_SoftDeletesComment()
    {
        var ticket = CreateOpenTicket();
        var comment = ticket.AddComment(id: null, "To remove", authorUserId: 7).Value!;

        var result = ticket.RemoveComment(comment.Id);

        result.IsSuccess.Should().BeTrue();
        comment.IsDeleted.Should().BeTrue();
    }

    // template:end child
    private static Ticket CreateOpenTicket() =>
        Ticket.Create(id: null, "Title", "Description", requesterUserId: 1).Value!;
}
