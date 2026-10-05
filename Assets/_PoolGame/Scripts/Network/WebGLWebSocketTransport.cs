#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;

namespace VTG.Pool.Network
{
    /// <summary>Browser WebSocket through Plugins/WebGL/VTGWebSocket.jslib (polled; no callbacks into C#).</summary>
    public sealed class WebGLWebSocketTransport : IWebSocketTransport
    {
        [DllImport("__Internal")] private static extern int VTGWS_Connect(string url);
        [DllImport("__Internal")] private static extern int VTGWS_State(int id);
        [DllImport("__Internal")] private static extern void VTGWS_Send(int id, string text);
        [DllImport("__Internal")] private static extern void VTGWS_Close(int id);
        [DllImport("__Internal")] private static extern string VTGWS_Next(int id);

        private int id = -1;
        private TransportState state;

        public TransportState State => state;

        public event Action Opened;

        public event Action<string> MessageReceived;

        public event Action<string> Closed;

        public void Connect(string url)
        {
            Close();
            id = VTGWS_Connect(url);
            state = TransportState.Connecting;
        }

        public void Send(string text)
        {
            if (state == TransportState.Open)
            {
                VTGWS_Send(id, text);
            }
        }

        public void Close()
        {
            if (id < 0)
            {
                return;
            }

            VTGWS_Close(id);
            id = -1;
            state = TransportState.Closed;
        }

        public void Poll()
        {
            if (id < 0)
            {
                return;
            }

            for (string text = VTGWS_Next(id); text != null; text = VTGWS_Next(id))
            {
                MessageReceived?.Invoke(text);
            }

            int native = VTGWS_State(id); // 0 connecting, 1 open, 2/3 closing/closed
            if (state == TransportState.Connecting && native == 1)
            {
                state = TransportState.Open;
                Opened?.Invoke();
            }
            else if (native >= 2)
            {
                VTGWS_Close(id);
                id = -1;
                state = TransportState.Closed;
                Closed?.Invoke("closed");
            }
        }

        public void Dispose() => Close();
    }
}
#endif
