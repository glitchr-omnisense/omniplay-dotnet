using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Omniplay.Tests
{
    /// <summary>The README's first call: Examples/FirstCall/Program.cs, shown in the README as it is (CI also runs it).</summary>
    public class FirstCallTest
    {
        private static string Repository => Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../.."));

        [Test]
        public void TheReadmeShowsTheExampleAsItIs()
        {
            var example = File.ReadAllText(Path.Combine(Repository, "Dotnet~/Examples/FirstCall/Program.cs")).Replace("\r\n", "\n");
            var readme = File.ReadAllText(Path.Combine(Repository, "README.md")).Replace("\r\n", "\n");
            Assert.That(readme, Does.Contain("```csharp\n" + example + "```"));
        }

        [Test]
        public void TheExampleSaysWhatItsCommentsSay()
        {
            const string fromPhp = "eyJzdWIiOiJwbGF5ZXItNDIiLCJuYW1lIjoiQWRhIiwicm9vbSI6ImV1LTEtYTNmOSIsImV4cCI6NDEwMjQ0NDgwMH0.0bb9f63e3f6c9d7237c18855738470bae0f54410d4d1177ee87cc65c4608a57c";
            const string secret = "s3cr3t-shared-with-the-servers";
            Assert.That(Tickets.Verify(fromPhp, secret, "eu-1-a3f9")?.Player, Is.EqualTo("player-42"));
            Assert.That(Tickets.Verify(fromPhp, secret, "eu-1-b7c2"), Is.Null);
            var ticket = Tickets.Mint(secret, "eu-1-a3f9", ttl: 60, claims: new Dictionary<string, object> { ["sub"] = "player-7" });
            Assert.That(Tickets.Verify(ticket.Token, secret, "eu-1-a3f9")?.Player, Is.EqualTo("player-7"));
        }
    }
}
