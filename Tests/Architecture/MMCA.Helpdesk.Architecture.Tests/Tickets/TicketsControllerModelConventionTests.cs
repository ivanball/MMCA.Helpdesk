using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Filters;
using MMCA.Common.API.Concurrency;
using MMCA.Common.API.Pagination;
using MMCA.Helpdesk.Tickets.API.Controllers;

namespace MMCA.Helpdesk.Architecture.Tests.Tickets;

/// <summary>
/// Pins what MVC's application-model conventions make of <see cref="TicketsController"/>, without a
/// host: each action is built into an <see cref="ActionModel"/> from the same attributes MVC reads
/// (inherited ones included), and the attribute conventions the framework ships are applied to it the
/// way MVC applies them.
/// <list type="bullet">
/// <item>Item 9: every <c>[SupportsIfMatch]</c> action documents 412 and 428 as Problem Details, which
/// is what the attribute contributes once the hand-written <c>ProducesResponseType</c> lines for those
/// two codes are deleted. The shared <c>ControllerConventionTests</c> fact
/// <c>ConditionalActions_ShouldNotRedeclare_PreconditionResponses</c> forbids the hand-written lines;
/// this class proves deleting them loses no documentation.</item>
/// <item>Items 10 and 47: the controller opts in to the framework's cursor-paged read, so the
/// <c>GET Tickets/cursor</c> action survives the opt-in convention that removes it from every
/// controller that did not ask for it.</item>
/// </list>
/// </summary>
public sealed class TicketsControllerModelConventionTests
{
    private static readonly int[] PreconditionStatusCodes =
        [StatusCodes.Status412PreconditionFailed, StatusCodes.Status428PreconditionRequired];

    [Fact]
    public void ConditionalActions_DocumentPreconditionResponses_AsProblemDetails()
    {
        var conditionalActions = typeof(TicketsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.IsDefined(typeof(SupportsIfMatchAttribute), inherit: true))
            .ToList();
        conditionalActions.Should().NotBeEmpty("the controller's updates are conditional writes (ADR-035)");

        foreach (var method in conditionalActions)
        {
            var action = BuildActionModel(method);
            ApplyActionConventions(action);

            var preconditionResponses = action.Filters
                .OfType<ProducesResponseTypeAttribute>()
                .Where(response => PreconditionStatusCodes.Contains(response.StatusCode))
                .ToList();

            preconditionResponses.Select(response => response.StatusCode).Should().BeEquivalentTo(
                PreconditionStatusCodes,
                "{0} must document 412 and 428 exactly once each",
                method.Name);
            preconditionResponses.Should().OnlyContain(
                response => response.Type == typeof(ProblemDetails),
                "{0} answers both refusals as Problem Details, and the attribute documents them that way",
                method.Name);
        }
    }

    [Fact]
    public void Controller_OptsInToCursorPaging() =>
        typeof(TicketsController).IsDefined(typeof(SupportsCursorPagingAttribute), inherit: true)
            .Should().BeTrue("the ticket list pages by cursor with load-more, which needs GET Tickets/cursor");

    [Fact]
    public void CursorAction_SurvivesItsOptInConvention()
    {
        var method = typeof(TicketsController).GetMethod(nameof(TicketsController.GetCursorPageAsync))!;
        var action = BuildActionModel(method);

        ApplyActionConventions(action);

        action.Controller.Actions.Should().Contain(
            action,
            "a controller carrying [SupportsCursorPaging] keeps the inherited cursor action; any other controller loses it");
    }

    /// <summary>
    /// Builds the action the way MVC's default application-model provider does: the method's
    /// attributes read with inheritance, the filter-metadata ones copied into the filters, and the
    /// action attached to a controller model carrying the controller's own (inherited) attributes.
    /// </summary>
    /// <param name="method">The action method.</param>
    /// <returns>The action model, already added to its controller's action list.</returns>
    private static ActionModel BuildActionModel(MethodInfo method)
    {
        var controllerType = typeof(TicketsController);
        var controllerAttributes = controllerType.GetCustomAttributes(inherit: true);
        var controller = new ControllerModel(controllerType.GetTypeInfo(), controllerAttributes);
        foreach (var filter in controllerAttributes.OfType<IFilterMetadata>())
        {
            controller.Filters.Add(filter);
        }

        var attributes = method.GetCustomAttributes(inherit: true);
        var action = new ActionModel(method, attributes) { Controller = controller };
        foreach (var filter in attributes.OfType<IFilterMetadata>())
        {
            action.Filters.Add(filter);
        }

        controller.Actions.Add(action);
        return action;
    }

    /// <summary>Applies every action-level attribute convention, as MVC does after the global ones.</summary>
    /// <param name="action">The action model.</param>
    private static void ApplyActionConventions(ActionModel action)
    {
        foreach (var convention in action.Attributes.OfType<IActionModelConvention>().ToList())
        {
            convention.Apply(action);
        }
    }
}
