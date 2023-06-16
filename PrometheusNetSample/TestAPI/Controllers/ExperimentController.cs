using AntifragilePolicies.Models;
using AntifragilePolicies.Polly;
using Microsoft.AspNetCore.Mvc;
using Polly;
using Polly.CircuitBreaker;

namespace PrometheusNetSample.WebApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class LatencyRecordController : ControllerBase
    {
        private readonly PrometheusLatencyQueryClient _latencyQueryClient;

        public LatencyRecordController(
            PrometheusLatencyQueryClient latencyQueryClient
              )
        {
            _latencyQueryClient = latencyQueryClient;
        }

        [HttpPost]
        public void Post(LatencyModel model)
        {

            _latencyQueryClient.LogLatency(model.Latency);
        }

    }
}
