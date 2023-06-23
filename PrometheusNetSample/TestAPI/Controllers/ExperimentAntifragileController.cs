using AntifragilePolicies.Polly;
using Microsoft.AspNetCore.Mvc;
using Polly;
using Polly.CircuitBreaker;

namespace PrometheusNetSample.WebApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ExperimentAntifragileController : ControllerBase
    {
        private readonly ILogger<ExperimentAntifragileController> _logger;
        private readonly AdaptiveConcurrencyPolicy _policy;
        private readonly HttpClient _httpClient;

        public ExperimentAntifragileController(
            ILogger<ExperimentAntifragileController> logger,
            IHttpClientFactory _clientFactor,
            AdaptiveConcurrencyPolicy policy
        )
        {
            _logger = logger;
            _policy = policy;   
            _httpClient = _clientFactor.CreateClient("backendHttpClient");
        }

        [HttpGet]
        public async Task<ActionResult<string>> Get()
        {
         
                var result = await
              _policy.ExecuteAndCaptureAsync<HttpResponseMessage>(
              () =>
              _httpClient.GetAsync("/"));

                if (result.Outcome == OutcomeType.Failure)
                {
                    return BadRequest("Concurrency Limits Exceeded");
                }
                return Ok(await result.Result.Content.ReadAsStringAsync());
          
         

        }

    }
}
