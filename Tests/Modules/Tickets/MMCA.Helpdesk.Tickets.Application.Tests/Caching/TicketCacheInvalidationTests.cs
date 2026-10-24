using System.Reflection;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MMCA.Common.Application.Interfaces;
using MMCA.Common.Application.Settings;
using MMCA.Common.Application.UseCases.Contracts;
using MMCA.Common.Application.UseCases.Crud;
using MMCA.Common.Application.UseCases.Decorators;
using MMCA.Common.Application.UseCases.Markers;
using MMCA.Common.Shared.Abstractions;
using MMCA.Helpdesk.Tickets.Application.Tickets;
// template:begin child
using MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.AddComment;
// template:end child
// template:begin status
using MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.ChangeStatus;
// template:end status
using MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.Create;
using MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.Delete;
// template:begin child
using MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.EditComment;
// template:end child
using MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.GetById;
// template:begin child
using MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.RemoveComment;
// template:end child
using MMCA.Helpdesk.Tickets.Domain.Tickets;
using MMCA.Helpdesk.Tickets.Shared.Tickets;

namespace MMCA.Helpdesk.Tickets.Application.Tests.Caching;

/// <summary>
/// Worked example of the framework's caching pair: a query that implements
/// <see cref="IQueryCacheable"/> is served from cache until a command that implements
/// <see cref="ICacheInvalidating"/> evicts its prefix, and only when that command succeeds.
/// <para>
/// The two real framework decorators are wired around stub handlers, so the tests exercise the
/// extension point rather than the database, and they hold against any cache substrate: the double
/// below is a plain dictionary, not <c>IMemoryCache</c> and not Redis.
/// </para>
/// </summary>
public class TicketCacheInvalidationTests
{
    private const TicketIdentifierType TicketId = 7;

    /// <summary>
    /// The concurrency token every conditional write states. Caching does not read it, but the
    /// commands require it (ADR-035), so one value serves every command built here.
    /// </summary>
    private static readonly byte[] ClientToken = [1, 2, 3, 4];

    /// <summary>
    /// The commands the hand-kept list held before discovery replaced it. A floor, not the source of
    /// what is checked: <see cref="Discovery_FindsAtLeastEveryCommandTheOldHandListNamed"/> asserts
    /// discovery finds each of them, so a change to the discovery cannot silently shrink coverage.
    /// </summary>
    private static readonly Type[] HandListedBeforeDiscovery =
    [
        typeof(TicketCreateRequest),
        typeof(UpdateEntityCommand<Ticket, TicketUpdateRequest, TicketIdentifierType>),
        typeof(DeleteTicketCommand),
        // template:begin status
        typeof(ChangeTicketStatusCommand),
        // template:end status
        // template:begin child
        typeof(AddCommentCommand),
        typeof(EditCommentCommand),
        typeof(RemoveCommentCommand),
        // template:end child
    ];

    /// <summary>
    /// The update command is the framework's generic one, and its default <c>CachePrefix</c> is the
    /// aggregate-prefix convention (<c>{entity full name}:</c>), which is what
    /// <see cref="TicketCacheKeys.Prefix"/> is derived from: the two agree with nothing to configure.
    /// Built once here so the shape flags reach a single object initializer.
    /// </summary>
    /// <param name="title">The new title.</param>
    /// <returns>An update command for <see cref="TicketId"/>.</returns>
    private static UpdateEntityCommand<Ticket, TicketUpdateRequest, TicketIdentifierType> UpdateCommand(string title) =>
        new(
            TicketId,
            new TicketUpdateRequest
            {
                Title = title,
                // template:begin description
                Description = "Returns a 500.",
                // template:end description
            },
            ClientToken);

    /// <summary>
    /// The framework's read decorator around a stub handler. Its logger is a constructor
    /// requirement, and a test has nothing to say through it, so the null one serves every case.
    /// </summary>
    /// <param name="handler">The handler the decorator wraps.</param>
    /// <param name="cache">The cache substrate under test.</param>
    /// <returns>The decorator to exercise.</returns>
    private static CachingQueryDecorator<GetTicketByIdQuery, Result<TicketDTO>> ReadDecorator(
        IQueryHandler<GetTicketByIdQuery, Result<TicketDTO>> handler,
        ICacheService cache) =>
        new(handler, cache, NullLogger<CachingQueryDecorator<GetTicketByIdQuery, Result<TicketDTO>>>.Instance);

    /// <summary>The framework's write decorator around a stub handler, logger included.</summary>
    /// <param name="handler">The handler the decorator wraps.</param>
    /// <param name="cache">The cache substrate under test.</param>
    /// <returns>The decorator to exercise.</returns>
    private static CachingCommandDecorator<UpdateEntityCommand<Ticket, TicketUpdateRequest, TicketIdentifierType>, Result<TicketDTO>> WriteDecorator(
        ICommandHandler<UpdateEntityCommand<Ticket, TicketUpdateRequest, TicketIdentifierType>, Result<TicketDTO>> handler,
        ICacheService cache) =>
        new(handler, cache, NullLogger<CachingCommandDecorator<UpdateEntityCommand<Ticket, TicketUpdateRequest, TicketIdentifierType>, Result<TicketDTO>>>.Instance);

    [Fact]
    public async Task Read_IsServedFromTheCache_OnTheSecondCall()
    {
        var cache = new DictionaryCache();
        var handler = new CountingQueryHandler(TicketResult());
        var read = ReadDecorator(handler, cache);

        var first = await read.HandleAsync(new GetTicketByIdQuery(TicketId));
        var second = await read.HandleAsync(new GetTicketByIdQuery(TicketId));

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        handler.Invocations.Should().Be(1, "the second read is a cache hit and never reaches the handler");
        cache.Keys.Should().ContainSingle().Which.Should().StartWith(
            TicketCacheKeys.Prefix,
            "a read stored outside the prefix the commands invalidate could never be evicted");
    }

    [Fact]
    public async Task SuccessfulCommand_EvictsThePrefix_SoTheNextReadMisses()
    {
        var cache = new DictionaryCache();
        var queryHandler = new CountingQueryHandler(TicketResult());
        var read = ReadDecorator(queryHandler, cache);
        var write = WriteDecorator(new StubCommandHandler(TicketResult()), cache);

        await read.HandleAsync(new GetTicketByIdQuery(TicketId));
        await read.HandleAsync(new GetTicketByIdQuery(TicketId));
        queryHandler.Invocations.Should().Be(1, "the cache is warm before the write");

        var written = await write.HandleAsync(UpdateCommand("Still cannot log in"));

        written.IsSuccess.Should().BeTrue();
        cache.Keys.Should().BeEmpty("a successful command evicts everything under its CachePrefix");

        var afterWrite = await read.HandleAsync(new GetTicketByIdQuery(TicketId));

        afterWrite.IsSuccess.Should().BeTrue();
        queryHandler.Invocations.Should().Be(2, "the read after the write is a miss and re-runs the handler");
    }

    [Fact]
    public async Task FailedCommand_LeavesTheCachedReadInPlace()
    {
        var cache = new DictionaryCache();
        var queryHandler = new CountingQueryHandler(TicketResult());
        var read = ReadDecorator(queryHandler, cache);
        var write = WriteDecorator(new StubCommandHandler(Result.Failure<TicketDTO>(Error.NotFound)), cache);

        await read.HandleAsync(new GetTicketByIdQuery(TicketId));

        var written = await write.HandleAsync(UpdateCommand("Still cannot log in"));

        written.IsFailure.Should().BeTrue();
        cache.RemoveByPrefixCalls.Should().Be(0, "a command that persisted nothing must not evict valid entries");

        await read.HandleAsync(new GetTicketByIdQuery(TicketId));

        queryHandler.Invocations.Should().Be(1, "the entry survived the failed write, so this read is still a hit");
    }

    [Fact]
    public void Discovery_FindsAtLeastEveryCommandTheOldHandListNamed() =>
        DiscoveredCommandTypes().Should().Contain(
            HandListedBeforeDiscovery,
            "discovery replaced a hand-kept list, and must never check fewer commands than that list did");

    [Fact]
    public void EveryTicketCommand_InvalidatesThePrefixTheReadIsKeyedUnder()
    {
        // Nothing at compile time ties CachePrefix to CacheKey: the decorator matches them as
        // strings. This is the test that keeps a renamed key from silently going stale, and the
        // commands are DISCOVERED, so a new ICacheInvalidating command is checked the day it lands.
        string readKey = new GetTicketByIdQuery(TicketId).CacheKey;
        var builders = CommandBuilders();
        var discovered = DiscoveredCommandTypes();

        string[] unbuilt = [.. discovered.Where(t => !builders.ContainsKey(t)).Select(t => t.ToString())];
        unbuilt.Should().BeEmpty(
            $"every discovered ICacheInvalidating command needs a builder (an entry in {nameof(HandWrittenBuilders)} or an {nameof(ICacheInvalidatingCommandBuilder)} in this project) so its CachePrefix can be checked; add one for: {string.Join(", ", unbuilt)}");

        foreach (var commandType in discovered)
        {
            var command = builders[commandType]();

            command.Should().BeOfType(commandType, $"the builder registered for {commandType.Name} must build that type");
            command.CachePrefix.Should().Be(
                TicketCacheKeys.Prefix,
                $"{commandType.Name} must invalidate the prefix every ticket read is keyed under");
            readKey.Should().StartWith(
                command.CachePrefix,
                $"{commandType.Name} would otherwise leave the cached ticket read behind");
        }
    }

    /// <summary>
    /// Every concrete <see cref="ICacheInvalidating"/> command the module can be sent, found two
    /// ways. The module's own commands are the concrete implementers declared in its assemblies. The
    /// generic write side's commands are declared in MMCA.Common and closed over the module's types
    /// by <c>AddEntityCrud</c>, so no module assembly declares them: they are found as the command
    /// type of every command handler the module registers.
    /// </summary>
    /// <returns>The distinct command types, each to be checked.</returns>
    private static IReadOnlyCollection<Type> DiscoveredCommandTypes()
    {
        Assembly[] moduleAssemblies = [typeof(ClassReference).Assembly, typeof(Ticket).Assembly, typeof(TicketDTO).Assembly];
        var declared = moduleAssemblies.Distinct().SelectMany(a => a.GetTypes()).Where(IsConcreteCacheInvalidating);

        var services = new ServiceCollection();
        services.AddModuleTicketsApplication(new ApplicationSettings());
        var handled = services
            .Select(d => d.ServiceType)
            .Where(t => t.IsConstructedGenericType && t.GetGenericTypeDefinition() == typeof(ICommandHandler<,>))
            .Select(t => t.GetGenericArguments()[0])
            .Where(IsConcreteCacheInvalidating);

        return [.. declared.Union(handled)];
    }

    private static bool IsConcreteCacheInvalidating(Type type) =>
        type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false }
        && typeof(ICacheInvalidating).IsAssignableFrom(type);

    /// <summary>
    /// One builder per discovered command type: the table in <see cref="HandWrittenBuilders"/>, plus
    /// every <see cref="ICacheInvalidatingCommandBuilder"/> in this assembly (the mmca-command slice
    /// template ships one with each command it scaffolds). A command added to the module with neither
    /// fails <see cref="EveryTicketCommand_InvalidatesThePrefixTheReadIsKeyedUnder"/> by name, which is
    /// the prompt to add one, and a command built in both places fails here so there is one owner.
    /// </summary>
    /// <returns>The builders, keyed by the exact command type each one builds.</returns>
    private static Dictionary<Type, Func<ICacheInvalidating>> CommandBuilders()
    {
        var builders = HandWrittenBuilders();
        var shipped = typeof(TicketCacheInvalidationTests).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(ICacheInvalidatingCommandBuilder).IsAssignableFrom(t))
            .Select(t => (ICacheInvalidatingCommandBuilder)Activator.CreateInstance(t)!);

        foreach (var builder in shipped)
        {
            builders.ContainsKey(builder.CommandType).Should().BeFalse(
                $"{builder.GetType().Name} builds {builder.CommandType.Name}, which already has an entry in {nameof(HandWrittenBuilders)}; keep one");
            builders[builder.CommandType] = builder.Build;
        }

        return builders;
    }

    /// <summary>The builders for the commands written by hand in this module.</summary>
    /// <returns>The builders, keyed by the exact command type each one builds.</returns>
    private static Dictionary<Type, Func<ICacheInvalidating>> HandWrittenBuilders() => new()
    {
        [typeof(TicketCreateRequest)] = () => new TicketCreateRequest
        {
            Title = "Cannot log in",
            // template:begin description
            Description = "Returns a 500.",
            // template:end description
            // template:begin owner
            RequesterUserId = 42,
            // template:end owner
        },
        [typeof(UpdateEntityCommand<Ticket, TicketUpdateRequest, TicketIdentifierType>)] = () => UpdateCommand("Cannot log in"),
        [typeof(DeleteEntityCommand<Ticket, TicketIdentifierType>)] = () => new DeleteEntityCommand<Ticket, TicketIdentifierType>(TicketId),
        // template:begin status
        [typeof(ChangeTicketStatusCommand)] = () => new ChangeTicketStatusCommand(TicketId, TicketStatus.Closed) { RowVersion = ClientToken },
        // template:end status
        // template:begin child
        [typeof(AddCommentCommand)] = () => new AddCommentCommand(TicketId, "Looking into it.", AuthorUserId: 7),
        [typeof(EditCommentCommand)] = () => new EditCommentCommand(TicketId, CommentId: 1, "Still looking into it.") { RowVersion = ClientToken },
        [typeof(RemoveCommentCommand)] = () => new RemoveCommentCommand(TicketId, CommentId: 1),
        // template:end child
    };

    private static Result<TicketDTO> TicketResult() =>
        Result.Success(new TicketDTO
        {
            Id = TicketId,
            RowVersion = ClientToken,
            Title = "Cannot log in",
            // template:begin description
            Description = "The login page returns a 500.",
            // template:end description
            // template:begin status
            Status = TicketStatus.Open,
            // template:end status
            // template:begin owner
            RequesterUserId = 42,
            // template:end owner
        });

    /// <summary>Counts how often the real handler is reached, which is what a cache hit prevents.</summary>
    private sealed class CountingQueryHandler(Result<TicketDTO> result)
        : IQueryHandler<GetTicketByIdQuery, Result<TicketDTO>>
    {
        public int Invocations { get; private set; }

        public Task<Result<TicketDTO>> HandleAsync(
            GetTicketByIdQuery query,
            CancellationToken cancellationToken = default)
        {
            Invocations++;
            return Task.FromResult(result);
        }
    }

    /// <summary>Stands in for the persistence-backed command handler, returning a fixed outcome.</summary>
    private sealed class StubCommandHandler(Result<TicketDTO> result)
        : ICommandHandler<UpdateEntityCommand<Ticket, TicketUpdateRequest, TicketIdentifierType>, Result<TicketDTO>>
    {
        public Task<Result<TicketDTO>> HandleAsync(
            UpdateEntityCommand<Ticket, TicketUpdateRequest, TicketIdentifierType> command,
            CancellationToken cancellationToken = default) => Task.FromResult(result);
    }

    /// <summary>
    /// Substrate-independent cache double. The behavior under test is prefix eviction, which every
    /// <see cref="ICacheService"/> implementation owes regardless of where it stores entries.
    /// </summary>
    private sealed class DictionaryCache : ICacheService
    {
        private readonly Dictionary<string, object> _entries = new(StringComparer.Ordinal);

        public int RemoveByPrefixCalls { get; private set; }

        public IReadOnlyCollection<string> Keys => _entries.Keys.ToArray();

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_entries.TryGetValue(key, out var value) ? (T)value : default);

        public Task SetAsync<T>(
            string key,
            T value,
            TimeSpan? expiration = null,
            CancellationToken cancellationToken = default)
        {
            if (value is not null)
            {
                _entries[key] = value;
            }

            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _entries.Remove(key);
            return Task.CompletedTask;
        }

        public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
        {
            RemoveByPrefixCalls++;

            foreach (string key in _entries.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToArray())
            {
                _entries.Remove(key);
            }

            return Task.CompletedTask;
        }
    }
}
