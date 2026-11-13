using AwesomeAssertions;
using MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.RemoveComment;

namespace MMCA.Helpdesk.Tickets.Application.Tests.Validation;

/// <summary>
/// The child identifier is a strongly typed wrapper (ADR-115), so the validator's <c>NotEmpty</c>
/// compares against the WRAPPER's default rather than the integer's. This pins that a route that
/// supplied no child id is still refused before the transaction opens.
/// </summary>
public sealed class RemoveCommentCommandValidatorTests
{
    private const TicketIdentifierType TicketId = 7;

    [Fact]
    public void DefaultCommentId_IsRejected()
    {
        var command = new RemoveCommentCommand(TicketId, CommentId: default);

        var result = new RemoveCommentCommandValidator().Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.PropertyName.Should().Be(nameof(RemoveCommentCommand.CommentId), "the ticket id is valid, so the default child id is the only broken rule");
    }
}
