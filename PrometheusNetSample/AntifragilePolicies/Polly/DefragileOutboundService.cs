using AntifragilePolicies.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualBasic;
using Prometheus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntifragilePolicies.Polly
{
    public class DefragileOutboundService : BackgroundService
    {
        private readonly IPrometheusQueryClient _prometheusQueryClient;
        private readonly SemaphoreSlimDynamic _semaphoreSlimDynamic;
        private readonly string _endpoint;
        private readonly int _intervalMs;
        private readonly int _jitterMs;
        private readonly double _timeWindowSeconds;
        private const int LatencyThresholdSeconds = 2;
        private const double DecreaseFactor = 0.75;
    
        public DefragileOutboundService(
            IPrometheusQueryClient prometheusQueryClient,
            SemaphoreSlimDynamic semaphoreSlimDynamic,
            string endpoint,
            int intervalMs,
            int jitterMs,
            double timeWindowSeconds
            
            )
        {
            _prometheusQueryClient = prometheusQueryClient;
            _semaphoreSlimDynamic = semaphoreSlimDynamic;
            _endpoint = endpoint;
            _intervalMs = intervalMs;
            _jitterMs = jitterMs;
            _timeWindowSeconds = timeWindowSeconds;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //repeat check every 1 minute
            while (true)
            {
                 var rand = new Random().Next(1, 10) > 5 ? 1 : -1;
                 var jitterDelay = rand * (_jitterMs);

                //get endpoint delay last 
                var latencySeconds = await _prometheusQueryClient.GetP95Latency(_timeWindowSeconds, _endpoint);
                if (latencySeconds > LatencyThresholdSeconds)
                {
                    //get new limit
                    var newLimit = AIMDEngine.UpdateConcurrencyLimit(
                        _semaphoreSlimDynamic.AvailableSlotsCount,
                        _semaphoreSlimDynamic.CurrentCount - _semaphoreSlimDynamic.AvailableSlotsCount,
                        _semaphoreSlimDynamic.MinimumSlotsCount,
                        latencySeconds,
                        LatencyThresholdSeconds,
                        _semaphoreSlimDynamic.MaximumSlotsCount,
                        false,
                        DecreaseFactor
                    );

                    //adjust semaphore
                    _semaphoreSlimDynamic.AdjustConcurrency(newLimit);
                    _prometheusQueryClient.LogLimit(newLimit, _endpoint);

                    Console.WriteLine($"Latency for endpoint {_endpoint} is {latencySeconds}ms, which is above the threshold of {LatencyThresholdSeconds}s");
                }
                else
                {
                    Console.WriteLine($"Latency for endpoint {_endpoint} is {latencySeconds}ms, which is below the threshold of {LatencyThresholdSeconds}s");
                }
                await Task.Delay(_intervalMs + jitterDelay, stoppingToken);
            }
        }
    }
}
