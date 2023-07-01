using AntifragilePolicies.Interfaces;
using AntifragilePolicies.Polly;
using HdrHistogram;
using Prometheus;
using System.Reflection;

// Build a config object, using env vars and JSON providers.
IConfiguration config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .Build();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddHealthChecks().ForwardToPrometheus();
builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

RegisterHttpClient(builder, config);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();
app.UseMetricServer();
app.UseHttpMetrics();

app.MapControllers();
app.Run();


static void RegisterHttpClient(WebApplicationBuilder builder, IConfiguration config)
{
    var host = config["Outbound:Host"];
    var toxiProxyUrl = config["Outbound:ToxiproxyUrl"];
    var clientName = "backendHttpClient";
    var semaphore = new SemaphoreSlimDynamic(2, 10, 150); //TODO: justify choice of these numbers
    var prometheusClient = new PrometheusLatencyQueryClient($"http://{host}:9090/api/v1/query" );
    
    // A Histogram covering the range from ~466 nanoseconds to 1 hour (3,600,000,000,000 ns) with a resolution of 3 significant figures:
    var histogram = new LongHistogram(TimeStamp.Hours(1), 3);

    builder.Services.AddSingleton<SemaphoreSlimDynamic>(semaphore);
    builder.Services.AddSingleton<AdaptiveConcurrencyPolicy>((provider) =>
    {
        return new AdaptiveConcurrencyPolicy(
                       semaphore,
                       histogram);
    });
    builder.Services.AddSingleton<IPrometheusQueryClient>(prometheusClient);
    builder.Services.AddHostedService<DefragileOutboundService>(x =>
    {
        return new DefragileOutboundService(
            prometheusClient,
            semaphore,
            histogram,
            clientName,
            (int)TimeSpan.FromSeconds(3).TotalMilliseconds,
            (int)TimeSpan.FromSeconds(0.5).TotalMilliseconds,
            5
        );
    });
    builder.Services
        .AddHttpClient(
            clientName,
            client =>
            {
                client.BaseAddress = new Uri(toxiProxyUrl);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            }
        )
        .UseHttpClientMetrics();
    prometheusClient.LogInjectedLatency(0); // set initial value for latency

}
