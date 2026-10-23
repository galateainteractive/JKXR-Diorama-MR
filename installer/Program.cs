using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyVersion("0.6.1.0")]
[assembly: AssemblyFileVersion("0.6.1.0")]

namespace JKXRDioramaMRInstaller
{
    internal static class GalateaTheme
    {
        internal static readonly Color Background = Color.FromArgb(8, 12, 24);
        internal static readonly Color Card = Color.FromArgb(18, 25, 43);
        internal static readonly Color CardBorder = Color.FromArgb(48, 59, 88);
        internal static readonly Color Text = Color.FromArgb(238, 241, 250);
        internal static readonly Color Muted = Color.FromArgb(157, 169, 198);
        internal static readonly Color Purple = Color.FromArgb(133, 95, 246);
        internal static readonly Color PurpleBright = Color.FromArgb(172, 139, 255);
        internal static readonly Color Orange = Color.FromArgb(246, 151, 79);
        internal static readonly Color Green = Color.FromArgb(76, 204, 144);
        internal static readonly Color Red = Color.FromArgb(244, 114, 118);
    }

    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length > 0 && String.Equals(args[0], "--self-test", StringComparison.OrdinalIgnoreCase))
            {
                string report = args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "JKXR-installer-self-test.txt");
                Environment.Exit(SelfTest.Run(report));
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new InstallerForm());
        }
    }

    internal sealed class BrandHeader : Panel
    {
        internal BrandHeader()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Rectangle bounds = ClientRectangle;
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            using (LinearGradientBrush brush = new LinearGradientBrush(
                bounds, Color.FromArgb(24, 31, 63), Color.FromArgb(55, 27, 82), 12F))
            {
                e.Graphics.FillRectangle(brush, bounds);
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush glow = new SolidBrush(Color.FromArgb(24, GalateaTheme.PurpleBright)))
                e.Graphics.FillEllipse(glow, bounds.Width - 250, -150, 360, 360);
            using (Pen accent = new Pen(GalateaTheme.Orange, 3F))
                e.Graphics.DrawLine(accent, 0, bounds.Height - 2, bounds.Width, bounds.Height - 2);
        }
    }

    internal sealed class CardPanel : Panel
    {
        internal CardPanel()
        {
            DoubleBuffered = true;
            BackColor = GalateaTheme.Card;
            ResizeRedraw = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen border = new Pen(GalateaTheme.CardBorder, 1F))
                e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        }
    }

    internal sealed class GalateaProgressBar : Control
    {
        private int progressValue;
        internal int Minimum { get; set; }
        internal int Maximum { get; set; }
        internal int Value
        {
            get { return progressValue; }
            set
            {
                progressValue = Math.Max(Minimum, Math.Min(Maximum, value));
                Invalidate();
            }
        }

        internal GalateaProgressBar()
        {
            Minimum = 0;
            Maximum = 100;
            Height = 10;
            DoubleBuffered = true;
            BackColor = Color.FromArgb(29, 37, 60);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            int range = Math.Max(1, Maximum - Minimum);
            int width = (int)((Width * (double)(Value - Minimum)) / range);
            if (width <= 0) return;
            Rectangle fill = new Rectangle(0, 0, width, Height);
            using (LinearGradientBrush brush = new LinearGradientBrush(
                fill, GalateaTheme.Purple, GalateaTheme.Orange, 0F))
                e.Graphics.FillRectangle(brush, fill);
        }
    }

    internal sealed class InstallerForm : Form
    {
        private readonly TextBox gamePath = new TextBox();
        private readonly Label gameStatus = new Label();
        private readonly ComboBox devices = new ComboBox();
        private readonly Label questStatus = new Label();
        private readonly Button detectButton = new Button();
        private readonly Button fullScanButton = new Button();
        private readonly Button cancelScanButton = new Button();
        private readonly Button browseButton = new Button();
        private readonly Button refreshButton = new Button();
        private readonly Button installButton = new Button();
        private readonly GalateaProgressBar progress = new GalateaProgressBar();
        private readonly TextBox log = new TextBox();
        private readonly Label footer = new Label();
        private bool busy;
        private bool driveSearchActive;
        private CancellationTokenSource driveSearchCancellation;

        public InstallerForm()
        {
            Text = ProductProfile.InstallerTitle;
            ClientSize = new Size(900, 760);
            MinimumSize = new Size(820, 700);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);
            BackColor = GalateaTheme.Background;
            ForeColor = GalateaTheme.Text;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            BuildInterface();
            FormClosing += delegate
            {
                if (driveSearchCancellation != null) driveSearchCancellation.Cancel();
            };
            Shown += async delegate
            {
                await DetectGame(false);
                await RefreshDevices();
            };
        }

        private void BuildInterface()
        {
            SuspendLayout();

            BrandHeader header = new BrandHeader();
            header.Location = new Point(0, 0);
            header.Size = new Size(ClientSize.Width, 146);
            header.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(header);

            Label brand = new Label();
            brand.Text = ProductProfile.BrandName.ToUpperInvariant();
            brand.Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold);
            brand.ForeColor = GalateaTheme.Orange;
            brand.AutoSize = true;
            brand.Location = new Point(29, 27);
            header.Controls.Add(brand);

            Label title = new Label();
            title.Text = ProductProfile.ProductName;
            title.Font = new Font("Segoe UI Semibold", 27F, FontStyle.Bold);
            title.ForeColor = Color.White;
            title.AutoSize = true;
            title.Location = new Point(25, 59);
            header.Controls.Add(title);

            CardPanel gameGroup = new CardPanel();
            gameGroup.Location = new Point(28, 168);
            gameGroup.Size = new Size(844, 170);
            gameGroup.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(gameGroup);

            Label gameStep = CreateStepLabel("01", ProductProfile.GameName + " on this PC");
            gameStep.Location = new Point(17, 13);
            gameGroup.Controls.Add(gameStep);

            gamePath.Location = new Point(17, 51);
            gamePath.Size = new Size(665, 27);
            gamePath.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            StyleTextBox(gamePath);
            gamePath.TextChanged += delegate { ValidateCurrentPath(); };
            gameGroup.Controls.Add(gamePath);

            browseButton.Text = "Choose folder...";
            browseButton.Location = new Point(692, 49);
            browseButton.Size = new Size(132, 31);
            browseButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            StyleSecondaryButton(browseButton);
            browseButton.Click += BrowseClicked;
            gameGroup.Controls.Add(browseButton);

            detectButton.Text = "Detect Steam/GOG";
            detectButton.Location = new Point(17, 91);
            detectButton.Size = new Size(154, 32);
            StyleSecondaryButton(detectButton);
            detectButton.Click += async delegate { await DetectGame(true); };
            gameGroup.Controls.Add(detectButton);

            fullScanButton.Text = "Search drives";
            fullScanButton.Location = new Point(181, 91);
            fullScanButton.Size = new Size(140, 32);
            StyleSecondaryButton(fullScanButton);
            fullScanButton.Click += async delegate { await FullScan(); };
            gameGroup.Controls.Add(fullScanButton);

            cancelScanButton.Text = "Cancel search";
            cancelScanButton.Location = new Point(331, 91);
            cancelScanButton.Size = new Size(124, 32);
            cancelScanButton.Enabled = false;
            StyleSecondaryButton(cancelScanButton);
            cancelScanButton.Click += delegate { CancelFullScan(); };
            gameGroup.Controls.Add(cancelScanButton);

            gameStatus.Location = new Point(17, 135);
            gameStatus.Size = new Size(807, 24);
            gameStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            gameStatus.Text = "Waiting for the game folder.";
            gameStatus.ForeColor = GalateaTheme.Muted;
            gameGroup.Controls.Add(gameStatus);

            CardPanel questGroup = new CardPanel();
            questGroup.Location = new Point(28, 352);
            questGroup.Size = new Size(844, 116);
            questGroup.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(questGroup);

            Label questStep = CreateStepLabel("02", "Meta Quest over USB");
            questStep.Location = new Point(17, 13);
            questGroup.Controls.Add(questStep);

            devices.DropDownStyle = ComboBoxStyle.DropDownList;
            devices.Location = new Point(17, 51);
            devices.Size = new Size(665, 27);
            devices.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            devices.BackColor = Color.FromArgb(28, 36, 58);
            devices.ForeColor = GalateaTheme.Text;
            devices.FlatStyle = FlatStyle.Flat;
            devices.SelectedIndexChanged += delegate { UpdateInstallState(); };
            questGroup.Controls.Add(devices);

            refreshButton.Text = "Refresh";
            refreshButton.Location = new Point(692, 49);
            refreshButton.Size = new Size(132, 31);
            refreshButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            StyleSecondaryButton(refreshButton);
            refreshButton.Click += async delegate { await RefreshDevices(); };
            questGroup.Controls.Add(refreshButton);

            questStatus.Location = new Point(17, 87);
            questStatus.Size = new Size(807, 24);
            questStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            questStatus.Text = "Enable Developer Mode, connect the Quest, and allow USB debugging inside the headset.";
            questStatus.ForeColor = GalateaTheme.Muted;
            questGroup.Controls.Add(questStatus);

            installButton.Text = "INSTALL " + ProductProfile.ProductName.ToUpperInvariant() + " ON QUEST";
            installButton.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold);
            installButton.BackColor = GalateaTheme.Purple;
            installButton.ForeColor = Color.White;
            installButton.FlatStyle = FlatStyle.Flat;
            installButton.FlatAppearance.BorderSize = 0;
            installButton.Enabled = false;
            installButton.Location = new Point(28, 484);
            installButton.Size = new Size(844, 48);
            installButton.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            installButton.Click += async delegate { await Install(); };
            Controls.Add(installButton);

            progress.Location = new Point(28, 544);
            progress.Size = new Size(844, 10);
            progress.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            progress.Minimum = 0;
            progress.Maximum = 100;
            Controls.Add(progress);

            log.Location = new Point(28, 568);
            log.Size = new Size(844, 142);
            log.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            log.Multiline = true;
            log.ReadOnly = true;
            log.ScrollBars = ScrollBars.Vertical;
            log.BorderStyle = BorderStyle.FixedSingle;
            log.BackColor = Color.FromArgb(11, 16, 29);
            log.ForeColor = Color.FromArgb(219, 226, 239);
            log.Font = new Font("Consolas", 8.5F);
            Controls.Add(log);

            footer.Text = ProductProfile.BrandName + "  |  The installer never contains commercial game data.";
            footer.ForeColor = GalateaTheme.Muted;
            footer.Location = new Point(28, 724);
            footer.Size = new Size(844, 24);
            footer.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(footer);

            ResumeLayout(false);
        }

        private static Label CreateStepLabel(string number, string text)
        {
            Label label = new Label();
            label.Text = number + "   " + text.ToUpperInvariant();
            label.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            label.ForeColor = GalateaTheme.PurpleBright;
            label.AutoSize = true;
            return label;
        }

        private static void StyleSecondaryButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = GalateaTheme.CardBorder;
            button.FlatAppearance.BorderSize = 1;
            button.BackColor = Color.FromArgb(28, 36, 58);
            button.ForeColor = GalateaTheme.Text;
            button.Cursor = Cursors.Hand;
        }

        private static void StyleTextBox(TextBox textBox)
        {
            textBox.BorderStyle = BorderStyle.FixedSingle;
            textBox.BackColor = Color.FromArgb(28, 36, 58);
            textBox.ForeColor = GalateaTheme.Text;
        }

        private async Task DetectGame(bool announceFailure)
        {
            if (busy) return;
            SetBusy(true, "Searching Steam, GOG, and LucasArts installations...");
            List<string> found = await Task.Run(delegate { return GameLocator.FindKnownInstallations(); });
            SetBusy(false, null);

            if (found.Count > 0)
            {
                gamePath.Text = found[0];
                AppendLog("Game detected: " + found[0]);
            }
            else if (announceFailure)
            {
                AppendLog("No registered installation was found. Search the drives or choose the folder manually.");
                MessageBox.Show(this,
                    ProductProfile.GameName + " was not found in the registered game libraries.\n\n" +
                    "Use 'Search drives' or 'Choose folder...' to locate it.",
                    "Game not found", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private async Task FullScan()
        {
            if (busy) return;

            CancellationTokenSource cancellation = new CancellationTokenSource();
            driveSearchCancellation = cancellation;
            driveSearchActive = true;
            SetBusy(true, "Searching all fixed drives. This may take a while...");
            gameStatus.Text = "Searching all fixed drives. You can cancel and choose the folder manually.";
            gameStatus.ForeColor = GalateaTheme.PurpleBright;
            AppendLog("Full search started on fixed drives.");

            string found = null;
            bool cancelled = false;
            try
            {
                found = await Task.Run(delegate
                {
                    return GameLocator.FindOnFixedDrives(cancellation.Token);
                }, cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }
            finally
            {
                driveSearchActive = false;
                if (ReferenceEquals(driveSearchCancellation, cancellation))
                    driveSearchCancellation = null;
                cancellation.Dispose();
                SetBusy(false, null);
            }

            if (cancelled)
            {
                gameStatus.Text = "Drive search cancelled. Choose the game folder manually or search again.";
                gameStatus.ForeColor = GalateaTheme.Muted;
                AppendLog("Full drive search cancelled.");
                return;
            }

            if (!String.IsNullOrEmpty(found))
            {
                gamePath.Text = found;
                AppendLog("Game found by full search: " + found);
            }
            else
            {
                AppendLog("Full search completed without finding the four PK3 files.");
                MessageBox.Show(this, "The game was not found. Select the installation folder manually.",
                    "Search complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void CancelFullScan()
        {
            if (!driveSearchActive || driveSearchCancellation == null) return;
            cancelScanButton.Enabled = false;
            gameStatus.Text = "Cancelling drive search...";
            gameStatus.ForeColor = GalateaTheme.Muted;
            AppendLog("Cancelling full drive search...");
            driveSearchCancellation.Cancel();
        }

        private void BrowseClicked(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select the " + ProductProfile.GameName + ", GameData, or GameData\\base folder";
                dialog.ShowNewFolderButton = false;
                if (Directory.Exists(gamePath.Text)) dialog.SelectedPath = gamePath.Text;
                if (dialog.ShowDialog(this) == DialogResult.OK) gamePath.Text = dialog.SelectedPath;
            }
        }

        private void ValidateCurrentPath()
        {
            string resolved = GameLocator.ResolveBaseDirectory(gamePath.Text);
            string problem = "";
            if (!String.IsNullOrEmpty(resolved) && GameLocator.ValidateBaseDirectory(resolved, out problem))
            {
                gameStatus.Text = "✓ Valid " + ProductProfile.GameName + " installation: all required game archives were found.";
                gameStatus.ForeColor = GalateaTheme.Green;
            }
            else
            {
                gameStatus.Text = String.IsNullOrEmpty(problem) ? "Select the " + ProductProfile.GameName + " folder." : problem;
                gameStatus.ForeColor = GalateaTheme.Red;
            }
            UpdateInstallState();
        }

        private async Task RefreshDevices()
        {
            if (busy) return;
            SetBusy(true, "Searching for a Quest over USB...");
            List<AdbDevice> found;
            try
            {
                found = await Task.Run(delegate
                {
                    AdbBundle.EnsureExtracted();
                    return AdbClient.ListDevices();
                });
            }
            catch (Exception ex)
            {
                found = new List<AdbDevice>();
                AppendLog("Unable to start ADB: " + ex.Message);
            }
            SetBusy(false, null);

            devices.Items.Clear();
            foreach (AdbDevice device in found) devices.Items.Add(device);
            if (devices.Items.Count > 0)
            {
                devices.SelectedIndex = 0;
                questStatus.Text = "✓ Quest authorized and ready for installation.";
                questStatus.ForeColor = GalateaTheme.Green;
                AppendLog("Quest detected: " + devices.SelectedItem);
            }
            else
            {
                questStatus.Text = "Quest not detected. Connect USB and allow USB debugging inside the headset.";
                questStatus.ForeColor = GalateaTheme.Red;
                AppendLog("No authorized ADB device was found.");
            }
            UpdateInstallState();
        }

        private async Task Install()
        {
            if (busy || devices.SelectedItem == null) return;
            string baseDirectory = GameLocator.ResolveBaseDirectory(gamePath.Text);
            string validationProblem;
            if (!GameLocator.ValidateBaseDirectory(baseDirectory, out validationProblem))
            {
                MessageBox.Show(this, validationProblem, "Game files", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            AdbDevice selected = (AdbDevice)devices.SelectedItem;
            SetBusy(true, "Installing on Quest...");
            progress.Value = 2;
            AppendLog("Starting installation on " + selected.Serial + ".");

            try
            {
                await Task.Run(delegate { PerformInstall(selected, baseDirectory); });
                progress.Value = 100;
                AppendLog("Installation and verification complete.");
                MessageBox.Show(this,
                    ProductProfile.ProductName + " was installed successfully.\n\n" +
                    ProductProfile.GameName + " will open on the Quest. Put on the headset and complete any permission request that appears.",
                    "Installation complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppendLog("FAILED: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Installation failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private void PerformInstall(AdbDevice device, string baseDirectory)
        {
            AdbBundle.EnsureExtracted();

            SetProgress(8, "Validating the USB connection...");
            AdbResult state = AdbClient.Run(device.Serial, "get-state", 15000);
            RequireSuccess(state, "The Quest stopped responding. Reconnect the USB cable.");

            SetProgress(18, "Installing the " + ProductProfile.ProductName + " APK...");
            AdbResult install = AdbClient.Run(device.Serial,
                "install -r -d " + AdbClient.Quote(AdbBundle.ApkPath), 180000);
            RequireSuccess(install,
                "APK installation failed. If an earlier build uses a different signing key, uninstall only " + ProductProfile.ProductName + " and try again. Its game-data folder is not deleted.");

            SetProgress(30, "Preparing the " + ProductProfile.GameName + " folder...");
            RequireSuccess(AdbClient.Run(device.Serial, "shell mkdir -p " + ProductProfile.GameDataDirectory, 15000),
                "Unable to create " + ProductProfile.GameDataDirectory + ".");

            string commandFile = Path.Combine(AdbBundle.WorkDirectory, "commandline.txt");
            File.WriteAllText(commandFile, ProductProfile.CommandLineValue + "\n", new UTF8Encoding(false));
            RequireSuccess(AdbClient.Run(device.Serial,
                "push " + AdbClient.Quote(commandFile) + " " + ProductProfile.DeviceRoot + "/commandline.txt", 30000),
                "Unable to select " + ProductProfile.GameName + " on the Quest.");

            // On Quest/Android 11+, this app-op is the programmatic equivalent
            // of the all-files access page. Failure is non-fatal: the headset
            // will present the permission screen on first launch instead.
            AdbClient.Run(device.Serial,
                "shell appops set " + ProductProfile.AndroidPackage + " MANAGE_EXTERNAL_STORAGE allow", 15000);

            string[] files = GameLocator.RequiredFiles;
            for (int i = 0; i < files.Length; ++i)
            {
                int percent = 38 + i * 12;
                string name = files[i];
                string source = Path.Combine(baseDirectory, name);
                SetProgress(percent, "Copying " + name + "...");
                AdbResult pushed = AdbClient.Run(device.Serial,
                    "push " + AdbClient.Quote(source) + " " + ProductProfile.GameDataDirectory + "/" + name, 600000);
                RequireSuccess(pushed, "Failed to copy " + name + ". Check the cable and free space on the Quest.");
            }

            SetProgress(88, "Verifying copied files...");
            foreach (string name in files)
            {
                long localSize = new FileInfo(Path.Combine(baseDirectory, name)).Length;
                AdbResult remote = AdbClient.Run(device.Serial,
                    "shell stat -c %s " + ProductProfile.GameDataDirectory + "/" + name, 20000);
                long remoteSize;
                if (!remote.Success || !Int64.TryParse(remote.Output.Trim(), out remoteSize) || remoteSize != localSize)
                {
                    throw new InvalidOperationException("Verification failed for " + name + ". The file on the Quest has an unexpected size.");
                }
                AppendLogThreadSafe("Verified: " + name + " (" + localSize + " bytes)");
            }

            SetProgress(96, "Launching " + ProductProfile.ProductName + "...");
            AdbClient.Run(device.Serial, "shell am force-stop " + ProductProfile.AndroidPackage, 15000);
            AdbResult launch = AdbClient.Run(device.Serial,
                "shell monkey -p " + ProductProfile.AndroidPackage + " -c android.intent.category.LAUNCHER 1", 30000);
            RequireSuccess(launch, "The files were installed, but the app could not be launched automatically. Open " + ProductProfile.ProductName + " from Library > Unknown Sources.");
        }

        private void RequireSuccess(AdbResult result, string friendlyMessage)
        {
            if (!String.IsNullOrWhiteSpace(result.Output)) AppendLogThreadSafe(result.Output.Trim());
            if (!result.Success)
            {
                string detail = String.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error;
                throw new InvalidOperationException(friendlyMessage + "\n\nADB details: " + detail.Trim());
            }
        }

        private void SetProgress(int value, string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<int, string>(SetProgress), value, message);
                return;
            }
            progress.Value = Math.Max(progress.Minimum, Math.Min(progress.Maximum, value));
            questStatus.Text = message;
            questStatus.ForeColor = GalateaTheme.PurpleBright;
            AppendLog(message);
        }

        private void SetBusy(bool value, string message)
        {
            busy = value;
            UseWaitCursor = value;
            detectButton.Enabled = !value;
            fullScanButton.Enabled = !value;
            cancelScanButton.Enabled = driveSearchActive;
            browseButton.Enabled = !value;
            refreshButton.Enabled = !value;
            gamePath.Enabled = !value;
            devices.Enabled = !value;
            if (!String.IsNullOrEmpty(message)) AppendLog(message);
            UpdateInstallState();
        }

        private void UpdateInstallState()
        {
            string resolved = GameLocator.ResolveBaseDirectory(gamePath.Text);
            string problem;
            installButton.Enabled = !busy && devices.SelectedItem != null &&
                GameLocator.ValidateBaseDirectory(resolved, out problem);
        }

        private void AppendLog(string message)
        {
            if (String.IsNullOrWhiteSpace(message)) return;
            log.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message + Environment.NewLine);
        }

        private void AppendLogThreadSafe(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(AppendLogThreadSafe), message);
                return;
            }
            AppendLog(message);
        }
    }

    internal static class GameLocator
    {
        internal static readonly string[] RequiredFiles = ProductProfile.RequiredFiles;

        internal static List<string> FindKnownInstallations()
        {
            List<string> result = new List<string>();
            HashSet<string> candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddSteamCandidates(candidates);
            AddUninstallRegistryCandidates(candidates);
            AddCommonCandidates(candidates);

            foreach (string candidate in candidates)
            {
                string resolved = ResolveBaseDirectory(candidate);
                string problem;
                if (!String.IsNullOrEmpty(resolved) && ValidateBaseDirectory(resolved, out problem) && !result.Contains(resolved))
                    result.Add(resolved);
            }
            return result;
        }

        private static void AddSteamCandidates(HashSet<string> candidates)
        {
            HashSet<string> steamRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] registryKeys =
            {
                @"HKEY_CURRENT_USER\Software\Valve\Steam",
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam",
                @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam"
            };
            foreach (string key in registryKeys)
            {
                object value = Registry.GetValue(key, "SteamPath", null) ?? Registry.GetValue(key, "InstallPath", null);
                if (value != null) steamRoots.Add(value.ToString());
            }

            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (!String.IsNullOrEmpty(programFilesX86)) steamRoots.Add(Path.Combine(programFilesX86, "Steam"));

            HashSet<string> libraries = new HashSet<string>(steamRoots, StringComparer.OrdinalIgnoreCase);
            foreach (string steamRoot in steamRoots)
            {
                string vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
                if (!File.Exists(vdf)) continue;
                try
                {
                    string text = File.ReadAllText(vdf);
                    MatchCollection matches = Regex.Matches(text, "\\\"path\\\"\\s+\\\"([^\\\"]+)\\\"", RegexOptions.IgnoreCase);
                    foreach (Match match in matches) libraries.Add(match.Groups[1].Value.Replace("\\\\", "\\"));
                }
                catch { }
            }

            foreach (string library in libraries)
            {
                string steamApps = Path.Combine(library, "steamapps");
                string manifest = Path.Combine(steamApps, "appmanifest_" + ProductProfile.SteamAppId + ".acf");
                if (File.Exists(manifest))
                {
                    try
                    {
                        Match match = Regex.Match(File.ReadAllText(manifest), "\\\"installdir\\\"\\s+\\\"([^\\\"]+)\\\"", RegexOptions.IgnoreCase);
                        if (match.Success) candidates.Add(Path.Combine(steamApps, "common", match.Groups[1].Value));
                    }
                    catch { }
                }
                foreach (string name in ProductProfile.CommonInstallNames)
                    candidates.Add(Path.Combine(steamApps, "common", name));
            }
        }

        private static void AddUninstallRegistryCandidates(HashSet<string> candidates)
        {
            RegistryHive[] hives = { RegistryHive.LocalMachine, RegistryHive.CurrentUser };
            RegistryView[] views = { RegistryView.Registry64, RegistryView.Registry32 };
            foreach (RegistryHive hive in hives)
            foreach (RegistryView view in views)
            {
                try
                {
                    using (RegistryKey root = RegistryKey.OpenBaseKey(hive, view))
                    using (RegistryKey uninstall = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                    {
                        if (uninstall == null) continue;
                        foreach (string subName in uninstall.GetSubKeyNames())
                        {
                            using (RegistryKey sub = uninstall.OpenSubKey(subName))
                            {
                                if (sub == null) continue;
                                string display = Convert.ToString(sub.GetValue("DisplayName"));
                                if (display.IndexOf(ProductProfile.GameRegistrySearchTerm, StringComparison.OrdinalIgnoreCase) < 0) continue;
                                string location = Convert.ToString(sub.GetValue("InstallLocation"));
                                if (!String.IsNullOrWhiteSpace(location)) candidates.Add(location.Trim('"'));
                            }
                        }
                    }
                }
                catch { }
            }
        }

        private static void AddCommonCandidates(HashSet<string> candidates)
        {
            string[] roots =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                @"C:\GOG Games", @"D:\GOG Games", @"C:\Games", @"D:\Games"
            };
            string[] names = ProductProfile.CommonInstallNames;
            foreach (string root in roots)
            foreach (string name in names)
            {
                if (String.IsNullOrEmpty(root)) continue;
                candidates.Add(Path.Combine(root, name));
                candidates.Add(Path.Combine(root, "GOG Galaxy", "Games", name));
                candidates.Add(Path.Combine(root, "LucasArts", name));
            }

            string besideInstaller = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "game-data-local", ProductProfile.LocalBundleFolder);
            candidates.Add(besideInstaller);
        }

        internal static string FindOnFixedDrives(CancellationToken cancellationToken)
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!drive.IsReady || drive.DriveType != DriveType.Fixed) continue;
                string found = SearchTree(drive.RootDirectory.FullName, 9, 250000, cancellationToken);
                if (!String.IsNullOrEmpty(found)) return found;
            }
            return null;
        }

        private static string SearchTree(string root, int maxDepth, int maxDirectories,
            CancellationToken cancellationToken)
        {
            Stack<SearchNode> pending = new Stack<SearchNode>();
            pending.Push(new SearchNode(root, 0));
            int visited = 0;
            while (pending.Count > 0 && visited < maxDirectories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                SearchNode node = pending.Pop();
                ++visited;
                string resolved = ResolveBaseDirectory(node.Path);
                string problem;
                if (!String.IsNullOrEmpty(resolved) && ValidateBaseDirectory(resolved, out problem)) return resolved;
                if (node.Depth >= maxDepth) continue;

                string[] children;
                try { children = Directory.GetDirectories(node.Path); }
                catch { continue; }

                foreach (string child in children)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        DirectoryInfo info = new DirectoryInfo(child);
                        if ((info.Attributes & FileAttributes.ReparsePoint) != 0 || ShouldSkip(info.Name)) continue;
                        pending.Push(new SearchNode(child, node.Depth + 1));
                    }
                    catch { }
                }
            }
            return null;
        }

        private static bool ShouldSkip(string name)
        {
            string lower = name.ToLowerInvariant();
            return lower == "$recycle.bin" || lower == "system volume information" ||
                lower == "windows" || lower == "winsxs" || lower == "node_modules" ||
                lower == ".git" || lower == "android" || lower == "programdata";
        }

        internal static string ResolveBaseDirectory(string selected)
        {
            if (String.IsNullOrWhiteSpace(selected)) return null;
            string value = selected.Trim().Trim('"');
            string[] candidates =
            {
                value,
                Path.Combine(value, "base"),
                Path.Combine(value, "GameData", "base")
            };
            foreach (string candidate in candidates)
            {
                if (HasRequiredNames(candidate)) return Path.GetFullPath(candidate);
            }
            return null;
        }

        private static bool HasRequiredNames(string directory)
        {
            if (!Directory.Exists(directory)) return false;
            foreach (string file in RequiredFiles)
                if (!File.Exists(Path.Combine(directory, file))) return false;
            return true;
        }

        internal static bool ValidateBaseDirectory(string directory, out string problem)
        {
            problem = "";
            if (String.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                problem = "Select the " + ProductProfile.GameName + " installation, GameData, or GameData\\base folder.";
                return false;
            }

            foreach (string name in RequiredFiles)
            {
                string path = Path.Combine(directory, name);
                if (!File.Exists(path))
                {
                    problem = "Missing file: " + name + ". Make sure you selected " + ProductProfile.GameName + ", not another game.";
                    return false;
                }
                try
                {
                    FileInfo info = new FileInfo(path);
                    if (info.Length < 1024 * 1024)
                    {
                        problem = name + " appears to be incomplete or invalid.";
                        return false;
                    }
                    using (ZipArchive archive = ZipFile.OpenRead(path))
                    {
                        if (archive.Entries.Count == 0)
                        {
                            problem = name + " does not contain valid data.";
                            return false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    problem = "Unable to validate " + name + ": " + ex.Message;
                    return false;
                }
            }
            return true;
        }

        private sealed class SearchNode
        {
            internal readonly string Path;
            internal readonly int Depth;
            internal SearchNode(string path, int depth) { Path = path; Depth = depth; }
        }
    }

    internal sealed class AdbDevice
    {
        internal string Serial;
        internal string Model;
        public override string ToString() { return Model + " — " + Serial; }
    }

    internal sealed class AdbResult
    {
        internal int ExitCode;
        internal string Output;
        internal string Error;
        internal bool Success { get { return ExitCode == 0; } }
    }

    internal static class AdbBundle
    {
        private static readonly object Sync = new object();
        internal static string WorkDirectory { get; private set; }
        internal static string AdbPath { get; private set; }
        internal static string ApkPath { get; private set; }

        internal static void EnsureExtracted()
        {
            lock (Sync)
            {
                if (!String.IsNullOrEmpty(AdbPath) && File.Exists(AdbPath) && File.Exists(ApkPath)) return;
                WorkDirectory = Path.Combine(Path.GetTempPath(), ProductProfile.TempFolderName);
                Directory.CreateDirectory(WorkDirectory);
                AdbPath = Extract("Installer.adb.exe", "adb.exe");
                Extract("Installer.AdbWinApi.dll", "AdbWinApi.dll");
                Extract("Installer.AdbWinUsbApi.dll", "AdbWinUsbApi.dll");
                Extract("Installer.ADB-NOTICE.txt", "ADB-NOTICE.txt");
                ApkPath = Extract(ProductProfile.EmbeddedApkResource, ProductProfile.ExtractedApkName);
            }
        }

        private static string Extract(string resourceName, string fileName)
        {
            string target = Path.Combine(WorkDirectory, fileName);
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream input = assembly.GetManifestResourceStream(resourceName))
            {
                if (input == null) throw new InvalidOperationException("Missing embedded resource: " + resourceName);
                FileInfo existing = new FileInfo(target);
                if (existing.Exists && existing.Length == input.Length) return target;
                using (FileStream output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.Read))
                    input.CopyTo(output);
            }
            return target;
        }
    }

    internal static class AdbClient
    {
        internal static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        internal static List<AdbDevice> ListDevices()
        {
            Run(null, "start-server", 30000);
            AdbResult result = Run(null, "devices -l", 30000);
            List<AdbDevice> devices = new List<AdbDevice>();
            if (!result.Success) return devices;
            string[] lines = result.Output.Replace("\r", "").Split('\n');
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("List of devices")) continue;
                string[] parts = Regex.Split(line, "\\s+");
                if (parts.Length < 2 || parts[1] != "device") continue;
                string model = "Meta Quest";
                foreach (string part in parts)
                    if (part.StartsWith("model:")) model = part.Substring(6).Replace('_', ' ');
                devices.Add(new AdbDevice { Serial = parts[0], Model = model });
            }
            return devices;
        }

        internal static AdbResult Run(string serial, string arguments, int timeoutMilliseconds)
        {
            AdbBundle.EnsureExtracted();
            string fullArguments = String.IsNullOrEmpty(serial) ? arguments : "-s " + Quote(serial) + " " + arguments;
            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = AdbBundle.AdbPath;
            start.Arguments = fullArguments;
            start.WorkingDirectory = AdbBundle.WorkDirectory;
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            start.StandardOutputEncoding = Encoding.UTF8;
            start.StandardErrorEncoding = Encoding.UTF8;

            using (Process process = Process.Start(start))
            {
                Task<string> output = process.StandardOutput.ReadToEndAsync();
                Task<string> error = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(timeoutMilliseconds))
                {
                    try { process.Kill(); } catch { }
                    return new AdbResult { ExitCode = -1, Output = output.Result, Error = "Operation timed out." };
                }
                Task.WaitAll(output, error);
                return new AdbResult { ExitCode = process.ExitCode, Output = output.Result, Error = error.Result };
            }
        }
    }

    internal static class SelfTest
    {
        internal static int Run(string reportPath)
        {
            List<string> report = new List<string>();
            try
            {
                report.Add(ProductProfile.InstallerTitle + " self-test");
                report.Add("Brand=" + ProductProfile.BrandName);
                report.Add("Assembly=" + Assembly.GetExecutingAssembly().GetName().Version);
                report.Add("Android-package=" + ProductProfile.AndroidPackage);
                report.Add("Device-root=" + ProductProfile.DeviceRoot);
                using (InstallerForm form = new InstallerForm())
                {
                    if (!String.Equals(form.Text, ProductProfile.InstallerTitle, StringComparison.Ordinal))
                        throw new InvalidOperationException("The installer window title does not match the product profile.");
                }
                report.Add("UI=OK");
                AdbBundle.EnsureExtracted();
                report.Add("Resources=OK");

                using (ZipArchive apk = ZipFile.OpenRead(AdbBundle.ApkPath))
                {
                    ZipArchiveEntry command = apk.GetEntry("assets/commandline.txt");
                    if (command == null) throw new InvalidOperationException("commandline.txt is missing from the APK.");
                    using (StreamReader reader = new StreamReader(command.Open()))
                    {
                        string value = reader.ReadToEnd().Trim();
                        if (value != ProductProfile.CommandLineValue)
                            throw new InvalidOperationException("APK is not configured for " + ProductProfile.GameName + ": " + value);
                        report.Add("APK-commandline=" + ProductProfile.CommandLineValue);
                    }
                }

                AdbResult adb = AdbClient.Run(null, "version", 30000);
                if (!adb.Success || adb.Output.IndexOf("Android Debug Bridge", StringComparison.OrdinalIgnoreCase) < 0)
                    throw new InvalidOperationException("Embedded ADB did not start: " + adb.Error);
                report.Add("ADB=OK");

                string testRoot = Path.Combine(Path.GetTempPath(), "JKXR-installer-path-test");
                string baseDirectory = Path.Combine(testRoot, "GameData", "base");
                if (Directory.Exists(testRoot)) Directory.Delete(testRoot, true);
                Directory.CreateDirectory(baseDirectory);
                if (GameLocator.ResolveBaseDirectory(testRoot) != null)
                    throw new InvalidOperationException("A folder without PK3 files was accepted incorrectly.");
                report.Add("Path-negative=OK");

                foreach (string name in GameLocator.RequiredFiles)
                {
                    using (ZipArchive archive = ZipFile.Open(Path.Combine(baseDirectory, name), ZipArchiveMode.Create))
                    {
                        ZipArchiveEntry entry = archive.CreateEntry("test/asset.bin", CompressionLevel.NoCompression);
                        using (Stream output = entry.Open())
                        {
                            byte[] block = new byte[65536];
                            for (int i = 0; i < 18; ++i) output.Write(block, 0, block.Length);
                        }
                    }
                }
                string problem;
                if (!GameLocator.ValidateBaseDirectory(baseDirectory, out problem))
                    throw new InvalidOperationException("Positive validation failed: " + problem);
                string expected = Path.GetFullPath(baseDirectory);
                if (!String.Equals(GameLocator.ResolveBaseDirectory(testRoot), expected, StringComparison.OrdinalIgnoreCase) ||
                    !String.Equals(GameLocator.ResolveBaseDirectory(Path.Combine(testRoot, "GameData")), expected, StringComparison.OrdinalIgnoreCase) ||
                    !String.Equals(GameLocator.ResolveBaseDirectory(baseDirectory), expected, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Game/GameData/base resolution did not produce the same folder.");
                report.Add("Path-root-GameData-base=OK");
                Directory.Delete(testRoot, true);

                using (CancellationTokenSource cancellation = new CancellationTokenSource())
                {
                    cancellation.Cancel();
                    try
                    {
                        GameLocator.FindOnFixedDrives(cancellation.Token);
                        throw new InvalidOperationException("A cancelled drive search continued running.");
                    }
                    catch (OperationCanceledException) { }
                }
                report.Add("Drive-search-cancel=OK");
                report.Add("RESULT=PASS");
                Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
                File.WriteAllLines(reportPath, report.ToArray(), new UTF8Encoding(false));
                return 0;
            }
            catch (Exception ex)
            {
                report.Add("RESULT=FAIL");
                report.Add(ex.ToString());
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
                    File.WriteAllLines(reportPath, report.ToArray(), new UTF8Encoding(false));
                }
                catch { }
                return 1;
            }
        }
    }
}
