// Copyright Kind Computers. Licensed under the MIT License.
using System.Collections.Concurrent;

namespace StabilityTest;

// A fixed set of low-priority workers. Every accepted task settles, including exceptions.
internal sealed class UtilityThreadPool<TResult> : IDisposable
{
    private readonly BlockingCollection<(Func<TResult> Work, TaskCompletionSource<TResult> Completion)> queue = new();
    private readonly List<Thread> threads = [];

    internal UtilityThreadPool(int count, ThreadPriority priority)
    {
        try
        {
            for (int i = 0; i < count; i++)
            {
                var thread = new Thread(() =>
                {
                    foreach (var task in queue.GetConsumingEnumerable())
                    {
                        try { task.Completion.TrySetResult(task.Work()); }
                        catch (OperationCanceledException ex) { task.Completion.TrySetCanceled(ex.CancellationToken); }
                        catch (Exception ex) { task.Completion.TrySetException(ex); }
                    }
                }) { IsBackground = true, Priority = priority, Name = "GameGarage RAM worker" };
                thread.Start();
                threads.Add(thread);
            }
        }
        catch
        {
            queue.CompleteAdding();
            foreach (var thread in threads) thread.Join();
            queue.Dispose();
            throw;
        }
    }

    internal Task<TResult> AddTask(Func<TResult> work)
    {
        var completion = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        queue.Add((work, completion));
        return completion.Task;
    }

    public void Dispose()
    {
        queue.CompleteAdding();
        foreach (var thread in threads) thread.Join();
        queue.Dispose();
    }
}
