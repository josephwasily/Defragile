using HdrHistogram;
using Polly;
using Polly.Bulkhead;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntifragilePolicies.Polly
{
    public class AdaptiveConcurrencyPolicy : AsyncPolicy
    {
        private readonly SemaphoreSlimDynamic _adjustableSemaphore;
        private readonly object histogramLock = new object();
        private readonly LongHistogram _longHistogram;
        public AdaptiveConcurrencyPolicy(SemaphoreSlimDynamic adjustableSemaphore, LongHistogram longHistogram)
        {
            _adjustableSemaphore = adjustableSemaphore;
            _longHistogram = longHistogram;

        }

        protected override async Task<TResult> ImplementationAsync<TResult>(
            Func<Context, CancellationToken, Task<TResult>> action,
            Context context,
            CancellationToken cancellationToken,
            bool continueOnCapturedContext
        )
        {
            var obtainedLock = await _adjustableSemaphore.WaitAsync(TimeSpan.Zero, cancellationToken).ConfigureAwait(continueOnCapturedContext);

            if (!obtainedLock)
            {
                throw new BulkheadRejectedException(); //TODO: create custom exception
            }
            try
            {
                long startTimestamp = Stopwatch.GetTimestamp();

                //Execute some action to be measured
                var actionResult =  await action(context, cancellationToken)
                   .ConfigureAwait(continueOnCapturedContext);

                long elapsed = Stopwatch.GetTimestamp() - startTimestamp;

                lock (histogramLock)
                {
                    _longHistogram.RecordValue(elapsed);

                }
                return actionResult;
               
            }
            finally
            {
                _adjustableSemaphore.Release();
            }

        }
    }
}
