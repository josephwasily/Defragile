using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntifragilePolicies.Polly
{
    public static class AIMDEngine
    {
        public static int UpdateConcurrencyLimit(
            int currentConcurrencyLimit,
            int currentRequestsInflight,
            int minConcurrencyLimit,
            double p95Latency,
            double latencyThreshold,
            int maxConcurrencyLimit,
            bool timeOutsObserved,
            double decreaseFactor
        )
        {
            if (timeOutsObserved || (p95Latency > latencyThreshold))
            {
                //multiplicative decrease
                currentConcurrencyLimit = (int)
                    Math.Floor((double)currentConcurrencyLimit * decreaseFactor);
            }
            else if (currentRequestsInflight * 2 > currentConcurrencyLimit)
            {
                // additive increase
                currentConcurrencyLimit += 1;
            }

            // keep current limits within min, max boundaries
            currentConcurrencyLimit = Math.Min(
                maxConcurrencyLimit,
                Math.Max(minConcurrencyLimit, currentConcurrencyLimit)
            );

            return currentConcurrencyLimit;
        }
    }
}
