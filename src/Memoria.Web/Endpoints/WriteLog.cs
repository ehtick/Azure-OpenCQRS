using Memoria.Web.Security;

namespace Memoria.Web.Endpoints;

/// <summary>
/// Every line the tool logs about a write: what was done, to what, and who asked.
/// </summary>
/// <remarks>
/// Each outcome is filed under an event of its own — an id and a name — because that is what a
/// line is found by in a tool like Application Insights, where the wording is one column among
/// many and the name is the one to filter on. The named values are the other columns: the file,
/// the model, the instance, the operator, and why when something could not be done.
/// <para>
/// Generated, so the template and its values cannot drift apart, and so nothing is formatted when
/// the level is off.
/// </para>
/// </remarks>
public static partial class WriteLog
{
    [LoggerMessage(EventId = 1001, EventName = "ExtensionInstalled", Level = LogLevel.Information,
        Message = "Installed {FileName}, asked by {Operator}.")]
    public static partial void ExtensionInstalled(this ILogger logger, string fileName, Operator @operator);

    [LoggerMessage(EventId = 1002, EventName = "ExtensionNotInstalled", Level = LogLevel.Error,
        Message = "Could not install {FileName}, asked by {Operator}.")]
    public static partial void ExtensionNotInstalled(
        this ILogger logger, Exception exception, string fileName, Operator @operator);

    [LoggerMessage(EventId = 1003, EventName = "ExtensionRemoved", Level = LogLevel.Information,
        Message = "Removed {FileName}, asked by {Operator}.")]
    public static partial void ExtensionRemoved(this ILogger logger, string fileName, Operator @operator);

    [LoggerMessage(EventId = 1004, EventName = "ExtensionNotRemoved", Level = LogLevel.Error,
        Message = "Could not remove {FileName}, asked by {Operator}.")]
    public static partial void ExtensionNotRemoved(
        this ILogger logger, Exception exception, string fileName, Operator @operator);

    [LoggerMessage(EventId = 1005, EventName = "ExtensionsReread", Level = LogLevel.Information,
        Message = "Reread the extensions, asked by {Operator}.")]
    public static partial void ExtensionsReread(this ILogger logger, Operator @operator);

    [LoggerMessage(EventId = 1011, EventName = "SnapshotRefreshed", Level = LogLevel.Information,
        Message = "Refreshed the snapshot for {Model} {Instance}, asked by {Operator}.")]
    public static partial void SnapshotRefreshed(
        this ILogger logger, string model, string instance, Operator @operator);

    /// <summary>
    /// Said at the same level as a refresh, because somebody pressed the button either way and a
    /// snapshot that was never written is the first thing they would look for.
    /// </summary>
    [LoggerMessage(EventId = 1012, EventName = "SnapshotUpToDate", Level = LogLevel.Information,
        Message = "Nothing to refresh for {Model} {Instance}, asked by {Operator}: no snapshot, and no events to fold.")]
    public static partial void SnapshotUpToDate(
        this ILogger logger, string model, string instance, Operator @operator);

    [LoggerMessage(EventId = 1013, EventName = "SnapshotNotRefreshed", Level = LogLevel.Warning,
        Message = "Could not refresh {Model} {Instance}, asked by {Operator}: {Error}")]
    public static partial void SnapshotNotRefreshed(
        this ILogger logger, string model, string instance, Operator @operator, string error);
}
