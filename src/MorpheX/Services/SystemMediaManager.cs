using Windows.Media.Control;

namespace MorpheX.Services;

/// <summary>
/// Reads current playing audio/media from Windows (Spotify, YouTube, Chrome, Edge, VLC, etc.)
/// and controls playback via Windows GlobalSystemMediaTransportControlsSessionManager.
/// </summary>
public sealed class SystemMediaManager
{
    private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
    private bool _initialized;

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        }
        catch
        {
            _sessionManager = null;
        }
    }

    public async Task<(string Title, string Artist, bool IsPlaying)> GetCurrentMediaInfoAsync()
    {
        try
        {
            if (_sessionManager == null)
            {
                await InitializeAsync();
            }

            var session = _sessionManager?.GetCurrentSession();
            if (session != null)
            {
                var mediaProps = await session.TryGetMediaPropertiesAsync();
                var playbackInfo = session.GetPlaybackInfo();

                bool isPlaying = playbackInfo?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                string title = mediaProps?.Title ?? "";
                string artist = mediaProps?.Artist ?? "";

                if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(artist))
                {
                    title = session.SourceAppUserModelId ?? "System Media";
                }

                return (title, artist, isPlaying);
            }
        }
        catch
        {
            // If session invalidated, allow re-initialization on next cycle
            _sessionManager = null;
            _initialized = false;
        }

        return ("", "", false);
    }

    public async Task TogglePlayPauseAsync()
    {
        try
        {
            var session = _sessionManager?.GetCurrentSession();
            if (session != null)
            {
                var info = session.GetPlaybackInfo();
                if (info?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                {
                    await session.TryPauseAsync();
                }
                else
                {
                    await session.TryPlayAsync();
                }
            }
        }
        catch { }
    }

    public async Task NextTrackAsync()
    {
        try
        {
            var session = _sessionManager?.GetCurrentSession();
            if (session != null)
            {
                await session.TrySkipNextAsync();
            }
        }
        catch { }
    }

    public async Task PreviousTrackAsync()
    {
        try
        {
            var session = _sessionManager?.GetCurrentSession();
            if (session != null)
            {
                await session.TrySkipPreviousAsync();
            }
        }
        catch { }
    }
}
