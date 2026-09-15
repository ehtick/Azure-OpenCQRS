namespace Memoria.Web.Extensibility;

/// <summary>
/// What a reload of the domain types is worth saying about itself.
/// </summary>
/// <remarks>
/// Here rather than beside any one caller because the registry is reloaded from three places — once
/// at start-up, and again on every upload, removal or refresh — and all three want to say the same
/// thing about what came back. Filed under events of their own, like the writes that cause them,
/// so the count a reload came back with can be charted against the reloads that were asked for.
/// </remarks>
public static partial class CatalogueLog
{
    /// <summary>
    /// Writes how many types a reload found, and one line for each assembly that could not be read.
    /// </summary>
    /// <param name="logger">Where the lines go.</param>
    /// <param name="catalogue">What the reload came back with.</param>
    public static void LogCatalogue(this ILogger logger, DomainTypeCatalogue catalogue)
    {
        logger.TypesRegistered(catalogue.Count, catalogue.Errors.Count);

        foreach (var error in catalogue.Errors)
        {
            logger.ExtensionProblem(error);
        }
    }

    [LoggerMessage(EventId = 1021, EventName = "TypesRegistered", Level = LogLevel.Information,
        Message = "Registered {TypeCount} domain type(s). {ErrorCount} problem(s).")]
    private static partial void TypesRegistered(this ILogger logger, int typeCount, int errorCount);

    [LoggerMessage(EventId = 1022, EventName = "ExtensionProblem", Level = LogLevel.Warning,
        Message = "Extension problem: {Error}")]
    private static partial void ExtensionProblem(this ILogger logger, string error);
}
