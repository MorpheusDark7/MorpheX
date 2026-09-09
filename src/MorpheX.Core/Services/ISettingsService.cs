using MorpheX.Core.Configuration;

namespace MorpheX.Core.Services;

public interface ISettingsService
{
    AppSettings Settings { get; }

    Task LoadAsync(CancellationToken ct = default);

    Task SaveAsync(CancellationToken ct = default);

    Task ResetAsync(CancellationToken ct = default);

    event EventHandler? SettingsChanged;
}
