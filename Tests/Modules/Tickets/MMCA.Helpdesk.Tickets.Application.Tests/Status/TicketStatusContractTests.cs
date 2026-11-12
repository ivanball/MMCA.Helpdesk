using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using MMCA.Common.Application;
using MMCA.Common.Application.Interfaces.Infrastructure.Persistence;
using MMCA.Common.Application.Settings;
using MMCA.Common.Application.UseCases.Contracts;
using MMCA.Common.Shared.Abstractions;
using MMCA.Common.Shared.Identifiers;
using MMCA.Common.Shared.ValueObjects;
using MMCA.Helpdesk.Tickets.Application.Tickets.DTOs;
using MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.ChangeStatus;
using MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.Create;
using MMCA.Helpdesk.Tickets.Domain.Tickets;
using MMCA.Helpdesk.Tickets.Shared.Tickets;
using Moq;

namespace MMCA.Helpdesk.Tickets.Application.Tests.Status;

/// <summary>
/// Pins the two conversion points between the aggregate's <see cref="TicketLifecycle"/> and the
/// wire enum <see cref="TicketStatus"/>: the DTO mapper on the way out and the status handler on the
/// way in. The contract stays the enum on purpose, so the API keeps sending and accepting NUMBERS:
/// were the DTO to carry the enumeration itself, the framework's enumeration converter (which
/// <c>AddAPI</c> registers) would turn every status into a string and refuse the numbers clients
/// send today.
/// </summary>
public sealed class TicketStatusContractTests
{
    private const TicketIdentifierType TicketId = 7;

    /// <summary>
    /// Web defaults plus the converter factories <c>AddAPI</c> registers
    /// (<c>MMCA.Common.API/DependencyInjection.cs</c>), which is what the API answers with.
    /// </summary>
    private static readonly JsonSerializerOptions AddApiOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new EnumerationJsonConverterFactory(), new StronglyTypedIdJsonConverterFactory() },
    };

    [Fact]
    public async Task Mapper_MapsEveryLifecycleToTheSameNamedEnum()
    {
        await using var provider = BuildModuleProvider();
        var mapper = provider.GetRequiredService<TicketDTOMapper>();

        foreach (var lifecycle in TicketLifecycle.All)
        {
            var ticket = await NewTicketAsync();
            ticket.ChangeStatus(lifecycle).IsSuccess.Should().BeTrue();

            var dto = mapper.MapToDTO(ticket);

            ((int)dto.Status).Should().Be(lifecycle.Value, "the wire number is the enumeration's value");
            Enum.GetName(dto.Status).Should().Be(lifecycle.Name, "the enum member and the enumeration member are the same status");
        }
    }

    [Fact]
    public async Task TicketDTO_SerializesStatusAsNumber_WithAddApiConverters()
    {
        await using var provider = BuildModuleProvider();
        var ticket = await NewTicketAsync();
        ticket.ChangeStatus(TicketLifecycle.Resolved).IsSuccess.Should().BeTrue();
        var dto = provider.GetRequiredService<TicketDTOMapper>().MapToDTO(ticket);

        string json = JsonSerializer.Serialize(dto, AddApiOptions);

        json.Should().Contain("\"status\":2", "the API's status stays the number it has always been");
        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("status").ValueKind.Should().Be(JsonValueKind.Number);
    }

    [Fact]
    public void ChangeTicketStatusRequest_DeserializesFromNumber()
    {
        const string json = """{"status":2}""";

        var request = JsonSerializer.Deserialize<ChangeTicketStatusRequest>(json, AddApiOptions);

        request.Should().NotBeNull();
        request!.Status.Should().Be(TicketStatus.Resolved);
    }

    [Fact]
    public async Task Handler_ChangesAggregateStatusFromContract()
    {
        var ticket = await NewTicketAsync();
        var handler = ResolveHandler(TicketRepositoryReturning(ticket));

        var result = await handler.HandleAsync(
            new ChangeTicketStatusCommand(TicketId, TicketStatus.Resolved) { RowVersion = [1, 2, 3, 4] },
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        ticket.Status.Should().Be(TicketLifecycle.Resolved, "the handler resolves the wire enum into the aggregate's enumeration");
        result.Value!.Status.Should().Be(TicketStatus.Resolved, "and the refreshed DTO maps it back");
    }

    private static ServiceProvider BuildModuleProvider()
    {
        var services = new ServiceCollection();
        services.ScanModuleApplicationServices<ClassReference>();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Builds the ticket through the module's own create mapper rather than the domain factory, so
    /// the shape flags reach a single object initializer instead of a positional argument list.
    /// </summary>
    /// <returns>A new, open ticket.</returns>
    private static async Task<Ticket> NewTicketAsync()
    {
        var created = await new TicketCreateRequestMapper().CreateEntityAsync(
            new TicketCreateRequest
            {
                Title = "Cannot log in",
                // template:begin description
                Description = "The login page returns a 500.",
                // template:end description
                // template:begin owner
                RequesterUserId = 42,
                // template:end owner
            },
            TestContext.Current.CancellationToken);

        return created.Value!;
    }

    private static Mock<IRepository<Ticket, TicketIdentifierType>> TicketRepositoryReturning(Ticket ticket)
    {
        var repository = new Mock<IRepository<Ticket, TicketIdentifierType>>();
        repository
            .Setup(r => r.GetByIdAsync(
                TicketId,
                It.IsAny<IEnumerable<string>>(),
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);
        return repository;
    }

    private static ICommandHandler<ChangeTicketStatusCommand, Result<TicketDTO>> ResolveHandler(
        Mock<IRepository<Ticket, TicketIdentifierType>> repository)
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.GetRepository<Ticket, TicketIdentifierType>()).Returns(repository.Object);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var services = new ServiceCollection();
        services.AddSingleton(unitOfWork.Object);
        services.AddModuleTicketsApplication(new ApplicationSettings());

        // Disposing the provider is safe here: the resolved handler already holds its dependencies.
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<ICommandHandler<ChangeTicketStatusCommand, Result<TicketDTO>>>();
    }
}
