using Memoria.EventSourcing.Dcb.Extensions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions;
using Memoria;
using Memoria.Examples.Ecommerce.Dcb.Commands;
using Memoria.Examples.Ecommerce.Dcb.Components;
using Memoria.Examples.Ecommerce.Dcb.Data;
using Memoria.Extensions;
using Memoria.Validation.FluentValidation.Extensions;
using Microsoft.AspNetCore.Mvc;
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

// A plain form post per row rather than a Blazor form: several delete buttons on one page
// would each need their own uniquely named EditForm, and this keeps the list statically
// rendered. Antiforgery still applies — the form carries the token via <AntiforgeryToken/>.
app.MapPost("/admin/products/delete", async (
    IDispatcher dispatcher,
    [FromForm] string productId,
    [FromForm] string? returnUrl) =>
{
    var response = await dispatcher.SendAndPublish(new DeleteProductCommand(productId));

    var target = string.IsNullOrWhiteSpace(returnUrl) ? "/admin/products" : returnUrl;

    if (response.CommandResult.IsNotSuccess)
    {
        var separator = target.Contains('?') ? "&" : "?";
        target += $"{separator}error={Uri.EscapeDataString(response.CommandResult.Failure!.Description ?? "Could not delete the product.")}";
    }

    // Local: the return address arrives on the form, so it may not send anyone off-site.
    return Results.LocalRedirect(target);
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
