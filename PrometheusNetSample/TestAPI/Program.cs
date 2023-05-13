using Prometheus;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services
        .AddHealthChecks()
       .ForwardToPrometheus();
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient("backendHttpClient", client =>
{
    client.BaseAddress = new Uri("https://mytoxiproxy:22220");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    
}).UseHttpClientMetrics();

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
