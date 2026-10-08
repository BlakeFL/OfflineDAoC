using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OfflineDaoc.LanClient
{
    public sealed class Identity
    {
        public string Account;
        public string Password;
        public static Identity Load(string directory)
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "account.txt");
            if (!File.Exists(path))
            {
                // Publish a complete file atomically. Never replace an existing identity.
                string temporary = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
                try
                {
                    File.WriteAllLines(temporary, new[] { "p" + RandomHex(9), RandomHex(10) });
                    try { File.Move(temporary, path); }
                    catch (IOException) { if (!File.Exists(path)) throw; }
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
            string[] lines = File.ReadAllLines(path);
            if (lines.Length != 2 || !Regex.IsMatch(lines[0], "^[a-zA-Z0-9]{1,20}$") ||
                !Regex.IsMatch(lines[1], "^[a-zA-Z0-9]{1,20}$"))
                throw new InvalidDataException("Saved account is invalid. Restore your account backup; do not delete it to fix a login problem.");
            return new Identity { Account = lines[0], Password = lines[1] };
        }
        private static string RandomHex(int count)
        {
            byte[] bytes = new byte[count];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }
    }

    public static class Connection
    {
        public static string ValidateAddress(string value)
        {
            IPAddress address;
            if (!IPAddress.TryParse(value.Trim(), out address) || address.AddressFamily != AddressFamily.InterNetwork)
                throw new ArgumentException("Enter the host PC's LAN IPv4 address, for example 192.168.1.20.");
            byte[] b = address.GetAddressBytes();
            if (!(b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) ||
                (b[0] == 192 && b[1] == 168) || b[0] == 127))
                throw new ArgumentException("Use a private LAN address (10.x, 172.16–31.x, or 192.168.x) or loopback.");
            return address.ToString();
        }
        public static ProcessStartInfo StartInfo(string app, string address, Identity identity)
        {
            return new ProcessStartInfo(Path.Combine(app, "connect.exe"),
                "game.dll " + ValidateAddress(address) + " " + identity.Account + " " + identity.Password)
                { WorkingDirectory = app, UseShellExecute = false };
        }
    }

    internal sealed class ClientForm : Form
    {
        private readonly string state = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OfflineDAoC-LAN", "Identity");
        private readonly TextBox address = new TextBox { Width = 350 };
        private readonly Label status = new Label { AutoSize = false, Width = 440, Height = 65 };
        private readonly Button play = new Button { Text = "Enter Realm", AutoSize = true };
        public ClientForm()
        {
            Text = "Offline DAoC — LAN Client";
            ClientSize = new System.Drawing.Size(490, 285);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(20) };
            Controls.Add(panel);
            panel.Controls.Add(new Label { Text = "Server PC's LAN IPv4 address", AutoSize = true });
            panel.Controls.Add(address);
            panel.Controls.Add(play);
            panel.Controls.Add(status);
            panel.Controls.Add(new Label { Text = "Your account is created automatically on first login.\nCharacters and progress stay on the server PC.", AutoSize = true });
            var backup = new Button { Text = "Open account backup folder", AutoSize = true };
            panel.Controls.Add(backup);
            backup.Click += delegate { Directory.CreateDirectory(state); Process.Start("explorer.exe", "\"" + state + "\""); };
            string settings = Path.Combine(state, "server.txt");
            if (File.Exists(settings)) address.Text = File.ReadAllText(settings).Trim();
            status.Text = "Start the server on the host PC, then enter its address here.";
            play.Click += async delegate
            {
                play.Enabled = false;
                try
                {
                    string host = Connection.ValidateAddress(address.Text);
                    string app = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "client");
                    if (!File.Exists(Path.Combine(app, "game.dll")) || !File.Exists(Path.Combine(app, "connect.exe")))
                        throw new FileNotFoundException("Client files are missing. Install the complete LAN client package that matches the server edition.");
                    status.Text = "Checking server connection…";
                    using (var tcp = new TcpClient())
                    {
                        Task connecting = tcp.ConnectAsync(host, 10300);
                        if (await Task.WhenAny(connecting, Task.Delay(3000)) != connecting)
                            throw new IOException("Server did not respond. Check its address, LAN hosting settings and firewall.");
                        await connecting;
                    }
                    Identity identity = Identity.Load(state);
                    File.WriteAllText(settings, host);
                    Process.Start(Connection.StartInfo(app, host, identity));
                    status.Text = "Client started. Account: " + identity.Account + "\nA reachable port does not guarantee the server has finished loading.";
                }
                catch (Exception ex) { status.Text = ex.Message; }
                finally { play.Enabled = true; }
            };
        }
    }
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try { Application.Run(new ClientForm()); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "LAN Client", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }
}
