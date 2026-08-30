using System.Globalization;
using MMCA.Common.Shared.Abstractions;
using MMCA.Common.Shared.Http;
using MMCA.Common.UI.Services;
using MMCA.Helpdesk.Tickets.Shared.Tickets;

namespace MMCA.Helpdesk.UI.Web.Services;

/// <summary>
/// Typed client for the Tickets REST API. Runs server-side (Blazor Server), so calls go directly to
/// the API host resolved by Aspire service discovery (no browser CORS).
/// <para>
/// Every method returns <see cref="Result"/> or <see cref="Result{T}"/> and throws nothing for a
/// server answer: the same contract MMCA.Common.UI's <c>EntityServiceBase</c> honors, built from the
/// same two framework pieces. <see cref="ProblemDetailsResultReader"/> turns the response into a
/// result, reading the RFC 9457 ProblemDetails body on a failure so the caller gets whatever
/// invariant the aggregate refused on rather than a bare status code.
/// <see cref="HttpResultExecutor"/> wraps the call so a fault with no response at all (connection
/// refused, DNS, a dropped socket, an HttpClient timeout) becomes a failure result too. The one
/// exception that still propagates is the caller's own <see cref="OperationCanceledException"/>.
/// </para>
/// <para>
/// Pages branch on the result with the ergonomics in <c>MMCA.Common.UI.Common</c>
/// (<c>TryGetValue</c>, <c>LocalizedErrorMessage</c>) instead of catching.
/// </para>
/// </summary>
public sealed class HelpdeskApiClient(HttpClient httpClient)
{
    public Task<Result<IReadOnlyList<TicketDTO>>> GetTicketsAsync(CancellationToken cancellationToken = default) =>
        HttpResultExecutor.ExecuteAsync(
            async () =>
            {
                using var response = await httpClient
                    .GetAsync(new Uri("Tickets", UriKind.Relative), cancellationToken)
                    .ConfigureAwait(false);

                var result = await ProblemDetailsResultReader
                    .ReadAsync<CollectionResult<TicketDTO>>(response, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                return result.Map<IReadOnlyList<TicketDTO>>(static collection => collection.Items is { } items ? [.. items] : []);
            },
            cancellationToken);

    /// <summary>
    /// Reads one ticket. A missing ticket comes back as a <c>NotFound</c> FAILURE, not a null value:
    /// the API's 404 ProblemDetails carries the reason, and a result can hold it where a null cannot.
    /// </summary>
    public Task<Result<TicketDTO>> GetTicketAsync(int id, CancellationToken cancellationToken = default) =>
        HttpResultExecutor.ExecuteAsync(
            async () =>
            {
                using var response = await httpClient
                    .GetAsync(new Uri(string.Create(CultureInfo.InvariantCulture, $"Tickets/{id}/details"), UriKind.Relative), cancellationToken)
                    .ConfigureAwait(false);

                return await ProblemDetailsResultReader
                    .ReadAsync<TicketDTO>(response, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            },
            cancellationToken);

    public Task<Result<TicketDTO>> CreateTicketAsync(string title, string description, int requesterUserId, CancellationToken cancellationToken = default) =>
        HttpResultExecutor.ExecuteAsync(
            async () =>
            {
                using var response = await httpClient
                    .PostAsJsonAsync("/Tickets", new { Title = title, Description = description, RequesterUserId = requesterUserId }, cancellationToken)
                    .ConfigureAwait(false);

                return await ProblemDetailsResultReader
                    .ReadAsync<TicketDTO>(response, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            },
            cancellationToken);

    /// <summary>
    /// Updates a ticket's editable details. The write is conditional (ADR-035): the concurrency
    /// token the caller last read travels as the <c>If-Match</c> header, which is the only route it
    /// takes. The endpoint refuses a write that states no precondition with 428, and answers a stale
    /// one with 412, both of which come back as failure results here.
    /// </summary>
    public Task<Result> UpdateTicketAsync(int id, string title, string description, byte[] rowVersion, CancellationToken cancellationToken = default) =>
        HttpResultExecutor.ExecuteAsync(
            async () =>
            {
                using var request = ConditionalPut(string.Create(CultureInfo.InvariantCulture, $"/Tickets/{id}"), new { Title = title, Description = description }, rowVersion);
                using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

                return await ProblemDetailsResultReader.ReadAsync(response, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);

    public Task<Result> DeleteTicketAsync(int id, CancellationToken cancellationToken = default) =>
        HttpResultExecutor.ExecuteAsync(
            async () =>
            {
                using var response = await httpClient
                    .DeleteAsync(new Uri(string.Create(CultureInfo.InvariantCulture, $"Tickets/{id}"), UriKind.Relative), cancellationToken)
                    .ConfigureAwait(false);

                return await ProblemDetailsResultReader.ReadAsync(response, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);

    // template:begin status
    /// <summary>Changes a ticket's status. Conditional on the ticket's token, like the details update.</summary>
    public Task<Result> ChangeStatusAsync(int id, TicketStatus status, byte[] rowVersion, CancellationToken cancellationToken = default) =>
        HttpResultExecutor.ExecuteAsync(
            async () =>
            {
                using var request = ConditionalPut(string.Create(CultureInfo.InvariantCulture, $"/Tickets/{id}/status"), new { Status = status }, rowVersion);
                using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

                return await ProblemDetailsResultReader.ReadAsync(response, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);

    // template:end status
    // template:begin child
    public Task<Result> AddCommentAsync(int id, string body, int authorUserId, CancellationToken cancellationToken = default) =>
        HttpResultExecutor.ExecuteAsync(
            async () =>
            {
                using var response = await httpClient
                    .PostAsJsonAsync(string.Create(CultureInfo.InvariantCulture, $"/Tickets/{id}/comments"), new { Body = body, AuthorUserId = authorUserId }, cancellationToken)
                    .ConfigureAwait(false);

                return await ProblemDetailsResultReader.ReadAsync(response, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);

    /// <summary>
    /// Edits a comment's body. The precondition is the OWNING ticket's token: concurrency is an
    /// aggregate-level invariant, and the ticket read's <c>ETag</c> is what carries it.
    /// </summary>
    public Task<Result> EditCommentAsync(int id, int commentId, string body, byte[] rowVersion, CancellationToken cancellationToken = default) =>
        HttpResultExecutor.ExecuteAsync(
            async () =>
            {
                using var request = ConditionalPut(string.Create(CultureInfo.InvariantCulture, $"/Tickets/{id}/comments/{commentId}"), new { Body = body }, rowVersion);
                using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

                return await ProblemDetailsResultReader.ReadAsync(response, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);

    public Task<Result> RemoveCommentAsync(int id, int commentId, CancellationToken cancellationToken = default) =>
        HttpResultExecutor.ExecuteAsync(
            async () =>
            {
                using var response = await httpClient
                    .DeleteAsync(new Uri(string.Create(CultureInfo.InvariantCulture, $"Tickets/{id}/comments/{commentId}"), UriKind.Relative), cancellationToken)
                    .ConfigureAwait(false);

                return await ProblemDetailsResultReader.ReadAsync(response, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);
    // template:end child

    /// <summary>
    /// Builds a conditional PUT: the caller's last-read concurrency token rendered as the weak
    /// entity tag the <c>If-Match</c> header carries (<see cref="ConcurrencyETag"/>). The header is
    /// set per request rather than on the client, because this typed client is shared across calls.
    /// </summary>
    /// <typeparam name="TBody">The request body's anonymous or declared type.</typeparam>
    /// <param name="relativeUrl">The endpoint, relative to the client's base address.</param>
    /// <param name="body">The payload to serialize.</param>
    /// <param name="rowVersion">The token the write is conditional on.</param>
    /// <returns>The request message, ready to send.</returns>
    private static HttpRequestMessage ConditionalPut<TBody>(string relativeUrl, TBody body, byte[] rowVersion)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, new Uri(relativeUrl, UriKind.Relative))
        {
            Content = JsonContent.Create(body),
        };

        request.Headers.Add(ConcurrencyETag.IfMatchHeaderName, ConcurrencyETag.Format(rowVersion));
        return request;
    }
}
