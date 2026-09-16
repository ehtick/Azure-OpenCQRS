using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// The lines the tool logs are worth nothing on a host nobody reads the console of. Told an
/// Application Insights connection string, it sends them there; told none, it says so at start-up
/// rather than leaving whoever looks for them in the portal to wonder why nothing arrived.
/// </summary>
[Collection(TelemetryCollection.Name)]
public class TelemetryTests
{
    [Fact]
    public async Task Says_at_start_up_that_nothing_is_sent_when_no_connection_string_is_set()
    {
        using var web = MemoriaWeb.Open();

        await web.Client.GetAsync("/");

        web.Logged.Should().Contain(entry =>
            entry.Level == LogLevel.Information &&
            entry.Event == "TelemetryKept" &&
            entry.Message.Contains("APPLICATIONINSIGHTS_CONNECTION_STRING"));
        web.Services.GetService<OpenTelemetry.Logs.LoggerProvider>().Should().BeNull(
            "nothing exports what the host's own log already keeps");
    }

    /// <summary>
    /// A request exported carries who made it, under the attribute Application Insights shows as
    /// the authenticated user of the request, and under the same two columns the write lines
    /// carry — so the request a write was made in, and every other request by the same person,
    /// answer to the same query.
    /// </summary>
    [Fact]
    public async Task Names_the_operator_on_the_request_exported()
    {
        using var web = MemoriaWeb.SignedInAs("Ada Lovelace")
            .SendingTelemetry();

        await web.Client.GetAsync("/");

        var requests = await web.Requests();
        requests.Should().NotBeEmpty();
        requests.Should().AllSatisfy(request =>
        {
            request.GetTagItem("enduser.id").Should().Be("ada lovelace");
            request.GetTagItem("OperatorName").Should().Be("Ada Lovelace");
            request.GetTagItem("OperatorSubject").Should().Be("ada lovelace");
        });
    }

    [Fact]
    public async Task Names_nobody_on_a_request_made_running_open()
    {
        using var web = MemoriaWeb.Open().SendingTelemetry();

        await web.Client.GetAsync("/");

        var requests = await web.Requests();
        requests.Should().NotBeEmpty();
        requests.Should().AllSatisfy(request =>
        {
            request.GetTagItem("enduser.id").Should().BeNull("nobody is signed in, and a blank would read as a missing name");
            request.GetTagItem("OperatorName").Should().BeNull();
            request.GetTagItem("OperatorSubject").Should().BeNull();
        });
    }

    [Fact]
    public async Task Sends_the_log_to_application_insights_when_told_where()
    {
        using var web = MemoriaWeb.Open().SendingTelemetry();

        await web.Client.GetAsync("/");

        web.Logged.Should().Contain(entry =>
            entry.Level == LogLevel.Information &&
            entry.Event == "TelemetrySent" &&
            entry.Message.Contains("Application Insights"));
        web.Services.GetService<OpenTelemetry.Logs.LoggerProvider>().Should().NotBeNull(
            "every line the tool logs goes through the OpenTelemetry pipeline that exports to Application Insights");
    }
}
