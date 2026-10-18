using Hangfire;
using Hangfire.Console;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .SetBasePath(builder.Environment.ContentRootPath)
#if DEBUG
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
#else
    .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
#endif
    .AddEnvironmentVariables();

// Add services to the container.
builder.Services.AddRazorPages();

builder.Services.AddDistributedMemoryCache();

var hanfireServerParametro = builder.Configuration.GetSection("HangfireServer");

builder.Services.AddHangfire(config =>
{
    config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
          .UseSimpleAssemblyNameTypeSerializer()
          .UseRecommendedSerializerSettings()
          .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(builder.Configuration.GetConnectionString("HangfireConnection")))
          .UseColouredConsoleLogProvider()
          .UseConsole(new ConsoleOptions
          {
              ExpireIn = TimeSpan.FromDays(hanfireServerParametro.GetValue<int>("Console:ExpireDays")),
              BackgroundColor = hanfireServerParametro.GetValue<string>("Console:BackgroundColor"),
              TextColor = hanfireServerParametro.GetValue<string>("Console:TextColor"),
              TimestampColor = hanfireServerParametro.GetValue<string>("Console:TimestampColor")
          })
          .UseDashboardMetric(DashboardMetrics.AwaitingCount)
          .UseDashboardMetric(DashboardMetrics.ProcessingCount)
          .UseDashboardMetric(DashboardMetrics.ScheduledCount)
          .UseDashboardMetric(DashboardMetrics.SucceededCount)
          .UseDashboardMetric(DashboardMetrics.FailedCount)
          .UseDashboardMetric(DashboardMetrics.DeletedCount)
          .UseDashboardMetric(DashboardMetrics.RetriesCount);

    config.UseFilter(new AutomaticRetryAttribute { Attempts = hanfireServerParametro.GetValue<int>("AutomaticRetryAttribute:RetryAttempts") });
    //config.UseFilter(new LogJobHangfire());
});

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = hanfireServerParametro.GetValue<int>("WorkerCount") > 0 ?
        hanfireServerParametro.GetValue<int>("WorkerCount")
        : Environment.ProcessorCount * hanfireServerParametro.GetValue<int>("WorkerMultiplier");

    options.Queues = hanfireServerParametro.GetSection("Queues").Get<string[]>();
    options.ServerTimeout = TimeSpan.FromMinutes(hanfireServerParametro.GetValue<int>("ServerTimeoutMinutes"));
    options.ShutdownTimeout = TimeSpan.FromMinutes(hanfireServerParametro.GetValue<int>("ShutdownTimeoutMinutes"));
    options.HeartbeatInterval = TimeSpan.FromSeconds(hanfireServerParametro.GetValue<int>("HeartbeatIntervalSeconds"));
    options.CancellationCheckInterval = TimeSpan.FromSeconds(hanfireServerParametro.GetValue<int>("CancellationCheckIntervalSeconds"));
});

var app = builder.Build();

app.MapGet("/", () => Results.Redirect("/hangfire", permanent: true));

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.MapHangfireDashboard("/hangfire",
    new DashboardOptions
    {
        Authorization = Array.Empty<IDashboardAuthorizationFilter>(),
        DisplayStorageConnectionString = hanfireServerParametro.GetValue<bool>("Dashboard:DisplayStorageConnectionString"),
        IsReadOnlyFunc = context => hanfireServerParametro.GetValue<bool>("Dashboard:IsReadOnly"),
        DisplayNameFunc = (context, job) => job.ToString(),
        StatsPollingInterval = hanfireServerParametro.GetValue<int>("Dashboard:StatsPollingInterval")
    });

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
