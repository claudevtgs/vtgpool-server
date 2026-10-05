#if !(UNITY_WEBGL && !UNITY_EDITOR)
using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VTG.Pool.Network
{
    /// <summary>
    /// <see cref="ClientWebSocket"/> transport. Receiving and sending run on background tasks; everything the game
    /// sees is queued and raised from <see cref="Poll"/> on the main thread.
    /// </summary>
    public sealed class NativeWebSocketTransport : IWebSocketTransport
    {
        private readonly ConcurrentQueue<Action> events = new ConcurrentQueue<Action>();
        private readonly ConcurrentQueue<string> outgoing = new ConcurrentQueue<string>();
        private readonly SemaphoreSlim sendSignal = new SemaphoreSlim(0);
        private ClientWebSocket socket;
        private CancellationTokenSource cancel;
        private volatile TransportState state;
        private int closedRaised;

        public TransportState State => state;

        public event Action Opened;

        public event Action<string> MessageReceived;

        public event Action<string> Closed;

        public void Connect(string url)
        {
            Close();
            closedRaised = 0;
            socket = new ClientWebSocket();
            socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
            cancel = new CancellationTokenSource();
            state = TransportState.Connecting;
            ClientWebSocket current = socket;
            CancellationToken token = cancel.Token;
            Task.Run(() => RunAsync(current, url, token));
        }

        public void Send(string text)
        {
            if (state != TransportState.Open)
            {
                return;
            }

            outgoing.Enqueue(text);
            sendSignal.Release();
        }

        public void Close()
        {
            if (cancel == null)
            {
                return;
            }

            ClientWebSocket current = socket;
            CancellationTokenSource source = cancel;
            socket = null;
            cancel = null;
            state = TransportState.Closed;
            Interlocked.Exchange(ref closedRaised, 1);
            Task.Run(async () =>
            {
                try
                {
                    if (current.State == WebSocketState.Open)
                    {
                        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
                        {
                            await current.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", timeout.Token);
                        }
                    }
                }
                catch (Exception)
                {
                    // Closing a dead socket.
                }
                finally
                {
                    source.Cancel();
                    current.Dispose();
                }
            });
        }

        public void Poll()
        {
            while (events.TryDequeue(out Action action))
            {
                action();
            }
        }

        public void Dispose() => Close();

        private async Task RunAsync(ClientWebSocket current, string url, CancellationToken token)
        {
            string reason = "closed";
            try
            {
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    // Free cloud hosts take up to a minute to wake up.
                    timeout.CancelAfter(TimeSpan.FromSeconds(90));
                    await current.ConnectAsync(new Uri(url), timeout.Token);
                }

                state = TransportState.Open;
                events.Enqueue(() => Opened?.Invoke());
                Task sender = Task.Run(() => SendLoopAsync(current, token));
                await ReceiveLoopAsync(current, token);
                sendSignal.Release();
                await sender;
            }
            catch (OperationCanceledException)
            {
                reason = token.IsCancellationRequested ? "closed" : "timeout";
            }
            catch (Exception exception)
            {
                reason = exception.Message;
            }

            RaiseClosed(reason);
        }

        private async Task ReceiveLoopAsync(ClientWebSocket current, CancellationToken token)
        {
            var buffer = new byte[16 * 1024];
            var message = new StringBuilder();
            while (!token.IsCancellationRequested && current.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result = await current.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }

                message.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                if (result.EndOfMessage)
                {
                    string text = message.ToString();
                    message.Clear();
                    events.Enqueue(() => MessageReceived?.Invoke(text));
                }
            }
        }

        private async Task SendLoopAsync(ClientWebSocket current, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && current.State == WebSocketState.Open)
                {
                    await sendSignal.WaitAsync(token);
                    while (current.State == WebSocketState.Open && outgoing.TryDequeue(out string text))
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes(text);
                        await current.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token);
                    }
                }
            }
            catch (Exception)
            {
                // The receive loop reports the failure.
            }
        }

        private void RaiseClosed(string reason)
        {
            state = TransportState.Closed;
            if (Interlocked.Exchange(ref closedRaised, 1) == 0)
            {
                events.Enqueue(() => Closed?.Invoke(reason));
            }
        }
    }
}
#endif
