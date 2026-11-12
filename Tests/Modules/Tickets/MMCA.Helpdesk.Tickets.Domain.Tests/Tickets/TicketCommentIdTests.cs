using System.Text.Json;
using AwesomeAssertions;
using MMCA.Common.Shared.Abstractions;
using MMCA.Common.Shared.Identifiers;
using MMCA.Common.Shared.ValueObjects;
using MMCA.Helpdesk.Tickets.Domain.Tickets;
using MMCA.Helpdesk.Tickets.Shared.Tickets;

namespace MMCA.Helpdesk.Tickets.Domain.Tests.Tickets;

/// <summary>
/// Pins the strongly typed child identifier (ADR-115) the Tickets module adopts: on the wire it is
/// the bare integer it wraps, whichever serializer options the caller holds, and in the aggregate
/// it is compared by value like the primitive it replaced.
/// <para>
/// Two option sets matter. The API serializes through <c>AddAPI</c>'s options, which register the
/// framework's converter factories; the UI's typed HttpClient and the cache serialize through plain
/// web defaults with no factory registered, which is why the wrapper carries the converter attribute
/// itself. A wrapper that only round-trips under one of the two would break the other silently.
/// </para>
/// </summary>
public sealed class TicketCommentIdTests
{
    private static readonly JsonSerializerOptions DefaultWebOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The converters <c>AddAPI</c> adds on top of the web defaults
    /// (<c>MMCA.Common.API/DependencyInjection.cs</c>), minus the currency converter no Tickets
    /// type uses.
    /// </summary>
    private static readonly JsonSerializerOptions AddApiOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new EnumerationJsonConverterFactory(), new StronglyTypedIdJsonConverterFactory() },
    };

    [Fact]
    public void Json_DefaultWebOptions_WritesBareInteger()
    {
        string json = JsonSerializer.Serialize(NewDto(TicketCommentId.From(5)), DefaultWebOptions);

        using var document = JsonDocument.Parse(json);
        var id = document.RootElement.GetProperty("id");
        id.ValueKind.Should().Be(JsonValueKind.Number, "the UI client and the cache hold no converter factory, so the wrapper's own attribute must write the bare number");
        id.GetInt32().Should().Be(5);
    }

    [Fact]
    public void Json_DefaultWebOptions_ReadsBareInteger()
    {
        const string json = """{"id":5,"body":"Looking into it.","authorUserId":7}""";

        var dto = JsonSerializer.Deserialize<TicketCommentDTO>(json, DefaultWebOptions);

        dto.Should().NotBeNull();
        dto!.Id.Should().Be(TicketCommentId.From(5));
    }

    [Fact]
    public void Json_WithAddApiConverters_WritesBareInteger()
    {
        string json = JsonSerializer.Serialize(NewDto(TicketCommentId.From(5)), AddApiOptions);

        using var document = JsonDocument.Parse(json);
        var id = document.RootElement.GetProperty("id");
        id.ValueKind.Should().Be(JsonValueKind.Number, "the API's response shape is unchanged by the wrapper: the id stays a bare integer");
        id.GetInt32().Should().Be(5);
    }

    [Fact]
    public void ToString_IsTheInvariantInteger()
    {
        // URLs, aria labels and log lines interpolate the id, so its text must stay the bare number.
        TicketCommentId.From(5).ToString().Should().Be("5");
    }

    [Fact]
    public void TwoIdsWithSameValue_AreEqual()
    {
        var first = TicketCommentId.From(5);
        var second = TicketCommentId.From(5);

        first.Should().Be(second);
        (first == second).Should().BeTrue();
        first.GetHashCode().Should().Be(second.GetHashCode());
        (first == TicketCommentId.From(6)).Should().BeFalse();
    }

    [Fact]
    public void EditComment_WithUnknownWrappedId_FailsNotFound()
    {
        var ticket = NewTicket();
        ticket.AddComment(id: null, "Looking into it.", authorUserId: 7).IsSuccess.Should().BeTrue();

        var result = ticket.EditComment(TicketCommentId.From(999), "Edited");

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(
            e => e.Type == ErrorType.NotFound,
            "the child lookup compares wrapped ids by value, so an id no child carries is NotFound, not a match");
    }

    private static TicketCommentDTO NewDto(TicketCommentId id) =>
        new() { Id = id, Body = "Looking into it.", AuthorUserId = 7 };

    /// <summary>
    /// Builds an open ticket through the aggregate's own factory, with the arguments filled by
    /// parameter type rather than written positionally. The factory's parameter list varies with the
    /// scaffold's optional shape flags, and only TicketTests.cs has its factory calls
    /// rewritten per axis when the template is staged, so a positional call here would not compile
    /// in a generated app that dropped one of them. TicketsExportScopeTests fills a constructor the
    /// same way for the same reason.
    /// </summary>
    /// <returns>A new, open ticket.</returns>
    private static Ticket NewTicket()
    {
        var create = typeof(Ticket).GetMethod(nameof(Ticket.Create))!;
        object?[] arguments = [.. create.GetParameters().Select(p => ArgumentFor(p.ParameterType))];

        var created = (Result<Ticket>)create.Invoke(null, arguments)!;
        return created.Value!;
    }

    /// <summary>
    /// A valid value for one factory parameter: text for the string fields, a user id for the int
    /// one, and null for the optional identifier (the id is database-generated).
    /// </summary>
    /// <param name="parameterType">The parameter's declared type.</param>
    /// <returns>The argument to pass.</returns>
    private static object? ArgumentFor(Type parameterType) => parameterType switch
    {
        var t when t == typeof(string) => "Cannot log in",
        var t when t == typeof(int) => 42,
        _ => null,
    };
}
