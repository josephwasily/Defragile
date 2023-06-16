using Polly;
using Polly.Bulkhead;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntifragilePolicies.Polly
{
    public class AdaptiveConcurrencyPolicy : AsyncPolicy
    {
        private readonly SemaphoreSlimDynamic _adjustableSemaphore;

        public AdaptiveConcurrencyPolicy(SemaphoreSlimDynamic adjustableSemaphore)
        {
            _adjustableSemaphore = adjustableSemaphore;
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
                return await action(context, cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext);
            }
            finally
            {
                _adjustableSemaphore.Release();
            }

        }
    }
}
