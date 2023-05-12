using CommandLine.Text;
using CommandLine;
using Newtonsoft.Json;

namespace PerturbationInjector
{
    public class Options
    {
        [Option('i', "initial", Required = true, HelpText = "Set initial injected latency value in seconds.")]
        public int Initial { get; set; }

        [Option('n', "interval", Required = true, HelpText = "Set the interval for each change action in seconds")]
        public int  Interval { get; set; }

        [Option('c', "change", Required = true, HelpText = "Set the increase or decrease change action value for the latency in seconds")]
        public int Change { get; set; }

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

            Parser.Default.ParseArguments<Options>(args)
                  .WithParsed(o =>
                  {
                      Task.Delay(10000).Wait(); 
                      Console.WriteLine($"Injecting latency to proxy: {o.Target} {o.Change}s every {o.Interval}s starting with latency " + o.Initial);
                      var toxiproxyUrl = "http://127.0.0.1:8474/proxies/"+ o.Target+"/toxics";
                      var toxicName = "latency";
                      var stopDate = DateTime.UtcNow.Add(TimeSpan.FromSeconds(o.Duration));
                      var client = new HttpClient();

                      RunInBackground(TimeSpan.FromSeconds(o.Interval), stopDate, async () =>
                      {
                          Console.WriteLine("Updating ToxiProxy ");
                          //call toxiproxy api 
                          var toxics = await GetToxics(toxiproxyUrl, client);
                          if (toxics.Any())
                          {
                              //get toxic 
                              var toxic = toxics.First();
                              var newLatency = toxic.Attributes.Latency + (o.Change * 1000);

                              Console.WriteLine("Injeting new Latency " + newLatency);


                              //delete toxic
                              var request = new HttpRequestMessage(HttpMethod.Delete, $"{toxiproxyUrl}/{toxicName}");
                              var response = await client.SendAsync(request);

                              //create toxic
                              await CreateToxic(newLatency, (long)Math.Floor((newLatency) * 0.1), toxiproxyUrl, toxicName);

                          }
                          else
                          {
                              Console.WriteLine("Injecting Initial Latency " + o.Initial*1000);
                              await CreateToxic(o.Initial*1000, (long)Math.Floor((o.Initial) * 0.1), toxiproxyUrl, toxicName);
                          }
                          //create toxic


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