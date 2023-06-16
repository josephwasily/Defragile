﻿using Prometheus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntifragilePolicies.Interfaces
{
    public interface IPrometheusQueryClient
    {
        Task<double> GetP95Latency(double timeWindowSeconds, string endpoint);
        void LogCurrentRequests(int newLimit);
        void LogLatency(double latency);
        void LogLimit(int newLimit, string endpoint);
    }
}
