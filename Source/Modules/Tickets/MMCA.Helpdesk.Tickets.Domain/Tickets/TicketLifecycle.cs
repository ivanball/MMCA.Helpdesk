using System.Text.Json.Serialization;
using MMCA.Common.Shared.Abstractions;
using MMCA.Common.Shared.ValueObjects;
using MMCA.Helpdesk.Tickets.Shared.Tickets;

namespace MMCA.Helpdesk.Tickets.Domain.Tickets;

/// <summary>
/// The ticket's lifecycle as a smart enumeration (ADR-104): the aggregate's own status type, with the
/// lifecycle rules on its members rather than in a switch elsewhere.
/// <para>
/// The wire contract stays the <see cref="TicketStatus"/> enum on purpose: requests, responses, the
/// UI and the cache keep sending and reading numbers, while the framework's enumeration converter
/// would turn this type into strings. So the two meet at two conversion points only, the status
/// handler on the way in (<see cref="FromContract"/>) and the DTO mapping on the way out
/// (<see cref="ToContract"/>). Every member's value AND name equal the enum member's: the value is
/// the wire number, and the name is the text the status column already holds.
/// </para>
/// </summary>
[JsonConverter(typeof(EnumerationJsonConverterFactory))]
public sealed class TicketLifecycle : Enumeration<TicketLifecycle>
{
    public static readonly TicketLifecycle Open = new(0, "Open", isLocked: false);
    public static readonly TicketLifecycle InProgress = new(1, "InProgress", isLocked: false);
    public static readonly TicketLifecycle Resolved = new(2, "Resolved", isLocked: false);
    public static readonly TicketLifecycle Closed = new(3, "Closed", isLocked: true);

    private TicketLifecycle(int value, string name, bool isLocked)
        : base(value, name) => IsLocked = isLocked;

    /// <summary>
    /// Gets a value indicating whether a ticket in this status takes no further activity (its status
    /// can still be changed). True for <see cref="Closed"/> only.
    /// </summary>
    public bool IsLocked { get; }

    /// <summary>Resolves the lifecycle member a wire status names.</summary>
    /// <param name="status">The status as the API received it.</param>
    /// <returns>The member with the same value, or an <c>Enumeration.UnknownValue</c> failure.</returns>
    public static Result<TicketLifecycle> FromContract(TicketStatus status) => FromValue((int)status);

    /// <summary>The wire status for this member.</summary>
    /// <returns>The enum member with the same value (and name).</returns>
    public TicketStatus ToContract() => (TicketStatus)Value;
}
