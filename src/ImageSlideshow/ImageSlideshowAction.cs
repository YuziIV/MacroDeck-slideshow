using MacroDeck.Localization;
using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;
using System.Globalization;
using SkiaSharp;

namespace ImageSlideshow;

public sealed class ImageSlideshowAction : IActionDefinition, IIconProviderActionDefinition
{
    private const string FolderParameter = "folder";
    private const string IntervalParameter = "intervalSeconds";
    private const string SizeParameter = "imageSize";

    private readonly object _sync = new();
    private readonly Dictionary<(string Folder, int Interval, int Size), SlideState> _slides = [];

    private sealed class SlideState
    {
        public string? Path { get; set; }
        public string? Version { get; set; }
        public DateTimeOffset NextChange { get; set; }
    }

    public string Id => "image-slideshow";
    public LocalizedText Name => Strings.Actions.ImageSlideshow.Name();
    public LocalizedText Description => Strings.Actions.ImageSlideshow.Description();
    public IReadOnlyList<ActionParameter> Parameters { get; } =
    [
        ActionParameter.Text(FolderParameter,
            label: Strings.Actions.ImageSlideshow.Folder.Label(),
            description: Strings.Actions.ImageSlideshow.Folder.Description(),
            placeholder: Strings.Actions.ImageSlideshow.Folder.Placeholder(),
            required: true),
        ActionParameter.Number(IntervalParameter,
            label: Strings.Actions.ImageSlideshow.Interval.Label(), defaultValue: 10),
        ActionParameter.Number(SizeParameter,
            label: Strings.Actions.ImageSlideshow.Size.Label(), defaultValue: 512),
    ];

    public MacroDeckPlatform Platforms => MacroDeckPlatform.All;

    // Check every second so manual button presses update the icon immediately
    public TimeSpan IconPollInterval => TimeSpan.FromSeconds(1);

    public IActionExecutor CreateExecutor() => new Executor(this);

    public Task<ActionIconSnapshot?> GetActionIconAsync(
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        if (!TryGetSettings(parameters, out var settings) || !Directory.Exists(settings.Folder))
        {
            return Task.FromResult<ActionIconSnapshot?>(null);
        }

        lock (_sync)
        {
            if (!_slides.TryGetValue(settings, out var state))
            {
                state = new SlideState();
                _slides.Add(settings, state);
            }

            if (state.Path is null || DateTimeOffset.UtcNow >= state.NextChange || !File.Exists(state.Path))
            {
                AdvanceToNextImage(settings, state);
            }

            if (state.Path is null || state.Version is null)
            {
                return Task.FromResult<ActionIconSnapshot?>(null);
            }

            return Task.FromResult<ActionIconSnapshot?>(new ActionIconSnapshot
            {
                Version = state.Version,
                MediaType = "image/png",
            });
        }
    }

    /// <summary>
    /// Selects a new image, updates the unique version token, and sets the automatic swap timer.
    /// </summary>
    private static void AdvanceToNextImage((string Folder, int Interval, int Size) settings, SlideState state)
    {
        var enumOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true
        };

        var images = Directory.EnumerateFiles(settings.Folder, "*", enumOptions)
            .Where(IsSupportedImage)
            .ToArray();

        if (images.Length == 0)
        {
            state.Path = null;
            state.Version = null;
            return;
        }

        // Avoid repeating the exact same image consecutively if alternatives exist
        var choices = images.Where(p => p != state.Path).ToArray();
        var pool = choices.Length > 0 ? choices : images;

        state.Path = pool[Random.Shared.Next(pool.Length)];

        // GUID guarantees Macro Deck invalidates its cache and requests fresh bytes immediately
        state.Version = $"{Guid.NewGuid():N}:{File.GetLastWriteTimeUtc(state.Path).Ticks}";

        state.NextChange = DateTimeOffset.UtcNow.AddSeconds(settings.Interval);
    }

    /// <summary>
    /// Explicitly called on button press to immediately advance to the next image.
    /// </summary>
    public bool Skip(IReadOnlyDictionary<string, object> parameters)
    {
        if (!TryGetSettings(parameters.ToDictionary(entry => entry.Key, entry => (object?)entry.Value), out var settings)
            || !Directory.Exists(settings.Folder))
        {
            return false;
        }

        lock (_sync)
        {
            if (!_slides.TryGetValue(settings, out var state))
            {
                state = new SlideState();
                _slides.Add(settings, state);
            }
            AdvanceToNextImage(settings, state);
            return state.Path is not null;
        }
    }

    public Task<ActionIconContent?> GetActionIconContentAsync(
        IReadOnlyDictionary<string, object?> parameters,
        string version,
        CancellationToken cancellationToken)
    {
        string? path = null;
        var size = 512;
        lock (_sync)
        {
            foreach (var (settings, state) in _slides)
            {
                if (state.Version != version) continue;
                path = state.Path;
                size = settings.Size;
                break;
            }
        }

        if (path is null || !File.Exists(path))
        {
            return Task.FromResult<ActionIconContent?>(null);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var source = SKBitmap.Decode(path);
            if (source is null || source.Width == 0 || source.Height == 0)
                return Task.FromResult<ActionIconContent?>(null);

            using var surface = SKSurface.Create(new SKImageInfo(size, size));
            if (surface is null) return Task.FromResult<ActionIconContent?>(null);

            var crop = Math.Min(source.Width, source.Height);
            var sourceRect = new SKRect(
                (source.Width - crop) / 2f, (source.Height - crop) / 2f,
                (source.Width + crop) / 2f, (source.Height + crop) / 2f);
            using var paint = new SKPaint { IsAntialias = true };
            surface.Canvas.Clear(SKColors.Transparent);
            surface.Canvas.DrawBitmap(source, sourceRect, new SKRect(0, 0, size, size),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear), paint);
            using var rendered = surface.Snapshot();
            using var data = rendered.Encode(SKEncodedImageFormat.Png, 100);
            return Task.FromResult<ActionIconContent?>(new ActionIconContent(data.ToArray(), "image/png"));
        }
        catch (IOException)
        {
            return Task.FromResult<ActionIconContent?>(null);
        }
        catch (UnauthorizedAccessException)
        {
            return Task.FromResult<ActionIconContent?>(null);
        }
    }

    private static bool TryGetSettings(IReadOnlyDictionary<string, object?> parameters,
        out (string Folder, int Interval, int Size) settings)
    {
        var folder = parameters.GetValueOrDefault(FolderParameter)?.ToString()?.Trim();
        settings = default;
        if (string.IsNullOrWhiteSpace(folder) ||
            !TryReadInt(parameters, IntervalParameter, 10, 1, 3600, out var interval) ||
            !TryReadInt(parameters, SizeParameter, 512, 64, 1024, out var size))
        {
            return false;
        }
        settings = (folder, interval, size);
        return true;
    }

    private static bool TryReadInt(IReadOnlyDictionary<string, object?> parameters, string name,
        int fallback, int min, int max, out int result)
    {
        result = fallback;
        if (!parameters.TryGetValue(name, out var value) || value is null) return true;
        return double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture),
                   NumberStyles.Float, CultureInfo.InvariantCulture, out var number) &&
               number >= min && number <= max && number == Math.Truncate(number) &&
               (result = (int)number) >= min;
    }

    private static bool IsSupportedImage(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".webp";

    private sealed class Executor(ImageSlideshowAction action) : IActionExecutor
    {
        public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
        {
            return action.Skip(context.Parameters)
                ? ActionResult.SucceededTask
                : Task.FromResult(ActionResult.Failed(ActionErrorCodes.InvalidParameter,
                    Strings.Actions.ImageSlideshow.InvalidSettings()));
        }
    }
}
