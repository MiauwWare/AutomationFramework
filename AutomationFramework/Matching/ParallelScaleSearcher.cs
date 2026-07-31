using System.Collections.Concurrent;
using AutomationFramework.Models;

namespace AutomationFramework.Matching;

internal static class ParallelScaleSearcher
{
    public static IReadOnlyList<MatchCandidate> Search(IEnumerable<double> scales, Func<double, MatchCandidate?> search, int maxDegreeOfParallelism, CancellationToken cancellationToken)
    {
        // ConcurrentBag lets workers publish successful candidates without serializing OpenCV work.
        var candidates = new ConcurrentBag<MatchCandidate>();
        Parallel.ForEach(scales, new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = maxDegreeOfParallelism }, scale =>
        {
            // The delegate must keep each worker's temporary Mats local to that worker.
            var candidate = search(scale);
            if (candidate is not null)
            {
                candidates.Add(candidate);
            }

        });
        return candidates.ToArray();
    }
}
