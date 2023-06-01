// See https://aka.ms/new-console-template for more information

using Microsoft.Extensions.Configuration;
using NBomber.Contracts;
using NBomber.Contracts.Stats;
using NBomber.CSharp;
using Serilog.Sinks.File;
using System.Net.Http.Headers;


// Build a config object, using env vars and JSON providers.
IConfiguration config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .Build();
var host = config["Outbound:Host"];
Console.WriteLine("Please Enter Test Name");
var test_name = Console.ReadLine();
var base_uri = new Uri($"http://{host}:62939");
Console.WriteLine("Please Enter Rate (Seconds)");
var rate = Convert.ToInt32(Console.ReadLine());

Console.WriteLine("Please Enter Interval (seconds)");
var interval = Convert.ToInt32(Console.ReadLine());

Console.WriteLine("Please Enter Duration (seconds)");
var duration = Convert.ToInt32(Console.ReadLine());

Console.WriteLine("Please Enter Experiment Timeout (seconds)");
var timeout = Convert.ToInt32(Console.ReadLine());


Console.WriteLine("Please Enter number of iterations");

var loop = Convert.ToInt32(Console.ReadLine());

var scenario_delay = Scenario.Create("Network-Bound API (With Delay)", async context =>
{
    // you can define and execute any logic here,
    // for example: send http request, SQL query etc
    // NBomber will measure how much time it takes to execute your logic
    using HttpClient client = new();
    client.BaseAddress = base_uri;
    await client.GetAsync("/Experiment?timeOut="+timeout);
    return Response.Ok();
})
       .WithoutWarmUp()
       .WithLoadSimulations(
           Simulation.Inject(rate: rate,
                             interval: TimeSpan.FromSeconds(interval),
                             during: TimeSpan.FromSeconds(duration))
       );

//var scenario_cpu = Scenario.Create("CPU-Bound API", async context =>
//{
//    // you can define and execute any logic here,
//    // for example: send http request, SQL query etc
//    // NBomber will measure how much time it takes to execute your logic
//    using HttpClient client = new();
//    client.BaseAddress = base_uri;
//    await client.GetAsync("/TestCpu");
//    return Response.Ok();
//})
//        //.WithWarmUpDuration(TimeSpan.FromSeconds(10))
//     .WithoutWarmUp()
//    .WithLoadSimulations(
//           Simulation.Inject(rate: rate,
//                             interval: TimeSpan.FromSeconds(interval),
//                             during: TimeSpan.FromSeconds(duration))
//       );

//var scenario_network = Scenario.Create("Network-Bound API (Without Delay)", async context =>
//{
//    using HttpClient client = new();
//    client.BaseAddress = base_uri;
//    await client.GetAsync("/TestNetwork");
//    return Response.Ok();
//})
//       //.WithWarmUpDuration(TimeSpan.FromSeconds(10))
//    .WithoutWarmUp()   
//    .WithLoadSimulations(
//           Simulation.Inject(rate: rate,
//                             interval: TimeSpan.FromSeconds(interval),
//                             during: TimeSpan.FromSeconds(duration))
//       );


    for(int i = 0; i< loop; i++)
    {
        var stats = NBomberRunner
        .RegisterScenarios(scenario_delay)
        .WithReportFileName($"fetch_users_report_{test_name}_{i}")
        .WithReportFolder($"fetch_users_report_{test_name}_{i}")
        .WithReportFormats(ReportFormat.Txt, ReportFormat.Csv, ReportFormat.Html, ReportFormat.Md)
        .Run();
        await Task.Delay(5000);
    }


Console.ReadLine();