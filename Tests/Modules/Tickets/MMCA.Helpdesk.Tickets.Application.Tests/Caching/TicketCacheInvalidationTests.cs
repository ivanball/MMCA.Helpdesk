using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MMCA.Common.Application.Interfaces;
using MMCA.Common.Application.UseCases;
using MMCA.Common.Application.UseCases.Decorators;
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
    public void EveryTicketCommand_InvalidatesThePrefixTheReadIsKeyedUnder()
    {
        // Nothing at compile time ties CachePrefix to CacheKey: the decorator matches them as
        // strings. This is the test that keeps a renamed key from silently going stale.
        string readKey = new GetTicketByIdQuery(TicketId).CacheKey;

        foreach (var command in AllTicketCommands())
        {
            command.CachePrefix.Should().Be(TicketCacheKeys.Prefix);
            readKey.Should().StartWith(
                command.CachePrefix,
                $"{command.GetType().Name} would otherwise leave the cached ticket read behind");
        }
    }

    private static IEnumerable<ICacheInvalidating> AllTicketCommands() =>
    [
        new TicketCreateRequest
        {
            Title = "Cannot log in",
            // template:begin description
            Description = "Returns a 500.",
            // template:end description
            // template:begin owner
            RequesterUserId = 42,
            // template:end owner
        },
        UpdateCommand("Cannot log in"),
        new DeleteTicketCommand(TicketId),
        // template:begin status
        new ChangeTicketStatusCommand(TicketId, TicketStatus.Closed) { RowVersion = ClientToken },
        // template:end status
        // template:begin child
        new AddCommentCommand(TicketId, "Looking into it.", AuthorUserId: 7),
        new EditCommentCommand(TicketId, CommentId: 1, "Still looking into it.") { RowVersion = ClientToken },
        new RemoveCommentCommand(TicketId, CommentId: 1),
        // template:end child
    ];

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
