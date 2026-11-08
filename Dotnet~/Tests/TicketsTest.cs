using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Omniplay.Tests
{
    /// <summary>The tickets against vectors.json, the copy of omnisense/omniplay's Tests/vectors.json.</summary>
    public class TicketsTest
    {
        private static JsonObject Vectors => (JsonObject)Json.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "vectors.json")));

        private static IEnumerable<JsonObject> List(string key) => ((List<object>)Vectors[key]).Cast<JsonObject>();

        public static IEnumerable<TestCaseData> Signed() => List("sign").Select(v => new TestCaseData(v).SetName("sign: " + v.String("name")));

        public static IEnumerable<TestCaseData> Verified() => List("verify").Select(v => new TestCaseData(v).SetName("verify: " + v.String("name")));

        [Test]
        public void ThereAreFiveSignVectorsAndTwentyTwoVerifyVectorsFiveOfThemValid()
        {
            Assert.That(List("sign").Count(), Is.EqualTo(5));
            Assert.That(List("verify").Count(), Is.EqualTo(22));
            Assert.That(List("verify").Count(v => true.Equals(v["valid"])), Is.EqualTo(5));
        }

        [TestCaseSource(nameof(Signed))]
        public void APayloadSignsToItsVector(JsonObject vector)
        {
            var payload = (JsonObject)vector["payload"];
            Assert.That(Json.Write(payload), Is.EqualTo(vector.String("json")), "JSON as PHP writes it");
            Assert.That(Tickets.Sign(payload, vector.String("secret")), Is.EqualTo(vector.String("ticket")));
            Assert.That(Tickets.SignJson(vector.String("json"), vector.String("secret")), Is.EqualTo(vector.String("ticket")));
        }

        [TestCaseSource(nameof(Verified))]
        public void ATicketHoldsOrNotAsItsVectorSays(JsonObject vector)
        {
            var ticket = Tickets.Verify(vector.String("ticket"), vector.String("secret"), vector.String("room"));
            if (true.Equals(vector["valid"]))
            {
                Assert.That(ticket, Is.Not.Null);
                Assert.That(Json.Write(ticket.Payload), Is.EqualTo(Json.Write(vector["payload"])), "the payload, key by key, in its order");
            }
            else
            {
                Assert.That(ticket, Is.Null);
            }
        }

        [Test]
        public void AnExpiryEqualToNowStillHoldsASecondLaterItDoesNot()
        {
            var token = Tickets.Sign(new JsonObject { { "room", "lobby" }, { "exp", 1800000000L } }, "secret");
            Assert.That(Tickets.Verify(token, "secret", "lobby", 1800000000), Is.Not.Null);
            Assert.That(Tickets.Verify(token, "secret", "lobby", 1800000001), Is.Null);
        }

        [Test]
        public void AMintedTicketLivesItsTtlAndNamesItsPlayer()
        {
            var ticket = Tickets.Mint("s", "eu-1", 60, new JsonObject { { "sub", "player-42" } }, 1800000000);
            var read = Tickets.Verify(ticket.Token, "s", "eu-1", 1800000000);
            Assert.That(read.Expires, Is.EqualTo(1800000060));
            Assert.That(read.Player, Is.EqualTo("player-42"));
            Assert.That(read.Payload.ToString(), Is.EqualTo("{\"sub\":\"player-42\",\"room\":\"eu-1\",\"exp\":1800000060}"));
            Assert.Throws<ArgumentException>(() => Tickets.Mint("s", "r", 60, new JsonObject { { "exp", 1L } }));
            Assert.Throws<ArgumentException>(() => Tickets.Sign(new JsonObject { { "room", "r" }, { "exp", "1" } }, "s"));
            Assert.Throws<ArgumentException>(() => Tickets.SignJson("{}", ""));
        }

        [Test]
        public void JsonIsWrittenAsPhpWritesIt()
        {
            // php -r 'echo json_encode(["a" => "x/y\u{e9}\u{1F3AE}\"\\\n\x01\x7f", "b" => [1, 1.5, 2.0, true, null]]);' on PHP 8.4.27, byte for byte.
            Assert.That(Json.Write(new JsonObject { { "a", "x/y\u00e9\U0001F3AE\"\\\n\u0001\u007f" }, { "b", new List<object> { 1L, 1.5, 2.0, true, null } } }),
                Is.EqualTo("{\"a\":\"x\\/y\\u00e9\\ud83c\\udfae\\\"\\\\\\n\\u0001\u007f\",\"b\":[1,1.5,2,true,null]}"));
            Assert.That(Json.TryParse("{\"a\":1} x", out _), Is.False);
            Assert.That(Json.TryParse("[01]", out _), Is.False);
        }
    }
}
