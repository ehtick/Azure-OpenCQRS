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
public class TelemetryTests
{
    private const string ConnectionString =
        "InstrumentationKey=00000000-0000-0000-0000-000000000000;" +
        "IngestionEndpoint=http://127.0.0.1:9/;LiveEndpoint=http://127.0.0.1:9/";

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

    [Fact]
    public async Task Sends_the_log_to_application_insights_when_told_where()
    {
        using var web = MemoriaWeb.Open().With("APPLICATIONINSIGHTS_CONNECTION_STRING", ConnectionString);

        await web.Client.GetAsync("/");

        web.Logged.Should().Contain(entry =>
            entry.Level == LogLevel.Information &&
            entry.Event == "TelemetrySent" &&
            entry.Message.Contains("Application Insights"));
        web.Services.GetService<OpenTelemetry.Logs.LoggerProvider>().Should().NotBeNull(
            "every line the tool logs goes through the OpenTelemetry pipeline that exports to Application Insights");
    }
}
