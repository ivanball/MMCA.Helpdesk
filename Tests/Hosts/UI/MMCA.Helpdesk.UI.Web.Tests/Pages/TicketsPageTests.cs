using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AngleSharp.Dom;
using AwesomeAssertions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MMCA.Common.Shared.Abstractions;
using MMCA.Common.Testing.UI;
using MMCA.Common.UI.Common.Interfaces;
using MMCA.Helpdesk.Tickets.Shared.Tickets;
using MMCA.Helpdesk.UI.Web.Services;
using Moq;
using Xunit;
using TicketsPage = MMCA.Helpdesk.UI.Web.Components.Pages.Tickets;

namespace MMCA.Helpdesk.UI.Web.Tests.Pages;

/// <summary>
/// The open-ticket form on the list page. A successful submit clears the form, and the cleared form
/// must come back clean: no required-field message under a control the user has not touched since.
/// A submit with the fields empty must still put the message on each control.
/// </summary>
public sealed class TicketsPageTests : BunitComponentTestBase
{
    private readonly Mock<IToastService> _toast = new();
    private readonly FakeTicketsApi _api = new();
    private readonly HttpClient _http;

    public TicketsPageTests()
    {
        Services.AddSingleton(_toast.Object);
        Services.AddSingleton(Mock.Of<IAppDialogService>());
        _http = new HttpClient(_api, disposeHandler: false) { BaseAddress = new Uri("http://api.test/") };
        Services.AddSingleton(new HelpdeskApiClient(_http));

        // After every Services.Add call: SetRendererInfo freezes the service provider.
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _http.Dispose();
            _api.Dispose();
        }

        base.Dispose(disposing);
    }

    private IStringLocalizer<TicketsPage> L => Services.GetRequiredService<IStringLocalizer<TicketsPage>>();

    [Fact]
    public void SuccessfulSubmit_ClearsTheForm_WithoutRequiredFieldErrors()
    {
        var cut = RenderUnderTest<TicketsPage>(_ => { });

        // What a user does: type into each field, leave it, then press the button.
        Type(cut, 0, "Printer jammed");
        // template:begin description
        Type(cut, 1, "Paper stuck in tray 2");
        // template:end description
        OpenButton(cut).Click();

        cut.WaitForAssertion(() => _toast.Verify(t => t.Success(L["Snackbar.Opened"].Value), Times.Once));
        cut.WaitForAssertion(() =>
        {
            cut.FindAll("input").Select(i => i.GetAttribute("value") ?? string.Empty).Should().AllBe(string.Empty);
            cut.Markup.Should().NotContain(L["Validation.TitleRequired"].Value);
            // template:begin description
            cut.Markup.Should().NotContain(L["Validation.DescriptionRequired"].Value);
            // template:end description
            cut.FindAll(".mud-input-error").Should().BeEmpty();
        });
        _api.Created.Should().Be(1);
    }

    [Fact]
    public void EmptySubmit_ShowsTheRequiredFieldErrors_AndCreatesNothing()
    {
        var cut = RenderUnderTest<TicketsPage>(_ => { });

        OpenButton(cut).Click();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain(L["Validation.TitleRequired"].Value);
            // template:begin description
            cut.Markup.Should().Contain(L["Validation.DescriptionRequired"].Value);
            // template:end description
        });
        _toast.Verify(t => t.Warning(L["Snackbar.RequiredFields"].Value), Times.Once);
        _api.Created.Should().Be(0);
    }

    private static void Type(IRenderedComponent<TicketsPage> cut, int index, string text)
    {
        cut.FindAll("input")[index].Change(text);
        cut.FindAll("input")[index].Blur();
    }

    private IElement OpenButton(IRenderedComponent<TicketsPage> cut) =>
        cut.FindAll("button").Single(b => b.TextContent.Trim() == L["Button.Open"].Value);

    /// <summary>
    /// Stands in for the API host: an empty first page for every list read, and a create that echoes
    /// the posted fields back as the new row, so the page runs its real success path.
    /// </summary>
    private sealed class FakeTicketsApi : HttpMessageHandler
    {
        public int Created { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post && request.Content is not null)
            {
                Created++;
                var row = JsonNode.Parse(await request.Content.ReadAsStringAsync(cancellationToken))!.AsObject();
                row["id"] = Created;
                row["rowVersion"] = Convert.ToBase64String([1]);
                row["status"] = 0;
                return Json(HttpStatusCode.Created, row.ToJsonString());
            }

            return Json(HttpStatusCode.OK, JsonSerializer.Serialize(new KeysetCollectionResult<TicketDTO>(), JsonSerializerOptions.Web));
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
            new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
