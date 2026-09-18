using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]

namespace JKXRTabletopInstaller
{
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

    internal sealed class InstallerForm : Form
    {
        private readonly TextBox gamePath = new TextBox();
        private readonly Label gameStatus = new Label();
        private readonly ComboBox devices = new ComboBox();
        private readonly Label questStatus = new Label();
        private readonly Button detectButton = new Button();
        private readonly Button fullScanButton = new Button();
        private readonly Button browseButton = new Button();
        private readonly Button refreshButton = new Button();
        private readonly Button installButton = new Button();
        private readonly ProgressBar progress = new ProgressBar();
        private readonly TextBox log = new TextBox();
        private readonly Label footer = new Label();
        private bool busy;

        public InstallerForm()
        {
            Text = "JKXR Tabletop V0 — Instalador";
            ClientSize = new Size(820, 700);
            MinimumSize = new Size(760, 640);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);
            BackColor = Color.FromArgb(245, 247, 251);
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            BuildInterface();
            Shown += async delegate
            {
                await DetectGame(false);
                await RefreshDevices();
            };
        }

        private void BuildInterface()
        {
            Label title = new Label();
            title.Text = "JKXR Tabletop V0";
            title.Font = new Font("Segoe UI Semibold", 21F, FontStyle.Bold);
            title.ForeColor = Color.FromArgb(29, 43, 72);
            title.AutoSize = true;
            title.Location = new Point(24, 18);
            Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "Instala o protótipo e os seus arquivos legais de Jedi Academy diretamente no Meta Quest.";
            subtitle.ForeColor = Color.FromArgb(72, 82, 103);
            subtitle.AutoSize = true;
            subtitle.Location = new Point(27, 60);
            Controls.Add(subtitle);

            GroupBox gameGroup = new GroupBox();
            gameGroup.Text = "1. Jedi Academy no computador";
            gameGroup.Location = new Point(24, 94);
            gameGroup.Size = new Size(772, 166);
            gameGroup.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(gameGroup);

            gamePath.Location = new Point(16, 31);
            gamePath.Size = new Size(626, 25);
            gamePath.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            gamePath.TextChanged += delegate { ValidateCurrentPath(); };
            gameGroup.Controls.Add(gamePath);

            browseButton.Text = "Escolher pasta...";
            browseButton.Location = new Point(650, 29);
            browseButton.Size = new Size(106, 29);
            browseButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            browseButton.Click += BrowseClicked;
            gameGroup.Controls.Add(browseButton);

            detectButton.Text = "Detectar Steam/GOG";
            detectButton.Location = new Point(16, 70);
            detectButton.Size = new Size(150, 31);
            detectButton.Click += async delegate { await DetectGame(true); };
            gameGroup.Controls.Add(detectButton);

            fullScanButton.Text = "Buscar nos discos";
            fullScanButton.Location = new Point(174, 70);
            fullScanButton.Size = new Size(140, 31);
            fullScanButton.Click += async delegate { await FullScan(); };
            gameGroup.Controls.Add(fullScanButton);

            gameStatus.Location = new Point(16, 112);
            gameStatus.Size = new Size(740, 40);
            gameStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            gameStatus.Text = "Aguardando a pasta do jogo.";
            gameStatus.ForeColor = Color.FromArgb(110, 80, 20);
            gameGroup.Controls.Add(gameStatus);

            GroupBox questGroup = new GroupBox();
            questGroup.Text = "2. Meta Quest por USB";
            questGroup.Location = new Point(24, 272);
            questGroup.Size = new Size(772, 112);
            questGroup.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(questGroup);

            devices.DropDownStyle = ComboBoxStyle.DropDownList;
            devices.Location = new Point(16, 31);
            devices.Size = new Size(626, 25);
            devices.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            devices.SelectedIndexChanged += delegate { UpdateInstallState(); };
            questGroup.Controls.Add(devices);

            refreshButton.Text = "Atualizar";
            refreshButton.Location = new Point(650, 29);
            refreshButton.Size = new Size(106, 29);
            refreshButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            refreshButton.Click += async delegate { await RefreshDevices(); };
            questGroup.Controls.Add(refreshButton);

            questStatus.Location = new Point(16, 69);
            questStatus.Size = new Size(740, 31);
            questStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            questStatus.Text = "Ative o Modo Desenvolvedor, conecte o Quest e aceite a depuração USB dentro do headset.";
            questStatus.ForeColor = Color.FromArgb(72, 82, 103);
            questGroup.Controls.Add(questStatus);

            installButton.Text = "INSTALAR NO QUEST";
            installButton.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold);
            installButton.BackColor = Color.FromArgb(48, 92, 196);
            installButton.ForeColor = Color.White;
            installButton.FlatStyle = FlatStyle.Flat;
            installButton.FlatAppearance.BorderSize = 0;
            installButton.Enabled = false;
            installButton.Location = new Point(24, 400);
            installButton.Size = new Size(772, 46);
            installButton.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            installButton.Click += async delegate { await Install(); };
            Controls.Add(installButton);

            progress.Location = new Point(24, 457);
            progress.Size = new Size(772, 18);
            progress.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            progress.Minimum = 0;
            progress.Maximum = 100;
            Controls.Add(progress);

            log.Location = new Point(24, 487);
            log.Size = new Size(772, 163);
            log.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            log.Multiline = true;
            log.ReadOnly = true;
            log.ScrollBars = ScrollBars.Vertical;
            log.BackColor = Color.FromArgb(26, 31, 43);
            log.ForeColor = Color.FromArgb(219, 226, 239);
            log.Font = new Font("Consolas", 8.5F);
            Controls.Add(log);

            footer.Text = "O instalador não contém o jogo. Ele copia somente assets0.pk3–assets3.pk3 da sua instalação legal.";
            footer.ForeColor = Color.FromArgb(92, 102, 123);
            footer.Location = new Point(24, 662);
            footer.Size = new Size(772, 24);
            footer.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(footer);
        }

        private async Task DetectGame(bool announceFailure)
        {
            if (busy) return;
            SetBusy(true, "Procurando instalações Steam, GOG e LucasArts...");
            List<string> found = await Task.Run(delegate { return GameLocator.FindKnownInstallations(); });
            SetBusy(false, null);

            if (found.Count > 0)
            {
                gamePath.Text = found[0];
                AppendLog("Jogo detectado: " + found[0]);
            }
            else if (announceFailure)
            {
                AppendLog("Nenhuma instalação registrada foi encontrada. Use a busca nos discos ou escolha a pasta.");
                MessageBox.Show(this,
                    "Não encontrei o Jedi Academy nas bibliotecas registradas.\n\n" +
                    "Você pode usar ‘Buscar nos discos’ ou ‘Escolher pasta...’.",
                    "Jogo não localizado", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private async Task FullScan()
        {
            if (busy) return;
            SetBusy(true, "Fazendo busca completa nos discos. Isso pode demorar...");
            AppendLog("Busca completa iniciada nos discos fixos.");
            string found = await Task.Run(delegate { return GameLocator.FindOnFixedDrives(); });
            SetBusy(false, null);
            if (!String.IsNullOrEmpty(found))
            {
                gamePath.Text = found;
                AppendLog("Jogo encontrado pela busca completa: " + found);
            }
            else
            {
                AppendLog("Busca completa terminada sem localizar os quatro PK3.");
                MessageBox.Show(this, "O jogo não foi encontrado. Escolha manualmente a pasta da instalação.",
                    "Busca concluída", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void BrowseClicked(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Escolha a pasta do Jedi Academy, GameData ou GameData\\base";
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
                gameStatus.Text = "✓ Jedi Academy válido: assets0.pk3, assets1.pk3, assets2.pk3 e assets3.pk3 encontrados.";
                gameStatus.ForeColor = Color.FromArgb(22, 117, 67);
            }
            else
            {
                gameStatus.Text = String.IsNullOrEmpty(problem) ? "Selecione a pasta do Jedi Academy." : problem;
                gameStatus.ForeColor = Color.FromArgb(166, 71, 50);
            }
            UpdateInstallState();
        }

        private async Task RefreshDevices()
        {
            if (busy) return;
            SetBusy(true, "Procurando o Quest por USB...");
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
                AppendLog("Erro ao iniciar ADB: " + ex.Message);
            }
            SetBusy(false, null);

            devices.Items.Clear();
            foreach (AdbDevice device in found) devices.Items.Add(device);
            if (devices.Items.Count > 0)
            {
                devices.SelectedIndex = 0;
                questStatus.Text = "✓ Quest autorizado e pronto para receber o mod.";
                questStatus.ForeColor = Color.FromArgb(22, 117, 67);
                AppendLog("Quest detectado: " + devices.SelectedItem);
            }
            else
            {
                questStatus.Text = "Quest não detectado. Conecte o USB e aceite ‘Permitir depuração USB’ dentro do headset.";
                questStatus.ForeColor = Color.FromArgb(166, 71, 50);
                AppendLog("Nenhum dispositivo ADB autorizado foi encontrado.");
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
                MessageBox.Show(this, validationProblem, "Arquivos do jogo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            AdbDevice selected = (AdbDevice)devices.SelectedItem;
            SetBusy(true, "Instalando no Quest...");
            progress.Value = 2;
            AppendLog("Iniciando instalação em " + selected.Serial + ".");

            try
            {
                await Task.Run(delegate { PerformInstall(selected, baseDirectory); });
                progress.Value = 100;
                AppendLog("Instalação e verificação concluídas.");
                MessageBox.Show(this,
                    "JKXR Tabletop V0 foi instalado com sucesso.\n\n" +
                    "O Jedi Academy será aberto no Quest. Coloque o headset e conclua qualquer solicitação de permissão que aparecer.",
                    "Instalação concluída", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppendLog("FALHA: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Não foi possível instalar", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private void PerformInstall(AdbDevice device, string baseDirectory)
        {
            AdbBundle.EnsureExtracted();

            SetProgress(8, "Validando conexão USB...");
            AdbResult state = AdbClient.Run(device.Serial, "get-state", 15000);
            RequireSuccess(state, "O Quest deixou de responder. Reconecte o cabo USB.");

            SetProgress(18, "Instalando o APK JKXR Tabletop V0...");
            AdbResult install = AdbClient.Run(device.Serial,
                "install -r -d " + AdbClient.Quote(AdbBundle.ApkPath), 180000);
            RequireSuccess(install,
                "A instalação do APK falhou. Se houver outra versão do JKXR com assinatura diferente, desinstale apenas o aplicativo e tente novamente. A pasta /JKXR não é apagada.");

            SetProgress(30, "Preparando a pasta do Jedi Academy...");
            RequireSuccess(AdbClient.Run(device.Serial, "shell mkdir -p /sdcard/JKXR/JK3/base", 15000),
                "Não consegui criar /sdcard/JKXR/JK3/base.");

            string commandFile = Path.Combine(AdbBundle.WorkDirectory, "commandline.txt");
            File.WriteAllText(commandFile, "ja\n", new UTF8Encoding(false));
            RequireSuccess(AdbClient.Run(device.Serial,
                "push " + AdbClient.Quote(commandFile) + " /sdcard/JKXR/commandline.txt", 30000),
                "Não consegui selecionar o Jedi Academy no Quest.");

            // On Quest/Android 11+, this app-op is the programmatic equivalent
            // of the all-files access page. Failure is non-fatal: the headset
            // will present the permission screen on first launch instead.
            AdbClient.Run(device.Serial,
                "shell appops set com.drbeef.jkxr MANAGE_EXTERNAL_STORAGE allow", 15000);

            string[] files = GameLocator.RequiredFiles;
            for (int i = 0; i < files.Length; ++i)
            {
                int percent = 38 + i * 12;
                string name = files[i];
                string source = Path.Combine(baseDirectory, name);
                SetProgress(percent, "Copiando " + name + "...");
                AdbResult pushed = AdbClient.Run(device.Serial,
                    "push " + AdbClient.Quote(source) + " /sdcard/JKXR/JK3/base/" + name, 600000);
                RequireSuccess(pushed, "Falha ao copiar " + name + ". Verifique o cabo e o espaço livre no Quest.");
            }

            SetProgress(88, "Verificando os arquivos copiados...");
            foreach (string name in files)
            {
                long localSize = new FileInfo(Path.Combine(baseDirectory, name)).Length;
                AdbResult remote = AdbClient.Run(device.Serial,
                    "shell stat -c %s /sdcard/JKXR/JK3/base/" + name, 20000);
                long remoteSize;
                if (!remote.Success || !Int64.TryParse(remote.Output.Trim(), out remoteSize) || remoteSize != localSize)
                {
                    throw new InvalidOperationException("A verificação de " + name + " falhou. O arquivo no Quest não tem o tamanho esperado.");
                }
                AppendLogThreadSafe("Verificado: " + name + " (" + localSize + " bytes)");
            }

            SetProgress(96, "Abrindo o JKXR Tabletop V0...");
            AdbClient.Run(device.Serial, "shell am force-stop com.drbeef.jkxr", 15000);
            AdbResult launch = AdbClient.Run(device.Serial,
                "shell monkey -p com.drbeef.jkxr -c android.intent.category.LAUNCHER 1", 30000);
            RequireSuccess(launch, "Os arquivos foram instalados, mas não consegui abrir o aplicativo automaticamente. Abra JKXR Tabletop V0 pela Biblioteca > Fontes desconhecidas.");
        }

        private void RequireSuccess(AdbResult result, string friendlyMessage)
        {
            if (!String.IsNullOrWhiteSpace(result.Output)) AppendLogThreadSafe(result.Output.Trim());
            if (!result.Success)
            {
                string detail = String.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error;
                throw new InvalidOperationException(friendlyMessage + "\n\nDetalhe do ADB: " + detail.Trim());
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
            questStatus.ForeColor = Color.FromArgb(48, 92, 196);
            AppendLog(message);
        }

        private void SetBusy(bool value, string message)
        {
            busy = value;
            UseWaitCursor = value;
            detectButton.Enabled = !value;
            fullScanButton.Enabled = !value;
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
        internal static readonly string[] RequiredFiles =
            { "assets0.pk3", "assets1.pk3", "assets2.pk3", "assets3.pk3" };

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
                string manifest = Path.Combine(steamApps, "appmanifest_6020.acf");
                if (File.Exists(manifest))
                {
                    try
                    {
                        Match match = Regex.Match(File.ReadAllText(manifest), "\\\"installdir\\\"\\s+\\\"([^\\\"]+)\\\"", RegexOptions.IgnoreCase);
                        if (match.Success) candidates.Add(Path.Combine(steamApps, "common", match.Groups[1].Value));
                    }
                    catch { }
                }
                candidates.Add(Path.Combine(steamApps, "common", "Jedi Academy"));
                candidates.Add(Path.Combine(steamApps, "common", "Star Wars Jedi Knight - Jedi Academy"));
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
                                if (display.IndexOf("Jedi Academy", StringComparison.OrdinalIgnoreCase) < 0) continue;
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
            string[] names =
            {
                "Jedi Academy", "Star Wars Jedi Knight - Jedi Academy",
                "STAR WARS Jedi Knight - Jedi Academy"
            };
            foreach (string root in roots)
            foreach (string name in names)
            {
                if (String.IsNullOrEmpty(root)) continue;
                candidates.Add(Path.Combine(root, name));
                candidates.Add(Path.Combine(root, "GOG Galaxy", "Games", name));
                candidates.Add(Path.Combine(root, "LucasArts", name));
            }

            string besideInstaller = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "game-data-local", "JediAcademy");
            candidates.Add(besideInstaller);
        }

        internal static string FindOnFixedDrives()
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady || drive.DriveType != DriveType.Fixed) continue;
                string found = SearchTree(drive.RootDirectory.FullName, 9, 250000);
                if (!String.IsNullOrEmpty(found)) return found;
            }
            return null;
        }

        private static string SearchTree(string root, int maxDepth, int maxDirectories)
        {
            Stack<SearchNode> pending = new Stack<SearchNode>();
            pending.Push(new SearchNode(root, 0));
            int visited = 0;
            while (pending.Count > 0 && visited < maxDirectories)
            {
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
                problem = "Selecione a instalação do Jedi Academy, a pasta GameData ou GameData\\base.";
                return false;
            }

            foreach (string name in RequiredFiles)
            {
                string path = Path.Combine(directory, name);
                if (!File.Exists(path))
                {
                    problem = "Arquivo ausente: " + name + ". Certifique-se de selecionar o Jedi Academy, não o Jedi Outcast.";
                    return false;
                }
                try
                {
                    FileInfo info = new FileInfo(path);
                    if (info.Length < 1024 * 1024)
                    {
                        problem = name + " parece incompleto ou inválido.";
                        return false;
                    }
                    using (ZipArchive archive = ZipFile.OpenRead(path))
                    {
                        if (archive.Entries.Count == 0)
                        {
                            problem = name + " não contém dados válidos.";
                            return false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    problem = "Não foi possível validar " + name + ": " + ex.Message;
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
                WorkDirectory = Path.Combine(Path.GetTempPath(), "JKXRTabletopInstaller", "v0");
                Directory.CreateDirectory(WorkDirectory);
                AdbPath = Extract("Installer.adb.exe", "adb.exe");
                Extract("Installer.AdbWinApi.dll", "AdbWinApi.dll");
                Extract("Installer.AdbWinUsbApi.dll", "AdbWinUsbApi.dll");
                Extract("Installer.ADB-NOTICE.txt", "ADB-NOTICE.txt");
                ApkPath = Extract("Installer.JKXRTabletop.apk", "JKXR-tabletop-v0.apk");
            }
        }

        private static string Extract(string resourceName, string fileName)
        {
            string target = Path.Combine(WorkDirectory, fileName);
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream input = assembly.GetManifestResourceStream(resourceName))
            {
                if (input == null) throw new InvalidOperationException("Recurso interno ausente: " + resourceName);
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
                    return new AdbResult { ExitCode = -1, Output = output.Result, Error = "Tempo limite excedido." };
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
                report.Add("JKXR Tabletop Installer self-test");
                report.Add("Assembly=" + Assembly.GetExecutingAssembly().GetName().Version);
                AdbBundle.EnsureExtracted();
                report.Add("Resources=OK");

                using (ZipArchive apk = ZipFile.OpenRead(AdbBundle.ApkPath))
                {
                    ZipArchiveEntry command = apk.GetEntry("assets/commandline.txt");
                    if (command == null) throw new InvalidOperationException("commandline.txt ausente no APK.");
                    using (StreamReader reader = new StreamReader(command.Open()))
                    {
                        string value = reader.ReadToEnd().Trim();
                        if (value != "ja") throw new InvalidOperationException("APK não está configurado para Jedi Academy: " + value);
                        report.Add("APK-commandline=ja");
                    }
                }

                AdbResult adb = AdbClient.Run(null, "version", 30000);
                if (!adb.Success || adb.Output.IndexOf("Android Debug Bridge", StringComparison.OrdinalIgnoreCase) < 0)
                    throw new InvalidOperationException("ADB incorporado não iniciou: " + adb.Error);
                report.Add("ADB=OK");

                string testRoot = Path.Combine(Path.GetTempPath(), "JKXR-installer-path-test");
                string baseDirectory = Path.Combine(testRoot, "GameData", "base");
                if (Directory.Exists(testRoot)) Directory.Delete(testRoot, true);
                Directory.CreateDirectory(baseDirectory);
                if (GameLocator.ResolveBaseDirectory(testRoot) != null)
                    throw new InvalidOperationException("Uma pasta sem PK3 foi aceita incorretamente.");
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
                    throw new InvalidOperationException("Validação positiva falhou: " + problem);
                string expected = Path.GetFullPath(baseDirectory);
                if (!String.Equals(GameLocator.ResolveBaseDirectory(testRoot), expected, StringComparison.OrdinalIgnoreCase) ||
                    !String.Equals(GameLocator.ResolveBaseDirectory(Path.Combine(testRoot, "GameData")), expected, StringComparison.OrdinalIgnoreCase) ||
                    !String.Equals(GameLocator.ResolveBaseDirectory(baseDirectory), expected, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("A resolução jogo/GameData/base não produziu a mesma pasta.");
                report.Add("Path-root-GameData-base=OK");
                Directory.Delete(testRoot, true);
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
