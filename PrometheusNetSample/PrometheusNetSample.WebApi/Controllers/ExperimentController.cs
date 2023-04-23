using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace PrometheusNetSample.WebApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ExperimentController : ControllerBase
    {
      
        private readonly ILogger<ExperimentController> _logger;
        private readonly HttpClient _httpClient;

        public ExperimentController(ILogger<ExperimentController> logger, IHttpClientFactory _clientFactor)
        {
            _logger = logger;
            _httpClient = _clientFactor.CreateClient("backendHttpClient");
        }

        [HttpGet]
        public async Task<string> Get()
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
            var response = await _httpClient.GetAsync("http://192.168.55.10:8080");
            return await response.Content.ReadAsStringAsync();
        }
    }
}
