using CompTracker.Components;
using CompTracker.Data;
using CompTracker.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Resolve a relative SQLite path against the content root so the DB lands in the same place however the app is started.
var connection = new SqliteConnectionStringBuilder(builder.Configuration.GetConnectionString("Db") ?? "Data Source=comptracker.db");
if (!Path.IsPathRooted(connection.DataSource))
    connection.DataSource = Path.Combine(builder.Environment.ContentRootPath, connection.DataSource);

builder.Services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite(connection.ToString()));
builder.Services.AddSingleton<ScoringService>();
builder.Services.AddSingleton<ChangeNotifier>();
builder.Services.AddScoped<AdminSession>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

using (var db = app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext())
{
    db.Database.Migrate();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
