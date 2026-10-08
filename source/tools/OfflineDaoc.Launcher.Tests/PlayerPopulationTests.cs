#nullable enable
using System.Collections;
using System.Reflection;
using System.Text.Json;
using NUnit.Framework;

[TestFixture]
public class PlayerPopulationTests
{
    private static readonly Type Form = Assembly.Load("OfflineDAoC").GetType("OfflineDaoc.Launcher.MainForm")!;
    private static readonly Type Snapshot = Form.GetNestedType("LiveBotSnapshot", BindingFlags.NonPublic)!;
    private static readonly MethodInfo Read = Form.GetMethod("ReadPlayerRows", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static IList? Rows(DateTime updated, bool running, object? players, bool includePlayers = true)
    {
        var data = new Dictionary<string, object?> { ["UpdatedUtc"] = updated, ["Running"] = running,
            ["RequestId"] = "test", ["Bots"] = Array.Empty<object>() };
        if (includePlayers) data["Players"] = players;
        object snapshot = JsonSerializer.Deserialize(JsonSerializer.Serialize(data), Snapshot)!;
        return (IList?)Read.Invoke(null, [snapshot, DateTime.UtcNow]);
    }
    private static object Player(string name, int realm) => new { Name = name, Realm = realm,
        RaceName = "Elf", Gender = 0, ClassName = "Eldritch", Level = 12, ZoneName = "Mag Mell" };
    private static object? Value(object row, string property) => row.GetType().GetProperty(property)!.GetValue(row);

    [Test]
    public void FreshPlayersAreVisibleWithNoBotManagementIdentity()
    {
        IList rows = Rows(DateTime.UtcNow, true, new[] { Player("Host", 1), Player("Guest", 3) })!;
        Assert.That(rows.Count, Is.EqualTo(2));
        Assert.That(Value(rows[1]!, "Name"), Is.EqualTo("Guest"));
        Assert.That(Value(rows[1]!, "Realm"), Is.EqualTo("Hibernia"));
        Assert.That(Value(rows[1]!, "ZoneName"), Is.EqualTo("Mag Mell"));
        foreach (object row in rows)
        {
            Assert.That(Value(row, "IsOnline"), Is.True);
            Assert.That(Value(row, "IsPlayer"), Is.True);
            Assert.That(Value(row, "PopulationType"), Is.EqualTo("Player"));
            Assert.That(Value(row, "CanDelete"), Is.False);
            Assert.That(Value(row, "BotId"), Is.Null, "Bot teleport and deletion require a bot ID");
            Assert.That(Value(row, "GroupId"), Is.EqualTo(""), "Players must not enter bot group controls");
        }
    }
    [Test]
    public void MissingOrStalePlayersAreUnknownNotZero()
    {
        Assert.That(Rows(DateTime.UtcNow, true, null, false), Is.Null, "Old server snapshot");
        Assert.That(Rows(DateTime.UtcNow, true, null), Is.Null);
        Assert.That(Rows(DateTime.UtcNow.AddMinutes(-1), true, new[] { Player("Guest", 3) }), Is.Null);
        Assert.That(Rows(DateTime.UtcNow, false, new[] { Player("Guest", 3) }), Is.Null);
    }
    [Test]
    public void FreshEmptySnapshotClearsDisconnectedPlayers()
    {
        Assert.That(Rows(DateTime.UtcNow, true, new[] { Player("Guest", 3) })!.Count, Is.EqualTo(1));
        Assert.That(Rows(DateTime.UtcNow, true, Array.Empty<object>()), Is.Empty);
    }

    [Test, Apartment(ApartmentState.STA), NonParallelizable]
    public void PlayerRowsUsePopulationFiltersAndNeverEnableBotDeletion()
    {
        using var form = (System.Windows.Forms.Form)Activator.CreateInstance(Form)!;
        const BindingFlags hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        T Field<T>(string name) => (T)Form.GetField(name, hidden)!.GetValue(form)!;
        form.ShowInTaskbar = false;
        form.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
        form.Location = new System.Drawing.Point(-20_000, -20_000);
        form.Show();
        var deadline = DateTime.UtcNow.AddSeconds(5);
        do
        {
            System.Windows.Forms.Application.DoEvents();
            Thread.Sleep(5);
        } while (Field<bool>("_refreshing") && DateTime.UtcNow < deadline);
        Assert.That(Field<bool>("_refreshing"), Is.False);
        IList bots = Field<IList>("_bots");
        foreach (object row in Rows(DateTime.UtcNow, true, new[] { Player("Host", 1), Player("Guest", 3) })!)
            bots.Add(row);
        Form.GetMethod("ApplyFilter", hidden)!.Invoke(form, null);
        var source = Field<System.Windows.Forms.BindingSource>("_botSource");
        Assert.That(source.Count, Is.EqualTo(2));
        Field<System.Windows.Forms.ComboBox>("_realmFilter").SelectedItem = "Hibernia";
        Assert.That(source.Count, Is.EqualTo(1));
        Field<System.Windows.Forms.TextBox>("_search").Text = "Host";
        Assert.That(source.Count, Is.Zero);
        Field<System.Windows.Forms.TextBox>("_search").Text = "Guest";
        Field<System.Windows.Forms.CheckBox>("_onlineOnly").Checked = true;
        Assert.That(source.Count, Is.EqualTo(1));
        var grid = Field<System.Windows.Forms.DataGridView>("_grid");
        grid.CurrentCell = grid.Rows[0].Cells[0];
        Form.GetMethod("UpdateDeleteButton", hidden)!.Invoke(form, null);
        Assert.That(Field<System.Windows.Forms.Button>("_deleteBotButton").Enabled, Is.False);
        Field<System.Windows.Forms.ComboBox>("_realmFilter").SelectedItem = "All realms";
        Field<System.Windows.Forms.TextBox>("_search").Text = "";
        Field<System.Windows.Forms.Label>("_onlineValue").Text = "2";
        Field<System.Windows.Forms.Label>("_footer").Text = "Preview fixture: 2 players · 0 bots online";
        form.PerformLayout();
        using var preview = new System.Drawing.Bitmap(form.Width, form.Height);
        form.DrawToBitmap(preview, new System.Drawing.Rectangle(System.Drawing.Point.Empty, preview.Size));
        preview.Save(Path.Combine(TestContext.CurrentContext.WorkDirectory, "population-players-preview.png"));
    }
}
