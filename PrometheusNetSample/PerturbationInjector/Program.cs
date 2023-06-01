using CommandLine.Text;
using CommandLine;
using Newtonsoft.Json;
using System.Runtime;
using AntifragilePolicies.Polly;
using Microsoft.Extensions.Configuration;

namespace PerturbationInjector
{
    public class Options
    {

        [Option('n', "interval", Required = true, HelpText = "Set the interval for each change action in seconds")]
        public double  Interval { get; set; }
        [Option('m', "maximum", Required = true, HelpText = "Set the maximum latency before decreasing")]
        public int Maximum { get; set; }

        [Option('t', "target", Required = true, HelpText = "Set the target proxy name in toxiproxy server")]
        public string Target { get; set; }

        [Option('d', "duration", Required = true, HelpText = "Set the perturbance duration in seconds")]
        public int Duration { get; set; }

    }

    internal class Program
    {
        public static async Task RunInBackground(TimeSpan tick, DateTimeOffset stopDate, Action action)
        {
            var periodicTimer = new PeriodicTimer(tick);
            while ( DateTime.UtcNow < stopDate &&   await periodicTimer.WaitForNextTickAsync() )
            {
                action();
            }
            Task.Delay(1000).Wait();
            Console.WriteLine("Ending the Perturbation");

        }

        static void Main(string[] args)
        {
            // Build a config object, using env vars and JSON providers.
            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddEnvironmentVariables()
                .Build();
            var hostUrl = config["Outbound:Host"];
            var prometheusUrl = $"http://{hostUrl}:9090/api/v1/query";
            var prometheusClient = new PrometheusLatencyQueryClient(prometheusUrl);

            Parser.Default.ParseArguments<Options>(args)
                  .WithParsed(async o =>
                  {
                      Console.WriteLine("Press Enter to start");
                      Console.ReadLine();
                      var toxiproxyUrl = $"http://{hostUrl}:8474/proxies/" + o.Target+"/toxics";
                      var toxicName = "mynginx";
                      var stopDate = DateTime.UtcNow.Add(TimeSpan.FromSeconds(o.Duration));
                      var client = new HttpClient();
                      var iterations =  (o.Duration / o.Interval); //number of iterations throughout the duration
                      double change = (o.Maximum / (iterations / 2.0 )); //change in latency per iteration
                      
                      Console.WriteLine($"Injecting latency to proxy: {o.Target} {change}s every {o.Interval}s ");

                      RunInBackground(TimeSpan.FromSeconds(o.Interval), stopDate, async () =>
                      {
                          
                          Console.WriteLine("Updating ToxiProxy ");
                          //call toxiproxy api 
                          var toxics = await GetToxics(toxiproxyUrl, client);
                          if (toxics.Any())
                          {
                              //get toxic 
                              var toxic = toxics.First();

                              if((toxic.Attributes.Latency / 1000) >= o.Maximum && change > 0)
                              {
                                  change *= -1;
                              }
                              var newLatency = (long) Math.Floor(toxic.Attributes.Latency + (change * 1000));
                              Console.WriteLine("Injeting new Latency " + newLatency);

                              ////delete toxic
                              // var request = new HttpRequestMessage(HttpMethod.Delete, $"{toxiproxyUrl}/{toxicName}");
                              //var response = await client.SendAsync(request);

                              //create toxic
                              await UpdateToxic(newLatency, (long)(newLatency * 0.1), toxiproxyUrl, toxicName);

                              //log in prometheus 
                              prometheusClient.LogLatency(newLatency, o.Target);
                          }
                          else
                          {
                              Console.WriteLine("Injecting Initial Latency " + (change*1000));
                              await CreateToxic((long)Math.Floor(change*1000), (long)((change) * 0.1), toxiproxyUrl, toxicName);
                          }
                      }).Wait() ;

                  });
        }

        public static async Task<List<Toxic>> GetToxics(string toxiproxyUrl, HttpClient client)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, toxiproxyUrl);
            var response = await client.SendAsync(request);
            var responseString = await response.Content.ReadAsStringAsync();
            var toxics = JsonConvert.DeserializeObject<List<Toxic>>(responseString);
            return toxics;
        }
        public static async Task CreateToxic(long latency, long jitter, string toxiproxyUrl, string toxicName)
        {   
            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, $"{toxiproxyUrl}");
            var toxic = new Toxic()
            {
                Name = toxicName,
                Stream = "downstream",
                Toxicity = 1,
                Type = "latency",
                Attributes = new Attributes
                {
                    Latency = latency,
                    Jitter = jitter,
                }
            };
            
            var content = new StringContent(JsonConvert.SerializeObject(toxic), null, "application/json");
            request.Content = content;
            var response = await client.SendAsync(request);
            
        }

        public static async Task UpdateToxic(long latency, long jitter, string toxiproxyUrl, string toxicName)
        {
            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, $"{toxiproxyUrl}/{toxicName}");
            var toxic = new Toxic()
            {
                Name = toxicName,
                Stream = "downstream",
                Toxicity = 1,
                Type = "latency",
                Attributes = new Attributes
                {
                    Latency = latency,
                    Jitter = jitter,
                }
            };

            var content = new StringContent(JsonConvert.SerializeObject(toxic), null, "application/json");
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

    }
}