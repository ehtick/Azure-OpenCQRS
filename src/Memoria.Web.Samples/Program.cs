using Memoria;
using Memoria.EventSourcing;
using Memoria.EventSourcing.Dcb;
using Memoria.EventSourcing.Dcb.Extensions;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Extensions;
using Memoria.Extensions;
using Memoria.Web.Data;
using Memoria.Web.Samples.Dcb.Aggregates;
using Memoria.Web.Samples.Seeding;
using Memoria.Web.Samples.Streamed.Aggregates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// The sample domain types the web tool loads, registered the way Memoria.Web registers them so
// that anything in Memoria.Web.Samples.Streamed and Memoria.Web.Samples.Dcb is exercised here
// first, against the same store. Each is its own assembly so that either can be uploaded alone;
// the scans below are pointed at them by one type from each.

// The content root is the output directory rather than whatever directory the process was
// started from, so appsettings.json is found by `dotnet run` from the repository root too.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

// The provider is read off the connection string the same way the tool reads its own, so seeding
// reaches whichever store the tool was pointed at rather than assuming Postgres.
var database = DatabaseConnection.Of(
    builder.Configuration.GetConnectionString(DatabaseConnection.Name),
    builder.Configuration[DatabaseConnection.Setting]);

builder.Services.AddMemoria(typeof(Program));

// The two event sourcing models side by side. Each store call replaces the default no-op service
// its model registers, so it comes after.
builder.Services.AddMemoriaEventSourcing(typeof(Order));
builder.Services.AddMemoriaDcb(typeof(Product));

// Whichever store the connection string named. A relational store brings a context for each model
// with it; a Cosmos store brings a client, the streamed model, and no context.
builder.Services.AddSampleStore(database, builder.Configuration);

var host = builder.Build();

// The scope the store is installed from, which is over before the first operation runs: each of
// those takes a scope of its own, so that a run doing one thing after another is not doing them
// all through one context holding everything every operation before it touched.
using var installation = host.Services.CreateScope();
var store = installation.ServiceProvider.GetRequiredService<ISampleStore>();

Console.WriteLine("Memoria.Web.Samples");
Console.WriteLine($"  store    : {database.Provider}");
Console.WriteLine($"  writing  : {store.Where}");
Console.WriteLine($"  bound    : {Count(TypeBindings.EventTypeBindings)} events, " +
                  $"{Count(TypeBindings.AggregateTypeBindings)}+{Count(DcbTypeBindings.AggregateTypeBindings)} aggregates, " +
                  $"{Count(TypeBindings.ProjectionTypeBindings)}+{Count(DcbTypeBindings.ProjectionTypeBindings)} projections " +
                  "(streamed+dcb)");

// The samples have a store to themselves, which may not exist yet and will hold nothing the first
// time. It is installed before anything asks a question, so a deletion has something to delete from.
try
{
    if (await store.Install())
    {
        Console.WriteLine("  schema   : installed");
    }
}
catch (Exception exception) when (exception is not OperationCanceledException)
{
    // Almost always a server that is not running. Said plainly, because a page of Npgsql stack
    // trace is a poor way to be told to start PostgreSQL.
    Console.Error.WriteLine();
    Console.Error.WriteLine($"Could not reach the database: {exception.GetBaseException().Message}");
    Console.Error.WriteLine("Check the 'Memoria' connection string in appsettings.json and that the server is up.");

    return 1;
}

// One operation after another, until whoever is running it has had enough: an answer is rarely the
// only thing somebody wants to do to a store, and a run that ended after one meant starting the tool
// again to do the next. Any question left unanswered — Esc at the keyboard, the end of a pipe — is
// the way out.
var didSomething = false;

while (true)
{
    var action = Menu.AskForAction();

    if (action == SampleDataAction.None)
    {
        break;
    }

    // A deletion on its own clears everything the store holds; a deletion before a write clears what
    // is about to be written, so that the operation leaves the store holding exactly what it just put
    // there. Either way the scope never reaches past what this store has in it — a Cosmos store has
    // no dynamic consistency boundary, so neither half of this offers one.
    var scopeOfRun = action == SampleDataAction.DeleteAll
        ? store.Holds
        : Menu.AskForScope(store.Holds);

    if (scopeOfRun == SampleDataScope.None)
    {
        break;
    }

    int? items = null;

    if (action is SampleDataAction.Add or SampleDataAction.ReplaceAll)
    {
        var count = Menu.AskForCount();

        if (count is null)
        {
            break;
        }

        items = count == Menu.ARandomHandful ? null : count;
    }

    // Its own scope, so that what this operation reads and writes is not read through contexts
    // still holding everything the one before it touched.
    using (var work = host.Services.CreateScope())
    {
        var services = work.ServiceProvider;

        if (action is SampleDataAction.ReplaceAll or SampleDataAction.DeleteAll)
        {
            Console.WriteLine();

            foreach (var (table, rows) in await services.GetRequiredService<ISampleStore>().Erase(scopeOfRun))
            {
                Console.WriteLine($"deleted {rows,7} from {table}");
            }
        }

        if (action is SampleDataAction.Add or SampleDataAction.ReplaceAll)
        {
            await Seed(services, scopeOfRun, items);
        }
    }

    didSomething = true;
}

if (!didSomething)
{
    Console.WriteLine("Nothing chosen. Nothing done.");
}

return 0;

async Task Seed(IServiceProvider services, SampleDataScope what, int? items)
{
    var random = new Random();
    var time = services.GetRequiredService<TimeProvider>();
    var report = new SeedReport();

    if (what.HasFlag(SampleDataScope.Streamed))
    {
        await StreamedSampleData.Add(
            services.GetRequiredService<IDomainService>(), random, time, report, items);
    }

    if (what.HasFlag(SampleDataScope.Dcb))
    {
        await DcbSampleData.Add(services.GetRequiredService<IDcbDomainService>(), random, report, items);
    }

    await report.Print();
}

int Count(Dictionary<string, Type> bindings) =>
    bindings.Count(binding => binding.Value.Assembly == typeof(Order).Assembly ||
                              binding.Value.Assembly == typeof(Product).Assembly);

/// <summary>
/// Named so that <c>typeof(Program)</c> can point the registration scans at this assembly, which a
/// top-level program's implicit entry point class cannot do from outside.
/// </summary>
public partial class Program;
