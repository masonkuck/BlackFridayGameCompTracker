using CompTracker.Components;
using CompTracker.Data;
using CompTracker.Services;
using Microsoft.AspNetCore.HttpOverrides;
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
builder.Services.AddSingleton<JoinLink>();

// Backups: a relative folder is resolved against the content root, like the database.
var backupOptions = builder.Configuration.GetSection("Backups").Get<BackupOptions>() ?? new BackupOptions();
backupOptions.Folder = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, backupOptions.Folder));
builder.Services.AddSingleton(backupOptions);
builder.Services.AddSingleton<BackupService>();
builder.Services.AddHostedService<BackupWorker>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

using (var db = app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext())
{
    db.Database.Migrate();
}

// A dev tunnel forwards requests as localhost; take the public host from its X-Forwarded-* headers so the page
// (and its QR code) knows the real address. Only trusted from loopback, where the tunnel client runs.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto,
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapGet("/backups/download/{token}", (string token, BackupService backups) =>
    backups.RedeemDownloadLink(token) is { } path
        ? Results.File(path, "application/octet-stream", Path.GetFileName(path))
        : Results.NotFound());
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
