using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace PrometheusNetSample.WebApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TestController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<TestController> _logger;
        private readonly HttpClient _httpClient;

        public TestController(ILogger<TestController> logger, IHttpClientFactory _clientFactor)
        {
            _logger = logger;
            _httpClient = _clientFactor.CreateClient();

        }

        [HttpGet]
        public async Task<string> Get()
        {
            var rng = new Random();
            var data = Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = rng.Next(-20, 55),
                Summary = Summaries[rng.Next(Summaries.Length)]
            })
            .ToArray();
            Console.WriteLine(JsonConvert.SerializeObject(data));
            var response = await _httpClient.GetAsync("https://example.com");
			return await response.Content.ReadAsStringAsync();
		}
    }
}
