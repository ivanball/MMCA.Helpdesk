using System.Globalization;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MMCA.Common.API.Controllers;
using MMCA.Common.Application.Interfaces;
using MMCA.Common.Application.UseCases;
using MMCA.Common.Shared.Abstractions;
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
using MMCA.Helpdesk.Tickets.Domain.Tickets;
using MMCA.Helpdesk.Tickets.Shared.Tickets;

namespace MMCA.Helpdesk.Tickets.API.Controllers;

/// <summary>
/// REST API for support tickets. Read endpoints (GetAll / paged) come from
/// <see cref="EntityControllerBase{TEntity, TDTO, TId}"/>; the write operations inject their
/// handlers directly. Failures map to RFC 9457 ProblemDetails via <c>HandleFailure</c>.
/// </summary>
[ApiController]
[Route("[controller]")]
[ApiVersion("1.0")]
// [AllowAnonymous] because this monolith seed ships without an Identity issuer. Once you add the
// Identity module (GETTING-STARTED.md Phase 8) and set Authentication:JwtBearer:Authority, switch
// this to [Authorize] (optionally with a policy) to require authenticated callers.
[AllowAnonymous]
public sealed class TicketsController(
    IEntityQueryService<Ticket, TicketDTO, TicketIdentifierType> queryService,
    IQueryHandler<GetTicketByIdQuery, Result<TicketDTO>> getByIdHandler,
    ICommandHandler<TicketCreateRequest, Result<TicketDTO>> createHandler,
    ICommandHandler<UpdateTicketCommand, Result<TicketDTO>> updateHandler,
    // template:begin status
    ICommandHandler<ChangeTicketStatusCommand, Result<TicketDTO>> changeStatusHandler,
    // template:end status
    ICommandHandler<DeleteTicketCommand, Result> deleteHandler,
    // template:begin child
    ICommandHandler<AddCommentCommand, Result<TicketCommentDTO>> addCommentHandler,
    ICommandHandler<EditCommentCommand, Result> editCommentHandler,
    ICommandHandler<RemoveCommentCommand, Result> removeCommentHandler,
    // template:end child
    ILogger<TicketsController> logger)
    : EntityControllerBase<Ticket, TicketDTO, TicketIdentifierType>(queryService, logger)
{
    /// <summary>Gets a single ticket by id, with its children.</summary>
    [HttpGet("{id}/details")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketDTO>> GetDetailsAsync(
        TicketIdentifierType id,
        CancellationToken cancellationToken)
    {
        var result = await getByIdHandler.HandleAsync(new GetTicketByIdQuery(id), cancellationToken).ConfigureAwait(false);
        return result.IsFailure ? HandleFailure(result.Errors) : Ok(result.Value);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TicketDTO>> CreateAsync(
        TicketCreateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await createHandler.HandleAsync(request, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return HandleFailure(result.Errors);
        }

        // Build a relative Location URI directly rather than CreatedAtAction(nameof(GetByIdAsync), ...):
        // route-link generation against the versioned base GetById route throws "No route matches the
        // supplied values".
        var dto = result.Value!;
        var locationUri = new Uri(string.Create(CultureInfo.InvariantCulture, $"Tickets/{dto.Id}"), UriKind.Relative);
        return Created(locationUri, dto);
    }

    /// <summary>Updates a ticket's editable details.</summary>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketDTO>> UpdateAsync(
        TicketIdentifierType id,
        TicketUpdateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await updateHandler.HandleAsync(
            new UpdateTicketCommand(id, request.Title, request.Description) { RowVersion = request.RowVersion },
            cancellationToken).ConfigureAwait(false);
        return result.IsFailure ? HandleFailure(result.Errors) : Ok(result.Value);
    }

    // template:begin status
    /// <summary>Changes a ticket's status.</summary>
    [HttpPut("{id}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketDTO>> ChangeStatusAsync(
        TicketIdentifierType id,
        ChangeTicketStatusRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await changeStatusHandler.HandleAsync(
            new ChangeTicketStatusCommand(id, request.Status),
            cancellationToken).ConfigureAwait(false);
        return result.IsFailure ? HandleFailure(result.Errors) : Ok(result.Value);
    }

    // template:end status
    /// <summary>Soft-deletes a ticket, cascading to its children.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(
        TicketIdentifierType id,
        CancellationToken cancellationToken)
    {
        var result = await deleteHandler.HandleAsync(new DeleteTicketCommand(id), cancellationToken).ConfigureAwait(false);
        return result.IsFailure ? HandleFailure(result.Errors) : NoContent();
    }

    // template:begin child
    [HttpPost("{id}/comments")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketCommentDTO>> AddCommentAsync(
        TicketIdentifierType id,
        AddCommentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await addCommentHandler.HandleAsync(
            new AddCommentCommand(id, request.Body, request.AuthorUserId),
            cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return HandleFailure(result.Errors);
        }

        return Ok(result.Value);
    }

    /// <summary>Edits the body of an existing comment.</summary>
    [HttpPut("{id}/comments/{commentId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EditCommentAsync(
        TicketIdentifierType id,
        TicketCommentIdentifierType commentId,
        EditCommentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await editCommentHandler.HandleAsync(
            new EditCommentCommand(id, commentId, request.Body),
            cancellationToken).ConfigureAwait(false);
        return result.IsFailure ? HandleFailure(result.Errors) : NoContent();
    }

    /// <summary>Removes (soft-deletes) a comment from a ticket.</summary>
    [HttpDelete("{id}/comments/{commentId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveCommentAsync(
        TicketIdentifierType id,
        TicketCommentIdentifierType commentId,
        CancellationToken cancellationToken)
    {
        var result = await removeCommentHandler.HandleAsync(
            new RemoveCommentCommand(id, commentId),
            cancellationToken).ConfigureAwait(false);
        return result.IsFailure ? HandleFailure(result.Errors) : NoContent();
    }
    // template:end child
}
