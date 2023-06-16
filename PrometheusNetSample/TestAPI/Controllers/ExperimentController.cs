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
        private readonly AdaptiveConcurrencyPolicy _policy;
        private readonly HttpClient _httpClient;

        public ExperimentController(
            ILogger<ExperimentController> logger,
            IHttpClientFactory _clientFactor,
            AdaptiveConcurrencyPolicy policy
        )
        {
            _logger = logger;
            _policy = policy;
            _httpClient = _clientFactor.CreateClient("backendHttpClient");
        }

        [HttpGet]
        public async Task<string> Get(bool withPolicy)
        {
            //        var circuitBreakerPolicy = Policy.Handle<Exception>()
            //.AdvancedCircuitBreaker(
            //    failureThreshold: 0.5, // 50% failure rate
            //    samplingDuration: TimeSpan.FromMinutes(1), // Last 1 minute
            //    minimumThroughput: 10, // Minimum number of requests before evaluating circuit
            //    durationOfBreak: TimeSpan.FromSeconds(30)); // Break for 30 seconds

            //        var timeoutPolicy = Policy.TimeoutAsync(TimeSpan.FromSeconds(1));

            //        var httpClient = new HttpClient();

            //        //var policy = Policy.Wrap( circuitBreakerPolicy, timeoutPolicy);

            //        // Usage example
            //        try
            //        {
            //            var result = await timeoutPolicy.ExecuteAsync(
            //                () => _httpClient.GetAsync("/"));
            //            return await result.Content.ReadAsStringAsync();
            //            // Handle successful response
            //        }

            if (withPolicy)
            {
                var result = await
              _policy.ExecuteAndCaptureAsync<HttpResponseMessage>(
              () =>
              _httpClient.GetAsync("/")
          );
                if (result.Outcome == OutcomeType.Failure)
                {
                    throw new Exception("Failed");
                }
                return await result.Result.Content.ReadAsStringAsync();
            }
            else
            {
                var result = await _httpClient.GetAsync("/");
                return await result.Content.ReadAsStringAsync();

            }

        }

    }
}
