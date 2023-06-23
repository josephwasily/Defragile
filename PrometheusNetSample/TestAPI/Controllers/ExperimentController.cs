using AntifragilePolicies.Polly;
using Microsoft.AspNetCore.Mvc;
using Polly;
using Polly.CircuitBreaker;

namespace PrometheusNetSample.WebApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ExperimentController : ControllerBase
    {
        private readonly ILogger<ExperimentController> _logger;
        private readonly HttpClient _httpClient;

        public ExperimentController(
            ILogger<ExperimentController> logger,
            IHttpClientFactory _clientFactor
        )
        {
            _logger = logger;
            _httpClient = _clientFactor.CreateClient("backendHttpClient");
        }

        [HttpGet]
        public async Task<ActionResult<string>> Get()
        {
            var result = await _httpClient.GetAsync("/");
            return Ok(await result.Content.ReadAsStringAsync());

        }

    }
}
