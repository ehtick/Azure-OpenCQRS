using System.Text.Encodings.Web;

namespace Memoria.Web;

/// <summary>
/// What the tool answers when it could not read its settings: one page, on every address, saying
/// which settings it needs, with the status a host reads as "not up".
/// </summary>
/// <remarks>
/// The store and the sign-in are decided before the application is built, and a tool told neither
/// refuses. Refusing used to mean exiting, and a host in front of an exited process answers 503
/// with a page of its own that says only that — whoever had just deployed the tool found the
/// reason in a log they had to go and open. Now the process stays up and says it in the browser.
///
/// It says it from a pipeline of its own, which maps nothing: not the pages, not the static
/// assets, and not the upload form, which is why a tool told nothing about sign-in must not answer
/// at all. The one middleware there is answers every address alike, so there is no route to
/// forget and no policy to remember. The status stays 503: a health check, a monitor and a crawler
/// all read it as a deployment that is not up, which it is not, while a person reads why.
///
/// The page shows the refusal's own message, so the messages that reach it are written for an
/// anonymous reader: they name the settings to set, and never the values that are set.
/// </remarks>
public static class StartupRefusal
{
    /// <summary>Where the page sends whoever is reading it for every setting.</summary>
    public const string ConfigurationDocumentation =
        "https://lucabriguglia.github.io/Memoria/tools/memoria-web-configuration.html";

    /// <summary>
    /// Builds the application that answers only the refusal, and says it in the log as well.
    /// </summary>
    /// <param name="builder">The application's builder, before any service was added to it.</param>
    /// <param name="refusal">Why the settings could not be read, as the reader said it.</param>
    /// <returns>The application to run in place of the tool.</returns>
    public static WebApplication Refusing(this WebApplicationBuilder builder, InvalidOperationException refusal)
    {
        var app = builder.Build();

        // The log is for whoever is looking at the host, and it is the one place that also says
        // where it was thrown from. The page is for whoever is looking at the browser.
        app.Logger.LogError(refusal, "Not started: {Refusal}", refusal.Message);

        var page = Page(refusal.Message);

        app.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.Headers.CacheControl = "no-store";
            await context.Response.WriteAsync(page);
        });

        return app;
    }

    private static string Page(string message)
    {
        var encoded = HtmlEncoder.Default.Encode(message);

        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>Memoria Web is not configured</title>
                <style>
                    body { margin: 0; padding: 3rem 1.5rem; font-family: system-ui, sans-serif; line-height: 1.5; color: #1f2937; background: #f9fafb; }
                    main { max-width: 40rem; margin: 0 auto; }
                    h1 { font-size: 1.5rem; margin: 0 0 1rem; }
                    p { margin: 0 0 1rem; }
                    a { color: #1d4ed8; }
                </style>
            </head>
            <body>
                <main>
                    <h1>Memoria Web is not configured</h1>
                    <p>{{encoded}}</p>
                    <p>Every setting is described in <a href="{{ConfigurationDocumentation}}">Configuration</a>. Nothing else answers until it is set.</p>
                </main>
            </body>
            </html>
            """;
    }
}
