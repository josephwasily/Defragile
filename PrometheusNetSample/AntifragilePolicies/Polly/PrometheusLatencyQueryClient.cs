﻿using AntifragilePolicies.Interfaces;
using Newtonsoft.Json;
using Prometheus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace AntifragilePolicies.Polly
{
    public class PrometheusLatencyQueryClient : IPrometheusQueryClient
    {
      

        private HttpClient _httpClient;
        private readonly string prometheusApiUrl ;
        public PrometheusLatencyQueryClient(
            string prometheusApiUrl
            )
        {
            _httpClient = new HttpClient();
            this.prometheusApiUrl = prometheusApiUrl;

        }
        public async Task<double> GetP95Latency(double timeWindowSeconds, string endpoint)
        {
            string query = $"histogram_quantile(0.95, sum by(le) (rate(httpclient_request_duration_seconds_bucket{{client=\"{endpoint}\"}}[{(int)Math.Floor(timeWindowSeconds)}s])))";
            var response = await _httpClient.GetAsync($"{prometheusApiUrl}?query={HttpUtility.UrlEncode(query)}");
            var responseString = await response.Content.ReadAsStringAsync();
            
            var obj = JsonConvert.DeserializeObject<Response>(responseString);  
            if(obj.Status == "success" && obj.Data?.Result?.Length > 0)
            {
                if ( obj.Data.Result[0].Value?.Length > 1)
                {
                    var val = obj.Data?.Result?[0]?.Value?[1];
                    if(val != "NaN" && !string.IsNullOrEmpty(val))
                    {
                        return double.Parse(val);
                    }
                   
                }
            }
            return 0;
        }

        public void LogLimit(int newLimit, string endpoint)
        {  
            
                 Gauge concurrent_limits_guage = Metrics
                    .CreateGauge("concurrency_limits", "Concurrent limits for each outbound service");

            concurrent_limits_guage.Set(newLimit);
            
        }
        public void LogCurrentRequests(int newLimit)
        {

            Gauge currentRequests = Metrics
               .CreateGauge("inflight_requests", "requests currently in flight");

            currentRequests.Set(newLimit);

        }
        public void LogInjectedLatency(double latency)
        {
            Gauge latency_injected = Metrics
              .CreateGauge($"injected_latency", "Injected Latency to the endpoint throught toxiproxy");
            latency_injected.Set(latency);

        }
        public void LogActualLatency(double latency)
        {
            Gauge latency_injected = Metrics
              .CreateGauge($"actual_latency", "Actual observed 95 percentile Latency to the endpoint throught toxiproxy");
            latency_injected.Set(latency);
        }
    }
    public partial class Response
    {
        [JsonProperty("status")]
        public string? Status { get; set; }

        [JsonProperty("data")]
        public Data? Data { get; set; }
    }

    public partial class Data
    {
        [JsonProperty("resultType")]
        public string? ResultType { get; set; }

        [JsonProperty("result")]
        public Result[]? Result { get; set; }
    }

    public partial class Result
    {
        [JsonProperty("metric")]
        public Metric? Metric { get; set; }

        [JsonProperty("value")]
        public string[]? Value { get; set; }
    }
    public partial class Metric
    {
    }

    //public partial struct Value
    //{
    //    public double? Double;
    //    public string String;

    //    public static implicit operator Value(double Double) => new Value { Double = Double };
    //    public static implicit operator Value(string String) => new Value { String = String };
    //}


}
