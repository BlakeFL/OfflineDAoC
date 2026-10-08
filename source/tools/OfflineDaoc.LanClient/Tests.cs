using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using OfflineDaoc.LanClient;
using OfflineDaoc.LanClient.Setup;

internal static class Tests
{
    private static int count;
    private static void Check(bool condition, string name) { if (!condition) throw new Exception(name); count++; Console.WriteLine("PASS " + name); }
    private static void Throws(Action action, string name)
    {
        try { action(); } catch { Check(true, name); return; }
        throw new Exception("Expected rejection: " + name);
    }
    private static string Hash(string path)
    {
        using (var sha = SHA256.Create()) using (var input = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
    }
    private static void Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "OfflineDaoc-LanTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        // Fixtures contain fake client bytes only. Never launch a game or server.
        try
        {
            string state = Path.Combine(root, "identity");
            Identity one = Identity.Load(state);
            Identity two = Identity.Load(state);
            Check(one.Account == two.Account && one.Password == two.Password, "Identity survives reload");
            Identity other = Identity.Load(Path.Combine(root, "other"));
            Check(one.Account != other.Account && one.Password != other.Password, "Independent players have distinct identities");
            Check(one.Account.Length <= 20 && one.Password.Length == 20, "Legacy packet lengths");
            string concurrent = Path.Combine(root, "concurrent");
            var identities = new Identity[8];
            Parallel.For(0, identities.Length, i => identities[i] = Identity.Load(concurrent));
            Check(identities.All(i => i.Account == identities[0].Account && i.Password == identities[0].Password), "Concurrent first use preserves one complete identity");
            File.WriteAllText(Path.Combine(state, "account.txt"), "broken");
            Throws(() => Identity.Load(state), "Corrupt identity rejected");
            Check(File.ReadAllText(Path.Combine(state, "account.txt")) == "broken", "Corrupt identity never overwritten");
            foreach (string address in new[] { "192.168.1.10", "10.0.0.2", "172.16.0.2", "172.31.0.2", "127.0.0.1" })
                Check(Connection.ValidateAddress(address) == address, "Accept " + address);
            foreach (string address in new[] { "8.8.8.8", "172.32.0.1", "::1", "192.168.1.1 & calc", "localhost:10300", "" })
                Throws(() => Connection.ValidateAddress(address), "Reject " + address);
            var start = Connection.StartInfo(Path.Combine(root, "space path"), "192.168.1.10", one);
            Check(!start.UseShellExecute && start.Arguments == "game.dll 192.168.1.10 " + one.Account + " " + one.Password, "Connector arguments and shell isolation");
            foreach (string path in new[] { "../escape", "a/../../escape", "C:/escape", "\\\\server\\share", "a:stream", "a//b" })
                Throws(() => Package.SafePath(root, path), "Reject package path " + path);
            string payload = Path.Combine(root, "payload");
            Directory.CreateDirectory(Path.Combine(payload, "client"));
            string[] files = { "OfflineDAoC-LAN.exe", "client/game.dll", "client/connect.exe", "release.txt" };
            foreach (string file in files) File.WriteAllText(Path.Combine(payload, file), "fixture " + file);
            File.WriteAllLines(Path.Combine(payload, "manifest.sha256"), files.Select(f => Hash(Path.Combine(payload, f)) + "\t" + f));
            string destination = Path.Combine(root, "installed");
            Package.Install(payload, destination, p => { });
            Check(files.All(f => File.ReadAllText(Path.Combine(destination, f)) == "fixture " + f), "Verified complete installation");
            Throws(() => Package.Install(payload, destination, p => { }), "Existing install protected");
            File.WriteAllText(Path.Combine(payload, "client/game.dll"), "tampered");
            string bad = Path.Combine(root, "bad");
            Throws(() => Package.Install(payload, bad, p => { }), "Corrupt payload rejected");
            Check(!Directory.Exists(bad), "Failed install never promoted");
            Check(File.ReadAllText(Path.Combine(destination, "client/game.dll")) == "fixture client/game.dll", "Prior installation intact after failure");
            Console.WriteLine(count + " checks passed.");
        }
        finally { Directory.Delete(root, true); }
    }
}
