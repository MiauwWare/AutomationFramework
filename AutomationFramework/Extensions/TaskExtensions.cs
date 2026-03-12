namespace AutomationFramework;


public static class TaskExtensions
{
    extension(Task)
    {
        /// <summary>
        /// Retries until the operation completes without throwing, or retries are exhausted.
        /// </summary>
        public static async Task<T> RunWithRetry<T>(Func<Task<T>> operation, int maxRetries, TimeSpan retryDelay)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            if (maxRetries < 0) throw new ArgumentOutOfRangeException(nameof(maxRetries), "Max retries must be non-negative.");
            if (retryDelay < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(retryDelay), "Retry delay must be non-negative.");

            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    return await operation().ConfigureAwait(false);
                }
                catch when (attempt < maxRetries)
                {
                    await Task.Delay(retryDelay).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Retries until the success condition returns true, or retries are exhausted.
        /// </summary>
        public static async Task<T> RunWithRetry<T>(Func<CancellationToken, Task<T>> operation, Func<T, bool> successCondition, int maxRetries, TimeSpan retryDelay, CancellationToken cancellationToken = default)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            if (successCondition == null) throw new ArgumentNullException(nameof(successCondition));
            if (maxRetries < 0) throw new ArgumentOutOfRangeException(nameof(maxRetries), "Max retries must be non-negative.");
            if (retryDelay < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(retryDelay), "Retry delay must be non-negative.");

            T result = default!;
            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                result = await operation(cancellationToken).ConfigureAwait(false);

                if (successCondition(result))
                {
                    return result;
                }

                if (attempt < maxRetries)
                {
                    await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
                }
            }

            return result;
        }
    }
}