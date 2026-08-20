using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

// 名前被り防止
using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;

namespace MinecraftServerGeneratorWpf
{
    public partial class MainWindow : Window
    {
        private Dictionary<string, string> _serverVersions = new Dictionary<string, string>();
        private const string VersionsUrl = "https://raw.githubusercontent.com/LEGOLANNDO/QuiCraft/main/servers.txt";

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadServerVersions();
        }

        private async Task LoadServerVersions()
        {
            try
            {
                LblStatus.Text = "バージョン情報を取得中...";
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(10);
                    string response = await client.GetStringAsync(VersionsUrl);

                    using (StringReader reader = new StringReader(response))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            line = line.Trim();
                            if (line.Contains(" = "))
                            {
                                var parts = line.Split(new[] { " = " }, StringSplitOptions.RemoveEmptyEntries);
                                if (parts.Length == 2)
                                {
                                    _serverVersions[parts[0]] = parts[1];
                                }
                            }
                        }
                    }
                }

                var sortedVersions = _serverVersions.Keys
                    .Select(v => {
                        Version.TryParse(v, out var parsed);
                        return new { Str = v, Ver = parsed };
                    })
                    .OrderByDescending(x => x.Ver)
                    .Select(x => x.Str)
                    .ToList();

                CmbVersions.ItemsSource = sortedVersions;

                if (sortedVersions.Count > 0)
                {
                    CmbVersions.SelectedIndex = 0;
                }

                LblStatus.Text = "準備完了";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"バージョン情報の取得に失敗しました。\n{ex.Message}", "ネットワークエラー", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        private void BtnSelectIcon_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "サーバーアイコンを選択",
                Filter = "PNGファイル|*.png"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var image = new BitmapImage();
                    using (var stream = File.OpenRead(dlg.FileName))
                    {
                        image.BeginInit();
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.StreamSource = stream;
                        image.EndInit();
                    }

                    if (image.PixelWidth == 64 && image.PixelHeight == 64)
                    {
                        TxtIconPath.Text = dlg.FileName;
                    }
                    else
                    {
                        TxtIconPath.Text = "";
                        MessageBox.Show($"画像サイズが64x64ではありません。(現在: {image.PixelWidth}x{image.PixelHeight})", "サイズエラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    TxtIconPath.Text = "";
                    MessageBox.Show($"ファイルを開けませんでした。\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnSelectWorld_Click(object sender, RoutedEventArgs e)
        {
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog())
            {
                dlg.Description = "既存ワールドフォルダを選択";
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    TxtWorldPath.Text = dlg.SelectedPath;
                }
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }

        private async void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            if (ChkEula.IsChecked != true)
            {
                MessageBox.Show("Minecraft EULA に同意してチェックを入れてください。", "確認", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string projectName = TxtProjectName.Text.Trim();
            if (string.IsNullOrEmpty(projectName))
            {
                MessageBox.Show("プロジェクト名を入力してください。", "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(TxtMinMem.Text, out int minMem) || !int.TryParse(TxtMaxMem.Text, out int maxMem) ||
                !int.TryParse(TxtMaxPlayers.Text, out _) || !int.TryParse(TxtServerPort.Text, out _) ||
                !int.TryParse(TxtViewDistance.Text, out _))
            {
                MessageBox.Show("数値項目（メモリ、人数、ポート、描画距離）は整数で入力してください。", "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnRun.IsEnabled = false;
            ProgressBarStatus.Value = 0;

            try
            {
                await RunGenerationProcess(projectName, minMem, maxMem);
                MessageBox.Show($"サーバーの作成が完了しました。\n'{projectName}' フォルダ内の run.bat を実行してください。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LblStatus.Text = "エラー発生";
                MessageBox.Show($"処理中にエラーが発生しました。\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnRun.IsEnabled = true;
                LblStatus.Text = "完了";
            }
        }

        private async Task RunGenerationProcess(string projectName, int minMem, int maxMem)
        {
            UpdateStatus($"プロジェクトフォルダ '{projectName}' を作成中...", 10);
            if (Directory.Exists(projectName))
            {
                var result = MessageBox.Show($"フォルダ '{projectName}' は既に存在します。上書きしますか？", "確認", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.No) throw new Exception("処理がキャンセルされました。");
                Directory.Delete(projectName, true);
            }
            Directory.CreateDirectory(projectName);

            string version = CmbVersions.SelectedItem as string;
            UpdateStatus($"サーバー ({version}) をダウンロード中...", 20);

            string url = _serverVersions[version];
            string jarName = $"server_{version}.jar";
            string jarPath = Path.Combine(projectName, jarName);

            using (HttpClient client = new HttpClient())
            {
                var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                using (var fs = new FileStream(jarPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await response.Content.CopyToAsync(fs);
                }
            }

            UpdateStatus("eula.txt を作成中...", 50);
            await File.WriteAllTextAsync(Path.Combine(projectName, "eula.txt"), "eula=true\n");

            UpdateStatus("run.bat を作成中...", 60);
            string runContent = $"java -Xmx{maxMem}G -Xms{minMem}G -jar {jarName} nogui\r\npause";
            await File.WriteAllTextAsync(Path.Combine(projectName, "run.bat"), runContent);

            if (!string.IsNullOrEmpty(TxtIconPath.Text) && File.Exists(TxtIconPath.Text))
            {
                UpdateStatus("サーバーアイコンをコピー中...", 70);
                File.Copy(TxtIconPath.Text, Path.Combine(projectName, "server-icon.png"), true);
            }

            string levelNameToSet;
            string seedToSet;
            string worldPath = TxtWorldPath.Text;

            if (!string.IsNullOrEmpty(worldPath) && Directory.Exists(worldPath))
            {
                UpdateStatus("既存ワールドをコピー中...", 80);
                levelNameToSet = new DirectoryInfo(worldPath).Name;
                seedToSet = "";
                CopyDirectory(worldPath, Path.Combine(projectName, levelNameToSet));
            }
            else
            {
                levelNameToSet = !string.IsNullOrWhiteSpace(TxtLevelName.Text) ? TxtLevelName.Text.Trim() : "world";
                seedToSet = TxtLevelSeed.Text.Trim();
            }

            UpdateStatus("server.properties を作成中...", 90);

            bool isLegacy = IsLegacyVersion(version);

            string gamemodeVal = (CmbGamemode.SelectedItem as ComboBoxItem)?.Content.ToString();
            string difficultyVal = (CmbDifficulty.SelectedItem as ComboBoxItem)?.Content.ToString();

            if (isLegacy)
            {
                gamemodeVal = ConvertGamemodeToLegacy(gamemodeVal);
                difficultyVal = ConvertDifficultyToLegacy(difficultyVal);
            }

            string motdVal = EscapeToUnicode(TxtMotd.Text);

            var sb = new StringBuilder();
            sb.AppendLine("spawn-protection=0");
            sb.AppendLine($"level-name={levelNameToSet}");
            sb.AppendLine($"level-seed={seedToSet}");
            sb.AppendLine($"gamemode={gamemodeVal}");
            sb.AppendLine($"difficulty={difficultyVal}");
            sb.AppendLine($"hardcore={ChkHardcore.IsChecked.GetValueOrDefault().ToString().ToLower()}");
            sb.AppendLine($"allow-flight={ChkAllowFlight.IsChecked.GetValueOrDefault().ToString().ToLower()}");
            // ★追加: コマンドブロックの設定 (true/false)
            sb.AppendLine($"enable-command-block={ChkEnableCommandBlock.IsChecked.GetValueOrDefault().ToString().ToLower()}");
            sb.AppendLine($"max-players={TxtMaxPlayers.Text.Trim()}");
            sb.AppendLine($"server-port={TxtServerPort.Text.Trim()}");
            sb.AppendLine($"view-distance={TxtViewDistance.Text.Trim()}");
            sb.AppendLine($"motd={motdVal}");

            await File.WriteAllTextAsync(Path.Combine(projectName, "server.properties"), sb.ToString(), Encoding.UTF8);

            UpdateStatus("完了！", 100);
        }

        private void UpdateStatus(string message, int progress)
        {
            LblStatus.Text = message;
            ProgressBarStatus.Value = progress;
        }

        private static void CopyDirectory(string sourceDir, string destinationDir)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists) throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            Directory.CreateDirectory(destinationDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath, true);
            }

            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir);
            }
        }

        // --- ヘルパーメソッド ---

        private bool IsLegacyVersion(string versionStr)
        {
            if (string.IsNullOrEmpty(versionStr)) return false;

            try
            {
                var parts = versionStr.Split('.');
                if (parts.Length >= 2)
                {
                    int major = int.Parse(parts[0]);
                    int minor = int.Parse(parts[1]);

                    // 1.13より前 (1.0 ～ 1.12)
                    if (major == 1 && minor < 13)
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }
            return false;
        }

        private string ConvertGamemodeToLegacy(string mode)
        {
            switch (mode)
            {
                case "survival": return "0";
                case "creative": return "1";
                case "adventure": return "2";
                case "spectator": return "4";
                default: return "0";
            }
        }

        private string ConvertDifficultyToLegacy(string difficulty)
        {
            switch (difficulty)
            {
                case "peaceful": return "0";
                case "easy": return "1";
                case "normal": return "2";
                case "hard": return "3";
                default: return "2";
            }
        }

        private string EscapeToUnicode(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";

            StringBuilder sb = new StringBuilder();
            foreach (char c in input)
            {
                if (c > 127)
                {
                    sb.Append("\\u");
                    sb.Append(((int)c).ToString("X4"));
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
    }
}