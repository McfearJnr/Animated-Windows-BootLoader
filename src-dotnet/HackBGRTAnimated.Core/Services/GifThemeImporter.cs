using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using HackBGRTAnimated.Core.Models;
using HackBGRTAnimated.Core.Utilities;

namespace HackBGRTAnimated.Core.Services;

public sealed class GifThemeImporter
{
    private const int MaxGifBytes = 32 * 1024 * 1024;
    private readonly AppDataPathProvider _paths;
    private readonly ThemeManager _themes;
    private readonly SettingsService _settings;

    public GifThemeImporter(AppDataPathProvider paths, ThemeManager? themes = null, SettingsService? settings = null)
    {
        _paths = paths;
        _settings = settings ?? new SettingsService(paths);
        _themes = themes ?? new ThemeManager(paths, _settings);
    }

    public GifImportResult ImportGif(GifImportOptions options, Action<string>? progress = null)
    {
        if (!File.Exists(options.GifPath))
        {
            throw new FileNotFoundException("GIF not found", options.GifPath);
        }

        var gifInfo = new FileInfo(options.GifPath);
        if (gifInfo.Length > MaxGifBytes)
        {
            throw new InvalidOperationException($"GIF too large ({gifInfo.Length / (1024 * 1024)} MB). Max allowed is {MaxGifBytes / (1024 * 1024)} MB.");
        }

        var themeName = SanitizeThemeName(options.ThemeName);
        if (string.IsNullOrWhiteSpace(themeName))
        {
            throw new InvalidOperationException("Theme name is invalid.");
        }

        var fps = Math.Clamp(options.Fps, 1, 60);
        var maxMs = Math.Clamp(options.MaxDurationMs, 1, 10000);
        var width = Math.Clamp(options.Width, 32, 1024);
        var height = Math.Clamp(options.Height, 32, 1024);

        progress?.Invoke("[1/6] Reading GIF...");
        using var gif = Image.FromFile(options.GifPath);
        if (gif.FrameDimensionsList.Length == 0)
        {
            throw new InvalidOperationException("GIF frame dimension metadata is missing.");
        }

        var dimension = new FrameDimension(gif.FrameDimensionsList[0]);
        var sourceFrameCount = gif.GetFrameCount(dimension);
        if (sourceFrameCount <= 0)
        {
            throw new InvalidOperationException("GIF contains no frames.");
        }

        if (sourceFrameCount > 500)
        {
            progress?.Invoke($"Warning: source GIF has {sourceFrameCount} frames; import may be slow.");
        }

        var delaysCs = Enumerable.Repeat(10, sourceFrameCount).ToArray();
        try
        {
            var prop = gif.GetPropertyItem(0x5100);
            var value = prop?.Value ?? Array.Empty<byte>();
            var count = value.Length / 4;
            for (var i = 0; i < Math.Min(sourceFrameCount, count); i++)
            {
                delaysCs[i] = Math.Max(1, BitConverter.ToInt32(value, i * 4));
            }
        }
        catch
        {
            // Default delay values are used.
        }

        var delaysMs = delaysCs.Select(v => v * 10).ToArray();
        var totalDurationMs = Math.Max(1, delaysMs.Sum());
        var outputFrames = Math.Max(1, fps * maxMs / 1000);
        if (outputFrames > 400)
        {
            progress?.Invoke($"Warning: output frame count is high ({outputFrames}).");
        }

        var estimatedBytes = (long)width * height * 3 * outputFrames;
        progress?.Invoke($"Estimated output size: ~{estimatedBytes / 1024} KB");

        _themes.EnsureThemeStructure(themeName);
        var themeDir = _paths.GetThemeDirectory(themeName);
        var animationDir = _paths.GetThemeAnimationDirectory(themeName);
        foreach (var f in Directory.GetFiles(animationDir, "*.bmp"))
        {
            File.Delete(f);
        }

        progress?.Invoke("[2/6] Normalizing frames...");
        var frameStartMs = new int[sourceFrameCount];
        var cursor = 0;
        for (var i = 0; i < sourceFrameCount; i++)
        {
            frameStartMs[i] = cursor;
            cursor += delaysMs[i];
        }

        static int PickSourceFrame(int[] starts, int[] lengths, int loopMs)
        {
            for (var i = 0; i < starts.Length; i++)
            {
                var end = starts[i] + lengths[i];
                if (loopMs >= starts[i] && loopMs < end)
                {
                    return i;
                }
            }

            return starts.Length - 1;
        }

        progress?.Invoke("[3/6] Resizing/compositing...");
        var bg = ParseHexColor(options.BackgroundHex);
        using var composed = new Bitmap(gif.Width, gif.Height, PixelFormat.Format32bppArgb);
        using var composedGraphics = Graphics.FromImage(composed);
        using var resized = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        using var resizedGraphics = Graphics.FromImage(resized);
        resizedGraphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        resizedGraphics.SmoothingMode = SmoothingMode.HighQuality;
        resizedGraphics.CompositingQuality = CompositingQuality.HighQuality;

        var frameIntervalMs = Math.Max(1, 1000 / fps);
        var lastSourceIndex = -1;

        progress?.Invoke("[4/6] Exporting BMP frames...");
        for (var i = 0; i < outputFrames; i++)
        {
            var tMs = i * frameIntervalMs;
            var sourceIndex = PickSourceFrame(frameStartMs, delaysMs, tMs % totalDurationMs);
            if (sourceIndex != lastSourceIndex)
            {
                gif.SelectActiveFrame(dimension, sourceIndex);
                composedGraphics.Clear(bg);
                composedGraphics.DrawImage(gif, 0, 0, gif.Width, gif.Height);
                lastSourceIndex = sourceIndex;
            }

            resizedGraphics.Clear(bg);
            resizedGraphics.DrawImage(composed, 0, 0, width, height);
            var frameName = $"frame_{(i + 1).ToString().PadLeft(3, '0')}.bmp";
            resized.Save(Path.Combine(animationDir, frameName), ImageFormat.Bmp);
        }

        var first = Path.Combine(animationDir, "frame_001.bmp");
        if (File.Exists(first))
        {
            File.Copy(first, Path.Combine(themeDir, "splash.bmp"), true);
        }

        progress?.Invoke("[5/6] Writing theme.ini...");
        var themeIni = new ConfigEditor(Path.Combine(themeDir, "theme.ini"));
        themeIni.Set("animation", "1");
        themeIni.Set("animation_path", $"\\{AppDataPathProvider.AnimatedFolderRelative}\\themes\\{themeName}\\animation\\");
        themeIni.Set("animation_prefix", "frame_");
        themeIni.Set("animation_digits", "3");
        themeIni.Set("animation_ext", ".bmp");
        themeIni.Set("animation_fps", fps.ToString());
        themeIni.Set("animation_max_ms", maxMs.ToString());
        themeIni.Set("animation_final", "last");
        themeIni.Set("animation_preload", "1");
        themeIni.Set("animation_clear_each_frame", "1");
        themeIni.Set("image", $"path=\\{AppDataPathProvider.AnimatedFolderRelative}\\themes\\{themeName}\\splash.bmp");
        themeIni.Save();

        if (options.SetActiveAfterImport)
        {
            _themes.SetActiveTheme(themeName);
        }

        _settings.AddRecentGif(options.GifPath);

        progress?.Invoke("[6/6] Verifying theme...");
        var files = Directory.GetFiles(animationDir, "*.bmp");
        if (files.Length == 0)
        {
            throw new InvalidOperationException("No BMP frames generated.");
        }

        return new GifImportResult
        {
            ThemeName = themeName,
            ThemeDirectory = themeDir,
            SourceFrameCount = sourceFrameCount,
            OutputFrameCount = files.Length,
            Width = width,
            Height = height,
            EstimatedBytes = files.Select(f => new FileInfo(f).Length).Sum(),
        };
    }

    private static Color ParseHexColor(string hex)
    {
        var clean = (hex ?? "000000").Trim().TrimStart('#');
        if (clean.Length != 6 || !int.TryParse(clean, System.Globalization.NumberStyles.HexNumber, null, out _))
        {
            clean = "000000";
        }

        return Color.FromArgb(
            Convert.ToInt32(clean[..2], 16),
            Convert.ToInt32(clean.Substring(2, 2), 16),
            Convert.ToInt32(clean.Substring(4, 2), 16));
    }

    private static string SanitizeThemeName(string raw)
    {
        var chars = (raw ?? string.Empty).Trim().Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-').ToArray();
        return new string(chars).Trim('-');
    }
}
