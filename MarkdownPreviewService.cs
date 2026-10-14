using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace SshFilePreview;

internal sealed class MarkdownPreviewResult : IDisposable
{
    public MarkdownPreviewResult(string markdown, string html, string assetDirectory)
    {
        Markdown = markdown;
        Html = html;
        AssetDirectory = assetDirectory;
    }

    public string Markdown { get; }
    public string Html { get; }
    private string AssetDirectory { get; }
    public void Dispose()
    {
        try { Directory.Delete(AssetDirectory, recursive: true); }
        catch (DirectoryNotFoundException) { }
    }
}

internal static class MarkdownPreviewService
{
    private const int MaxAssetBytes = 12 * 1024 * 1024;
    private const int MaxAssets = 16;
    private const int MaxTotalAssetBytes = 24 * 1024 * 1024;
    private static readonly HashSet<string> RasterExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tif", ".tiff" };
    private static readonly Regex RawHtmlTag = new(@"</?[A-Za-z][^>]*>|<!--[\s\S]*?-->|<![A-Z][^>]*>", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();

    public static async Task<MarkdownPreviewResult> RenderAsync(
        string source,
        string remoteMarkdownPath,
        Func<string, int, Task<byte[]>> readRemoteFile)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(readRemoteFile);

        var assetDirectory = Path.Combine(Path.GetTempPath(), "SshFilePreview", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(assetDirectory);
        try
        {
            var document = Markdown.Parse(source, Pipeline);
            var images = EnumerateImages(document).ToArray();
            var allowedSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var markdownReplacements = new List<(int Start, int Length, string Value)>();
            foreach (var literal in EnumerateLiterals(document))
            {
                var literalText = literal.Content.ToString();
                foreach (Match match in RawHtmlTag.Matches(literalText))
                {
                    var escapedTag = match.Value.Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal);
                    markdownReplacements.Add((literal.Span.Start + match.Index, match.Length, escapedTag));
                }
            }

            var totalBytes = 0;
            var loadedAssets = 0;

            foreach (var image in images)
            {
                var alt = GetAltText(image);
                var remotePath = ResolveRasterPath(remoteMarkdownPath, image.Url);
                if (remotePath is null || loadedAssets >= MaxAssets || totalBytes >= MaxTotalAssetBytes)
                {
                    image.Url = string.Empty;
                    markdownReplacements.Add((image.Span.Start, image.Span.End - image.Span.Start + 1, EscapeMarkdown(alt)));
                    continue;
                }

                try
                {
                    var remainingBudget = MaxTotalAssetBytes - totalBytes;
                    var bytes = await readRemoteFile(remotePath, Math.Min(MaxAssetBytes, remainingBudget));
                    if (bytes.Length == 0 || bytes.Length > MaxAssetBytes || bytes.Length > remainingBudget)
                    {
                        image.Url = string.Empty;
                        markdownReplacements.Add((image.Span.Start, image.Span.End - image.Span.Start + 1, EscapeMarkdown(alt)));
                        continue;
                    }

                    var assetPath = Path.Combine(assetDirectory, $"{loadedAssets}{Path.GetExtension(remotePath).ToLowerInvariant()}");
                    await File.WriteAllBytesAsync(assetPath, bytes);
                    var uri = new Uri(assetPath).AbsoluteUri;
                    image.Url = uri;
                    allowedSources.Add(uri);
                    markdownReplacements.Add((image.Span.Start, image.Span.End - image.Span.Start + 1, $"![{EscapeMarkdown(alt)}](<{uri}>)"));
                    loadedAssets++;
                    totalBytes += bytes.Length;
                }
                catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
                {
                    image.Url = string.Empty;
                    markdownReplacements.Add((image.Span.Start, image.Span.End - image.Span.Start + 1, EscapeMarkdown(alt)));
                }
            }
            var html = Markdig.Markdown.ToHtml(document, Pipeline);
            var sanitizedHtml = SanitizeImages(html, allowedSources);
            var markdown = source;
            foreach (var replacement in markdownReplacements.OrderByDescending(replacement => replacement.Start))
                markdown = markdown.Remove(replacement.Start, replacement.Length).Insert(replacement.Start, replacement.Value);
            return new MarkdownPreviewResult(markdown, sanitizedHtml, assetDirectory);
        }
        catch
        {
            try { Directory.Delete(assetDirectory, recursive: true); } catch { }
            throw;
        }
    }

    private static string? ResolveRasterPath(string markdownPath, string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || url.Contains('\\') || url.Contains('?') || url.Contains('#') || url.StartsWith('/') || url.StartsWith("//"))
            return null;
        if (Uri.TryCreate(url, UriKind.Absolute, out _)) return null;
        var segments = url.Split('/');
        if (segments.Any(segment => segment is ".." or "")) return null;
        if (!RasterExtensions.Contains(Path.GetExtension(segments[^1]))) return null;

        var directory = markdownPath[..Math.Max(markdownPath.LastIndexOf('/'), 0)];
        var combined = (directory == "" ? "/" : directory + "/") + string.Join('/', segments);
        try
        {
            if (string.IsNullOrWhiteSpace(combined) || !combined.StartsWith('/') || combined.Split('/').Contains("..")) return null;
            var normalized = string.Join('/', combined.Split('/').Where(part => part.Length > 0 && part != "."));
            return MainWindow.NormalizeRemotePath("/" + normalized);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
    private static string GetAltText(LinkInline image) => GetAltText((ContainerInline)image);

    private static string GetAltText(ContainerInline container)
    {
        var text = new StringBuilder();
        for (var child = container.FirstChild; child is not null; child = child.NextSibling)
        {
            if (child is LiteralInline literal) text.Append(literal.Content.ToString());
            else if (child is CodeInline code) text.Append(code.Content);
            else if (child is LineBreakInline) text.Append(' ');
            else if (child is ContainerInline nested) text.Append(GetAltText(nested));
        }
        return text.ToString();
    }

    private static string EscapeMarkdown(string text)
    {
        var escaped = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (character is '\\' or '`' or '*' or '_' or '{' or '}' or '[' or ']' or '(' or ')' or '#' or '+' or '-' or '.' or '!' or '>')
                escaped.Append('\\');
            escaped.Append(character);
        }
        return escaped.ToString();
    }

    private static IEnumerable<LiteralInline> EnumerateLiterals(MarkdownDocument document)
    {
        foreach (var block in document)
            foreach (var literal in EnumerateLiterals(block))
                yield return literal;
    }

    private static IEnumerable<LiteralInline> EnumerateLiterals(Block block)
    {
        if (block is LeafBlock { Inline: not null } leaf)
            foreach (var literal in EnumerateLiterals(leaf.Inline))
                yield return literal;

        if (block is ContainerBlock container)
            foreach (var child in container)
                foreach (var literal in EnumerateLiterals(child))
                    yield return literal;
    }

    private static IEnumerable<LiteralInline> EnumerateLiterals(ContainerInline container)
    {
        for (var inline = container.FirstChild; inline is not null; inline = inline.NextSibling)
        {
            if (inline is LinkInline { IsImage: true })
                continue;

            if (inline is LiteralInline literal)
                yield return literal;

            if (inline is ContainerInline nested)
                foreach (var nestedLiteral in EnumerateLiterals(nested))
                    yield return nestedLiteral;
        }
    }

    private static IEnumerable<LinkInline> EnumerateImages(MarkdownDocument document)
    {
        foreach (var block in document)
            foreach (var image in EnumerateImages(block))
                yield return image;
    }

    private static IEnumerable<LinkInline> EnumerateImages(Block block)
    {
        if (block is LeafBlock { Inline: not null } leaf)
            foreach (var image in EnumerateImages(leaf.Inline))
                yield return image;

        if (block is ContainerBlock container)
            foreach (var child in container)
                foreach (var image in EnumerateImages(child))
                    yield return image;
    }

    private static IEnumerable<LinkInline> EnumerateImages(ContainerInline container)
    {
        for (var inline = container.FirstChild; inline is not null; inline = inline.NextSibling)
        {
            if (inline is LinkInline { IsImage: true } image)
                yield return image;

            if (inline is ContainerInline nested)
                foreach (var nestedImage in EnumerateImages(nested))
                    yield return nestedImage;
        }
    }

    private static string SanitizeImages(string html, HashSet<string> allowedSources)
    {
        var parsed = new HtmlDocument();
        parsed.LoadHtml(html);
        var images = parsed.DocumentNode.SelectNodes("//img");
        if (images is not null)
        {
            foreach (var image in images)
            {
                var source = image.GetAttributeValue("src", "");
                if (allowedSources.Contains(source)) continue;
                var alt = HtmlEntity.DeEntitize(image.GetAttributeValue("alt", ""));
                image.ParentNode?.ReplaceChild(parsed.CreateTextNode(alt), image);
            }
        }
        return parsed.DocumentNode.InnerHtml;
    }
}
