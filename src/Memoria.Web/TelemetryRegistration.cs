using Azure.Monitor.OpenTelemetry.AspNetCore;

namespace Memoria.Web;

/// <summary>
/// Where the log goes besides the host's own: to Application Insights, when told which one.
/// </summary>
/// <remarks>
/// The lines this tool writes about a write — who uploaded what, who refreshed which snapshot —
/// are worth nothing on a host nobody reads the console of. Given a connection string, every one
/// of them is exported through OpenTelemetry to Application Insights, along with the request it
/// was written in, so it is found there by its event name and the request it belongs to. Given
/// none, nothing is exported and the start-up log says so, because "nothing arrived" is otherwise
/// the first thing anyone looking in the portal would have to work out.
/// <para>
/// Read from the setting App Service sets when Application Insights is connected to it, so a
/// deployment there needs no setting of this tool's own.
/// </para>
/// </remarks>
public static partial class TelemetryRegistration
{
    /// <summary>The setting the connection string is read from, named as App Service names it.</summary>
    public const string Setting = "APPLICATIONINSIGHTS_CONNECTION_STRING";

    /// <summary>
    /// Exports the log, and the requests it is written in, to Application Insights when
    /// <see cref="Setting"/> is set.
    /// </summary>
    /// <param name="builder">The application being built.</param>
    /// <returns>Whether anything is exported.</returns>
    public static bool AddTelemetry(this WebApplicationBuilder builder)
    {
        if (builder.Configuration[Setting] is not { Length: > 0 } connectionString)
        {
            return false;
        }

        builder.Services.AddOpenTelemetry()
            .UseAzureMonitor(options => options.ConnectionString = connectionString);

        return true;
    }

    /// <summary>
    /// Says, once, when it starts, whether the log leaves the host.
    /// </summary>
    /// <param name="logger">The application's logger.</param>
    /// <param name="sent">What <see cref="AddTelemetry"/> answered.</param>
    public static void LogTelemetry(this ILogger logger, bool sent)
    {
        if (sent)
        {
            logger.TelemetrySent();
            return;
        }

        logger.TelemetryKept(Setting);
    }

    [LoggerMessage(EventId = 1031, EventName = "TelemetrySent", Level = LogLevel.Information,
        Message = "The log is sent to Application Insights.")]
    private static partial void TelemetrySent(this ILogger logger);

    [LoggerMessage(EventId = 1032, EventName = "TelemetryKept", Level = LogLevel.Information,
        Message = "The log stays on this host: set {Setting} to send it to Application Insights.")]
    private static partial void TelemetryKept(this ILogger logger, string setting);
}
