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
    public class TestNetworkController : ControllerBase
    {
        private readonly ILogger<TestNetworkController> _logger;
        private readonly HttpClient _httpClient;

        public TestNetworkController(
            ILogger<TestNetworkController> logger,
            IHttpClientFactory _clientFactor
        )
        {
            _logger = logger;
            _httpClient = _clientFactor.CreateClient();
        }

        [HttpGet]
        public async Task<string> Get()
        {
            List<string> urls = new List<string>()
            {
                "https://www.example.com/",
                "https://www.google.com/",
                "https://www.bing.com/",
            };
            string randomUrl = GetRandomUrl(urls);
            var response = await _httpClient.GetAsync(randomUrl);
            return await response.Content.ReadAsStringAsync();
        }

        static string GetRandomUrl(List<string> urls)
        {
            Random rand = new Random();
            int randomIndex = rand.Next(urls.Count);
            return urls[randomIndex];
        }
    }
}
