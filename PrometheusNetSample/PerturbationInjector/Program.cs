using CommandLine;
using Newtonsoft.Json;
using AntifragilePolicies.Polly;
using Microsoft.Extensions.Configuration;
using AntifragilePolicies.Models;
using NBomber.CSharp;
using NBomber.Contracts.Stats;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Docker.DotNet;
using Docker.DotNet.Models;
using NBomber.Sinks.InfluxDB;
using InfluxDB.Client;
using System.Net.Http.Headers;

namespace PerturbationInjector
{
    public class Experiment
    {
        public string Name { get; set; }
        public bool InjectFailure { get; set; }
    }
    public class Options
    {
        [Option(
            'n',
            "interval",
            Required = true,
            HelpText = "Set the interval for each change action in seconds"
        )]
        public double Interval { get; set; }

        [Option(
            'm',
            "maximum",
            Required = true,
            HelpText = "Set the maximum latency before decreasing"
        )]
        public int Maximum { get; set; }

        [Option(
            't',
            "target",
            Required = true,
            HelpText = "Set the target proxy name in toxiproxy server"
        )]
        public string Target { get; set; }

        [Option(
            'd',
            "duration",
            Required = true,
            HelpText = "Set the perturbance duration in seconds"
        )]
        public int Duration { get; set; }

        [Option(
            'i',
            "iterations",
            Required = true,
            HelpText = "how many times to repeat the experiment "
        )]
        public int Iterations { get; set; }

        [Option(
            'r',
            "rate",
            Required = true,
            HelpText = "Rate of generated traffic using nbomber per interval"
        )]
        public int Rate { get; set; }
    }

    internal class Program
    {
        //public static async Task RunInBackground(TimeSpan tick, DateTimeOffset stopDate, Action action)
        //{

        //    Task.Delay(1000).Wait();
        //    Console.WriteLine("Ending the Perturbation");

        //}

        static async Task Main(string[] args)
        {
            // Build a config object, using env vars and JSON providers.
            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddEnvironmentVariables()
                .Build();

            
            var o = new Options();
            config.GetSection("Options").Bind(o);

            var hostUrl = config["Outbound:Host"]?.ToString() ?? throw new Exception();
            var apiUrl = $"http://{hostUrl}:62939";
            var dockerApiUrl = $"http://{hostUrl}:2375";

            DockerClient client = new DockerClientConfiguration(
            new Uri(dockerApiUrl))
             .CreateClient();
            var experiments = new List<Experiment>() {
                new Experiment{ Name =  "Fragile", InjectFailure = false }, //Steady State
                new Experiment{ Name =  "Fragile", InjectFailure = true },
                new Experiment{ Name =  "Resilient", InjectFailure = true },
                new Experiment{ Name =  "Antifragile", InjectFailure = true },

            };
            foreach(var experiment in experiments)
            {
                for (int i = 0; i < o.Iterations; i++)
                {
                    var trafficGenerator = Task.Factory.StartNew(TrafficGenerator(o, apiUrl, i, experiment));

                    if (experiment.InjectFailure)
                    {
                        var chaosEngineeringTask = Task.Factory.StartNew(InjectLatency(o, hostUrl: hostUrl, i));
                        Task.WaitAll(chaosEngineeringTask, trafficGenerator);
                    }
                    else
                    {
                        Task.WaitAll(trafficGenerator);
                    }

                    Console.WriteLine("Finished the experiment no. " + i + 1);
                    await RestartContainer(client);

                }
            }
           
        }

        private static async Task RestartContainer(DockerClient client)
        {
            var containers = await client.Containers.ListContainersAsync( new ContainersListParameters()
            //new ContainersListParameters
            //{
            //    Filters = new Dictionary<string, IDictionary<string, bool>>
            //        {
            //            {
            //                "ancestor",
            //                new Dictionary<string, bool>
            //                {
            //                    { "prometheusnetsamplewebapi", true }
            //                }
            //            }

            //        }
            //}
            
            );
            await Task.Delay((int)TimeSpan.FromMinutes(1).TotalMilliseconds);

            if (containers.Any())
            {
                var restarted_containers = new List<string> { "toxiproxy", "nginx", "prometheusnetsamplewebapi" };

                foreach (var container in containers)
                {
                   if(container.State == "running" && restarted_containers.Any(z=> container.Image.Contains(z)))
                    {
                        Console.WriteLine($"Restarting Prometheus Container {container.Image}");
                        await client.Containers.RestartContainerAsync(container.ID, new ContainerRestartParameters());
                    }
                  
                }
            }
            await Task.Delay((int)TimeSpan.FromMinutes(4).TotalMilliseconds);
        }

        private static Func<Task> TrafficGenerator(Options o, string apiUrl, int iteration, Experiment experiment)
        {

            //var config =
            //new InfluxDBClientOptions.Builder()
            //    .Url("https://eu-central-1-1.aws.cloud2.influxdata.com")
            //    .AuthenticateToken("QYeAIQkkL7yElyAPwCb3RK4xJYJF66Ehbwu7vL1yruIpJ_tGgYRlx1bDuuz6W7MykgQI29BUffhqBYNep_4Xow==")
            //    .Org("Antifragile")
            //    .Bucket("Results")
            //    .VerifySsl(verifySsl: true)
            //    .Build();

            //var sink = new InfluxDBSink(new InfluxDBClient(config));

            var delayString = experiment.InjectFailure ? "With Delay" : "Without Delay";
            return (
                async () =>
                {
                    var scenario_delay = Scenario
                        .Create(
                            $"Network-Bound API - {delayString}",
                            async context =>
                            {
                                // you can define and execute any logic here,
                                // for example: send http request, SQL query etc
                                // NBomber will measure how much time it takes to execute your logic
                                using HttpClient client = new();
                                client.BaseAddress = new Uri(apiUrl);
                                await client.GetAsync($"/Experiment{experiment.Name}");
                                return NBomber.CSharp.Response.Ok();
                            }
                        )
                        .WithWarmUpDuration(TimeSpan.FromSeconds(5))
                        .WithLoadSimulations(
                            Simulation.Inject(
                                rate: o.Rate,
                                interval: TimeSpan.FromSeconds(o.Interval),
                                during: TimeSpan.FromSeconds(o.Duration)
                            )
                        );

                    var stats =  NBomberRunner
                        .RegisterScenarios(scenario_delay)
                        .WithReportFileName(
                            $"experiments_{DateTime.UtcNow.Day}_{experiment.Name}_{delayString}_{iteration+1}"
                        )
                        .WithReportFolder(
                            $"experiments_{DateTime.UtcNow.Day}"
                        )
                        .WithReportFormats(
                            ReportFormat.Txt,
                            ReportFormat.Csv,
                            ReportFormat.Html,
                            ReportFormat.Md
                        )
                        .Run();
                }
            );
        }

        private static Func<Task> InjectLatency(Options o, string hostUrl, int i)
        {
            return (
                async () =>
                {
                    var prometheusUrl = $"http://{hostUrl}:9090/api/v1/query";
                    var prometheusClient = new PrometheusLatencyQueryClient(prometheusUrl);
                    var apiUrl = $"http://{hostUrl}:62939";
                    var toxiproxyUrl = $"http://{hostUrl}:8474/proxies/" + o.Target + "/toxics";
                    var toxicName = "mynginx";
                    var stayAtPeakDuration = TimeSpan.FromSeconds(o.Duration / 5); // 1/5 of the duration the latency should stay at the maximum level
                    var stopDate = DateTime.UtcNow.Add(TimeSpan.FromSeconds(o.Duration));
                    var toxiProxyClient = new HttpClient();
                    var apiClient = new HttpClient();
                    var iterations = ((o.Duration - (o.Duration / 5)) / o.Interval); //number of iterations throughout the duration
                    double change = (o.Maximum / (iterations / 2.0)); //change in latency per iteration

                    var periodicTimer = new PeriodicTimer(TimeSpan.FromSeconds(o.Interval));
                    Console.WriteLine(
                        $"Injecting latency to proxy: {o.Target} {change}s every {o.Interval}s "
                    );
                    while (DateTime.UtcNow < stopDate && await periodicTimer.WaitForNextTickAsync())
                    {
                        Console.WriteLine("Updating ToxiProxy ");
                        //call toxiproxy api
                        var toxics = await GetToxics(toxiproxyUrl, toxiProxyClient);
                        if (toxics != null && toxics.Any())
                        {
                            //get toxic
                            var toxic = toxics.First();

                            if ((toxic.Attributes.Latency / 1000) >= o.Maximum && change > 0)
                            {
                                change *= -1;

                                //keep the latency at the maximum for a while (20% of the duration)
                                await Task.Delay(stayAtPeakDuration);
                            }
                            var newLatency = (long)
                                Math.Floor(toxic.Attributes.Latency + (change * 1000));
                            Console.WriteLine("Injeting new Latency " + newLatency);

                            ////delete toxic
                            // var request = new HttpRequestMessage(HttpMethod.Delete, $"{toxiproxyUrl}/{toxicName}");
                            //var response = await client.SendAsync(request);

                            //create toxic
                            await UpdateToxic(
                                newLatency,
                                (long)(newLatency * 0.1),
                                toxiproxyUrl,
                                toxicName
                            );

                            //log in prometheus
                            await LogLatency(newLatency, apiUrl);
                        }
                        else
                        {
                            Console.WriteLine("Injecting Initial Latency " + (change * 1000));
                            await CreateToxic(
                                (long)Math.Floor(change * 1000),
                                (long)((change) * 0.1),
                                toxiproxyUrl,
                                toxicName
                            );
                        }
                    }

                    //experiment ended - revert to latency of zero 
                    await LogLatency(0, apiUrl);

                }
            );
        }

        public static async Task<List<Toxic>> GetToxics(string toxiproxyUrl, HttpClient client)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, toxiproxyUrl);
            var response = await client.SendAsync(request);
            var responseString = await response.Content.ReadAsStringAsync();
            var toxics = JsonConvert.DeserializeObject<List<Toxic>>(responseString);
            return toxics;
        }

        public static async Task CreateToxic(
            long latency,
            long jitter,
            string toxiproxyUrl,
            string toxicName
        )
        {
            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, $"{toxiproxyUrl}");
            var toxic = new Toxic()
            {
                Name = toxicName,
                Stream = "downstream",
                Toxicity = 1,
                Type = "latency",
                Attributes = new Attributes { Latency = latency, Jitter = jitter, }
            };

            var content = new StringContent(
                JsonConvert.SerializeObject(toxic),
                null,
                "application/json"
            );
            request.Content = content;
            var response = await client.SendAsync(request);
        }

        public static async Task UpdateToxic(
            long latency,
            long jitter,
            string toxiproxyUrl,
            string toxicName
        )
        {
            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, $"{toxiproxyUrl}/{toxicName}");
            var toxic = new Toxic()
            {
                Name = toxicName,
                Stream = "downstream",
                Toxicity = 1,
                Type = "latency",
                Attributes = new Attributes { Latency = latency, Jitter = jitter, }
            };

            var content = new StringContent(
                JsonConvert.SerializeObject(toxic),
                null,
                "application/json"
            );
            request.Content = content;
            var response = await client.SendAsync(request);
        }

        public static async Task LogLatency(double latency, string apiUrl)
        {
            var client = new HttpClient();

            var request = new HttpRequestMessage(HttpMethod.Post, $"{apiUrl}/LatencyRecord");
            var toxic = new LatencyModel { Latency = latency, };

            var content = new StringContent(
                JsonConvert.SerializeObject(toxic),
                null,
                "application/json"
            );
            request.Content = content;
            var response = await client.SendAsync(request);
        }

        public partial class Toxic
        {
            [JsonProperty("attributes")]
            public Attributes Attributes { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("type")]
            public string Type { get; set; }

            [JsonProperty("stream")]
            public string Stream { get; set; }

            [JsonProperty("toxicity")]
            public long Toxicity { get; set; }
        }

        public partial class Attributes
        {
            [JsonProperty("latency")]
            public long Latency { get; set; }

            [JsonProperty("jitter")]
            public long Jitter { get; set; }
        }

        // http://msdn.microsoft.com/en-us/library/ms686033(VS.85).aspx
        [DllImport("kernel32.dll")]
        public static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        private const uint ENABLE_EXTENDED_FLAGS = 0x0080;

        //private static void DisableQuickEditMode()
        //{
        //    // Disable QuickEdit Mode
        //    // Quick Edit mode freezes the app to let users select text.
        //    // We don't want that. We want the app to run smoothly in the background.
        //    // - https://stackoverflow.com/q/4453692
        //    // - https://stackoverflow.com/a/4453779
        //    // - https://stackoverflow.com/a/30517482

        //    IntPtr handle = Process.GetCurrentProcess().MainWindowHandle;
        //    SetConsoleMode(handle, ENABLE_EXTENDED_FLAGS);
        //}

    }


}
