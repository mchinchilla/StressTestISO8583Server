using System;
using System.Buffers;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StressTestISO8583Server;

public sealed class IsoMessageSender : IDisposable
{
    private const int ReadBufferSize = 8192;
    private readonly string _serverAddress;
    private readonly int _serverPort;
    private readonly bool _useTls;

    public IsoMessageSender(string serverAddress, int serverPort, bool useTls)
    {
        _serverAddress = serverAddress;
        _serverPort = serverPort;
        _useTls = useTls;
    }

    public async Task<bool> SendAsync(byte[] message, CancellationToken cancellationToken = default)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(_serverAddress, _serverPort, cancellationToken);

        if (_useTls)
        {
            await using var sslStream = new SslStream(client.GetStream(), false,
                (_, _, _, _) => true);

            await sslStream.AuthenticateAsClientAsync(_serverAddress);
            return await SendAndReceiveAsync(sslStream, message, client.ReceiveBufferSize, cancellationToken);
        }
        else
        {
            var stream = client.GetStream();
            return await SendAndReceiveAsync(stream, message, client.ReceiveBufferSize, cancellationToken);
        }
    }

    private static async Task<bool> SendAndReceiveAsync(System.IO.Stream stream, byte[] message,
        int receiveBufferSize, CancellationToken cancellationToken)
    {
        await stream.WriteAsync(message, cancellationToken);
        await stream.FlushAsync(cancellationToken);

        byte[] buffer = ArrayPool<byte>.Shared.Rent(ReadBufferSize);
        try
        {
            int bytesRead = await stream.ReadAsync(buffer.AsMemory(0, Math.Min(receiveBufferSize, ReadBufferSize)), cancellationToken);
            return bytesRead > 0;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public void Dispose()
    {
        // No persistent resources to dispose — each SendAsync manages its own TcpClient.
        // This class implements IDisposable for future extensibility (e.g., connection pooling).
    }
}
