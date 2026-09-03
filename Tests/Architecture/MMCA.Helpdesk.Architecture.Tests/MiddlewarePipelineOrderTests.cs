using MMCA.Common.Testing.Conformance;

namespace MMCA.Helpdesk.Architecture.Tests;

/// <summary>
/// Opt-in v1.158.0 fitness function for the ADR-079 shared HTTP edge pipeline, the edge counterpart
/// of <see cref="DecoratorPipelineOrderTests"/>: the Helpdesk Web host calls the zero-argument
/// <c>UseCommonMiddlewarePipeline()</c>, so the framework's default step order is the contract and
/// the base needs no overrides. It seeds the default builder and asserts the step sequence plus the
/// load-bearing adjacencies (authentication immediately before tenant resolution, authentication
/// before the rate limiter per ADR-019, forwarded headers before the HTTPS redirect, the
/// pre-forwarded capture immediately before the forwarded-headers rewrite), turning a framework-side
/// reorder into a red test here instead of a silent behavior change.
/// </summary>
public sealed class MiddlewarePipelineOrderTests : MiddlewarePipelineOrderTestsBase;
