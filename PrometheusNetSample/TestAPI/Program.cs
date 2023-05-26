using AntifragilePolicies.Interfaces;
using AntifragilePolicies.Polly;
using Prometheus;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddHealthChecks().ForwardToPrometheus();
builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

RegisterHttpClient(builder);

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

static void RegisterHttpClient(WebApplicationBuilder builder)
{
    var clientName = "backendHttpClient";
    var semaphore = new SemaphoreSlimDynamic(5, 50, 100); //TODO: justify choice of these numbers
    var prometheusClient = new PrometheusLatencyQueryClient();
    builder.Services.AddSingleton<SemaphoreSlimDynamic>(semaphore);
    builder.Services.AddSingleton<AdaptiveConcurrencyPolicy>((provider) =>
    {
        return new AdaptiveConcurrencyPolicy(
                       semaphore);
    });
    builder.Services.AddSingleton<IPrometheusQueryClient>(prometheusClient);
    builder.Services.AddHostedService<DefragileOutboundService>(x =>
    {
        return new DefragileOutboundService(
            prometheusClient,
            semaphore,
            clientName,
            (int)TimeSpan.FromSeconds(2).TotalMilliseconds,
            (int)TimeSpan.FromSeconds(0.5).TotalMilliseconds,
            5
        );
    });
    builder.Services
        .AddHttpClient(
            clientName,
            client =>
            {
                client.BaseAddress = new Uri("http://mytoxiproxy:22220");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            }
        )
        .UseHttpClientMetrics();
}
