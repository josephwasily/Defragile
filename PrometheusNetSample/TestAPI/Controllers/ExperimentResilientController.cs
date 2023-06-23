using AntifragilePolicies.Polly;
using Microsoft.AspNetCore.Mvc;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace PrometheusNetSample.WebApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ExperimentResilientController : ControllerBase
    {
        private readonly ILogger<ExperimentController> _logger;
        private readonly HttpClient _httpClient;

        public ExperimentResilientController(
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

            var _policy = Policy
                .TimeoutAsync(10, TimeoutStrategy.Pessimistic);

              var result = await _policy.ExecuteAndCaptureAsync<HttpResponseMessage>(
              () =>
              _httpClient.GetAsync("/"));

                if (result.Outcome != OutcomeType.Successful)
                {
                    return BadRequest("Timout");
                }
                return Ok(await result.Result.Content.ReadAsStringAsync());
          
        }

    }
}
