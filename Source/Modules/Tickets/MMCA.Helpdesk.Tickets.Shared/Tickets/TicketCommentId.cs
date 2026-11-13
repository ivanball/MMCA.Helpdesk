using System.Globalization;
using System.Text.Json.Serialization;
using MMCA.Common.Shared.Identifiers;

namespace MMCA.Helpdesk.Tickets.Shared.Tickets;

/// <summary>
/// Strongly typed identifier of a ticket's child (ADR-115): a distinct type over the <see langword="int"/>
/// identity column, so a child id can no longer be passed where a ticket id is expected. On the wire
/// and in the database it is the bare integer it wraps.
/// <para>
/// The converter attribute is what keeps the JSON a bare integer for a serializer that has no
/// converter factory registered (the UI's typed HttpClient and the cache); the API's own options
/// register the same factory, so both paths write and read the same shape.
/// </para>
/// </summary>
/// <param name="Value">The wrapped identity value.</param>
[JsonConverter(typeof(StronglyTypedIdJsonConverterFactory))]
public readonly record struct TicketCommentId(int Value) : IStronglyTypedId<TicketCommentId, int>
{
    /// <inheritdoc />
    public static TicketCommentId From(int value) => new(value);

    /// <summary>The invariant integer text, so URLs, labels and log lines read the bare number.</summary>
    /// <returns>The wrapped value formatted with the invariant culture.</returns>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
