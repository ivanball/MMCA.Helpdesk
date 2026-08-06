using AwesomeAssertions;
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
using MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.Update;
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

    [Fact]
    public async Task Read_IsServedFromTheCache_OnTheSecondCall()
    {
        var cache = new DictionaryCache();
        var handler = new CountingQueryHandler(TicketResult());
        var read = new CachingQueryDecorator<GetTicketByIdQuery, Result<TicketDTO>>(handler, cache);

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
        var read = new CachingQueryDecorator<GetTicketByIdQuery, Result<TicketDTO>>(queryHandler, cache);
        var write = new CachingCommandDecorator<UpdateTicketCommand, Result<TicketDTO>>(
            new StubCommandHandler(TicketResult()), cache);

        await read.HandleAsync(new GetTicketByIdQuery(TicketId));
        await read.HandleAsync(new GetTicketByIdQuery(TicketId));
        queryHandler.Invocations.Should().Be(1, "the cache is warm before the write");

        var written = await write.HandleAsync(new UpdateTicketCommand(TicketId, "Still cannot log in", "Returns a 500."));

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
        var read = new CachingQueryDecorator<GetTicketByIdQuery, Result<TicketDTO>>(queryHandler, cache);
        var write = new CachingCommandDecorator<UpdateTicketCommand, Result<TicketDTO>>(
            new StubCommandHandler(Result.Failure<TicketDTO>(Error.NotFound)), cache);

        await read.HandleAsync(new GetTicketByIdQuery(TicketId));

        var written = await write.HandleAsync(new UpdateTicketCommand(TicketId, "Still cannot log in", "Returns a 500."));

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
        new UpdateTicketCommand(TicketId, "Cannot log in", "Returns a 500."),
        new DeleteTicketCommand(TicketId),
        // template:begin status
        new ChangeTicketStatusCommand(TicketId, TicketStatus.Closed),
        // template:end status
        // template:begin child
        new AddCommentCommand(TicketId, "Looking into it.", AuthorUserId: 7),
        new EditCommentCommand(TicketId, CommentId: 1, "Still looking into it."),
        new RemoveCommentCommand(TicketId, CommentId: 1),
        // template:end child
    ];

    private static Result<TicketDTO> TicketResult() =>
        Result.Success(new TicketDTO
        {
            Id = TicketId,
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
        : ICommandHandler<UpdateTicketCommand, Result<TicketDTO>>
    {
        public Task<Result<TicketDTO>> HandleAsync(
            UpdateTicketCommand command,
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
