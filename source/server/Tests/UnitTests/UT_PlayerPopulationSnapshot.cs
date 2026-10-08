using System;
using System.Linq;
using System.Text.Json;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_PlayerPopulationSnapshot
    {
        [Test]
        public void HumanPlayersRoundTripSeparatelyFromBotsWithoutPrivateAccountData()
        {
            var players = new[] { new AutonomousBotDashboard.PlayerStatus("Host", 1, "Briton", 0, "Armsman", 10, "Camelot"),
                new AutonomousBotDashboard.PlayerStatus("Guest", 3, "Elf", 1, "Eldritch", 12, "Mag Mell") };
            var snapshot = new AutonomousBotDashboard.Snapshot(DateTime.UtcNow, true, "test", [], players);
            string json = JsonSerializer.Serialize(snapshot);
            var decoded = JsonSerializer.Deserialize<AutonomousBotDashboard.Snapshot>(json);
            Assert.That(decoded.Players, Is.EqualTo(players));
            Assert.That(decoded.Bots, Is.Empty);
            using var document = JsonDocument.Parse(json);
            Assert.That(document.RootElement.GetProperty("Players")[0].EnumerateObject().Select(p => p.Name),
                Is.EquivalentTo(new[] { "Name", "Realm", "RaceName", "Gender", "ClassName", "Level", "ZoneName" }));
        }
        [Test]
        public void OldSnapshotRemainsReadableWithUnknownPlayerPopulation()
        {
            var old = JsonSerializer.Deserialize<AutonomousBotDashboard.Snapshot>("{\"Running\":true,\"Bots\":[]}");
            Assert.That(old.Players, Is.Null);
            Assert.That(old.Bots, Is.Empty);
        }
    }
}
