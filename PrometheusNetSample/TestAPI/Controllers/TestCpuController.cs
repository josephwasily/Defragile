using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TestAPI;

namespace PrometheusNetSample.WebApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TestCpuController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<TestCpuController> _logger;
        private readonly HttpClient _httpClient;

        public TestCpuController(ILogger<TestCpuController> logger, IHttpClientFactory _clientFactor)
        {
            _logger = logger;
            _httpClient = _clientFactor.CreateClient();

        }

        [HttpGet]
        public string Get()
        {
            var rng = new Random();
            var data = Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now).AddDays(index),
                TemperatureC = rng.Next(-20, 55),
                Summary = Summaries[rng.Next(Summaries.Length)]
            })
            .ToArray();
            CalculateSumOfPrimes(109_000);  
            Console.WriteLine(JsonConvert.SerializeObject(data));
            // var response = await _httpClient.GetAsync("https://example.com");
            //return await response.Content.ReadAsStringAsync();
            return JsonConvert.SerializeObject(data);

        }
        public static long CalculateSumOfPrimes(int n)
        {
            long sum = 0;
            for (int num = 2; num <= n; num++)
            {
                bool isPrime = true;
                for (int i = 2; i <= Math.Sqrt(num); i++)
                {
                    if (num % i == 0)
                    {
                        isPrime = false;
                        break;
                    }
                }
                if (isPrime)
                {
                    sum += num;
                }
            }
            return sum;
        }
    }
}
