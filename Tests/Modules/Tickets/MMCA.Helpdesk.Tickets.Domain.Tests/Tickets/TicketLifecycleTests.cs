using AwesomeAssertions;
using MMCA.Helpdesk.Tickets.Domain.Tickets;
using MMCA.Helpdesk.Tickets.Shared.Tickets;

namespace MMCA.Helpdesk.Tickets.Domain.Tests.Tickets;

/// <summary>
/// Pins the aggregate's status enumeration against the wire enum it sits behind. The API keeps
/// sending and accepting <see cref="TicketStatus"/> numbers, and the column keeps holding the member
/// name, so <see cref="TicketLifecycle"/> must carry exactly the enum's values AND names: a member
/// whose value drifts changes the wire, and one whose name drifts orphans every stored row.
/// </summary>
public sealed class TicketLifecycleTests
{
    public static TheoryData<TicketStatus> ContractMembers => new(Enum.GetValues<TicketStatus>());

    [Theory]
    [MemberData(nameof(ContractMembers))]
    public void FromContract_ThenToContract_RoundTripsEveryMember_WithTheSameName(TicketStatus status)
    {
        var lifecycle = TicketLifecycle.FromContract(status);

        lifecycle.IsSuccess.Should().BeTrue();
        lifecycle.Value!.ToContract().Should().Be(status);
        lifecycle.Value.Name.Should().Be(Enum.GetName(status), "the stored column text is the member name, and rows written before the change hold the enum's names");
    }

    [Fact]
    public void FromContract_UnknownValue_FailsWithEnumerationUnknownValue()
    {
        var lifecycle = TicketLifecycle.FromContract((TicketStatus)7);

        lifecycle.IsFailure.Should().BeTrue();
        lifecycle.Errors.Should().Contain(e => e.Code == "Enumeration.UnknownValue");
    }

    // The behaviour lives on the enumeration and is named without the child collection, so a scaffold
    // with no children keeps it: a locked ticket takes no further activity, though its status can
    // still be changed.
    [Fact]
    public void IsLocked_IsTrueOnlyForClosed()
    {
        TicketLifecycle.All.Where(member => member.IsLocked)
            .Should().ContainSingle()
            .Which.Should().Be(TicketLifecycle.Closed);
    }

    [Fact]
    public void All_IsOrderedOpenInProgressResolvedClosed()
    {
        TicketLifecycle.All.Should().Equal(
            TicketLifecycle.Open,
            TicketLifecycle.InProgress,
            TicketLifecycle.Resolved,
            TicketLifecycle.Closed);
    }
}
