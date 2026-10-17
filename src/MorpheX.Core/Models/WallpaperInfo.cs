using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace MorpheX.Core.Models;

public sealed class WallpaperInfo : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString("N");
    private string _name = string.Empty;
    private WallpaperType _type;
    private string _sourcePath = string.Empty;
    private string? _importedPath;
    private string? _thumbnailPath;
    private int? _width;
    private int? _height;
    private double? _duration;
    private long _fileSize;
    private bool _isFavorite;
    private List<string> _tags = new();
    private DateTimeOffset _dateAdded = DateTimeOffset.UtcNow;
    private bool _isActive;

    public string Id
    {
        get => _id;
        set => SetField(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public WallpaperType Type
    {
        get => _type;
        set => SetField(ref _type, value);
    }

    public string SourcePath
    {
        get => _sourcePath;
        set
        {
            if (SetField(ref _sourcePath, value))
            {
                OnPropertyChanged(nameof(EffectivePath));
                OnPropertyChanged(nameof(PreviewImagePath));
                OnPropertyChanged(nameof(HasPreviewImage));
            }
        }
    }

    public string? ImportedPath
    {
        get => _importedPath;
        set
        {
            if (SetField(ref _importedPath, value))
            {
                OnPropertyChanged(nameof(EffectivePath));
                OnPropertyChanged(nameof(PreviewImagePath));
                OnPropertyChanged(nameof(HasPreviewImage));
            }
        }
    }

    public string? ThumbnailPath
    {
        get => _thumbnailPath;
        set
        {
            if (SetField(ref _thumbnailPath, value))
            {
                OnPropertyChanged(nameof(PreviewImagePath));
                OnPropertyChanged(nameof(HasPreviewImage));
            }
        }
    }

    public int? Width
    {
        get => _width;
        set
        {
            if (SetField(ref _width, value))
            {
                OnPropertyChanged(nameof(ResolutionText));
            }
        }
    }

    public int? Height
    {
        get => _height;
        set
        {
            if (SetField(ref _height, value))
            {
                OnPropertyChanged(nameof(ResolutionText));
            }
        }
    }

    public double? Duration
    {
        get => _duration;
        set => SetField(ref _duration, value);
    }

    public long FileSize
    {
        get => _fileSize;
        set
        {
            if (SetField(ref _fileSize, value))
            {
                OnPropertyChanged(nameof(FileSizeText));
            }
        }
    }

    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            if (SetField(ref _isFavorite, value))
            {
                OnPropertyChanged(nameof(FavoriteBrush));
            }
        }
    }

    /// <summary>
    /// User-defined labels used for searching and organizing the local library.
    /// </summary>
    public List<string> Tags
    {
        get => _tags;
        set
        {
            if (SetField(ref _tags, value))
            {
                OnPropertyChanged(nameof(TagsText));
            }
        }
    }

    public DateTimeOffset DateAdded
    {
        get => _dateAdded;
        set => SetField(ref _dateAdded, value);
    }

    [JsonIgnore]
    public string EffectivePath => !string.IsNullOrEmpty(ImportedPath) ? ImportedPath : SourcePath;

    [JsonIgnore]
    public string? ResolutionText =>
        Width.HasValue && Height.HasValue ? $"{Width}×{Height}" : null;

    [JsonIgnore]
    public string? FileSizeText
    {
        get
        {
            long size = FileSize;
            if (size <= 0 && !string.IsNullOrEmpty(EffectivePath) && File.Exists(EffectivePath))
            {
                try { size = new FileInfo(EffectivePath).Length; } catch { }
            }

            return size > 0
                ? size switch
                {
                    < 1024 => $"{size} B",
                    < 1024 * 1024 => $"{size / 1024.0:F1} KB",
                    < 1024 * 1024 * 1024 => $"{size / (1024.0 * 1024.0):F1} MB",
                    _ => $"{size / (1024.0 * 1024.0 * 1024.0):F2} GB"
                }
                : null;
        }
    }

    [JsonIgnore]
    public bool IsActive
    {
        get => _isActive;
        set => SetField(ref _isActive, value);
    }

    [JsonIgnore]
    public string FavoriteBrush => IsFavorite ? "#10B981" : "#88FFFFFF";

    [JsonIgnore]
    public string TagsText => Tags is { Count: > 0 } ? string.Join(" • ", Tags) : string.Empty;

    [JsonIgnore]
    public string? PreviewImagePath
    {
        get
        {
            if (!string.IsNullOrEmpty(ThumbnailPath) && File.Exists(ThumbnailPath))
                return ThumbnailPath;
            if (Type is WallpaperType.Image or WallpaperType.AnimatedImage && File.Exists(EffectivePath))
                return EffectivePath;
            if (Type == WallpaperType.Scene && Directory.Exists(SourcePath))
            {
                var gif = Path.Combine(SourcePath, "preview.gif");
                if (File.Exists(gif)) return gif;
                var png = Path.Combine(SourcePath, "preview.png");
                if (File.Exists(png)) return png;
                var jpg = Path.Combine(SourcePath, "preview.jpg");
                if (File.Exists(jpg)) return jpg;
            }
            return null;
        }
    }

    [JsonIgnore]
    public bool HasPreviewImage => !string.IsNullOrEmpty(PreviewImagePath) && File.Exists(PreviewImagePath);

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
