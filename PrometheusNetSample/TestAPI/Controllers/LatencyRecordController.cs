using AntifragilePolicies.Interfaces;
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
        private readonly IPrometheusQueryClient _latencyQueryClient;

        public LatencyRecordController(
            IPrometheusQueryClient latencyQueryClient
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
