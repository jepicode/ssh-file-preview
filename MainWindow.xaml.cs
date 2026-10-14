using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace SshFilePreview;

public partial class MainWindow : Window
{
    private const string DefaultSshAlias = "dansaihomelab";
    private const string StartPath = "/home/tl-1-dsc-consumer";
    private const int MaxPreviewBytes = 12 * 1024 * 1024;
    private static readonly string SshTargetSettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SshFilePreview", "ssh-target.txt");
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tif", ".tiff" };
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".txt", ".md", ".json", ".js", ".jsx", ".ts", ".tsx", ".vue", ".html", ".css", ".scss", ".py", ".go", ".rs", ".c", ".h", ".cpp", ".cs", ".java", ".kt", ".sh", ".bash", ".yaml", ".yml", ".toml", ".xml", ".sql", ".log", ".ini", ".conf", ".env", ".dockerfile" };
    private static readonly HashSet<string> CodeExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".json", ".js", ".jsx", ".ts", ".tsx", ".vue", ".svelte", ".html", ".css", ".scss", ".py", ".go", ".rs", ".c", ".h", ".cpp", ".cs", ".java", ".kt", ".sh", ".bash", ".yaml", ".yml", ".toml", ".xml", ".sql", ".php", ".rb", ".swift", ".dart", ".graphql", ".tf", ".hcl" };
    private readonly ObservableCollection<RemoteEntry> _entries = [];
    private string _currentPath = StartPath;
    private string _sshTarget = string.Empty;
    private bool _loadingEntries;

    public MainWindow()
    {
        InitializeComponent();
        EntriesList.ItemsSource = _entries;
        _sshTarget = LoadSshTarget();
        SshTargetText.Text = string.IsNullOrEmpty(_sshTarget) ? "target belum disetel" : _sshTarget;
        PathBox.Text = StartPath;
        Loaded += async (_, _) => await ConnectAtStartupAsync();
    }
    private async Task ConnectAtStartupAsync()
    {
        if (!string.IsNullOrEmpty(_sshTarget))
        {
            try
            {
                var initialPath = _sshTarget.Equals(DefaultSshAlias, StringComparison.OrdinalIgnoreCase)
                    ? StartPath
                    : await ReadRemoteHomePathAsync();
                if (await BrowseAsync(initialPath)) return;
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }
        while (true)
        {
            var dialog = new SshTargetDialog(_sshTarget) { Owner = this };
            if (dialog.ShowDialog() != true) return;

            _sshTarget = dialog.Target;
            SshTargetText.Text = _sshTarget;
            try
            {
                var initialPath = _sshTarget.Equals(DefaultSshAlias, StringComparison.OrdinalIgnoreCase)
                    ? StartPath
                    : await ReadRemoteHomePathAsync();
                if (!await BrowseAsync(initialPath)) continue;
                if (!SaveSshTarget(_sshTarget))
                    StatusText.Text = "Terhubung, tetapi target SSH tidak dapat disimpan untuk sesi berikutnya.";
                return;
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }
    }

    private async Task<string> ReadRemoteHomePathAsync()
    {
        var output = await RunSshAsync("printf '%s\\n' \"$HOME\"", 4096);
        return NormalizeRemotePath(Encoding.UTF8.GetString(output).Trim());
    }

    private static string LoadSshTarget()
    {
        try
        {
            var target = File.ReadAllText(SshTargetSettingsPath).Trim();
            return IsValidSshTarget(target) ? target : string.Empty;
        }
        catch (IOException) { return string.Empty; }
        catch (UnauthorizedAccessException) { return string.Empty; }
    }

    private static bool SaveSshTarget(string target)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SshTargetSettingsPath)!);
            File.WriteAllText(SshTargetSettingsPath, target);
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    internal static bool IsValidSshTarget(string target)
    {
        if (string.IsNullOrWhiteSpace(target) || target != target.Trim() || target.StartsWith('-') ||
            target.Any(char.IsWhiteSpace) || target.Count(character => character == '@') > 1)
            return false;

        var separator = target.IndexOf('@');
        var username = separator < 0 ? null : target[..separator];
        var host = separator < 0 ? target : target[(separator + 1)..];
        if (username is not null && !IsSshName(username)) return false;
        if (host.Length >= 3 && host[0] == '[' && host[^1] == ']')
            return host[1..^1].Contains(':') && host[1..^1].All(character => char.IsAsciiHexDigit(character) || character == ':');
        return IsSshName(host);
    }

    private static bool IsSshName(string value) =>
        value.Length > 0 && (char.IsAsciiLetterOrDigit(value[0]) || value[0] == '_') &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '.' or '-');


    private async Task<bool> BrowseAsync(string requestedPath)
    {
        try
        {
            var path = NormalizeRemotePath(requestedPath);
            SetConnectionState(connected: false, connecting: true);
            SetBusy(true, "Memuat folder…");
            var output = await RunSshAsync(
                $"find {QuoteRemote(path)} -mindepth 1 -maxdepth 1 -printf '%y\\t%f\\n'",
                MaxPreviewBytes);
            var nextEntries = Encoding.UTF8.GetString(output).Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(ParseEntry)
                .Where(entry => entry is not null)
                .Cast<RemoteEntry>()
                .OrderByDescending(entry => entry.IsDirectory)
                .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            _currentPath = path;
            PathBox.Text = path;
            _loadingEntries = true;
            _entries.Clear();
            foreach (var entry in nextEntries) _entries.Add(entry);
            EntriesList.SelectedItem = null;
            _loadingEntries = false;
            ShowEmptyPreview("Pilih file teks atau gambar dari daftar.");
            StatusText.Text = $"{nextEntries.Length} item · {path}";
            SetConnectionState(connected: true);
            return true;
        }
        catch (Exception ex)
        {
            _loadingEntries = false;
            SetConnectionState(connected: false);
            ShowError(ex.Message);
            return false;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task PreviewAsync(RemoteEntry entry)
    {
        if (entry.IsDirectory)
        {
            await BrowseAsync(JoinRemotePath(_currentPath, entry.Name));
            return;
        }

        var filePath = JoinRemotePath(_currentPath, entry.Name);
        var extension = Path.GetExtension(entry.Name);
        if (!ImageExtensions.Contains(extension) && !TextExtensions.Contains(extension))
        {
            ShowError("Preview hanya mendukung file kode/teks dan gambar PNG, JPEG, GIF, BMP, atau TIFF.");
            return;
        }

        try
        {
            MarkdownPreview.Markdown = "";
            MarkdownPreview.Visibility = Visibility.Collapsed;
            CodePreview.Visibility = Visibility.Collapsed;
            ImageScroll.Visibility = Visibility.Collapsed;
            ImagePreview.Source = null;
            SetBusy(true, $"Membaca {entry.Name}…");
            PreviewTitle.Text = entry.Name;
            FileTypeText.Text = extension.TrimStart('.').ToUpperInvariant();
            FileTypeBadge.Visibility = Visibility.Visible;
            FileSize.Text = "";
            var encoded = await RunSshAsync($"base64 -w 0 -- {QuoteRemote(filePath)}", MaxPreviewBytes * 2);
            var bytes = Convert.FromBase64String(Encoding.ASCII.GetString(encoded));
            if (bytes.Length > MaxPreviewBytes) throw new InvalidOperationException("File melebihi batas preview 12 MiB.");
            EmptyState.Visibility = Visibility.Collapsed;
 

            if (ImageExtensions.Contains(extension))
            {
                var bitmap = new BitmapImage();
                using var stream = new MemoryStream(bytes);
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 1600;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                ImagePreview.Source = bitmap;
                ImageScroll.Visibility = Visibility.Visible;
                CodePreview.Visibility = Visibility.Collapsed;
                MarkdownPreview.Markdown = "";
            }
            else if (extension.Equals(".md", StringComparison.OrdinalIgnoreCase))
            {
                if (bytes.Contains((byte)0)) throw new InvalidOperationException("File mengandung data biner dan bukan file teks.");
                var source = new UTF8Encoding(false, true).GetString(bytes);
                using var rendered = await MarkdownPreviewService.RenderAsync(source, filePath, async (remotePath, maxBytes) =>
                {
                    var normalizedPath = NormalizeRemotePath(remotePath);
                    var encodedAsset = await RunSshAsync($"base64 -w 0 -- {QuoteRemote(normalizedPath)}", checked(maxBytes * 2));
                    var assetBytes = Convert.FromBase64String(Encoding.ASCII.GetString(encodedAsset));
                    if (assetBytes.Length > maxBytes) throw new InvalidOperationException("Aset Markdown melebihi batas baca.");
                    return assetBytes;
                });
                MarkdownPreview.Markdown = rendered.Markdown;
                MarkdownPreview.Visibility = Visibility.Visible;
                ImagePreview.Source = null;
                ImageScroll.Visibility = Visibility.Collapsed;
                CodePreview.Visibility = Visibility.Collapsed;
            }
            else
            {
                if (bytes.Contains((byte)0)) throw new InvalidOperationException("File mengandung data biner dan bukan file teks.");
                CodePreview.Text = new UTF8Encoding(false, true).GetString(bytes);
                ImagePreview.Source = null;
                ImageScroll.Visibility = Visibility.Collapsed;
                CodePreview.Visibility = Visibility.Visible;
                MarkdownPreview.Markdown = "";
            }
            StatusText.Text = $"Preview lengkap · {filePath}";
            SetConnectionState(connected: true);
        }
        catch (Exception ex)
        {
            if (ex is TimeoutException) SetConnectionState(connected: false);
            ShowError(ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private static RemoteEntry? ParseEntry(string line)
    {
        var separator = line.IndexOf('\t');
        if (separator < 1 || separator == line.Length - 1) return null;
        var type = line[0];
        var name = line[(separator + 1)..];
        if (name is "." or "..") return null;
        return new RemoteEntry(name, type == 'd');
    }

    internal static string NormalizeRemotePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith('/') || path.Contains('\0'))
            throw new ArgumentException("Masukkan path remote absolut, dimulai dengan /.");
        var segments = path.Split('/');
        if (segments.Contains("..")) throw new ArgumentException("Segmen .. tidak diizinkan.");
        var normalized = string.Join('/', segments.Where(segment => segment.Length > 0 && segment != "."));
        return "/" + normalized;
    }

    private static string JoinRemotePath(string parent, string name) => parent == "/" ? "/" + name : parent + "/" + name;

    private static string QuoteRemote(string value) => "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    private async Task<byte[]> RunSshAsync(string remoteCommand, int maxBytes)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ssh",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            }
        };
        process.StartInfo.ArgumentList.Add("-T");
        process.StartInfo.ArgumentList.Add("-n");
        process.StartInfo.ArgumentList.Add("-o");
        process.StartInfo.ArgumentList.Add("BatchMode=yes");
        process.StartInfo.ArgumentList.Add("-o");
        process.StartInfo.ArgumentList.Add("ConnectTimeout=8");
        process.StartInfo.ArgumentList.Add(_sshTarget);
        process.StartInfo.ArgumentList.Add(remoteCommand);

        try
        {
            if (!process.Start()) throw new InvalidOperationException("Gagal menjalankan OpenSSH.");
            process.StandardInput.Close();
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException("OpenSSH tidak ditemukan. Aktifkan Windows OpenSSH Client dan pastikan perintah ssh tersedia di PATH.", ex);
        }

        using var output = new MemoryStream();
        using var timeout = new CancellationTokenSource();
        async Task DrainOutputAsync()
        {
            var buffer = new byte[81920];
            while (true)
            {
                var read = await process.StandardOutput.BaseStream.ReadAsync(buffer, timeout.Token);
                if (read == 0) return;
                if (output.Length + read > maxBytes)
                    throw new InvalidOperationException($"Output melebihi batas {maxBytes / 1024 / 1024} MiB.");
                await output.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
            }
        }

        var readTask = DrainOutputAsync();
        var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
        var exitTask = process.WaitForExitAsync(timeout.Token);
        var completion = Task.WhenAll(readTask, errorTask, exitTask);
        if (await Task.WhenAny(completion, Task.Delay(TimeSpan.FromSeconds(25))) != completion)
        {
            KillProcessTree(process);
            timeout.Cancel();
            try { await completion; } catch (OperationCanceledException) { } catch (IOException) { }
            throw new TimeoutException("Operasi SSH melewati batas 25 detik.");
        }

        try
        {
            await completion;
        }
        catch
        {
            KillProcessTree(process);
            throw;
        }

        if (process.ExitCode != 0)
        {
            var detail = errorTask.Result.Trim();
            throw new InvalidOperationException(string.IsNullOrEmpty(detail) ? $"SSH gagal (exit {process.ExitCode})." : detail);
        }
        return output.ToArray();
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(true);
        }
        catch (InvalidOperationException) { }
        catch (Win32Exception) { }
    }

    private void ShowEmptyPreview(string message)
    {
        PreviewTitle.Text = "Pilih file untuk preview";
        FileTypeBadge.Visibility = Visibility.Collapsed;
        FileSize.Text = "";
        CodePreview.Clear();
        CodePreview.Visibility = Visibility.Collapsed;
        MarkdownPreview.Markdown = "";
        MarkdownPreview.Visibility = Visibility.Collapsed;
        ImageScroll.Visibility = Visibility.Collapsed;
        ImagePreview.Source = null;
        EmptyState.Visibility = Visibility.Visible;
    }

    private void ShowError(string message)
    {
        ShowEmptyPreview(message);
        StatusText.Text = message;
    }

    private void SetBusy(bool busy, string? message = null)
    {
        IsEnabled = !busy;
        if (message is not null) StatusText.Text = message;
    }
    private void SetConnectionState(bool connected, bool connecting = false)
    {
        var state = connecting ? ("Menghubungkan", "#FFF4DF", "#D28B1E", "#986516")
            : connected ? ("Terhubung", "#E7F5EF", "#29A36A", "#28764F")
            : ("Tidak terhubung", "#FCEBEC", "#D64545", "#A4333D");
        ConnectionStateText.Text = state.Item1;
        ConnectionBadge.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(state.Item2)!;
        ConnectionDot.Fill = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(state.Item3)!;
        ConnectionStateText.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(state.Item4)!;
    }

    private async void GoButton_Click(object sender, RoutedEventArgs e) => await BrowseAsync(PathBox.Text);
    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await BrowseAsync(_currentPath);
    private async void UpButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPath == "/") return;
        var parentPath = _currentPath[.._currentPath.LastIndexOf('/')];
        await BrowseAsync(string.IsNullOrEmpty(parentPath) ? "/" : parentPath);
    }
    private async void PathBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) await BrowseAsync(PathBox.Text);
    }
    private async void EntriesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loadingEntries && EntriesList.SelectedItem is RemoteEntry entry) await PreviewAsync(entry);
    }

    private sealed record RemoteEntry(string Name, bool IsDirectory)
    {
        public string Kind => IsDirectory ? "folder" : ImageExtensions.Contains(Path.GetExtension(Name))
            ? "image" : CodeExtensions.Contains(Path.GetExtension(Name)) ? "code" : "text";
        public string KindLabel => Kind switch
        {
            "folder" => "Folder",
            "image" => "Gambar",
            "code" => "Source",
            _ => "Teks"
        };
        public string IconBackground => Kind switch
        {
            "folder" => "#EEF2FF",
            "image" => "#F5EDFF",
            "code" => "#E7F7F1",
            _ => "#EDF2F8"
        };
        public string IconColor => Kind switch
        {
            "folder" => "#5D70D6",
            "image" => "#9254DE",
            "code" => "#168A70",
            _ => "#6480A8"
        };
    }
}
