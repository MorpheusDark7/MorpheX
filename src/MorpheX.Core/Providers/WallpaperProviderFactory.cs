using MorpheX.Core.Models;

namespace MorpheX.Core.Providers;

public sealed class WallpaperProviderFactory
{
    private readonly Dictionary<WallpaperType, Func<IWallpaperProvider>> _factories = new();
    private readonly Dictionary<string, WallpaperType> _extensionMap = new(StringComparer.OrdinalIgnoreCase);

    public void Register(WallpaperType type, Func<IWallpaperProvider> factory, IEnumerable<string> extensions)
    {
        _factories[type] = factory;
        foreach (var ext in extensions)
        {
            _extensionMap[ext.StartsWith('.') ? ext : $".{ext}"] = type;
        }
    }

    public IWallpaperProvider Create(WallpaperType type)
    {
        if (_factories.TryGetValue(type, out var factory))
            return factory();

        throw new NotSupportedException($"No provider registered for wallpaper type: {type}");
    }

    public WallpaperType? GetTypeForExtension(string extension)
    {
        var ext = extension.StartsWith('.') ? extension : $".{extension}";
        return _extensionMap.TryGetValue(ext, out var type) ? type : null;
    }

    public IReadOnlyCollection<string> SupportedExtensions => _extensionMap.Keys;
}
