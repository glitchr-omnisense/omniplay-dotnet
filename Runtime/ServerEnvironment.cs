using System;
using System.Globalization;

namespace Omniplay
{
    /// <summary>
    /// What a server started by a provider finds in its environment (omniplay/docker sets them): its
    /// ticket secret, its id and room, its service, capacity, port, declared engine and heartbeat route.
    /// A server started by hand sets the same variables, or gives the values itself.
    /// </summary>
    public sealed class ServerEnvironment
    {
        public string Secret { get; set; }
        public string ServerId { get; set; }
        public string Room { get; set; }
        public string Service { get; set; }
        public int? Capacity { get; set; }
        public int? Port { get; set; }
        public string Engine { get; set; }
        public string HeartbeatUrl { get; set; }

        /// <param name="read">a variable by its name (Environment.GetEnvironmentVariable when null)</param>
        /// <param name="secretVariable">the variable holding the secret (a provider's secret_env)</param>
        public static ServerEnvironment Read(Func<string, string> read = null, string secretVariable = "OMNIPLAY_TICKET_SECRET")
        {
            read = read ?? Environment.GetEnvironmentVariable;
            string Text(string name)
            {
                var value = read(name);
                return string.IsNullOrEmpty(value) ? null : value;
            }
            int? Number(string name) => int.TryParse(Text(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : (int?)null;
            var server = Text("OMNIPLAY_SERVER_ID");
            return new ServerEnvironment
            {
                Secret = Text(secretVariable),
                ServerId = server,
                Room = Text("OMNIPLAY_ROOM") ?? server,
                Service = Text("OMNIPLAY_SERVICE"),
                Capacity = Number("OMNIPLAY_CAPACITY"),
                Port = Number("OMNIPLAY_PORT"),
                Engine = Text("OMNIPLAY_ENGINE"),
                HeartbeatUrl = Text("OMNIPLAY_HEARTBEAT_URL"),
            };
        }

        /// <summary>The ticket, when it holds for this server's room with its secret; null otherwise (and when the secret or room is missing).</summary>
        public Ticket Verify(string token, long? now = null) => string.IsNullOrEmpty(Secret) || string.IsNullOrEmpty(Room) ? null : Tickets.Verify(token, Secret, Room, now);
    }
}
