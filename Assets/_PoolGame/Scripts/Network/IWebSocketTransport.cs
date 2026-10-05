using System;

namespace VTG.Pool.Network
{
    public enum TransportState
    {
        Closed,
        Connecting,
        Open
    }

    /// <summary>
    /// Minimal text WebSocket. Events are raised from <see cref="Poll"/> on the main thread.
    /// Native (Editor, Windows, Android, iOS): <see cref="NativeWebSocketTransport"/>; browser: WebGLWebSocketTransport.
    /// </summary>
    public interface IWebSocketTransport : IDisposable
    {
        TransportState State { get; }

        event Action Opened;

        event Action<string> MessageReceived;

        /// <summary>Connection closed or failed; the argument is a short reason.</summary>
        event Action<string> Closed;

        void Connect(string url);

        void Send(string text);

        void Close();

        /// <summary>Dispatches queued events (call every frame).</summary>
        void Poll();
    }

    public static class WebSocketTransportFactory
    {
        public static IWebSocketTransport Create()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new WebGLWebSocketTransport();
#else
            return new NativeWebSocketTransport();
#endif
        }
    }
}
