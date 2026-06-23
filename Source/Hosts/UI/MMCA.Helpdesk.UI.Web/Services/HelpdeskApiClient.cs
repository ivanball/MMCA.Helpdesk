using System.Net.Http.Json;
using MMCA.Common.Shared.Abstractions;
using MMCA.Common.UI.Services;
using MMCA.Helpdesk.Tickets.Shared.Tickets;

namespace MMCA.Helpdesk.UI.Web.Services;

/// <summary>
/// Typed client for the Tickets REST API. Runs server-side (Blazor Server), so calls go directly to
/// the API host resolved by Aspire service discovery (no browser CORS). On failure it uses the
/// framework's <see cref="ServiceExceptionHelper"/> to read the RFC 9457 ProblemDetails body and throw
/// with the domain error message (e.g. "Comments cannot be added to a closed ticket.") before the
/// generic <c>EnsureSuccessStatusCode</c> fallback — the same pattern MMCA.Common.UI's
/// <c>EntityServiceBase</c> uses, so pages surface a meaningful message.
/// </summary>
public sealed class HelpdeskApiClient(HttpClient httpClient)
{
    public async Task<IReadOnlyList<TicketDTO>> GetTicketsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient
            .GetAsync(new Uri("Tickets", UriKind.Relative), cancellationToken)
            .ConfigureAwait(false);
        await ServiceExceptionHelper.ThrowIfDomainExceptionAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var result = await response.Content
            .ReadFromJsonAsync<CollectionResult<TicketDTO>>(cancellationToken)
            .ConfigureAwait(false);
        return result?.Items is { } items ? [.. items] : [];
    }

    public async Task<TicketDTO?> GetTicketAsync(int id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient
            .GetAsync(new Uri($"Tickets/{id}/details", UriKind.Relative), cancellationToken)
            .ConfigureAwait(false);
        await ServiceExceptionHelper.ThrowIfDomainExceptionAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<TicketDTO>(cancellationToken).ConfigureAwait(false);
    }

    public async Task<TicketDTO?> CreateTicketAsync(string title, string description, int requesterUserId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient
            .PostAsJsonAsync("/Tickets", new { Title = title, Description = description, RequesterUserId = requesterUserId }, cancellationToken)
            .ConfigureAwait(false);
        await ServiceExceptionHelper.ThrowIfDomainExceptionAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<TicketDTO>(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateTicketAsync(int id, string title, string description, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient
            .PutAsJsonAsync($"/Tickets/{id}", new { Title = title, Description = description }, cancellationToken)
            .ConfigureAwait(false);
        await ServiceExceptionHelper.ThrowIfDomainExceptionAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteTicketAsync(int id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient
            .DeleteAsync(new Uri($"Tickets/{id}", UriKind.Relative), cancellationToken)
            .ConfigureAwait(false);
        await ServiceExceptionHelper.ThrowIfDomainExceptionAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task ChangeStatusAsync(int id, TicketStatus status, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient
            .PutAsJsonAsync($"/Tickets/{id}/status", new { Status = status }, cancellationToken)
            .ConfigureAwait(false);
        await ServiceExceptionHelper.ThrowIfDomainExceptionAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task AddCommentAsync(int id, string body, int authorUserId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient
            .PostAsJsonAsync($"/Tickets/{id}/comments", new { Body = body, AuthorUserId = authorUserId }, cancellationToken)
            .ConfigureAwait(false);
        await ServiceExceptionHelper.ThrowIfDomainExceptionAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task EditCommentAsync(int id, int commentId, string body, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient
            .PutAsJsonAsync($"/Tickets/{id}/comments/{commentId}", new { Body = body }, cancellationToken)
            .ConfigureAwait(false);
        await ServiceExceptionHelper.ThrowIfDomainExceptionAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemoveCommentAsync(int id, int commentId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient
            .DeleteAsync(new Uri($"Tickets/{id}/comments/{commentId}", UriKind.Relative), cancellationToken)
            .ConfigureAwait(false);
        await ServiceExceptionHelper.ThrowIfDomainExceptionAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }
}
