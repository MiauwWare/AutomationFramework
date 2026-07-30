namespace AutomationFramework;

public static class Retry
{
    public static async Task ExecuteAsync(
    Func<Task> action,
    int maxAttempts = 3,
    TimeSpan? delay = null,
    CancellationToken cancellationToken = default)
    {
        delay ??= TimeSpan.FromSeconds(1);

        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await action();
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                lastException = ex;
                await Task.Delay(delay.Value, cancellationToken);
            }
            catch (Exception ex)
            {
                lastException = ex;
            }
        }

        throw lastException!;
    }


    public static async Task<T> ExecuteAsync<T>(
        Func<Task<T>> action,
        int maxAttempts = 3,
        TimeSpan? delay = null,
        CancellationToken cancellationToken = default)
    {
        delay ??= TimeSpan.FromSeconds(1);

        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await action();
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                lastException = ex;
                await Task.Delay(delay.Value, cancellationToken);
            }
            catch (Exception ex)
            {
                lastException = ex;
            }
        }

        throw lastException!;
    }
}