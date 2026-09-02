using System.Collections.Concurrent;
using Engine.Assets.Contracts;
using Engine.Kernel.Diagnostics;
using Engine.Kernel.Events;

namespace Engine.Assets;

internal sealed class AssetService(IEventBus events, ILogger log) : IAssetService, IDisposable
{
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly ConcurrentQueue<TextureReloaded> _pendingEvents = new();
    private readonly Dictionary<string, DateTime> _lastTriggered = [];
    private readonly Lock _debounceLock = new();

    public TextureData LoadTexture(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var data = Decode(fullPath);
        Watch(fullPath);
        return data;
    }

    /// <summary>
    /// Drains reloads decoded on background threads and publishes them on
    /// whichever thread calls this — meant to run once per Update stage,
    /// which always runs on the frame loop's own thread. A subscriber that
    /// reacts to TextureReloaded by touching a GL texture needs that: GL
    /// contexts are thread-affine, and FileSystemWatcher.Changed fires on
    /// a ThreadPool thread that was never made current for any of them.
    /// </summary>
    public void PumpReloads()
    {
        while (_pendingEvents.TryDequeue(out var evt))
        {
            events.Publish(evt);
            log.Info($"reloaded texture '{evt.Path}'");
        }
    }

    private void Watch(string fullPath)
    {
        var directory = Path.GetDirectoryName(fullPath)!;
        var fileName = Path.GetFileName(fullPath);

        var watcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
        };

        watcher.Changed += (_, _) => OnChanged(fullPath);
        watcher.EnableRaisingEvents = true;
        _watchers.Add(watcher);
    }

    private void OnChanged(string path)
    {
        // Editors/tools often fire several Changed events for one save
        // (truncate + write, or multiple flushes) — a quiet window per
        // path collapses those into a single reload instead of several.
        lock (_debounceLock)
        {
            var now = DateTime.UtcNow;
            if (_lastTriggered.TryGetValue(path, out var last) && now - last < TimeSpan.FromMilliseconds(200))
                return;
            _lastTriggered[path] = now;
        }

        _ = Task.Run(() => ReloadWithRetry(path));
    }

    private async Task ReloadWithRetry(string path)
    {
        // The writer may still be flushing when Changed fires — a short
        // retry window absorbs that instead of surfacing a transient
        // IOException as a real failure.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                _pendingEvents.Enqueue(new TextureReloaded(path, Decode(path)));
                return;
            }
            catch (IOException) when (attempt < 4)
            {
                await Task.Delay(50);
            }
            catch (IOException ex)
            {
                // The 5th and final attempt — the `when` guard above only
                // covers attempts 0-3, so this is the one case that used
                // to fall out of the loop and propagate from a
                // fire-and-forget Task (`_ = Task.Run(...)`) as an
                // unobserved exception: no log, no event, nothing to show
                // the reload silently never happened.
                log.Warn($"Giving up reloading '{path}' after 5 attempts: {ex.Message}");
            }
        }
    }

    private static TextureData Decode(string path)
    {
        var (width, height, rgba) = PngReader.Read(path);
        return new TextureData(width, height, rgba);
    }

    public void Dispose()
    {
        // Undisposed FileSystemWatchers would pin this plugin's ALC the
        // same way a forgotten Schedule/EventBus registration would —
        // each one's Changed handler is a delegate into this assembly.
        foreach (var watcher in _watchers)
            watcher.Dispose();

        _watchers.Clear();
    }
}
