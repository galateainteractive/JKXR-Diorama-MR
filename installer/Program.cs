using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyVersion("1.2.1.0")]
[assembly: AssemblyFileVersion("1.2.1.0")]

namespace GalateaDioramaInstaller
{
    internal enum InstallerWorkflow
    {
        EmbeddedApkWithPayload,
        PersonalSourceKit
    }

    internal enum PayloadEntryType
    {
        File,
        Directory
    }

    internal enum PayloadValidation
    {
        Exists,
        ZipArchive
    }

    internal sealed class PayloadRule
    {
        internal readonly string SourceRelativePath;
        internal readonly string DestinationRelativePath;
        internal readonly PayloadEntryType EntryType;
        internal readonly PayloadValidation Validation;
        internal readonly long MinimumBytes;

        internal PayloadRule(string sourceRelativePath, string destinationRelativePath,
            PayloadEntryType entryType, PayloadValidation validation, long minimumBytes)
        {
            SourceRelativePath = sourceRelativePath;
            DestinationRelativePath = destinationRelativePath;
            EntryType = entryType;
            Validation = validation;
            MinimumBytes = minimumBytes;
        }
    }

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
                string report = args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "Galatea-installer-self-test.txt");
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
        private readonly TextBox audioPath = new TextBox();
        private readonly Label audioStatus = new Label();
        private readonly Button browseGameFileButton = new Button();
        private readonly Button browseGameFolderButton = new Button();
        private readonly Button extractPhoneButton = new Button();
        private readonly Button browseAudioFileButton = new Button();
        private readonly Button browseAudioFolderButton = new Button();
        private readonly System.Windows.Forms.Timer questMonitorTimer = new System.Windows.Forms.Timer();
        private bool busy;
        private bool deviceRefreshActive;
        private bool driveSearchActive;
        private string lastDeviceSnapshot = "";
        private CancellationTokenSource driveSearchCancellation;

        public InstallerForm()
        {
            Text = ProductProfile.InstallerTitle;
            ClientSize = ProductProfile.Workflow == InstallerWorkflow.PersonalSourceKit
                ? new Size(900, 880) : new Size(900, 760);
            MinimumSize = ProductProfile.Workflow == InstallerWorkflow.PersonalSourceKit
                ? new Size(820, 820) : new Size(820, 700);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);
            BackColor = GalateaTheme.Background;
            ForeColor = GalateaTheme.Text;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            BuildInterface();
            questMonitorTimer.Interval = 1500;
            questMonitorTimer.Tick += async delegate { await RefreshDevices(false); };
            FormClosing += delegate
            {
                questMonitorTimer.Stop();
                if (driveSearchCancellation != null) driveSearchCancellation.Cancel();
            };
            Shown += async delegate
            {
                if (ProductProfile.Workflow == InstallerWorkflow.EmbeddedApkWithPayload)
                    await DetectGame(false);
                await RefreshDevices(true);
                if (!IsDisposed && !Disposing) questMonitorTimer.Start();
            };
        }

        private void BuildInterface()
        {
            if (ProductProfile.Workflow == InstallerWorkflow.PersonalSourceKit)
            {
                BuildSourceKitInterface();
                return;
            }
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
            refreshButton.Click += async delegate { await RefreshDevices(true); };
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

        private void BuildSourceKitInterface()
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

            Label subtitle = new Label();
            subtitle.Text = "Installs the Quest mod using your own official game copy.";
            subtitle.ForeColor = GalateaTheme.Muted;
            subtitle.AutoSize = true;
            subtitle.Location = new Point(29, 118);
            header.Controls.Add(subtitle);

            CardPanel sourceGroup = new CardPanel();
            sourceGroup.Location = new Point(28, 162);
            sourceGroup.Size = new Size(844, 166);
            sourceGroup.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(sourceGroup);

            Label sourceStep = CreateStepLabel("01", "Your official " + ProductProfile.GameName + " copy");
            sourceStep.Location = new Point(17, 12);
            sourceGroup.Controls.Add(sourceStep);

            gamePath.Location = new Point(17, 45);
            gamePath.Size = new Size(493, 27);
            gamePath.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            StyleTextBox(gamePath);
            gamePath.TextChanged += delegate { ValidateSourceKitInputs(); };
            sourceGroup.Controls.Add(gamePath);

            browseGameFileButton.Text = "Choose file...";
            browseGameFileButton.Location = new Point(520, 43);
            browseGameFileButton.Size = new Size(142, 31);
            browseGameFileButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            StyleSecondaryButton(browseGameFileButton);
            browseGameFileButton.Click += delegate { BrowseSourceFile(gamePath, ProductProfile.GamePackageFilter); };
            sourceGroup.Controls.Add(browseGameFileButton);

            browseGameFolderButton.Text = "Choose folder...";
            browseGameFolderButton.Location = new Point(672, 43);
            browseGameFolderButton.Size = new Size(152, 31);
            browseGameFolderButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            StyleSecondaryButton(browseGameFolderButton);
            browseGameFolderButton.Click += delegate { BrowseSourceFolder(gamePath, "Select the folder containing base.apk and every split APK"); };
            sourceGroup.Controls.Add(browseGameFolderButton);

            extractPhoneButton.Text = "EXTRACT FROM ANDROID PHONE";
            extractPhoneButton.Location = new Point(17, 84);
            extractPhoneButton.Size = new Size(244, 34);
            extractPhoneButton.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            StyleSecondaryButton(extractPhoneButton);
            extractPhoneButton.Click += async delegate { await ExtractFromPhone(); };
            sourceGroup.Controls.Add(extractPhoneButton);

            gameStatus.Location = new Point(17, 128);
            gameStatus.Size = new Size(807, 25);
            gameStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            gameStatus.Text = "Choose an exported APK set, or connect the Android phone that contains your Google Play copy.";
            gameStatus.ForeColor = GalateaTheme.Muted;
            sourceGroup.Controls.Add(gameStatus);

            CardPanel audioGroup = new CardPanel();
            audioGroup.Location = new Point(28, 340);
            audioGroup.Size = new Size(844, 139);
            audioGroup.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(audioGroup);

            Label audioStep = CreateStepLabel("02", ProductProfile.AudioSourceLabel);
            audioStep.Location = new Point(17, 12);
            audioGroup.Controls.Add(audioStep);

            audioPath.Location = new Point(17, 45);
            audioPath.Size = new Size(493, 27);
            audioPath.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            StyleTextBox(audioPath);
            audioPath.TextChanged += delegate { ValidateSourceKitInputs(); };
            audioGroup.Controls.Add(audioPath);

            browseAudioFileButton.Text = "Choose archive...";
            browseAudioFileButton.Location = new Point(520, 43);
            browseAudioFileButton.Size = new Size(142, 31);
            browseAudioFileButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            StyleSecondaryButton(browseAudioFileButton);
            browseAudioFileButton.Click += delegate { BrowseSourceFile(audioPath, ProductProfile.AudioSourceFilter); };
            audioGroup.Controls.Add(browseAudioFileButton);

            browseAudioFolderButton.Text = "Choose folder...";
            browseAudioFolderButton.Location = new Point(672, 43);
            browseAudioFolderButton.Size = new Size(152, 31);
            browseAudioFolderButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            StyleSecondaryButton(browseAudioFolderButton);
            browseAudioFolderButton.Click += delegate { BrowseSourceFolder(audioPath, "Select the extracted audio-pack folder"); };
            audioGroup.Controls.Add(browseAudioFolderButton);

            audioStatus.Location = new Point(17, 87);
            audioStatus.Size = new Size(560, 24);
            audioStatus.Text = "Select the required verified audio archive or its extracted folder.";
            audioStatus.ForeColor = GalateaTheme.Muted;
            audioGroup.Controls.Add(audioStatus);

            if (!String.IsNullOrWhiteSpace(ProductProfile.AudioDownloadUrl))
            {
                LinkLabel audioLink = new LinkLabel();
                audioLink.Text = "Open the audio-pack download page";
                audioLink.LinkColor = GalateaTheme.PurpleBright;
                audioLink.ActiveLinkColor = GalateaTheme.Orange;
                audioLink.AutoSize = true;
                audioLink.Location = new Point(596, 88);
                audioLink.LinkClicked += delegate
                {
                    Process.Start(new ProcessStartInfo(ProductProfile.AudioDownloadUrl) { UseShellExecute = true });
                };
                audioGroup.Controls.Add(audioLink);
            }

            CardPanel questGroup = new CardPanel();
            questGroup.Location = new Point(28, 491);
            questGroup.Size = new Size(844, 116);
            questGroup.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(questGroup);

            Label questStep = CreateStepLabel("03", "Meta Quest over USB");
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
            refreshButton.Click += async delegate { await RefreshDevices(true); };
            questGroup.Controls.Add(refreshButton);

            questStatus.Location = new Point(17, 87);
            questStatus.Size = new Size(807, 24);
            questStatus.Text = "Enable Developer Mode, connect the Quest, and allow USB debugging inside the headset.";
            questStatus.ForeColor = GalateaTheme.Muted;
            questGroup.Controls.Add(questStatus);

            installButton.Text = "BUILD AND INSTALL " + ProductProfile.ProductName.ToUpperInvariant();
            installButton.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold);
            installButton.BackColor = GalateaTheme.Purple;
            installButton.ForeColor = Color.White;
            installButton.FlatStyle = FlatStyle.Flat;
            installButton.FlatAppearance.BorderSize = 0;
            installButton.Enabled = false;
            installButton.Location = new Point(28, 623);
            installButton.Size = new Size(844, 48);
            installButton.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            installButton.Click += async delegate { await Install(); };
            Controls.Add(installButton);

            progress.Location = new Point(28, 683);
            progress.Size = new Size(844, 10);
            progress.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            progress.Minimum = 0;
            progress.Maximum = 100;
            Controls.Add(progress);

            log.Location = new Point(28, 707);
            log.Size = new Size(844, 126);
            log.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            log.Multiline = true;
            log.ReadOnly = true;
            log.ScrollBars = ScrollBars.Vertical;
            log.BorderStyle = BorderStyle.FixedSingle;
            log.BackColor = Color.FromArgb(11, 16, 29);
            log.ForeColor = Color.FromArgb(219, 226, 239);
            log.Font = new Font("Consolas", 8.5F);
            Controls.Add(log);

            footer.Text = ProductProfile.BrandName + "  |  No Rockstar APK, game data, or audio is included.";
            footer.ForeColor = GalateaTheme.Muted;
            footer.Location = new Point(28, 845);
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
            SetBusy(true, "Searching Steam, registered stores, and common installation folders...");
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
                AppendLog("Full search completed without finding the required game data.");
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
                dialog.Description = "Select the " + ProductProfile.GameName + " installation folder";
                dialog.ShowNewFolderButton = false;
                if (Directory.Exists(gamePath.Text)) dialog.SelectedPath = gamePath.Text;
                if (dialog.ShowDialog(this) == DialogResult.OK) gamePath.Text = dialog.SelectedPath;
            }
        }

        private void BrowseSourceFile(TextBox target, string filter)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Select a user-owned source file";
                dialog.Filter = String.IsNullOrWhiteSpace(filter) ? "All files (*.*)|*.*" : filter;
                dialog.CheckFileExists = true;
                if (dialog.ShowDialog(this) == DialogResult.OK) target.Text = dialog.FileName;
            }
        }

        private void BrowseSourceFolder(TextBox target, string description)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = description;
                dialog.ShowNewFolderButton = false;
                if (Directory.Exists(target.Text)) dialog.SelectedPath = target.Text;
                if (dialog.ShowDialog(this) == DialogResult.OK) target.Text = dialog.SelectedPath;
            }
        }

        private static bool ExistingFileOrDirectory(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return false;
            string path = value.Trim().Trim('"');
            return File.Exists(path) || Directory.Exists(path);
        }

        private void ValidateSourceKitInputs()
        {
            bool gameReady = ExistingFileOrDirectory(gamePath.Text);
            bool audioReady = ExistingFileOrDirectory(audioPath.Text);

            gameStatus.Text = gameReady
                ? "✓ Game source selected. Its version, signer and complete split set will be verified before building."
                : "Choose an exported APK set, or extract it from the Android phone that contains your Google Play copy.";
            gameStatus.ForeColor = gameReady ? GalateaTheme.Green : GalateaTheme.Muted;

            audioStatus.Text = audioReady
                ? "✓ Audio source selected. All required files and hashes will be verified before building."
                : "Select the required verified audio archive or its extracted folder.";
            audioStatus.ForeColor = audioReady ? GalateaTheme.Green : GalateaTheme.Muted;
            UpdateInstallState();
        }

        private async Task ExtractFromPhone()
        {
            if (busy) return;
            SetBusy(true, "Looking for the Android phone that contains the official Google Play game...");
            try
            {
                AdbBundle.EnsureExtracted();
                SourceKitBundle.EnsureExtracted();
                List<AdbDevice> phones = await Task.Run(delegate
                {
                    return AdbClient.ListDevices().FindAll(delegate(AdbDevice item)
                    {
                        return String.Equals(item.State, "device", StringComparison.OrdinalIgnoreCase) && !item.IsQuest;
                    });
                });
                if (phones.Count == 0)
                    throw new InvalidOperationException("No authorized Android phone was found. Enable USB debugging, keep the phone unlocked, reconnect it, and approve this computer.");
                if (phones.Count > 1)
                    throw new InvalidOperationException("More than one Android phone is connected. Leave only the phone containing GTA San Andreas connected, then try again.");

                string destination = Path.Combine(SourceKitBundle.PersistentWorkDirectory, "exports",
                    "GTA-SA-Play-export-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                string script = Path.Combine(SourceKitBundle.SourceRoot, ProductProfile.SourceKitExportScript);
                string arguments = "-WorkDir " + ProcessRunner.Quote(SourceKitBundle.PersistentWorkDirectory) +
                    " -Destination " + ProcessRunner.Quote(destination) +
                    " -Serial " + ProcessRunner.Quote(phones[0].Serial) +
                    " -AdbPath " + ProcessRunner.Quote(AdbBundle.AdbPath) + " -NoOpenFolder";
                SetProgress(8, "Exporting the verified Google Play APK set from " + phones[0].Model + "...");
                int exitCode = await Task.Run(delegate
                {
                    return ProcessRunner.RunPowerShell(script, arguments, AppendLogThreadSafe);
                });
                if (exitCode != 0)
                    throw new InvalidOperationException("The APK export did not complete. Review the log above, keep the phone unlocked, and try again.");
                gamePath.Text = destination;
                SetProgress(20, "Official APK set exported and ready.");
                AppendLog("APK set exported to: " + destination);
            }
            catch (Exception ex)
            {
                AppendLog("EXPORT FAILED: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Phone extraction failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private void ValidateCurrentPath()
        {
            string resolved = GameLocator.ResolveInstallDirectory(gamePath.Text);
            string problem = "";
            if (!String.IsNullOrEmpty(resolved) && GameLocator.ValidateInstallDirectory(resolved, out problem))
            {
                gameStatus.Text = "✓ Valid " + ProductProfile.GameName + " installation: all required game data was found.";
                gameStatus.ForeColor = GalateaTheme.Green;
            }
            else
            {
                gameStatus.Text = String.IsNullOrEmpty(problem) ? "Select the " + ProductProfile.GameName + " folder." : problem;
                gameStatus.ForeColor = GalateaTheme.Red;
            }
            UpdateInstallState();
        }

        private async Task RefreshDevices(bool announce)
        {
            if (busy || deviceRefreshActive || IsDisposed || Disposing) return;
            deviceRefreshActive = true;
            refreshButton.Enabled = false;
            UpdateInstallState();
            if (announce)
            {
                questStatus.Text = "Searching for a Quest over USB...";
                questStatus.ForeColor = GalateaTheme.PurpleBright;
                AppendLog("Searching for a Quest over USB...");
            }

            List<AdbDevice> inventory = null;
            string discoveryError = null;
            try
            {
                inventory = await Task.Run(delegate
                {
                    AdbBundle.EnsureExtracted();
                    return AdbClient.ListDevices();
                });
                if (IsDisposed || Disposing) return;
                ApplyDeviceInventory(inventory);
            }
            catch (Exception ex)
            {
                discoveryError = ex.Message;
                if (!IsDisposed && !Disposing)
                {
                    ReplaceQuestChoices(new List<AdbDevice>());
                    string snapshot = "error:" + discoveryError;
                    questStatus.Text = "ADB is unavailable. Check the USB connection and try Refresh.";
                    questStatus.ForeColor = GalateaTheme.Red;
                    if (!String.Equals(lastDeviceSnapshot, snapshot, StringComparison.Ordinal))
                    {
                        lastDeviceSnapshot = snapshot;
                        AppendLog("Unable to query ADB: " + discoveryError);
                    }
                }
            }
            finally
            {
                deviceRefreshActive = false;
                if (!IsDisposed && !Disposing)
                {
                    refreshButton.Enabled = !busy;
                    UpdateInstallState();
                }
            }
        }

        private void ApplyDeviceInventory(List<AdbDevice> inventory)
        {
            inventory.Sort(delegate(AdbDevice left, AdbDevice right)
            {
                return String.Compare(left.Serial, right.Serial, StringComparison.OrdinalIgnoreCase);
            });
            List<AdbDevice> quests = new List<AdbDevice>();
            int unauthorized = 0;
            int offline = 0;
            int otherAuthorized = 0;
            StringBuilder snapshot = new StringBuilder();
            foreach (AdbDevice item in inventory)
            {
                snapshot.Append(item.Serial).Append('|').Append(item.State).Append('|')
                    .Append(item.Model).Append('|').Append(item.IsQuest ? 'Q' : 'A').Append(';');
                if (String.Equals(item.State, "device", StringComparison.OrdinalIgnoreCase))
                {
                    if (item.IsQuest) quests.Add(item);
                    else otherAuthorized++;
                }
                else if (String.Equals(item.State, "unauthorized", StringComparison.OrdinalIgnoreCase)) unauthorized++;
                else if (String.Equals(item.State, "offline", StringComparison.OrdinalIgnoreCase)) offline++;
            }

            ReplaceQuestChoices(quests);
            string message;
            Color color;
            if (quests.Count > 0)
            {
                message = quests.Count == 1
                    ? "✓ Quest connected and authorized — ready for installation."
                    : "✓ " + quests.Count + " authorized Quest headsets found — choose one above.";
                color = GalateaTheme.Green;
            }
            else if (unauthorized > 0)
            {
                message = "Quest connected — put on the headset and allow USB debugging.";
                color = GalateaTheme.Orange;
            }
            else if (offline > 0)
            {
                message = "USB device detected, but ADB reports it offline. Reconnect USB and unlock the headset.";
                color = GalateaTheme.Red;
            }
            else if (otherAuthorized > 0)
            {
                message = "Android device detected, but no authorized Meta Quest was found.";
                color = GalateaTheme.Red;
            }
            else
            {
                message = "Quest not detected. Connect USB and allow USB debugging inside the headset.";
                color = GalateaTheme.Red;
            }
            questStatus.Text = message;
            questStatus.ForeColor = color;

            string snapshotValue = snapshot.ToString();
            if (!String.Equals(lastDeviceSnapshot, snapshotValue, StringComparison.Ordinal))
            {
                lastDeviceSnapshot = snapshotValue;
                AppendLog(message);
            }
        }

        private void ReplaceQuestChoices(List<AdbDevice> quests)
        {
            string selectedSerial = null;
            AdbDevice selected = devices.SelectedItem as AdbDevice;
            if (selected != null) selectedSerial = selected.Serial;

            bool changed = devices.Items.Count != quests.Count;
            if (!changed)
            {
                for (int index = 0; index < quests.Count; index++)
                {
                    AdbDevice current = devices.Items[index] as AdbDevice;
                    if (current == null ||
                        !String.Equals(current.Serial, quests[index].Serial, StringComparison.OrdinalIgnoreCase) ||
                        !String.Equals(current.Model, quests[index].Model, StringComparison.Ordinal))
                    {
                        changed = true;
                        break;
                    }
                }
            }
            if (!changed) return;

            devices.BeginUpdate();
            try
            {
                devices.Items.Clear();
                foreach (AdbDevice quest in quests) devices.Items.Add(quest);
                int selectedIndex = -1;
                if (!String.IsNullOrEmpty(selectedSerial))
                {
                    for (int index = 0; index < quests.Count; index++)
                    {
                        if (String.Equals(quests[index].Serial, selectedSerial, StringComparison.OrdinalIgnoreCase))
                        {
                            selectedIndex = index;
                            break;
                        }
                    }
                }
                if (selectedIndex < 0 && devices.Items.Count > 0) selectedIndex = 0;
                devices.SelectedIndex = selectedIndex;
            }
            finally
            {
                devices.EndUpdate();
            }
        }

        private async Task Install()
        {
            if (ProductProfile.Workflow == InstallerWorkflow.PersonalSourceKit)
            {
                await InstallSourceKit();
                return;
            }
            if (busy || devices.SelectedItem == null) return;
            string sourceDirectory = GameLocator.ResolveInstallDirectory(gamePath.Text);
            string validationProblem;
            if (!GameLocator.ValidateInstallDirectory(sourceDirectory, out validationProblem))
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
                await Task.Run(delegate { PerformInstall(selected, sourceDirectory); });
                progress.Value = 100;
                AppendLog("Installation and verification complete.");
                MessageBox.Show(this,
                    AddPostInstallInstructions(
                        ProductProfile.ProductName + " was installed successfully.\n\n" +
                        ProductProfile.GameName + " will open on the Quest. Put on the headset and complete any permission request that appears."),
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

        private async Task InstallSourceKit()
        {
            if (busy || devices.SelectedItem == null) return;
            if (!ExistingFileOrDirectory(gamePath.Text) || !ExistingFileOrDirectory(audioPath.Text))
            {
                MessageBox.Show(this, "Select your official game package and the required audio source first.",
                    "Source files", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult consent = MessageBox.Show(this,
                "The installer will build a personal mod from your own official game copy.\n\n" +
                "On the first run it may download hash-verified build tools and accept the Google Android SDK licenses on your behalf. " +
                "If Android reports a signing conflict, it may back up ADB-accessible saves/settings, uninstall only the GTA package, install the personal build, and restore that backup.\n\n" +
                "No Rockstar APK, game data, audio, or personal signing key is uploaded or redistributed. Continue?",
                "Build and installation consent", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (consent != DialogResult.Yes) return;

            AdbDevice selected = (AdbDevice)devices.SelectedItem;
            SetBusy(true, "Preparing the personal source kit...");
            progress.Value = 2;
            AppendLog("Starting a personal source build for Quest " + selected.Serial + ".");
            AppendLog("The first build is large and can take a considerable time while verified tools are downloaded.");
            try
            {
                SourceKitBundle.EnsureExtracted();
                AdbBundle.EnsureExtracted();
                string script = Path.Combine(SourceKitBundle.SourceRoot, ProductProfile.SourceKitBuildScript);
                string arguments = "-GamePackage " + ProcessRunner.Quote(gamePath.Text.Trim().Trim('"')) +
                    " -AudioSource " + ProcessRunner.Quote(audioPath.Text.Trim().Trim('"')) +
                    " -WorkDir " + ProcessRunner.Quote(SourceKitBundle.PersistentWorkDirectory) +
                    " -AdbPath " + ProcessRunner.Quote(AdbBundle.AdbPath) +
                    " -Serial " + ProcessRunner.Quote(selected.Serial) +
                    " -NonInteractive -AcceptAndroidLicenses -ApproveSignatureReplacement";
                SetProgress(8, "Validating sources and preparing the build environment...");
                int exitCode = await Task.Run(delegate
                {
                    return ProcessRunner.RunPowerShell(script, arguments, AppendLogThreadSafe);
                });
                if (exitCode != 0)
                    throw new InvalidOperationException("The personal build or installation did not complete. Review the final messages in the log above.");
                progress.Value = 100;
                questStatus.Text = "✓ Personal build installed and verified. The game was left stopped.";
                questStatus.ForeColor = GalateaTheme.Green;
                AppendLog("Build, installation and verification complete.");
                MessageBox.Show(this,
                    AddPostInstallInstructions(
                        ProductProfile.ProductName + " was built from your official copy and installed successfully.\n\n" +
                        "The game was intentionally left stopped. Open it from the Quest library when ready."),
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

        private static string AddPostInstallInstructions(string message)
        {
            if (string.IsNullOrWhiteSpace(ProductProfile.PostInstallInstructions)) return message;
            return message + "\n\n" + ProductProfile.PostInstallInstructions;
        }

        private void PerformInstall(AdbDevice device, string sourceDirectory)
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
            RequireSuccess(AdbClient.Run(device.Serial, "shell mkdir -p " + ProductProfile.DevicePayloadRoot, 15000),
                "Unable to create " + ProductProfile.DevicePayloadRoot + ".");

            if (!String.IsNullOrWhiteSpace(ProductProfile.CommandLineValue) &&
                !String.IsNullOrWhiteSpace(ProductProfile.CommandLineDeviceRelativePath))
            {
                string commandFile = Path.Combine(AdbBundle.WorkDirectory, "commandline.txt");
                File.WriteAllText(commandFile, ProductProfile.CommandLineValue + "\n", new UTF8Encoding(false));
                string commandDestination = CombineDevicePath(ProductProfile.DeviceRoot,
                    ProductProfile.CommandLineDeviceRelativePath);
                RequireSuccess(AdbClient.Run(device.Serial,
                    "push " + AdbClient.Quote(commandFile) + " " + commandDestination, 30000),
                    "Unable to configure " + ProductProfile.GameName + " on the Quest.");
            }

            // On Quest/Android 11+, this app-op is the programmatic equivalent
            // of the all-files access page. Failure is non-fatal: the headset
            // will present the permission screen on first launch instead.
            if (ProductProfile.GrantAllFilesAccess)
            {
                AdbClient.Run(device.Serial,
                    "shell appops set " + ProductProfile.AndroidPackage + " MANAGE_EXTERNAL_STORAGE allow", 15000);
            }

            PayloadRule[] payload = GameLocator.RequiredPayload;
            for (int i = 0; i < payload.Length; ++i)
            {
                PayloadRule rule = payload[i];
                int percent = 38 + (int)(45.0 * i / Math.Max(1, payload.Length));
                string source = Path.Combine(sourceDirectory, rule.SourceRelativePath);
                string destination = CombineDevicePath(ProductProfile.DevicePayloadRoot,
                    rule.DestinationRelativePath);
                SetProgress(percent, "Copying " + rule.SourceRelativePath + "...");

                if (rule.EntryType == PayloadEntryType.File)
                {
                    string destinationParent = GetDeviceParent(destination);
                    RequireSuccess(AdbClient.Run(device.Serial,
                        "shell mkdir -p " + destinationParent, 15000),
                        "Unable to create " + destinationParent + ".");
                    RequireSuccess(AdbClient.Run(device.Serial,
                        "push " + AdbClient.Quote(source) + " " + destination, 600000),
                        "Failed to copy " + rule.SourceRelativePath + ". Check the cable and free space on the Quest.");

                    long localSize = new FileInfo(source).Length;
                    AdbResult remote = AdbClient.Run(device.Serial,
                        "shell stat -c %s " + destination, 20000);
                    long remoteSize;
                    if (!remote.Success || !Int64.TryParse(remote.Output.Trim(), out remoteSize) ||
                        remoteSize != localSize)
                    {
                        throw new InvalidOperationException("Verification failed for " +
                            rule.SourceRelativePath + ". The file on the Quest has an unexpected size.");
                    }
                    AppendLogThreadSafe("Verified: " + rule.SourceRelativePath +
                        " (" + localSize + " bytes)");
                }
                else
                {
                    RequireSuccess(AdbClient.Run(device.Serial,
                        "shell mkdir -p " + destination, 15000),
                        "Unable to create " + destination + ".");
                    string directoryContents = Path.Combine(source, ".");
                    RequireSuccess(AdbClient.Run(device.Serial,
                        "push " + AdbClient.Quote(directoryContents) + " " + destination + "/", 1800000),
                        "Failed to copy folder " + rule.SourceRelativePath + ". Check the cable and free space on the Quest.");
                    RequireSuccess(AdbClient.Run(device.Serial,
                        "shell test -d " + destination, 20000),
                        "Verification failed for folder " + rule.SourceRelativePath + ".");
                    AppendLogThreadSafe("Verified folder: " + rule.SourceRelativePath);
                }
            }

            SetProgress(88, "Game data copied and verified.");
            SetProgress(96, "Launching " + ProductProfile.ProductName + "...");
            AdbClient.Run(device.Serial, "shell am force-stop " + ProductProfile.AndroidPackage, 15000);
            AdbResult launch = AdbClient.Run(device.Serial,
                "shell monkey -p " + ProductProfile.AndroidPackage + " -c android.intent.category.LAUNCHER 1", 30000);
            RequireSuccess(launch, "The files were installed, but the app could not be launched automatically. Open " + ProductProfile.ProductName + " from Library > Unknown Sources.");
        }

        private static string CombineDevicePath(string root, string relative)
        {
            if (String.IsNullOrWhiteSpace(relative)) return root.TrimEnd('/');
            return root.TrimEnd('/') + "/" + relative.Replace('\\', '/').Trim('/');
        }

        private static string GetDeviceParent(string path)
        {
            int separator = path.LastIndexOf('/');
            return separator > 0 ? path.Substring(0, separator) : path;
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
            browseGameFileButton.Enabled = !value;
            browseGameFolderButton.Enabled = !value;
            extractPhoneButton.Enabled = !value;
            browseAudioFileButton.Enabled = !value;
            browseAudioFolderButton.Enabled = !value;
            refreshButton.Enabled = !value && !deviceRefreshActive;
            gamePath.Enabled = !value;
            audioPath.Enabled = !value;
            devices.Enabled = !value;
            if (!String.IsNullOrEmpty(message)) AppendLog(message);
            UpdateInstallState();
        }

        private void UpdateInstallState()
        {
            if (ProductProfile.Workflow == InstallerWorkflow.PersonalSourceKit)
            {
                installButton.Enabled = !busy && !deviceRefreshActive && devices.SelectedItem != null &&
                    ExistingFileOrDirectory(gamePath.Text) && ExistingFileOrDirectory(audioPath.Text);
                return;
            }
            string resolved = GameLocator.ResolveInstallDirectory(gamePath.Text);
            string problem;
            installButton.Enabled = !busy && !deviceRefreshActive && devices.SelectedItem != null &&
                GameLocator.ValidateInstallDirectory(resolved, out problem);
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
        internal static readonly PayloadRule[] RequiredPayload = ProductProfile.Payload;

        internal static List<string> FindKnownInstallations()
        {
            List<string> result = new List<string>();
            HashSet<string> candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddSteamCandidates(candidates);
            AddUninstallRegistryCandidates(candidates);
            AddCommonCandidates(candidates);

            foreach (string candidate in candidates)
            {
                string resolved = ResolveInstallDirectory(candidate);
                string problem;
                if (!String.IsNullOrEmpty(resolved) && ValidateInstallDirectory(resolved, out problem) && !result.Contains(resolved))
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
                foreach (string appId in ProductProfile.SteamAppIds)
                {
                    string manifest = Path.Combine(steamApps, "appmanifest_" + appId + ".acf");
                    if (File.Exists(manifest))
                    {
                        try
                        {
                            Match match = Regex.Match(File.ReadAllText(manifest), "\\\"installdir\\\"\\s+\\\"([^\\\"]+)\\\"", RegexOptions.IgnoreCase);
                            if (match.Success) candidates.Add(Path.Combine(steamApps, "common", match.Groups[1].Value));
                        }
                        catch { }
                    }
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
                                if (!MatchesAny(display, ProductProfile.RegistrySearchTerms)) continue;
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
            string[] roots = ProductProfile.CommonSearchRoots;
            string[] names = ProductProfile.CommonInstallNames;
            foreach (string root in roots)
            foreach (string parent in ProductProfile.CommonInstallParentPaths)
            foreach (string name in names)
            {
                if (String.IsNullOrEmpty(root)) continue;
                candidates.Add(String.IsNullOrEmpty(parent)
                    ? Path.Combine(root, name)
                    : Path.Combine(root, parent, name));
            }

            string besideInstaller = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "game-data-local", ProductProfile.LocalBundleFolder);
            candidates.Add(besideInstaller);
        }

        private static bool MatchesAny(string value, string[] terms)
        {
            if (String.IsNullOrWhiteSpace(value)) return false;
            foreach (string term in terms)
            {
                if (!String.IsNullOrWhiteSpace(term) &&
                    value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
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
                string resolved = ResolveInstallDirectory(node.Path);
                string problem;
                if (!String.IsNullOrEmpty(resolved) && ValidateInstallDirectory(resolved, out problem)) return resolved;
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

        internal static string ResolveInstallDirectory(string selected)
        {
            if (String.IsNullOrWhiteSpace(selected)) return null;
            string value = selected.Trim().Trim('"');
            foreach (string relative in ProductProfile.InstallRootCandidates)
            {
                string candidate = String.IsNullOrWhiteSpace(relative)
                    ? value : Path.Combine(value, relative);
                if (HasRequiredPayload(candidate)) return Path.GetFullPath(candidate);
            }
            return null;
        }

        private static bool HasRequiredPayload(string directory)
        {
            if (!Directory.Exists(directory)) return false;
            foreach (PayloadRule rule in RequiredPayload)
            {
                string path = Path.Combine(directory, rule.SourceRelativePath);
                if (rule.EntryType == PayloadEntryType.File && !File.Exists(path)) return false;
                if (rule.EntryType == PayloadEntryType.Directory && !Directory.Exists(path)) return false;
            }
            return true;
        }

        internal static bool ValidateInstallDirectory(string directory, out string problem)
        {
            problem = "";
            if (String.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                problem = "Select the " + ProductProfile.GameName + " installation folder.";
                return false;
            }

            foreach (PayloadRule rule in RequiredPayload)
            {
                string path = Path.Combine(directory, rule.SourceRelativePath);
                if (rule.EntryType == PayloadEntryType.Directory)
                {
                    if (!Directory.Exists(path))
                    {
                        problem = "Missing folder: " + rule.SourceRelativePath + ". Make sure you selected " +
                            ProductProfile.GameName + ", not another game.";
                        return false;
                    }
                    try
                    {
                        if (Directory.GetFileSystemEntries(path).Length == 0)
                        {
                            problem = rule.SourceRelativePath + " is empty.";
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        problem = "Unable to validate " + rule.SourceRelativePath + ": " + ex.Message;
                        return false;
                    }
                    continue;
                }

                if (!File.Exists(path))
                {
                    problem = "Missing file: " + rule.SourceRelativePath + ". Make sure you selected " +
                        ProductProfile.GameName + ", not another game.";
                    return false;
                }

                try
                {
                    FileInfo info = new FileInfo(path);
                    if (info.Length < rule.MinimumBytes)
                    {
                        problem = rule.SourceRelativePath + " appears to be incomplete or invalid.";
                        return false;
                    }

                    if (rule.Validation == PayloadValidation.ZipArchive)
                    {
                        using (ZipArchive archive = ZipFile.OpenRead(path))
                        {
                            if (archive.Entries.Count == 0)
                            {
                                problem = rule.SourceRelativePath + " does not contain valid archive data.";
                                return false;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    problem = "Unable to validate " + rule.SourceRelativePath + ": " + ex.Message;
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
        internal string State;
        internal string Model;
        internal string Manufacturer;
        internal bool IsQuest;
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
                bool productReady = ProductProfile.Workflow == InstallerWorkflow.PersonalSourceKit ||
                    (!String.IsNullOrEmpty(ApkPath) && File.Exists(ApkPath));
                if (!String.IsNullOrEmpty(AdbPath) && File.Exists(AdbPath) && productReady) return;
                WorkDirectory = Path.Combine(Path.GetTempPath(), ProductProfile.TempFolderName);
                Directory.CreateDirectory(WorkDirectory);
                AdbPath = Extract("Installer.adb.exe", "adb.exe");
                Extract("Installer.AdbWinApi.dll", "AdbWinApi.dll");
                Extract("Installer.AdbWinUsbApi.dll", "AdbWinUsbApi.dll");
                Extract("Installer.ADB-NOTICE.txt", "ADB-NOTICE.txt");
                if (ProductProfile.Workflow == InstallerWorkflow.EmbeddedApkWithPayload)
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

    internal static class SourceKitBundle
    {
        private static readonly object Sync = new object();
        internal static string SourceRoot { get; private set; }
        internal static string PersistentWorkDirectory { get; private set; }

        internal static void EnsureExtracted()
        {
            if (ProductProfile.Workflow != InstallerWorkflow.PersonalSourceKit)
                throw new InvalidOperationException("This profile does not use a personal source kit.");
            lock (Sync)
            {
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (String.IsNullOrWhiteSpace(local))
                    throw new InvalidOperationException("Windows Local AppData is unavailable.");
                string productRoot = Path.Combine(local, ProductProfile.SourceKitWorkFolder);
                PersistentWorkDirectory = Path.Combine(productRoot, "work");
                SourceRoot = Path.Combine(productRoot, "source-kit-" + ProductProfile.SourceKitVersion);
                string buildScript = Path.Combine(SourceRoot, ProductProfile.SourceKitBuildScript);
                string exportScript = Path.Combine(SourceRoot, ProductProfile.SourceKitExportScript);
                string expectedHash = GetEmbeddedHash();
                string marker = Path.Combine(SourceRoot, ".galatea-source-kit.sha256");
                string existingHash = File.Exists(marker) ? File.ReadAllText(marker).Trim() : "";
                if (File.Exists(buildScript) && File.Exists(exportScript) &&
                    String.Equals(existingHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    Directory.CreateDirectory(PersistentWorkDirectory);
                    return;
                }

                if (Directory.Exists(SourceRoot))
                {
                    string aside = SourceRoot + ".incomplete-" + Guid.NewGuid().ToString("N");
                    Directory.Move(SourceRoot, aside);
                }
                string staging = SourceRoot + ".extract-" + Guid.NewGuid().ToString("N");
                Directory.CreateDirectory(staging);
                try
                {
                    Assembly assembly = Assembly.GetExecutingAssembly();
                    using (Stream resource = assembly.GetManifestResourceStream(ProductProfile.SourceKitResource))
                    {
                        if (resource == null)
                            throw new InvalidOperationException("Missing embedded source kit: " + ProductProfile.SourceKitResource);
                        using (ZipArchive archive = new ZipArchive(resource, ZipArchiveMode.Read, false))
                        {
                            string stagingRoot = Path.GetFullPath(staging).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                            foreach (ZipArchiveEntry entry in archive.Entries)
                            {
                                string relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                                string target = Path.GetFullPath(Path.Combine(staging, relative));
                                if (!target.StartsWith(stagingRoot, StringComparison.OrdinalIgnoreCase))
                                    throw new InvalidOperationException("Unsafe path in embedded source kit: " + entry.FullName);
                                if (String.IsNullOrEmpty(entry.Name))
                                {
                                    Directory.CreateDirectory(target);
                                    continue;
                                }
                                string parent = Path.GetDirectoryName(target);
                                if (!String.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                                using (Stream input = entry.Open())
                                using (FileStream output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.Read))
                                    input.CopyTo(output);
                            }
                        }
                    }
                    if (!File.Exists(Path.Combine(staging, ProductProfile.SourceKitBuildScript)) ||
                        !File.Exists(Path.Combine(staging, ProductProfile.SourceKitExportScript)))
                        throw new InvalidOperationException("The embedded source kit is incomplete.");
                    File.WriteAllText(Path.Combine(staging, ".galatea-source-kit.sha256"),
                        expectedHash + Environment.NewLine, new UTF8Encoding(false));
                    Directory.Move(staging, SourceRoot);
                }
                catch
                {
                    if (Directory.Exists(staging))
                    {
                        string failed = staging + ".failed";
                        if (!Directory.Exists(failed)) Directory.Move(staging, failed);
                    }
                    throw;
                }
                Directory.CreateDirectory(PersistentWorkDirectory);
            }
        }

        private static string GetEmbeddedHash()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream resource = assembly.GetManifestResourceStream(ProductProfile.SourceKitResource))
            {
                if (resource == null)
                    throw new InvalidOperationException("Missing embedded source kit: " + ProductProfile.SourceKitResource);
                using (SHA256 sha = SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(resource);
                    StringBuilder value = new StringBuilder(hash.Length * 2);
                    foreach (byte item in hash) value.Append(item.ToString("X2"));
                    return value.ToString();
                }
            }
        }
    }

    internal static class ProcessRunner
    {
        internal static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        internal static int RunPowerShell(string scriptPath, string arguments, Action<string> output)
        {
            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = "powershell.exe";
            start.Arguments = "-NoLogo -NoProfile -ExecutionPolicy Bypass -File " + Quote(scriptPath) + " " + arguments;
            start.WorkingDirectory = Path.GetDirectoryName(scriptPath);
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            start.StandardOutputEncoding = Encoding.UTF8;
            start.StandardErrorEncoding = Encoding.UTF8;

            using (Process process = new Process())
            {
                process.StartInfo = start;
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (!String.IsNullOrWhiteSpace(e.Data)) output(e.Data);
                };
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (!String.IsNullOrWhiteSpace(e.Data)) output(e.Data);
                };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
                return process.ExitCode;
            }
        }
    }

    internal static class AdbClient
    {
        private static readonly object ManufacturerCacheSync = new object();
        private static readonly Dictionary<string, string> ManufacturerCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        internal static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        internal static List<AdbDevice> ListDevices()
        {
            AdbResult result = Run(null, "devices -l", 30000);
            if (!result.Success)
                throw new InvalidOperationException("ADB could not list connected devices: " +
                    (String.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error).Trim());
            return ParseDeviceListing(result.Output, LookupManufacturer);
        }

        internal static List<AdbDevice> ParseDeviceListing(string output, Func<string, string> manufacturerLookup)
        {
            List<AdbDevice> devices = new List<AdbDevice>();
            string[] lines = output.Replace("\r", "").Split('\n');
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("List of devices")) continue;
                string[] parts = Regex.Split(line, "\\s+");
                if (parts.Length < 2) continue;
                string state = parts[1];
                string model = "Android device";
                foreach (string part in parts)
                    if (part.StartsWith("model:")) model = part.Substring(6).Replace('_', ' ');
                string serial = parts[0];
                string manufacturer = "";
                if (String.Equals(state, "device", StringComparison.OrdinalIgnoreCase) && manufacturerLookup != null)
                    manufacturer = manufacturerLookup(serial);
                bool isQuest = Regex.IsMatch(manufacturer + " " + model, "(?i)\\b(oculus|quest|meta)\\b");
                devices.Add(new AdbDevice
                {
                    Serial = serial,
                    State = state,
                    Model = model,
                    Manufacturer = manufacturer,
                    IsQuest = isQuest
                });
            }
            return devices;
        }

        private static string LookupManufacturer(string serial)
        {
            lock (ManufacturerCacheSync)
            {
                string cached;
                if (ManufacturerCache.TryGetValue(serial, out cached)) return cached;
            }
            AdbResult result = Run(serial, "shell getprop ro.product.manufacturer", 10000);
            string manufacturer = result.Success ? result.Output.Trim() : "";
            lock (ManufacturerCacheSync) ManufacturerCache[serial] = manufacturer;
            return manufacturer;
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
                report.Add("Profile=" + ProductProfile.ProfileId);
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

                if (!String.IsNullOrWhiteSpace(ProductProfile.CommandLineValue) &&
                    !String.IsNullOrWhiteSpace(ProductProfile.CommandLineApkAssetPath))
                {
                    using (ZipArchive apk = ZipFile.OpenRead(AdbBundle.ApkPath))
                    {
                        ZipArchiveEntry command = apk.GetEntry(ProductProfile.CommandLineApkAssetPath);
                        if (command == null)
                            throw new InvalidOperationException(ProductProfile.CommandLineApkAssetPath + " is missing from the APK.");
                        using (StreamReader reader = new StreamReader(command.Open()))
                        {
                            string value = reader.ReadToEnd().Trim();
                            if (value != ProductProfile.CommandLineValue)
                                throw new InvalidOperationException("APK is not configured for " + ProductProfile.GameName + ": " + value);
                            report.Add("APK-commandline=" + ProductProfile.CommandLineValue);
                        }
                    }
                }

                AdbResult adb = AdbClient.Run(null, "version", 30000);
                if (!adb.Success || adb.Output.IndexOf("Android Debug Bridge", StringComparison.OrdinalIgnoreCase) < 0)
                    throw new InvalidOperationException("Embedded ADB did not start: " + adb.Error);
                report.Add("ADB=OK");

                List<AdbDevice> parsedDevices = AdbClient.ParseDeviceListing(
                    "QUEST123 device product:hollywood model:Quest_3 transport_id:1\n" +
                    "PENDING456 unauthorized usb:1-2 transport_id:2\n" +
                    "OFFLINE789 offline transport_id:3\n",
                    delegate(string serial) { return serial == "QUEST123" ? "Meta" : ""; });
                bool parsedQuest = false;
                bool parsedUnauthorized = false;
                bool parsedOffline = false;
                foreach (AdbDevice parsed in parsedDevices)
                {
                    if (parsed.Serial == "QUEST123" && parsed.State == "device" && parsed.IsQuest) parsedQuest = true;
                    if (parsed.Serial == "PENDING456" && parsed.State == "unauthorized") parsedUnauthorized = true;
                    if (parsed.Serial == "OFFLINE789" && parsed.State == "offline") parsedOffline = true;
                }
                if (parsedDevices.Count != 3 || !parsedQuest || !parsedUnauthorized || !parsedOffline)
                    throw new InvalidOperationException("ADB device-state parsing did not preserve ready, unauthorized and offline devices.");
                report.Add("ADB-device-states=OK");

                if (ProductProfile.Workflow == InstallerWorkflow.PersonalSourceKit)
                {
                    SourceKitBundle.EnsureExtracted();
                    if (!File.Exists(Path.Combine(SourceKitBundle.SourceRoot, ProductProfile.SourceKitBuildScript)) ||
                        !File.Exists(Path.Combine(SourceKitBundle.SourceRoot, ProductProfile.SourceKitExportScript)))
                        throw new InvalidOperationException("The source-kit entry points were not extracted.");
                    report.Add("Source-kit=OK");
                }
                else
                {
                    string testRoot = Path.Combine(Path.GetTempPath(),
                        "Galatea-installer-path-test-" + ProductProfile.ProfileId);
                    if (Directory.Exists(testRoot)) Directory.Delete(testRoot, true);
                    Directory.CreateDirectory(testRoot);
                    if (GameLocator.ResolveInstallDirectory(testRoot) != null)
                        throw new InvalidOperationException("A folder without the required payload was accepted incorrectly.");
                    report.Add("Path-negative=OK");

                    string nestedCandidate = "";
                    foreach (string candidate in ProductProfile.InstallRootCandidates)
                    {
                        if (!String.IsNullOrWhiteSpace(candidate)) nestedCandidate = candidate;
                    }
                    string installDirectory = String.IsNullOrWhiteSpace(nestedCandidate)
                        ? testRoot : Path.Combine(testRoot, nestedCandidate);
                    Directory.CreateDirectory(installDirectory);

                    foreach (PayloadRule rule in GameLocator.RequiredPayload)
                        CreateTestPayload(installDirectory, rule);

                    string problem;
                    if (!GameLocator.ValidateInstallDirectory(installDirectory, out problem))
                        throw new InvalidOperationException("Positive validation failed: " + problem);
                    string expected = Path.GetFullPath(installDirectory);
                    if (!String.Equals(GameLocator.ResolveInstallDirectory(testRoot), expected, StringComparison.OrdinalIgnoreCase) ||
                        !String.Equals(GameLocator.ResolveInstallDirectory(installDirectory), expected, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Profile install-root resolution did not produce the expected folder.");
                    report.Add("Path-profile-resolution=OK");
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
                }
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

        private static void CreateTestPayload(string installDirectory, PayloadRule rule)
        {
            string path = Path.Combine(installDirectory, rule.SourceRelativePath);
            if (rule.EntryType == PayloadEntryType.Directory)
            {
                Directory.CreateDirectory(path);
                File.WriteAllText(Path.Combine(path, "galatea-self-test.txt"), "test");
                return;
            }

            string parent = Path.GetDirectoryName(path);
            if (!String.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
            if (rule.Validation == PayloadValidation.ZipArchive)
            {
                using (ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create))
                {
                    ZipArchiveEntry entry = archive.CreateEntry("test/asset.bin", CompressionLevel.NoCompression);
                    using (Stream output = entry.Open())
                    {
                        byte[] block = new byte[65536];
                        long remaining = Math.Max(rule.MinimumBytes, block.Length);
                        while (remaining > 0)
                        {
                            int count = (int)Math.Min(block.Length, remaining);
                            output.Write(block, 0, count);
                            remaining -= count;
                        }
                    }
                }
            }
            else
            {
                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write))
                    stream.SetLength(Math.Max(1, rule.MinimumBytes));
            }
        }
    }
}
