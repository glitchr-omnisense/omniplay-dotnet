using System.Collections.Generic;

namespace Omniplay
{
    /// <summary>
    /// A server's door: a ticket is admitted when it holds for this server's room with its secret, and
    /// once - the same token presented again before it expires is refused, so that a ticket seen on the
    /// wire opens no second connection. The tokens admitted are forgotten once expired. Safe to call
    /// from several threads.
    /// </summary>
    public sealed class TicketGate
    {
        private readonly Dictionary<string, long> admitted = new Dictionary<string, long>();
        private readonly object guard = new object();

        public TicketGate(string secret, string room)
        {
            Secret = secret;
            Room = room;
        }

        /// <summary>The gate of a server a provider started, from its environment (secret and room).</summary>
        public static TicketGate FromEnvironment(ServerEnvironment environment) => new TicketGate(environment?.Secret, environment?.Room);

        public string Room { get; }
        private string Secret { get; }

        /// <summary>Whether the gate can admit anyone: a secret and a room.</summary>
        public bool Configured => !string.IsNullOrEmpty(Secret) && !string.IsNullOrEmpty(Room);

        /// <summary>The ticket when it holds and was not admitted before; null otherwise.</summary>
        public Ticket Admit(string token, long? now = null)
        {
            if (!Configured) return null;
            var at = now ?? Tickets.Now();
            var ticket = Tickets.Verify(token, Secret, Room, at);
            if (ticket == null) return null;
            lock (guard)
            {
                if (admitted.Count > 256) Forget(at);
                if (admitted.ContainsKey(token)) return null;
                admitted[token] = ticket.Expires;
            }
            return ticket;
        }

        private void Forget(long now)
        {
            var expired = new List<string>();
            foreach (var pair in admitted)
            {
                if (pair.Value < now) expired.Add(pair.Key);
            }
            foreach (var token in expired) admitted.Remove(token);
        }
    }
}
