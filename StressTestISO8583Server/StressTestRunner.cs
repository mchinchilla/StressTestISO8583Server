using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace StressTestISO8583Server;

public readonly record struct MessageResult(bool Success, int BatchIndex, int MessageIndex);

public sealed class StressTestRunner
{
    private readonly IsoMessageSender _sender;
    private readonly byte[] _message;
    private readonly int _quantity;
    private readonly int _batchSize;
    private int _successCount;
    private int _failedCount;

    public int SuccessCount => _successCount;
    public int FailedCount => _failedCount;
    public int TotalMessages => _quantity * _batchSize;

    public StressTestRunner(IsoMessageSender sender, byte[] message, int quantity, int batchSize)
    {
        _sender = sender;
        _message = message;
        _quantity = quantity;
        _batchSize = batchSize;
    }

    public async Task RunAsync(Action<MessageResult> onMessageCompleted, CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateBounded<(int batchIndex, int messageIndex)>(
            new BoundedChannelOptions(_batchSize * 2)
            {
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.Wait
            });

        // Producer: enqueue all messages
        var producerTask = Task.Run(async () =>
        {
            try
            {
                for (int batch = 0; batch < _quantity; batch++)
                {
                    for (int msg = 0; msg < _batchSize; msg++)
                    {
                        await channel.Writer.WriteAsync((batch, msg), cancellationToken);
                    }
                }
            }
            finally
            {
                channel.Writer.Complete();
            }
        }, cancellationToken);

        // Consumers: process messages with controlled concurrency
        using var semaphore = new SemaphoreSlim(_batchSize);
        var consumerTasks = new System.Collections.Generic.List<Task>();

        await foreach (var (batchIndex, messageIndex) in channel.Reader.ReadAllAsync(cancellationToken))
        {
            await semaphore.WaitAsync(cancellationToken);

            consumerTasks.Add(ProcessMessageAsync(batchIndex, messageIndex, semaphore, onMessageCompleted, cancellationToken));
        }

        await producerTask;
        await Task.WhenAll(consumerTasks);
    }

    private async Task ProcessMessageAsync(int batchIndex, int messageIndex, SemaphoreSlim semaphore,
        Action<MessageResult> onMessageCompleted, CancellationToken cancellationToken)
    {
        try
        {
            bool success = await _sender.SendAsync(_message, cancellationToken);

            if (success)
                Interlocked.Increment(ref _successCount);
            else
                Interlocked.Increment(ref _failedCount);

            onMessageCompleted(new MessageResult(success, batchIndex, messageIndex));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            Interlocked.Increment(ref _failedCount);
            onMessageCompleted(new MessageResult(false, batchIndex, messageIndex));
        }
        finally
        {
            semaphore.Release();
        }
    }
}
