using System;
using System.Collections.Generic;
using Omniplay;

// A ticket the PHP application signed (omnisense/omniplay's Tickets, recorded in its vectors):
var fromPhp = "eyJzdWIiOiJwbGF5ZXItNDIiLCJuYW1lIjoiQWRhIiwicm9vbSI6ImV1LTEtYTNmOSIsImV4cCI6NDEwMjQ0NDgwMH0.0bb9f63e3f6c9d7237c18855738470bae0f54410d4d1177ee87cc65c4608a57c";
var secret = "s3cr3t-shared-with-the-servers";   // OMNIPLAY_TICKET_SECRET on a real server
Console.WriteLine(Tickets.Verify(fromPhp, secret, "eu-1-a3f9")?.Player);    // player-42: the server eu-1-a3f9 lets the player in
Console.WriteLine(Tickets.Verify(fromPhp, secret, "eu-1-b7c2") == null);   // True: another server refuses it

var ticket = Tickets.Mint(secret, "eu-1-a3f9", ttl: 60, claims: new Dictionary<string, object> { ["sub"] = "player-7" });
Console.WriteLine(Tickets.Verify(ticket.Token, secret, "eu-1-a3f9")?.Player);   // player-7
