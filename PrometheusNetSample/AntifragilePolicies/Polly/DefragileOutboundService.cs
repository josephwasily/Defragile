﻿using AntifragilePolicies.Interfaces;
using HdrHistogram;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualBasic;
using Prometheus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace AntifragilePolicies.Polly
{
    public class DefragileOutboundService : BackgroundService
    {
        private readonly IPrometheusQueryClient _prometheusQueryClient;
        private readonly SemaphoreSlimDynamic _semaphoreSlimDynamic;
        private readonly LongHistogram _longHistogram;
        private readonly string _endpoint;
        private readonly int _intervalMs;
        private readonly int _jitterMs;
        private readonly double _timeWindowSeconds;
        private const int LatencyThresholdSeconds = 1;
        private const double DecreaseFactor = 0.6;
        private readonly object histogramLock = new object ();
    
        public DefragileOutboundService(
            IPrometheusQueryClient prometheusQueryClient,
            SemaphoreSlimDynamic semaphoreSlimDynamic,
            LongHistogram longHistogram,
            string endpoint,
            int intervalMs,
            int jitterMs,
            double timeWindowSeconds
            
            )
        {
            _prometheusQueryClient = prometheusQueryClient;
            _semaphoreSlimDynamic = semaphoreSlimDynamic;
            _longHistogram = longHistogram;
            _endpoint = endpoint;
            _intervalMs = intervalMs;
            _jitterMs = jitterMs;
            _timeWindowSeconds = timeWindowSeconds;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(10000);
            //repeat check every 1 minute
            while (true)
            {
                 var rand = new Random().Next(1, 10) > 5 ? 1 : -1;
                 var jitterDelay = rand * (_jitterMs);
                var requestsInFlight = _semaphoreSlimDynamic.AvailableSlotsCount - _semaphoreSlimDynamic.CurrentCount;
                //get endpoint delay last 
                //var latencySeconds = await _prometheusQueryClient.GetP95Latency(_timeWindowSeconds, _endpoint);

                double latencySeconds = GetLatency(95) / 1000.0;

                //reset histogram for next interval
                lock (histogramLock)
                {
                    _longHistogram.Reset();
                }

                if (latencySeconds > 0)
                {
                    var newLimit = AIMDEngine.UpdateConcurrencyLimit(
                 _semaphoreSlimDynamic.AvailableSlotsCount,
                  requestsInFlight,
                 _semaphoreSlimDynamic.MinimumSlotsCount,
                 latencySeconds,
                 LatencyThresholdSeconds,
                 _semaphoreSlimDynamic.MaximumSlotsCount,
                 false,
                 DecreaseFactor
             );
                    _semaphoreSlimDynamic.AdjustConcurrency(newLimit);
                    _prometheusQueryClient.LogLimit(_semaphoreSlimDynamic.AvailableSlotsCount, _endpoint);
                    _prometheusQueryClient.LogCurrentRequests(requestsInFlight);
                    _prometheusQueryClient.LogActualLatency(latencySeconds);

                    if (latencySeconds > LatencyThresholdSeconds)
                    {
                        //adjust semaphore
                        Console.WriteLine($"Latency for endpoint {_endpoint} is {latencySeconds}s, which is above the threshold of {LatencyThresholdSeconds}s");
                    }
                    else
                    {
                        Console.WriteLine($"Latency for endpoint {_endpoint} is {latencySeconds}s, which is below the threshold of {LatencyThresholdSeconds}s");

                    }
                }
             
                await Task.Delay(_intervalMs + jitterDelay, stoppingToken);
            }
        }

        private double GetLatency(double percentile)
        {
            //get 95 percentile latency from hdrhistogram 
            lock (histogramLock)
            {
                try
                {
                    var latency = _longHistogram.GetValueAtPercentile(percentile);
                    return latency;
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    return 0;
                }

            }
        
        }
    }
}
