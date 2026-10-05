using NUnit.Framework;
using VTG.Pool.Network;
using VTG.Pool.UI;

namespace VTG.Pool.Tests
{
    public sealed class OnlineAddressTests
    {
        [Test]
        public void ServerEntry_BecomesWebSocketUrl()
        {
            Assert.AreEqual("wss://pool.example.com/ws", OnlineClient.NormalizeUrl("pool.example.com"));
            Assert.AreEqual("wss://pool.example.com/ws", OnlineClient.NormalizeUrl(" https://pool.example.com "));
            Assert.AreEqual("ws://192.168.1.5:8765/ws", OnlineClient.NormalizeUrl("http://192.168.1.5:8765"));
            Assert.AreEqual("ws://127.0.0.1:8765/ws", OnlineClient.NormalizeUrl("ws://127.0.0.1:8765/ws"));
            Assert.AreEqual("wss://a.b/custom", OnlineClient.NormalizeUrl("wss://a.b/custom"));
            Assert.AreEqual(string.Empty, OnlineClient.NormalizeUrl("  "));
        }

        [Test]
        public void BrowserPage_DefaultsToItsOwnServer()
        {
            Assert.AreEqual("wss://pool.example.com/ws", OnlineClient.DefaultUrlForPage("https://pool.example.com/play/index.html?x=1"));
            Assert.AreEqual("ws://127.0.0.1:8766/ws", OnlineClient.DefaultUrlForPage("http://127.0.0.1:8766/"));
            Assert.AreEqual(string.Empty, OnlineClient.DefaultUrlForPage(string.Empty), "Native builds have no page URL");
            Assert.AreEqual(string.Empty, OnlineClient.DefaultUrlForPage("file:///C:/game/index.html"));
            Assert.AreEqual("wss://pool.example.com/ws", OnlineClient.SuggestedUrl("https://pool.example.com/"), "Self-hosted page: its own server");
            Assert.AreEqual(OnlineClient.PublicServerUrl, OnlineClient.SuggestedUrl("https://html-classic.itch.zone/html/123/index.html"), "itch.io frame: public server");
            Assert.AreEqual(OnlineClient.PublicServerUrl, OnlineClient.SuggestedUrl(string.Empty), "Native builds: public server");
            Assert.AreEqual(string.Empty, OnlineLobbyPanel.InviteLinkFor("https://html-classic.itch.zone/html/123/index.html", "K7P2QX"), "No invite links inside itch.io");
        }

        [Test]
        public void InviteLinks_RoundTrip()
        {
            string link = OnlineLobbyPanel.InviteLinkFor("https://pool.example.com/?room=OLD123", "K7P2QX");
            Assert.AreEqual("https://pool.example.com/?room=K7P2QX", link);
            Assert.AreEqual("K7P2QX", OnlineLobbyPanel.RoomFromUrl(link));
            Assert.AreEqual("K7P2QX", OnlineLobbyPanel.RoomFromUrl("https://pool.example.com/index.html?lang=vi&room=k7p2qx"));
            Assert.IsNull(OnlineLobbyPanel.RoomFromUrl("https://pool.example.com/"));
            Assert.IsNull(OnlineLobbyPanel.RoomFromUrl(string.Empty));
            Assert.AreEqual(string.Empty, OnlineLobbyPanel.InviteLinkFor(string.Empty, "K7P2QX"));
        }
    }
}
