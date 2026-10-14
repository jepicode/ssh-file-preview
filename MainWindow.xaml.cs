using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfMarkdown.Controls;

namespace SshFilePreview;

public partial class MainWindow : Window
{
    private const string DefaultSshAlias = "dansaihomelab";
    private const string StartPath = "/home/tl-1-dsc-consumer";
    private const int MaxPreviewBytes = 12 * 1024 * 1024;
    private static readonly string SshTargetSettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SshFilePreview", "ssh-target.txt");
    private static readonly string SshTargetsSettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SshFilePreview", "ssh-targets.txt");
    private static readonly string ThemeSettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SshFilePreview", "theme.txt");
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tif", ".tiff" };
    private static readonly double[] ImageZoomRatios = [0.5, 0.75, 1.0, 1.25, 1.5, 2.0, 3.0, 4.0];
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".txt", ".md", ".json", ".js", ".jsx", ".ts", ".tsx", ".vue", ".html", ".css", ".scss", ".py", ".go", ".rs", ".c", ".h", ".cpp", ".cs", ".java", ".kt", ".sh", ".bash", ".yaml", ".yml", ".toml", ".xml", ".sql", ".log", ".ini", ".conf", ".env", ".dockerfile" };
    private static readonly HashSet<string> CodeExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".json", ".js", ".jsx", ".ts", ".tsx", ".vue", ".svelte", ".html", ".css", ".scss", ".py", ".go", ".rs", ".c", ".h", ".cpp", ".cs", ".java", ".kt", ".sh", ".bash", ".yaml", ".yml", ".toml", ".xml", ".sql", ".php", ".rb", ".swift", ".dart", ".graphql", ".tf", ".hcl" };
    private static readonly IReadOnlyDictionary<string, string> LightThemeColors = new Dictionary<string, string>
    {
        ["WindowBackgroundBrush"] = "#F5F7FC",
        ["CardBackgroundBrush"] = "#FFFFFF",
        ["PreviewBackgroundBrush"] = "#F8FAFD",
        ["PrimaryTextBrush"] = "#182238",
        ["BodyTextBrush"] = "#25324A",
        ["SecondaryTextBrush"] = "#65728A",
        ["MutedTextBrush"] = "#77839A",
        ["BorderBrush"] = "#E7EAF1",
        ["ButtonBackgroundBrush"] = "#FFFFFF",
        ["ButtonForegroundBrush"] = "#46536B",
        ["ButtonBorderBrush"] = "#E3E8F1",
        ["ButtonHoverBrush"] = "#F3F6FC",
        ["PrimaryButtonBackgroundBrush"] = "#263446",
        ["PrimaryButtonBorderBrush"] = "#263446",
        ["PrimaryButtonForegroundBrush"] = "#FFFFFF",
        ["PrimaryButtonHoverBrush"] = "#1C2735",
        ["InputBackgroundBrush"] = "#FFFFFF",
        ["InputForegroundBrush"] = "#25324A",
        ["InputBorderBrush"] = "#E3E8F1",
        ["InputFocusBorderBrush"] = "#7890F5",
        ["EntryHoverBrush"] = "#F4F6FB",
        ["EntrySelectedBrush"] = "#EDF1FF",
        ["EntryNameBrush"] = "#27334A",
        ["EntryMetadataBrush"] = "#8A95A9",
        ["FileTypeBadgeBackgroundBrush"] = "#F0F3F9",
        ["FileTypeBadgeForegroundBrush"] = "#62708A",
        ["ImageSurfaceBrush"] = "#F8FAFD",
        ["IconTileBackgroundBrush"] = "#EEF2FF",
        ["EmptyIconBackgroundBrush"] = "#EDF1FF",
        ["AccentBrush"] = "#5472E8",
        ["EmptyPreviewBrush"] = "#65728A",
        ["FooterBrush"] = "#9AA4B6",
        ["BrowserHeadingBrush"] = "#929DB0",
        ["ConnectingBadgeBackgroundBrush"] = "#FFF4DF",
        ["ConnectingBadgeDotBrush"] = "#D28B1E",
        ["ConnectingBadgeForegroundBrush"] = "#986516",
        ["ConnectedBadgeBackgroundBrush"] = "#E7F5EF",
        ["ConnectedBadgeDotBrush"] = "#29A36A",
        ["ConnectedBadgeForegroundBrush"] = "#28764F",
        ["DisconnectedBadgeBackgroundBrush"] = "#FCEBEC",
        ["DisconnectedBadgeDotBrush"] = "#D64545",
        ["DisconnectedBadgeForegroundBrush"] = "#A4333D",
        ["ErrorTextBrush"] = "#A4333D",
        ["ScrollThumbBrush"] = "#AAB4C3",
        ["ScrollThumbHoverBrush"] = "#78869A"
    };
    private static readonly IReadOnlyDictionary<string, string> DarkThemeColors = new Dictionary<string, string>
    {
        ["WindowBackgroundBrush"] = "#121826",
        ["CardBackgroundBrush"] = "#1D2636",
        ["PreviewBackgroundBrush"] = "#151D2B",
        ["PrimaryTextBrush"] = "#E8EEF7",
        ["BodyTextBrush"] = "#D5DEEB",
        ["SecondaryTextBrush"] = "#ACB8CB",
        ["MutedTextBrush"] = "#929FB2",
        ["BorderBrush"] = "#37445A",
        ["ButtonBackgroundBrush"] = "#263247",
        ["ButtonForegroundBrush"] = "#DCE6F5",
        ["ButtonBorderBrush"] = "#44526A",
        ["ButtonHoverBrush"] = "#334158",
        ["PrimaryButtonBackgroundBrush"] = "#E8EEF7",
        ["PrimaryButtonBorderBrush"] = "#E8EEF7",
        ["PrimaryButtonForegroundBrush"] = "#121826",
        ["PrimaryButtonHoverBrush"] = "#CFD9E5",
        ["InputBackgroundBrush"] = "#182132",
        ["InputForegroundBrush"] = "#E8EEF7",
        ["InputBorderBrush"] = "#43516A",
        ["InputFocusBorderBrush"] = "#8CA8FF",
        ["EntryHoverBrush"] = "#273349",
        ["EntrySelectedBrush"] = "#33496A",
        ["EntryNameBrush"] = "#E8EEF7",
        ["EntryMetadataBrush"] = "#A9B5C8",
        ["FileTypeBadgeBackgroundBrush"] = "#263247",
        ["FileTypeBadgeForegroundBrush"] = "#D0DAE9",
        ["ImageSurfaceBrush"] = "#101722",
        ["IconTileBackgroundBrush"] = "#283650",
        ["EmptyIconBackgroundBrush"] = "#283650",
        ["AccentBrush"] = "#95ACFF",
        ["EmptyPreviewBrush"] = "#A9B5C8",
        ["FooterBrush"] = "#7F8DA4",
        ["BrowserHeadingBrush"] = "#A9B5C8",
        ["ConnectingBadgeBackgroundBrush"] = "#3D321E",
        ["ConnectingBadgeDotBrush"] = "#E7AF48",
        ["ConnectingBadgeForegroundBrush"] = "#F2D490",
        ["ConnectedBadgeBackgroundBrush"] = "#1D3C30",
        ["ConnectedBadgeDotBrush"] = "#51C78A",
        ["ConnectedBadgeForegroundBrush"] = "#98E0B8",
        ["DisconnectedBadgeBackgroundBrush"] = "#43282E",
        ["DisconnectedBadgeDotBrush"] = "#F07478",
        ["DisconnectedBadgeForegroundBrush"] = "#F5B0B4",
        ["ErrorTextBrush"] = "#F5B0B4",
        ["ScrollThumbBrush"] = "#53627A",
        ["ScrollThumbHoverBrush"] = "#8190A8"
    };
    private readonly ObservableCollection<RemoteEntry> _entries = [];
    private readonly ObservableCollection<string> _sshTargets = [];
    private string _currentPath = StartPath;
    private readonly Stack<string> _backHistory = [];
    private string _sshTarget = string.Empty;
    private bool _loadingEntries;
    private bool _suppressSelectionPreview;
    private bool _loadingTargetSelector;
    private bool _isConnected;
    private double _imageZoom = 1.0;

    private bool _isDarkTheme;
    public MainWindow()
    {
        InitializeComponent();
        ZoomRatioSelector.ItemsSource = ImageZoomRatios.Select(ratio => $"{ratio * 100:0}%").ToArray();
        ZoomRatioSelector.SelectedIndex = Array.IndexOf(ImageZoomRatios, 1.0);
        _isDarkTheme = LoadTheme();
        ApplyTheme(_isDarkTheme);
        EntriesList.ItemsSource = _entries;
        _sshTarget = LoadSshTarget();
        foreach (var target in LoadSshTargets()) _sshTargets.Add(target);
        if (string.IsNullOrEmpty(_sshTarget) && _sshTargets.Count > 0) _sshTarget = _sshTargets[0];
        _loadingTargetSelector = true;
        SshTargetSelector.ItemsSource = _sshTargets;
        SshTargetSelector.SelectedItem = _sshTarget;
        _loadingTargetSelector = false;
        PathBox.Text = StartPath;
        Loaded += async (_, _) => await ConnectAtStartupAsync();
    }

    private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyTheme(!_isDarkTheme);
        if (!SaveTheme(_isDarkTheme))
            StatusText.Text = "Tema berubah, tetapi pengaturan tema tidak dapat disimpan.";
    }

    private void ApplyTheme(bool isDark)
    {
        _isDarkTheme = isDark;
        var palette = isDark ? DarkThemeColors : LightThemeColors;
        foreach (var (key, color) in palette)
            Application.Current.Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)!);
        MarkdownPreview.Theme = isDark ? MarkdownViewerTheme.Dark : MarkdownViewerTheme.Light;
        ThemeToggleGlyph.Data = Geometry.Parse(isDark
            ? "M12,2 C17.5,2 22,6.5 22,12 C22,17.5 17.5,22 12,22 C6.5,22 2,17.5 2,12 C2,6.5 6.5,2 12,2 Z M12,0 L12,2 M12,22 L12,24 M0,12 L2,12 M22,12 L24,12 M3.5,3.5 L5,5 M19,19 L20.5,20.5 M3.5,20.5 L5,19 M19,5 L20.5,3.5"
            : "M17,14 C13.7,14 11,11.3 11,8 C11,6 12,4.3 13.5,3.2 C13,3.1 12.5,3 12,3 C7,3 3,7 3,12 C3,17 7,21 12,21 C15.8,21 19,18.6 20.4,15.3 C19.4,14.5 18.3,14 17,14 Z");
        ThemeToggleButton.ToolTip = isDark ? "Ganti ke tema Light" : "Ganti ke tema Dark";
        SetConnectionState(_isConnected);
    }

    private static bool LoadTheme()
    {
        try { return File.ReadAllText(ThemeSettingsPath).Trim().Equals("dark", StringComparison.OrdinalIgnoreCase); }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private static bool SaveTheme(bool isDark)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ThemeSettingsPath)!);
            File.WriteAllText(ThemeSettingsPath, isDark ? "dark" : "light");
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
    private async Task ConnectAtStartupAsync()
    {
        var inputTarget = _sshTarget;
        if (!string.IsNullOrEmpty(_sshTarget) && await ConnectToTargetAsync(_sshTarget, addToProfiles: false)) return;

        while (true)
        {
            var dialog = new SshTargetDialog(inputTarget) { Owner = this };
            if (dialog.ShowDialog() != true) return;
            inputTarget = dialog.Target;
            if (await ConnectToTargetAsync(inputTarget, addToProfiles: true)) return;
        }
    }
    private async void SshTargetSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingTargetSelector || SshTargetSelector.SelectedItem is not string target ||
            target.Equals(_sshTarget, StringComparison.OrdinalIgnoreCase))
            return;

        await ConnectToTargetAsync(target, addToProfiles: false);
    }

    private async void AddHostButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SshTargetDialog(string.Empty) { Owner = this };
        if (dialog.ShowDialog() == true)
            await ConnectToTargetAsync(dialog.Target, addToProfiles: true);
    }


    private async Task<bool> ConnectToTargetAsync(string target, bool addToProfiles)
    {
        if (!IsValidSshTarget(target))
        {
            ShowError("Gunakan alias SSH atau format user@host yang valid.");
            return false;
        }

        var previousTarget = _sshTarget;
        var wasConnected = _isConnected;
        SetBusy(true, $"Menghubungkan ke {target}…");
        SetConnectionState(_isConnected, connecting: true);
        try
        {
            var initialPath = target.Equals(DefaultSshAlias, StringComparison.OrdinalIgnoreCase)
                ? StartPath
                : await ReadRemoteHomePathAsync(target);
            var entries = await FetchEntriesAsync(target, initialPath);

            if (addToProfiles && !_sshTargets.Contains(target, StringComparer.OrdinalIgnoreCase))
            {
                _loadingTargetSelector = true;
                _sshTargets.Add(target);
                _loadingTargetSelector = false;
            }

            _sshTarget = target;
            _loadingTargetSelector = true;
            SshTargetSelector.SelectedItem = target;
            _loadingTargetSelector = false;
            _currentPath = initialPath;
            PathBox.Text = initialPath;
            _backHistory.Clear();
            UpdateNavigationButtons();
            SetEntries(entries);
            ShowEmptyPreview("Pilih file teks atau gambar dari daftar.");
            StatusText.Text = $"{entries.Length} item · {initialPath}";
            SetConnectionState(connected: true);

            var activeTargetSaved = SaveSshTarget(target);
            var profilesSaved = SaveSshTargets();
            if (!activeTargetSaved || !profilesSaved)
                StatusText.Text = "Terhubung, tetapi sebagian pengaturan host tidak dapat disimpan.";
            return true;
        }
        catch (Exception ex)
        {
            _sshTarget = previousTarget;
            _loadingTargetSelector = true;
            SshTargetSelector.SelectedItem = previousTarget;
            _loadingTargetSelector = false;
            StatusText.Text = ex.Message;
            if (wasConnected)
                SetConnectionState(connected: true);
            else
            {
                SetConnectionState(connected: false);
                ShowEmptyPreview(ex.Message);
            }
            return false;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task<string> ReadRemoteHomePathAsync(string target)
    {
        var output = await RunSshAsync(target, "printf '%s\\n' \"$HOME\"", 4096);
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

    private static List<string> LoadSshTargets()
    {
        var targets = new List<string>();
        try
        {
            foreach (var line in File.ReadAllLines(SshTargetsSettingsPath))
            {
                var target = line.Trim();
                if (IsValidSshTarget(target) && !targets.Contains(target, StringComparer.OrdinalIgnoreCase))
                    targets.Add(target);
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        var activeTarget = LoadSshTarget();
        if (!string.IsNullOrEmpty(activeTarget) && !targets.Contains(activeTarget, StringComparer.OrdinalIgnoreCase))
            targets.Insert(0, activeTarget);
        return targets;
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

    private bool SaveSshTargets()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SshTargetsSettingsPath)!);
            File.WriteAllLines(SshTargetsSettingsPath, _sshTargets);
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


    private async Task<bool> BrowseAsync(string requestedPath, bool addToHistory = true)
    {
        try
        {
            var path = NormalizeRemotePath(requestedPath);
            SetConnectionState(_isConnected, connecting: true);
            SetBusy(true, "Memuat folder…");
            var entries = await FetchEntriesAsync(_sshTarget, path);

            if (addToHistory && !path.Equals(_currentPath, StringComparison.Ordinal))
                _backHistory.Push(_currentPath);
            _currentPath = path;
            PathBox.Text = path;
            SetEntries(entries);
            UpdateNavigationButtons();
            ShowEmptyPreview("Pilih file teks atau gambar dari daftar.");
            StatusText.Text = $"{entries.Length} item · {path}";
            SetConnectionState(connected: true);
            return true;
        }
        catch (Exception ex)
        {
            _loadingEntries = false;
            PathBox.Text = _currentPath;
            UpdateNavigationButtons();
            SetConnectionState(connected: false);
            ShowError(ex.Message);
            return false;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private static async Task<RemoteEntry[]> FetchEntriesAsync(string target, string remotePath)
    {
        var path = NormalizeRemotePath(remotePath);
        var output = await RunSshAsync(
            target,
            $"find {QuoteRemote(path)} -mindepth 1 -maxdepth 1 -printf '%y\\t%f\\n'",
            MaxPreviewBytes);
        return Encoding.UTF8.GetString(output).Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseEntry)
            .Where(entry => entry is not null)
            .Cast<RemoteEntry>()
            .OrderByDescending(entry => entry.IsDirectory)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void SetEntries(IEnumerable<RemoteEntry> entries)
    {
        _loadingEntries = true;
        try
        {
            _entries.Clear();
            foreach (var entry in entries) _entries.Add(entry);
            EntriesList.SelectedItem = null;
        }
        finally
        {
            _loadingEntries = false;
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
            ZoomControls.Visibility = Visibility.Collapsed;
            ImagePreview.Source = null;
            SetBusy(true, $"Membaca {entry.Name}…");
            PreviewTitle.Text = entry.Name;
            FileTypeText.Text = extension.TrimStart('.').ToUpperInvariant();
            FileTypeBadge.Visibility = Visibility.Visible;
            FileSize.Text = "";
            var encoded = await RunSshAsync(_sshTarget, $"base64 -w 0 -- {QuoteRemote(filePath)}", MaxPreviewBytes * 2);
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
                ZoomRatioSelector.SelectedIndex = Array.IndexOf(ImageZoomRatios, 1.0);
                ApplyImageZoom(Array.IndexOf(ImageZoomRatios, 1.0));
                ImageScroll.ScrollToHorizontalOffset(0);
                ImageScroll.ScrollToVerticalOffset(0);
                ImageScroll.Visibility = Visibility.Visible;
                ZoomControls.Visibility = Visibility.Visible;
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
                    var encodedAsset = await RunSshAsync(_sshTarget, $"base64 -w 0 -- {QuoteRemote(normalizedPath)}", checked(maxBytes * 2));
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

    private static async Task<byte[]> RunSshAsync(string target, string remoteCommand, int maxBytes)
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
        process.StartInfo.ArgumentList.Add(target);
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

    private static async Task RunSshToFileAsync(string target, string remotePath, string destinationPath)
    {
        var normalizedPath = NormalizeRemotePath(remotePath);
        var destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (string.IsNullOrEmpty(destinationDirectory))
            throw new ArgumentException("Path tujuan download harus berupa file lokal.", nameof(destinationPath));
        var partialPath = Path.Combine(destinationDirectory, $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.partial");

        try
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
            process.StartInfo.ArgumentList.Add(target);
            process.StartInfo.ArgumentList.Add($"cat -- {QuoteRemote(normalizedPath)}");

            try
            {
                if (!process.Start()) throw new InvalidOperationException("Gagal menjalankan OpenSSH.");
                process.StandardInput.Close();
            }
            catch (Win32Exception ex)
            {
                throw new InvalidOperationException("OpenSSH tidak ditemukan. Aktifkan Windows OpenSSH Client dan pastikan perintah ssh tersedia di PATH.", ex);
            }

            using var timeout = new CancellationTokenSource();
            await using (var file = new FileStream(partialPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                async Task StreamOutputAsync()
                {
                    var buffer = new byte[81920];
                    while (true)
                    {
                        var read = await process.StandardOutput.BaseStream.ReadAsync(buffer, timeout.Token);
                        if (read == 0) return;
                        await file.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
                    }
                }

                var streamTask = StreamOutputAsync();
                var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
                var exitTask = process.WaitForExitAsync(timeout.Token);
                var completion = Task.WhenAll(streamTask, errorTask, exitTask);
                if (await Task.WhenAny(completion, Task.Delay(TimeSpan.FromSeconds(25))) != completion)
                {
                    KillProcessTree(process);
                    timeout.Cancel();
                    try { await completion; } catch (OperationCanceledException) { } catch (IOException) { }
                    throw new TimeoutException("Download SSH melewati batas 25 detik.");
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
                await file.FlushAsync();
            }

            File.Move(partialPath, destinationPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(partialPath)) File.Delete(partialPath);
        }
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
        ZoomControls.Visibility = Visibility.Collapsed;
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
        if (!connecting) _isConnected = connected;
        var state = connecting ? "Menghubungkan" : connected ? "Terhubung" : "Tidak terhubung";
        var palettePrefix = connecting ? "Connecting" : connected ? "Connected" : "Disconnected";
        ConnectionStateText.Text = state;
        ConnectionBadge.Background = (Brush)Application.Current.Resources[$"{palettePrefix}BadgeBackgroundBrush"];
        ConnectionDot.Fill = (Brush)Application.Current.Resources[$"{palettePrefix}BadgeDotBrush"];
        ConnectionStateText.Foreground = (Brush)Application.Current.Resources[$"{palettePrefix}BadgeForegroundBrush"];
    }

    private async void GoButton_Click(object sender, RoutedEventArgs e) => await BrowseAsync(PathBox.Text);
    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await BrowseAsync(_currentPath);
    private void ZoomRatioSelector_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ApplyImageZoom(ZoomRatioSelector.SelectedIndex);

    private void ZoomOutButton_Click(object sender, RoutedEventArgs e) =>
        ApplyImageZoom(Math.Max(0, ZoomRatioSelector.SelectedIndex - 1));

    private void ZoomInButton_Click(object sender, RoutedEventArgs e) =>
        ApplyImageZoom(Math.Min(ImageZoomRatios.Length - 1, ZoomRatioSelector.SelectedIndex + 1));

    private void ApplyImageZoom(int presetIndex)
    {
        if (ZoomRatioSelector.SelectedIndex != presetIndex)
            ZoomRatioSelector.SelectedIndex = presetIndex;
        _imageZoom = ImageZoomRatios[presetIndex];
        ImagePreview.LayoutTransform = new ScaleTransform(_imageZoom, _imageZoom);
        ZoomOutButton.IsEnabled = presetIndex > 0;
        ZoomInButton.IsEnabled = presetIndex < ImageZoomRatios.Length - 1;
    }

    private async void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (_backHistory.Count == 0) return;
        var previousPath = _backHistory.Peek();
        if (await BrowseAsync(previousPath, addToHistory: false))
        {
            _backHistory.Pop();
            UpdateNavigationButtons();
        }
    }

    private void UpdateNavigationButtons()
    {
        BackButton.IsEnabled = _backHistory.Count > 0;
        UpButton.IsEnabled = !string.Equals(_currentPath, "/", StringComparison.Ordinal);
    }

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
        if (!_loadingEntries && !_suppressSelectionPreview && EntriesList.SelectedItem is RemoteEntry entry)
            await PreviewAsync(entry);
    }

    private void EntriesList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var clickedElement = e.OriginalSource as DependencyObject ?? e.Source as DependencyObject;
        var container = clickedElement is null ? null : ItemsControl.ContainerFromElement(EntriesList, clickedElement) as ListBoxItem;
        _suppressSelectionPreview = true;
        try
        {
            EntriesList.SelectedItem = container?.DataContext is RemoteEntry entry ? entry : null;
        }
        finally
        {
            _suppressSelectionPreview = false;
        }
    }

    private void EntryContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu) return;
        var download = menu.Items.OfType<MenuItem>().FirstOrDefault(item => item.Tag as string == "download");
        if (download is not null)
            download.IsEnabled = EntriesList.SelectedItem is RemoteEntry { IsDirectory: false };
    }

    private void CopyEntryPath_Click(object sender, RoutedEventArgs e)
    {
        if (EntriesList.SelectedItem is not RemoteEntry entry) return;
        var path = JoinRemotePath(_currentPath, entry.Name);
        Clipboard.SetText(path);
        StatusText.Text = $"Path disalin · {path}";
    }

    private async void DownloadEntry_Click(object sender, RoutedEventArgs e)
    {
        if (EntriesList.SelectedItem is not RemoteEntry { IsDirectory: false } entry) return;

        var invalidCharacters = Path.GetInvalidFileNameChars().ToHashSet();
        var proposedName = new string(entry.Name.Select(character => invalidCharacters.Contains(character) ? '_' : character).ToArray());
        if (string.IsNullOrWhiteSpace(proposedName) || proposedName is "." or "..") proposedName = "download";
        var dialog = new SaveFileDialog
        {
            AddExtension = false,
            CheckPathExists = true,
            FileName = proposedName,
            Filter = "All files (*.*)|*.*",
            OverwritePrompt = true,
            ValidateNames = true
        };
        if (dialog.ShowDialog(this) != true) return;

        var remotePath = NormalizeRemotePath(JoinRemotePath(_currentPath, entry.Name));
        try
        {
            SetBusy(true, $"Mengunduh {entry.Name}…");
            await RunSshToFileAsync(_sshTarget, remotePath, dialog.FileName);
            StatusText.Text = $"Download selesai · {dialog.FileName}";
        }
        catch (Exception ex)
        {
            if (ex is TimeoutException) SetConnectionState(connected: false);
            StatusText.Text = ex.Message;
        }
        finally
        {
            SetBusy(false);
        }
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
        public string IconColor => Kind switch
        {
            "folder" => "#5D70D6",
            "image" => "#9254DE",
            "code" => "#168A70",
            _ => "#6480A8"
        };
    }
}
