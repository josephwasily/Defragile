// See https://aka.ms/new-console-template for more information

using NBomber.Contracts;
using NBomber.Contracts.Stats;
using NBomber.CSharp;
using Serilog.Sinks.File;
using System.Net.Http.Headers;

Console.WriteLine("Please Enter Test Name");
var test_name = Console.ReadLine();
var base_uri = new Uri("http://127.0.0.1:62939");
var scenario_delay = Scenario.Create("Network-Bound API (With Delay)", async context =>
{
    // you can define and execute any logic here,
    // for example: send http request, SQL query etc
    // NBomber will measure how much time it takes to execute your logic
    using HttpClient client = new();
    client.BaseAddress = base_uri;
    await client.GetAsync("/Experiment");
    return Response.Ok();
})
       .WithoutWarmUp()
       .WithLoadSimulations(
           Simulation.Inject(rate: 30,
                             interval: TimeSpan.FromSeconds(5),
                             during: TimeSpan.FromSeconds(120))
       );

var scenario_cpu = Scenario.Create("CPU-Bound API", async context =>
{
    // you can define and execute any logic here,
    // for example: send http request, SQL query etc
    // NBomber will measure how much time it takes to execute your logic
    using HttpClient client = new();
    client.BaseAddress = base_uri;
    await client.GetAsync("/TestCpu");
    return Response.Ok();
})
        //.WithWarmUpDuration(TimeSpan.FromSeconds(10))
     .WithoutWarmUp()
    .WithLoadSimulations(
           Simulation.Inject(rate: 30,
                             interval: TimeSpan.FromSeconds(5),
                             during: TimeSpan.FromSeconds(120))
       );

var scenario_network = Scenario.Create("Network-Bound API (Without Delay)", async context =>
{
    using HttpClient client = new();
    client.BaseAddress = base_uri;
    await client.GetAsync("/TestNetwork");
    return Response.Ok();
})
       //.WithWarmUpDuration(TimeSpan.FromSeconds(10))
    .WithoutWarmUp()   
    .WithLoadSimulations(
           Simulation.Inject(rate: 30,
                             interval: TimeSpan.FromSeconds(5),
                             during: TimeSpan.FromSeconds(120))
       );
var stats = NBomberRunner
    .RegisterScenarios(scenario_delay, scenario_cpu, scenario_network)
  .WithReportFileName($"fetch_users_report_{test_name}")
  .WithReportFolder($"fetch_users_report_{test_name}")
  .WithReportFormats(ReportFormat.Txt, ReportFormat.Csv, ReportFormat.Html, ReportFormat.Md)
    .Run();

Console.ReadLine();