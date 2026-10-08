using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OfflineDaoc.LanClient.Setup
{
    public static class Package
    {
        public static string SafePath(string root, string relative)
        {
            if (String.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(":") ||
                relative.Split('/', '\\').Any(p => p == ".." || p == "." || p.Length == 0))
                throw new InvalidDataException("Invalid package path.");
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(Path.Combine(fullRoot, relative));
            if (!full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Path outside package.");
            return full;
        }
        public static void Install(string source, string destination, Action<int> progress)
        {
            destination = Path.GetFullPath(destination);
            if (Directory.Exists(destination) || File.Exists(destination))
                throw new IOException("Choose a new folder. Existing installations are preserved for rollback.");
            string[] entries = File.ReadAllLines(Path.Combine(source, "manifest.sha256"));
            if (entries.Length == 0) throw new InvalidDataException("Empty package.");
            string staging = destination + ".installing-" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(staging);
            // Never copy into a played install. Incomplete staging is retained on failure.
            for (int index = 0; index < entries.Length; index++)
            {
                string[] entry = entries[index].Split('\t');
                if (entry.Length != 2 || !System.Text.RegularExpressions.Regex.IsMatch(entry[0], "^[0-9a-fA-F]{64}$"))
                    throw new InvalidDataException("Invalid package manifest.");
                string input = SafePath(source, entry[1]);
                string output = SafePath(staging, entry[1]);
                // Reject links anywhere in the source path.
                for (string p = input; p != null && p.Length >= Path.GetFullPath(source).Length; p = Path.GetDirectoryName(p))
                    if ((File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Package contains a link.");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                using (var read = File.OpenRead(input))
                using (var write = new FileStream(output, FileMode.CreateNew)) read.CopyTo(write);
                using (var sha = SHA256.Create())
                using (var read = File.OpenRead(output))
                    if (!String.Equals(BitConverter.ToString(sha.ComputeHash(read)).Replace("-", ""), entry[0], StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Package checksum failed: " + entry[1]);
                progress((index + 1) * 100 / entries.Length);
            }
            foreach (string required in new[] { "OfflineDAoC-LAN.exe", "client/game.dll", "client/connect.exe", "release.txt" })
                if (!File.Exists(SafePath(staging, required))) throw new InvalidDataException("Incomplete client package: " + required);
            File.Copy(Path.Combine(source, "manifest.sha256"), Path.Combine(staging, "manifest.sha256"));
            Directory.Move(staging, destination);
        }
    }
    internal sealed class SetupForm : Form
    {
        private readonly TextBox target = new TextBox { Width = 490 };
        private readonly Button install = new Button { Text = "Install client", AutoSize = true };
        private readonly Label status = new Label { Width = 500, Height = 90 };
        private bool busy;
        public SetupForm()
        {
            Text = "Offline DAoC LAN Client Setup";
            ClientSize = new System.Drawing.Size(545, 280);
            StartPosition = FormStartPosition.CenterScreen;
            var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(16) };
            Controls.Add(panel);
            panel.Controls.Add(new Label { Text = "Install to a new folder (existing versions stay available):", AutoSize = true });
            target.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OfflineDAoC-LAN", "Client-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            panel.Controls.Add(target);
            panel.Controls.Add(install);
            panel.Controls.Add(status);
            status.Text = "Installs client files only. No server, database or administrator access needed.\nYour account is stored separately and survives upgrades.";
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (busy) e.Cancel = true; };
            install.Click += async delegate
            {
                busy = true;
                install.Enabled = target.Enabled = false;
                try
                {
                    string destination = Path.GetFullPath(target.Text);
                    string payload = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "payload");
                    var progress = new Progress<int>(p => status.Text = "Installing and verifying files: " + p + "%");
                    await Task.Run(() => Package.Install(payload, destination, p => ((IProgress<int>)progress).Report(p)));
                    status.Text = "Installed successfully: " + destination;
                    try
                    {
                        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                        dynamic shortcut = shell.CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Offline DAoC LAN.lnk"));
                        shortcut.TargetPath = Path.Combine(destination, "OfflineDAoC-LAN.exe");
                        shortcut.WorkingDirectory = destination;
                        shortcut.Save();
                        status.Text += "\nUse the Offline DAoC LAN desktop shortcut.";
                    }
                    catch { status.Text += "\nCould not create a shortcut. Open OfflineDAoC-LAN.exe in the installed folder."; }
                }
                catch (Exception ex) { status.Text = ex.Message + "\nAn incomplete .installing folder may remain; no existing installation was replaced."; }
                finally { busy = false; install.Enabled = target.Enabled = true; }
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
            Application.Run(new SetupForm());
        }
    }
}
