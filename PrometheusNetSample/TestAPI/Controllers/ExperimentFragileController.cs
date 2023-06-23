using AntifragilePolicies.Polly;
using Microsoft.AspNetCore.Mvc;
using Polly;
using Polly.CircuitBreaker;

namespace PrometheusNetSample.WebApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ExperimentFragileController : ControllerBase
    {
        private readonly HttpClient _httpClient;

        public ExperimentFragileController(
            IHttpClientFactory _clientFactor
        )
        {
     
            _httpClient = _clientFactor.CreateClient("backendHttpClient");
        }

        [HttpGet]
        public ActionResult<string> Get()
        {
            var result = _httpClient.GetAsync("/").Result;
            return Ok(result.Content.ReadAsStringAsync().Result);
             
         

        }

    }
}
