using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Omniplay.Tests
{
    public class HeartbeatTest
    {
        private const string Secret = "not-a-real-secret";

        [Test]
        public void AHeartbeatTokenIsTheVectorOmniplaySigned()
        {
            var vectors = (JsonObject)Json.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "vectors.json")));
            var vector = ((List<object>)vectors["sign"]).Cast<JsonObject>().Single(v => ((JsonObject)v["payload"]).String("room") == "heartbeat:eu-1-a3f9");
            Assert.That(HeartbeatClient.Token("eu-1-a3f9", vector.String("secret"), 60, 4102444800 - 60), Is.EqualTo(vector.String("ticket")));
        }

        [Test]
        public async Task AHeartbeatPostsTheServersStateWithATokenForItsHeartbeatRoomOnly()
        {
            var application = new Application(HttpStatusCode.Created, "{\"status\":201,\"reason\":\"Registered.\"}");
            using (var client = new HeartbeatClient("https://www.example.org/omniplay/heartbeat", Secret, "home-1", "game", new HttpClient(application)))
            {
                var answer = await client.SendAsync(3, Health.Starting, new Registration { Address = "198.51.100.4:7777", Capacity = 4, Tags = new[] { "casual" }, Engine = "dotnet" });

                Assert.That(answer.Status, Is.EqualTo(201));
                Assert.That(answer.Accepted, Is.True);
            }
            var request = application.Received.Single();
            Assert.That(request.Method, Is.EqualTo("POST"));
            Assert.That(request.Url, Is.EqualTo("https://www.example.org/omniplay/heartbeat"));
            Assert.That(request.Body, Is.EqualTo("{\"server\":\"home-1\",\"service\":\"game\",\"players\":3,\"health\":\"starting\",\"address\":\"198.51.100.4:7777\",\"capacity\":4,\"tags\":[\"casual\"],\"engine\":\"dotnet\"}"));
            Assert.That(request.Authorization, Does.StartWith("Bearer "));
            var token = request.Authorization.Substring("Bearer ".Length);
            Assert.That(Tickets.Verify(token, Secret, "heartbeat:home-1"), Is.Not.Null);
            Assert.That(Tickets.Verify(token, Secret, "home-1"), Is.Null, "not a player's ticket");
        }

        [Test]
        public void TheBeatGoesOnEveryIntervalWithTheStateOfTheMoment()
        {
            var application = new Application(HttpStatusCode.OK, "{\"status\":200}");
            var players = 0;
            var answers = 0;
            using (var client = new HeartbeatClient("https://www.example.org/omniplay/heartbeat", Secret, "eu-1", "game", new HttpClient(application)))
            using (client.Start(() => (Interlocked.Increment(ref players) - 1, Health.Healthy), TimeSpan.FromMilliseconds(30), _ => Interlocked.Increment(ref answers)))
            {
                Thread.Sleep(200);
            }
            Assert.That(application.Received.Count, Is.GreaterThanOrEqualTo(3), application.Received.Count + " heartbeats in 200 ms at one every 30 ms");
            Assert.That(application.Received.Take(3).Select(r => ((JsonObject)Json.Parse(r.Body))["players"]), Is.EquivalentTo(new object[] { 0L, 1L, 2L }));
        }

        [Test]
        public void WhatAProviderHandsAServerIsReadFromItsEnvironment()
        {
            var variables = new Dictionary<string, string>
            {
                ["GAME_TICKET_SECRET"] = Secret,
                ["OMNIPLAY_SERVER_ID"] = "omniplay-game-1",
                ["OMNIPLAY_SERVICE"] = "game",
                ["OMNIPLAY_CAPACITY"] = "8",
                ["OMNIPLAY_PORT"] = "7777",
                ["OMNIPLAY_HEARTBEAT_URL"] = "https://www.example.org/omniplay/heartbeat",
            };
            var environment = ServerEnvironment.Read(name => variables.TryGetValue(name, out var value) ? value : null, "GAME_TICKET_SECRET");

            Assert.That(new object[] { environment.Secret, environment.ServerId, environment.Room, environment.Service, environment.Capacity, environment.Port, environment.Engine }, Is.EqualTo(new object[] { Secret, "omniplay-game-1", "omniplay-game-1", "game", 8, 7777, null }));
            var ticket = Tickets.Mint(Secret, "omniplay-game-1", 60, new JsonObject { { "sub", "player-42" } });
            Assert.That(environment.Verify(ticket.Token)?.Player, Is.EqualTo("player-42"));
            Assert.That(HeartbeatClient.FromEnvironment(environment)?.Server, Is.EqualTo("omniplay-game-1"));
        }

        /// <summary>The application's heartbeat route, answering in this process.</summary>
        private sealed class Application : HttpMessageHandler
        {
            private readonly HttpStatusCode status;
            private readonly string body;

            public Application(HttpStatusCode status, string body)
            {
                this.status = status;
                this.body = body;
            }

            public List<(string Method, string Url, string Authorization, string Body)> Received { get; } = new List<(string, string, string, string)>();

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var text = await request.Content.ReadAsStringAsync(cancellationToken);
                lock (Received)
                {
                    Received.Add((request.Method.Method, request.RequestUri.ToString(), request.Headers.Authorization?.ToString(), text));
                }
                return new HttpResponseMessage(status) { Content = new StringContent(body) };
            }
        }
    }
}

namespace Omniplay.Tests
{
    public class TicketGateTest
    {
        [Test]
        public void ATicketOpensOneConnectionForItsRoomOnly()
        {
            var gate = new TicketGate("not-a-real-secret", "eu-1");
            var ticket = Tickets.Mint("not-a-real-secret", "eu-1", 60, new JsonObject { { "sub", "player-42" } }, 1800000000);

            Assert.That(gate.Admit(ticket.Token, 1800000000)?.Player, Is.EqualTo("player-42"));
            Assert.That(gate.Admit(ticket.Token, 1800000001), Is.Null, "the same ticket a second time");
            Assert.That(gate.Admit(Tickets.Mint("not-a-real-secret", "eu-2", 60, null, 1800000000).Token, 1800000000), Is.Null, "another server's");
            Assert.That(new TicketGate(null, "eu-1").Admit(ticket.Token, 1800000000), Is.Null, "no secret: nobody");
            Assert.That(new TicketGate(null, "eu-1").Configured, Is.False);
        }
    }
}
