using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Omniplay
{
    /// <summary>What a server says of itself: "starting", "healthy", "draining" (no new player), "down".</summary>
    public static class Health
    {
        public const string Starting = "starting";
        public const string Healthy = "healthy";
        public const string Draining = "draining";
        public const string Down = "down";
    }

    /// <summary>What the application answered a heartbeat: its status (200 heard, 201 registered, 401, 404, 409) and body.</summary>
    public sealed class HeartbeatAnswer
    {
        public HeartbeatAnswer(int status, string body)
        {
            Status = status;
            Body = body;
        }

        public int Status { get; }
        public string Body { get; }
        public bool Accepted => Status >= 200 && Status < 300;
    }

    /// <summary>
    /// What registers a server the fleet does not know (one started by hand): its address, and what it
    /// may say besides.
    /// </summary>
    public sealed class Registration
    {
        public string Address { get; set; }
        public int? Capacity { get; set; }
        public string Region { get; set; }
        public IList<string> Tags { get; set; }
        public string Engine { get; set; }
    }

    /// <summary>
    /// A server's heartbeat to the application (omnisense/omniplay's docs/heartbeat.md): a POST of the
    /// server's state, with a token signed as a ticket is for the room "heartbeat:&lt;server id&gt;" -
    /// a player's ticket, whose room is the server's id, never passes for one.
    /// </summary>
    public sealed class HeartbeatClient : IDisposable
    {
        public const string Prefix = "heartbeat:";

        private readonly HttpClient http;
        private readonly bool ownsHttp;

        public HeartbeatClient(string url, string secret, string server, string service, HttpClient http = null)
        {
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(server) || string.IsNullOrEmpty(service)) throw new ArgumentException("A heartbeat names its url, its server and its service.");
            if (string.IsNullOrEmpty(secret)) throw new ArgumentException("A heartbeat is signed with a non-empty secret.");
            Url = url;
            Secret = secret;
            Server = server;
            Service = service;
            ownsHttp = http == null;
            this.http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        }

        /// <summary>The heartbeat client of a server a provider started, from its environment; null when it has no heartbeat route.</summary>
        public static HeartbeatClient FromEnvironment(ServerEnvironment environment, HttpClient http = null)
        {
            if (environment == null || string.IsNullOrEmpty(environment.HeartbeatUrl) || string.IsNullOrEmpty(environment.Secret) || string.IsNullOrEmpty(environment.ServerId) || string.IsNullOrEmpty(environment.Service)) return null;
            return new HeartbeatClient(environment.HeartbeatUrl, environment.Secret, environment.ServerId, environment.Service, http);
        }

        public string Url { get; }
        public string Server { get; }
        public string Service { get; }
        private string Secret { get; }

        /// <summary>The token of a server's heartbeat.</summary>
        public static string Token(string server, string secret, long ttl = 60, long? now = null)
        {
            return Tickets.Sign(new JsonObject { { "room", Prefix + server }, { "exp", (now ?? Tickets.Now()) + ttl } }, secret);
        }

        /// <summary>The JSON body of a heartbeat.</summary>
        public string Body(int players, string health = Health.Healthy, Registration registration = null)
        {
            var body = new JsonObject { { "server", Server }, { "service", Service }, { "players", (long)Math.Max(0, players) }, { "health", health ?? Health.Healthy } };
            if (registration != null)
            {
                if (registration.Address != null) body.Set("address", registration.Address);
                if (registration.Capacity.HasValue) body.Set("capacity", (long)registration.Capacity.Value);
                if (registration.Region != null) body.Set("region", registration.Region);
                if (registration.Tags != null) body.Set("tags", new List<object>(registration.Tags));
                if (registration.Engine != null) body.Set("engine", registration.Engine);
            }
            return Json.Write(body);
        }

        /// <summary>One heartbeat. Call it every 10 s or so (the fleet's ttl is 30 s), from the thread that reads the players.</summary>
        public async Task<HeartbeatAnswer> SendAsync(int players, string health = Health.Healthy, Registration registration = null, CancellationToken cancellation = default)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, Url))
            {
                request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + Token(Server, Secret));
                request.Headers.TryAddWithoutValidation("Accept", "application/json");
                request.Content = new StringContent(Body(players, health, registration), Encoding.UTF8, "application/json");
                using (var response = await http.SendAsync(request, cancellation).ConfigureAwait(false))
                {
                    var text = response.Content != null ? await response.Content.ReadAsStringAsync().ConfigureAwait(false) : "";
                    return new HeartbeatAnswer((int)response.StatusCode, text);
                }
            }
        }

        /// <summary>
        /// A heartbeat every interval on a timer of the thread pool, the first at once, for a plain .NET
        /// server: state is read on that thread. A Unity or Godot server calls SendAsync from its own loop
        /// instead. Dispose the result to stop.
        /// </summary>
        public IDisposable Start(Func<(int players, string health)> state, TimeSpan every, Action<HeartbeatAnswer> onAnswer = null, Action<Exception> onError = null)
        {
            var timer = new Timer(async _ =>
            {
                try
                {
                    var (players, health) = state();
                    var answer = await SendAsync(players, health).ConfigureAwait(false);
                    onAnswer?.Invoke(answer);
                }
                catch (Exception e)
                {
                    onError?.Invoke(e);
                }
            }, null, TimeSpan.Zero, every);
            return timer;
        }

        public void Dispose()
        {
            if (ownsHttp) http.Dispose();
        }
    }
}
