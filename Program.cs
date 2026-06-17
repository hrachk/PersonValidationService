using Microsoft.EntityFrameworkCore;
using PersonValidationService.Data;
using PersonValidationService.Services;
using PersonValidationService.Workers;




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
builder.Services.AddSingleton<DecisionService>();

builder.Services.AddHostedService<ValidationWorker>();

var host = builder.Build();
await host.RunAsync();
