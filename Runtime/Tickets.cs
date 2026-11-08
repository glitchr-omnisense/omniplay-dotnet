using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Omniplay
{
    /// <summary>
    /// A ticket that held: its token, its room, its expiry (Unix seconds) and its whole payload (room,
    /// exp and the application's claims, "sub" the player when omniplay seated them).
    /// </summary>
    public sealed class Ticket
    {
        public Ticket(string token, string room, long expires, JsonObject payload)
        {
            Token = token;
            Room = room;
            Expires = expires;
            Payload = payload;
        }

        public string Token { get; }
        public string Room { get; }
        public long Expires { get; }
        public JsonObject Payload { get; }

        /// <summary>The player, as omniplay's seating names them ("sub"); null when the ticket has none.</summary>
        public string Player => Payload.String("sub");
    }

    /// <summary>
    /// The tickets' one format, as omnisense/omniplay's Ticket\TicketSigner writes it:
    /// base64url(json payload) "." hex(hmac_sha256(that base64url, secret)). The payload names its room
    /// ("room", a string) and its expiry ("exp", Unix seconds). The signature is over the base64url
    /// text as it stands: a verifier never encodes anything again.
    /// </summary>
    public static class Tickets
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        /// <summary>Unix seconds now.</summary>
        public static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        /// <summary>
        /// The ticket's payload when its signature holds, it is for this room and it has not expired;
        /// null otherwise. An empty secret verifies nothing; the signature is what follows the last dot,
        /// lower-case hex compared in constant time; "room" must be a string equal to the room served;
        /// "exp" a number, its integer part compared (an expiry equal to now still holds).
        /// </summary>
        public static Ticket Verify(string token, string secret, string room, long? now = null)
        {
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(secret) || room == null) return null;
            var dot = token.LastIndexOf('.');
            if (dot < 0) return null;
            var body = token.Substring(0, dot);
            if (!FixedTimeEquals(Hmac(body, secret), token.Substring(dot + 1))) return null;
            var bytes = Base64UrlDecode(body);
            if (bytes == null) return null;
            string text;
            try
            {
                text = Utf8.GetString(bytes);
            }
            catch (ArgumentException)
            {
                return null;
            }
            if (!Json.TryParse(text, out var parsed) || !(parsed is JsonObject payload)) return null;
            if (!(payload.TryGetValue("room", out var r) && r is string ticketRoom && string.Equals(ticketRoom, room, StringComparison.Ordinal))) return null;
            long expires;
            if (payload.TryGetValue("exp", out var exp) && exp is long whole) expires = whole;
            else if (exp is double d && !double.IsNaN(d) && !double.IsInfinity(d) && Math.Abs(d) < 9.2e18) expires = (long)Math.Truncate(d);
            else return null;
            if (expires < (now ?? Now())) return null;
            return new Ticket(token, room, expires, payload);
        }

        /// <summary>A ticket for this payload: it names its room (a non-empty string) and its expiry (an integer).</summary>
        public static string Sign(JsonObject payload, string secret)
        {
            if (payload == null || !(payload.TryGetValue("room", out var room) && room is string text && text.Length > 0))
                throw new ArgumentException("A ticket names its room: \"room\", a non-empty string.");
            if (!(payload.TryGetValue("exp", out var exp) && (exp is long || exp is int)))
                throw new ArgumentException("A ticket names its expiry: \"exp\", an integer (Unix seconds).");
            return SignJson(Json.Write(payload), secret);
        }

        /// <summary>A ticket over this JSON text as it is.</summary>
        public static string SignJson(string json, string secret)
        {
            if (string.IsNullOrEmpty(secret)) throw new ArgumentException("A ticket is signed with a non-empty secret.");
            var body = Base64UrlEncode(Encoding.UTF8.GetBytes(json));
            return body + "." + Hmac(body, secret);
        }

        /// <summary>A ticket to this room living ttl seconds: the claims, then room, then exp.</summary>
        public static Ticket Mint(string secret, string room, long ttl = 60, IEnumerable<KeyValuePair<string, object>> claims = null, long? now = null)
        {
            var payload = new JsonObject();
            if (claims != null)
            {
                foreach (var claim in claims)
                {
                    if (claim.Key == "room" || claim.Key == "exp") throw new ArgumentException("\"room\" and \"exp\" are the ticket's own: they are not claims.");
                    payload.Set(claim.Key, claim.Value);
                }
            }
            var expires = (now ?? Now()) + ttl;
            payload.Set("room", room).Set("exp", expires);
            return new Ticket(Sign(payload, secret), room, expires, payload);
        }

        private static string Hmac(string body, string secret)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
            {
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
                var hex = new StringBuilder(hash.Length * 2);
                foreach (var b in hash) hex.Append(b.ToString("x2"));
                return hex.ToString();
            }
        }

        private static bool FixedTimeEquals(string expected, string given)
        {
            if (given == null || given.Length != expected.Length) return false;
            var difference = 0;
            for (var i = 0; i < expected.Length; i++) difference |= expected[i] ^ given[i];
            return difference == 0;
        }

        private static string Base64UrlEncode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static byte[] Base64UrlDecode(string text)
        {
            var base64 = text.Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4)
            {
                case 1: return null;
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }
            try
            {
                return Convert.FromBase64String(base64);
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}
