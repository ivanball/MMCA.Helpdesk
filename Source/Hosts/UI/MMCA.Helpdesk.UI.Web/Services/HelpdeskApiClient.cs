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

    public Task<Result> UpdateTicketAsync(int id, string title, string description, CancellationToken cancellationToken = default) =>
        HttpResultExecutor.ExecuteAsync(
            async () =>
            {
                using var response = await httpClient
                    .PutAsJsonAsync(string.Create(CultureInfo.InvariantCulture, $"/Tickets/{id}"), new { Title = title, Description = description }, cancellationToken)
                    .ConfigureAwait(false);

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
    public Task<Result> ChangeStatusAsync(int id, TicketStatus status, CancellationToken cancellationToken = default) =>
        HttpResultExecutor.ExecuteAsync(
            async () =>
            {
                using var response = await httpClient
                    .PutAsJsonAsync(string.Create(CultureInfo.InvariantCulture, $"/Tickets/{id}/status"), new { Status = status }, cancellationToken)
                    .ConfigureAwait(false);

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

    public Task<Result> EditCommentAsync(int id, int commentId, string body, CancellationToken cancellationToken = default) =>
        HttpResultExecutor.ExecuteAsync(
            async () =>
            {
                using var response = await httpClient
                    .PutAsJsonAsync(string.Create(CultureInfo.InvariantCulture, $"/Tickets/{id}/comments/{commentId}"), new { Body = body }, cancellationToken)
                    .ConfigureAwait(false);

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
}
