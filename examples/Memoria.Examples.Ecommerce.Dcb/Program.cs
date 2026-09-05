using Memoria.EventSourcing.Dcb.Extensions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions;
using Memoria.Examples.Ecommerce.Dcb.Components;
using Memoria.Examples.Ecommerce.Dcb.Data;
using Memoria.Extensions;
using Memoria.Validation.FluentValidation.Extensions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var connectionString = builder.Configuration.GetConnectionString("Ecommerce")
                       ?? throw new InvalidOperationException(
                           "Connection string 'Ecommerce' is not configured in appsettings.json.");

// DcbDbContext takes its options as DbContextOptions<DcbDbContext>, so the derived context is
// registered against that rather than against its own closed type.
builder.Services.AddScoped(serviceProvider => new DbContextOptionsBuilder<DcbDbContext>()
    .UseNpgsql(connectionString)
    .UseApplicationServiceProvider(serviceProvider)
    .Options);

builder.Services.AddDbContext<EcommerceDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddMemoria(typeof(Program));
builder.Services.AddMemoriaDcb(typeof(Program));
builder.Services.AddMemoriaDcbEntityFrameworkCore<EcommerceDbContext>();
builder.Services.AddMemoriaFluentValidation(typeof(Program));

var app = builder.Build();

// Demo app: create the database and the four DCB tables on start-up rather than shipping migrations.
using (var scope = app.Services.CreateScope())
{
    try
    {
        await scope.ServiceProvider.GetRequiredService<EcommerceDbContext>().Database.EnsureCreatedAsync();
    }
    catch (NpgsqlException exception)
    {
        throw new InvalidOperationException(
            "Could not reach Postgres. Check that it is running and that the 'Ecommerce' connection string in " +
            "appsettings.json is correct.",
            exception);
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
