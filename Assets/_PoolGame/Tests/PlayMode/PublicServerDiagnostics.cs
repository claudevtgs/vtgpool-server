using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VTG.Pool.Network;

namespace VTG.Pool.Tests
{
    /// <summary>
    /// Talks to the real public room server (<see cref="OnlineClient.PublicServerUrl"/>). Explicit: needs the internet
    /// and may wait up to a minute while a sleeping free server wakes up.
    /// </summary>
    [Explicit("Uses the public internet server")]
    public sealed class PublicServerDiagnostics
    {
        [UnityTest]
        public IEnumerator PublicServer_TwoClientsMeetAndRelay()
        {
            var goA = new GameObject("PublicA");
            var goB = new GameObject("PublicB");
            try
            {
                OnlineClient a = goA.AddComponent<OnlineClient>();
                OnlineClient b = goB.AddComponent<OnlineClient>();
                string code = null;
                string error = null;
                a.RoomCreated += c => code = c;
                a.Error += e => error = e;
                b.Error += e => error = e;
                float start = Time.realtimeSinceStartup;
                a.CreateRoom(OnlineClient.PublicServerUrl, "DiagA", 1);
                while (code == null && error == null && Time.realtimeSinceStartup - start < 100f) yield return null;
                Assert.IsNull(error, "Server error");
                Assert.IsNotNull(code, "Room created");
                Debug.Log($"[PublicServer] room {code} after {Time.realtimeSinceStartup - start:0.0}s");
                b.JoinRoom(OnlineClient.PublicServerUrl, code, "DiagB");
                while (!(a.InMatch && b.InMatch) && error == null && Time.realtimeSinceStartup - start < 120f) yield return null;
                Assert.IsTrue(a.InMatch && b.InMatch, "Both in the match");
                string got = null;
                b.GameMessage += m => got = m;
                float sent = Time.realtimeSinceStartup;
                a.SendGame("{\"type\":\"aim\",\"yaw\":1}");
                while (got == null && Time.realtimeSinceStartup - sent < 10f) yield return null;
                Assert.IsNotNull(got, "Relay");
                Debug.Log($"[PublicServer] relay {(Time.realtimeSinceStartup - sent) * 1000f:0} ms");
                a.Leave();
                b.Leave();
            }
            finally
            {
                Object.Destroy(goA);
                Object.Destroy(goB);
            }
        }
    }
}
