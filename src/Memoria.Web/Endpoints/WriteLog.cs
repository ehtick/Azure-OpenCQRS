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
/// The operator is three of those columns. <c>Operator</c> is the one the wording uses, name and
/// subject together, which is what a person reads; <c>OperatorName</c> and <c>OperatorSubject</c>
/// are the two apart, so everything one subject did can be asked for without matching text, and
/// still found after a rename.
/// </para>
/// <para>
/// The generated methods below name their parameters as the columns are to read, capitals and
/// all, because the parameter name is the column name: a value the wording does not use is
/// written under its parameter's name exactly, and one written as <c>@operator</c> comes out with
/// the <c>@</c> on. The two apart are not in the wording, which is what the generator is told not
/// to mind. Generated at all so the template and its values cannot drift apart, and so nothing is
/// formatted when the level is off.
/// </para>
/// </remarks>
public static partial class WriteLog
{
    public static void ExtensionInstalled(this ILogger logger, string fileName, Operator asked) =>
        logger.ExtensionInstalled(fileName, asked.ToString(), asked.Name, asked.Subject);

    public static void ExtensionNotInstalled(
        this ILogger logger, Exception exception, string fileName, Operator asked) =>
        logger.ExtensionNotInstalled(exception, fileName, asked.ToString(), asked.Name, asked.Subject);

    public static void ExtensionRemoved(this ILogger logger, string fileName, Operator asked) =>
        logger.ExtensionRemoved(fileName, asked.ToString(), asked.Name, asked.Subject);

    public static void ExtensionNotRemoved(
        this ILogger logger, Exception exception, string fileName, Operator asked) =>
        logger.ExtensionNotRemoved(exception, fileName, asked.ToString(), asked.Name, asked.Subject);

    public static void ExtensionsReread(this ILogger logger, Operator asked) =>
        logger.ExtensionsReread(asked.ToString(), asked.Name, asked.Subject);

    public static void BrandingSaved(this ILogger logger, string name, bool logoReplaced, Operator asked) =>
        logger.BrandingSaved(name, logoReplaced, asked.ToString(), asked.Name, asked.Subject);

    public static void BrandingNotSaved(this ILogger logger, string error, Operator asked) =>
        logger.BrandingNotSaved(asked.ToString(), asked.Name, asked.Subject, error);

    public static void BrandingReset(this ILogger logger, Operator asked) =>
        logger.BrandingReset(asked.ToString(), asked.Name, asked.Subject);

    public static void CachingSettingsSaved(this ILogger logger, TimeSpan figuresKeptFor, Operator asked) =>
        logger.CachingSettingsSaved(figuresKeptFor, asked.ToString(), asked.Name, asked.Subject);

    public static void CachingSettingsNotSaved(this ILogger logger, string error, Operator asked) =>
        logger.CachingSettingsNotSaved(asked.ToString(), asked.Name, asked.Subject, error);

    public static void SnapshotRefreshed(this ILogger logger, string model, string instance, Operator asked) =>
        logger.SnapshotRefreshed(model, instance, asked.ToString(), asked.Name, asked.Subject);

    /// <summary>
    /// Said at the same level as a refresh, because somebody pressed the button either way and a
    /// snapshot that was never written is the first thing they would look for.
    /// </summary>
    public static void SnapshotUpToDate(this ILogger logger, string model, string instance, Operator asked) =>
        logger.SnapshotUpToDate(model, instance, asked.ToString(), asked.Name, asked.Subject);

    public static void SnapshotNotRefreshed(
        this ILogger logger, string model, string instance, Operator asked, string error) =>
        logger.SnapshotNotRefreshed(model, instance, asked.ToString(), asked.Name, asked.Subject, error);

#pragma warning disable SYSLIB1015 // OperatorName and OperatorSubject are columns, not wording — see above.
#pragma warning disable IDE1006 // Parameter names are column names — see above.

    [LoggerMessage(EventId = 1001, EventName = "ExtensionInstalled", Level = LogLevel.Information,
        Message = "Installed {FileName}, asked by {Operator}.")]
    private static partial void ExtensionInstalled(
        this ILogger logger, string FileName, string Operator, string? OperatorName, string? OperatorSubject);

    [LoggerMessage(EventId = 1002, EventName = "ExtensionNotInstalled", Level = LogLevel.Error,
        Message = "Could not install {FileName}, asked by {Operator}.")]
    private static partial void ExtensionNotInstalled(
        this ILogger logger, Exception exception, string FileName, string Operator,
        string? OperatorName, string? OperatorSubject);

    [LoggerMessage(EventId = 1003, EventName = "ExtensionRemoved", Level = LogLevel.Information,
        Message = "Removed {FileName}, asked by {Operator}.")]
    private static partial void ExtensionRemoved(
        this ILogger logger, string FileName, string Operator, string? OperatorName, string? OperatorSubject);

    [LoggerMessage(EventId = 1004, EventName = "ExtensionNotRemoved", Level = LogLevel.Error,
        Message = "Could not remove {FileName}, asked by {Operator}.")]
    private static partial void ExtensionNotRemoved(
        this ILogger logger, Exception exception, string FileName, string Operator,
        string? OperatorName, string? OperatorSubject);

    [LoggerMessage(EventId = 1005, EventName = "ExtensionsReread", Level = LogLevel.Information,
        Message = "Reread the extensions, asked by {Operator}.")]
    private static partial void ExtensionsReread(
        this ILogger logger, string Operator, string? OperatorName, string? OperatorSubject);

    [LoggerMessage(EventId = 1006, EventName = "BrandingSaved", Level = LogLevel.Information,
        Message = "Saved the branding as {Name}, asked by {Operator}.")]
    private static partial void BrandingSaved(
        this ILogger logger, string Name, bool LogoReplaced, string Operator,
        string? OperatorName, string? OperatorSubject);

    /// <summary>
    /// A warning rather than an error: what was refused was the upload, and the branding is as
    /// it was.
    /// </summary>
    [LoggerMessage(EventId = 1007, EventName = "BrandingNotSaved", Level = LogLevel.Warning,
        Message = "Could not save the branding, asked by {Operator}: {Error}")]
    private static partial void BrandingNotSaved(
        this ILogger logger, string Operator, string? OperatorName, string? OperatorSubject, string Error);

    [LoggerMessage(EventId = 1008, EventName = "BrandingReset", Level = LogLevel.Information,
        Message = "Restored Memoria's own branding, asked by {Operator}.")]
    private static partial void BrandingReset(
        this ILogger logger, string Operator, string? OperatorName, string? OperatorSubject);

    [LoggerMessage(EventId = 1009, EventName = "CachingSettingsSaved", Level = LogLevel.Information,
        Message = "Saved the caching settings, figures kept for {FiguresKeptFor}, asked by {Operator}.")]
    private static partial void CachingSettingsSaved(
        this ILogger logger, TimeSpan FiguresKeptFor, string Operator, string? OperatorName,
        string? OperatorSubject);

    /// <summary>A warning, as a refused branding is: what was refused was the save, and the settings are as they were.</summary>
    [LoggerMessage(EventId = 1010, EventName = "CachingSettingsNotSaved", Level = LogLevel.Warning,
        Message = "Did not save the caching settings, asked by {Operator}: {Error}")]
    private static partial void CachingSettingsNotSaved(
        this ILogger logger, string Operator, string? OperatorName, string? OperatorSubject, string Error);

    [LoggerMessage(EventId = 1011, EventName = "SnapshotRefreshed", Level = LogLevel.Information,
        Message = "Refreshed the snapshot for {Model} {Instance}, asked by {Operator}.")]
    private static partial void SnapshotRefreshed(
        this ILogger logger, string Model, string Instance, string Operator,
        string? OperatorName, string? OperatorSubject);

    [LoggerMessage(EventId = 1012, EventName = "SnapshotUpToDate", Level = LogLevel.Information,
        Message = "Nothing to refresh for {Model} {Instance}, asked by {Operator}: no snapshot, and no events to fold.")]
    private static partial void SnapshotUpToDate(
        this ILogger logger, string Model, string Instance, string Operator,
        string? OperatorName, string? OperatorSubject);

    [LoggerMessage(EventId = 1013, EventName = "SnapshotNotRefreshed", Level = LogLevel.Warning,
        Message = "Could not refresh {Model} {Instance}, asked by {Operator}: {Error}")]
    private static partial void SnapshotNotRefreshed(
        this ILogger logger, string Model, string Instance, string Operator,
        string? OperatorName, string? OperatorSubject, string Error);

#pragma warning restore IDE1006
#pragma warning restore SYSLIB1015
}
