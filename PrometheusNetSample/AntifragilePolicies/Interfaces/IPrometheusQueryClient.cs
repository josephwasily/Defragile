using Prometheus;
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
        void LogInjectedLatency(double latency);
        void LogActualLatency(double latencySeconds);
        void LogLimit(int newLimit, string endpoint);
    }
}
