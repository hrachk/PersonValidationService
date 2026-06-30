using Microsoft.EntityFrameworkCore;
using PersonValidationService.Data;
using PersonValidationService.Services;
using PersonValidationService.Workers;

// .NET doesn't register legacy code pages (Windows-1252, etc.) by default —
// needed by PersonRepository.FixLatin1Mojibake to correctly recover
// Armenian text from the DicFirstNames/DicLastNames tables (MySQL's
// "latin1" charset is actually Windows-1252, not true ISO-8859-1).
System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

var builder = Host.CreateApplicationBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContextFactory<AppDbContext>(options =>
{
    options.UseMySql(
        connectionString,
        ServerVersion.AutoDetect(connectionString));
});

builder.Services.AddHttpClient<ValidatorApiClient>(
   (sp, client) =>
   {
       var configuration =
           sp.GetRequiredService<IConfiguration>();

       client.BaseAddress = new Uri(
           configuration["ValidatorApi:BaseUrl"]!);

       client.Timeout = TimeSpan.FromSeconds(30);
   });
builder.Services.AddSingleton<CsvResultWriter>();
builder.Services.AddSingleton<FilePersonReader>();
builder.Services.AddSingleton<PersonRepository>();
builder.Services.AddSingleton<DocumentComparisonService>();
builder.Services.AddSingleton<JsonReportWriter>();
builder.Services.AddSingleton<WorkerStatusService>();
builder.Services.AddSingleton<DecisionService>();

builder.Services.AddHostedService<ValidationWorker>();

var host = builder.Build();
await host.RunAsync();
