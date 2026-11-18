using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Threading;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.PerLibraryPlaybackThresholds;

/// <summary>
/// Decides the effective thresholds for an item and applies them.
/// Anything this plugin does not need to change is delegated to Jellyfin's own implementation,
/// so behaviour stays in sync with future server releases.
/// </summary>
/// <remarks>
/// Logging (all at Information level so it appears in Jellyfin's default log):
///  - always: proxy active (once), configuration in effect (when it changes),
///    and one line whenever an item's decision changes (below minimum / in progress / played).
///  - Debug logging option: one "[Check]" line for every single percentage check.
/// </remarks>
internal sealed class PlaybackThresholdService
{
    private const long CompletionToleranceTicks = 10_000_000; // 1 second, as in Jellyfin.
    private const int MaxCacheEntries = 5000;
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(5);

    private readonly IServerConfigurationManager _serverConfiguration;
    private readonly Lazy<ILibraryManager> _libraryManager;
    private readonly ILogger<PlaybackThresholdService> _logger;

    private readonly ConcurrentDictionary<Guid, (Guid? LibraryId, string LibraryName, DateTime ExpiresUtc)> _libraryCache = new();
    private readonly ConcurrentDictionary<Guid, string> _lastDecision = new();

    private int _firstCallLogged;
    private string? _lastConfigurationSignature;

    public PlaybackThresholdService(
        IServerConfigurationManager serverConfiguration,
        IServiceProvider serviceProvider,
        ILogger<PlaybackThresholdService> logger)
    {
        _serverConfiguration = serverConfiguration;
        _logger = logger;

        // Resolved lazily to avoid a circular dependency during startup.
        _libraryManager = new Lazy<ILibraryManager>(
            () => serviceProvider.GetRequiredService<ILibraryManager>());
    }

    private readonly record struct Resolved(int Max, int Min, string MaxSource, string MinSource, string Library);

    public bool UpdatePlayState(
        IUserDataManager original,
        BaseItem item,
        UserItemData data,
        long? reportedPositionTicks)
    {
        var configuration = Plugin.Instance?.Configuration;
        bool debug = configuration?.EnableDebugLogging == true;

        if (Interlocked.Exchange(ref _firstCallLogged, 1) == 0)
        {
            _logger.LogInformation(
                "Per-Library Playback Thresholds: playback proxy is active. First UpdatePlayState call intercepted " +
                "(Item={ItemName}, Type={ItemType}).",
                item.Name,
                item.GetType().Name);
        }

        // Only video libraries are customised; everything else is Jellyfin's default behaviour.
        if (item is not Video)
        {
            if (debug)
            {
                _logger.LogInformation(
                    "[Check] {ItemName}: {ItemType} is not a video, Jellyfin default behaviour.",
                    item.Name,
                    item.GetType().Name);
            }

            return original.UpdatePlayState(item, data, reportedPositionTicks);
        }

        var serverConfig = _serverConfiguration.Configuration;
        int serverMax = serverConfig.MaxResumePct;
        int serverMin = serverConfig.MinResumePct;

        Resolved resolved;
        try
        {
            resolved = Resolve(configuration, item, serverMin, serverMax);

            if (configuration is not null)
            {
                LogConfigurationIfChanged(configuration, serverMin, serverMax);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unable to determine playback thresholds for item {ItemId}. Using Jellyfin defaults.",
                item.Id);

            return original.UpdatePlayState(item, data, reportedPositionTicks);
        }

        bool custom = resolved.Max != serverMax || resolved.Min != serverMin;

        long runtimeTicks = item.GetRunTimeTicksForPlayState();
        decimal? percent = runtimeTicks > 0
            ? decimal.Divide(reportedPositionTicks ?? runtimeTicks, runtimeTicks) * 100m
            : null;

        bool result = custom
            ? Apply(item, data, reportedPositionTicks, resolved, serverConfig.MinResumeDurationSeconds)
            : original.UpdatePlayState(item, data, reportedPositionTicks);

        try
        {
            LogOutcome(item, data, reportedPositionTicks, runtimeTicks, percent, resolved, custom, result, debug);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to log playback check for item {ItemId}.", item.Id);
        }

        return result;
    }

    private bool Apply(
        BaseItem item,
        UserItemData data,
        long? reportedPositionTicks,
        Resolved thresholds,
        int minResumeDurationSeconds)
    {
        long runtimeTicks = item.GetRunTimeTicksForPlayState();
        long position = reportedPositionTicks ?? runtimeTicks;
        bool hasRuntime = runtimeTicks > 0;
        bool played = false;

        if (position > 0 && hasRuntime)
        {
            decimal percentage = decimal.Divide(position, runtimeTicks) * 100m;

            if (percentage < thresholds.Min)
            {
                position = 0;
            }
            else if (percentage > thresholds.Max || position >= runtimeTicks - CompletionToleranceTicks)
            {
                position = 0;
                played = data.Played = true;
            }
            else if (TimeSpan.FromTicks(runtimeTicks).TotalSeconds < minResumeDurationSeconds)
            {
                position = 0;
                played = data.Played = true;
            }
        }
        else if (!hasRuntime)
        {
            played = data.Played = true;
            position = 0;
        }

        if (!item.SupportsPlayedStatus)
        {
            position = 0;
            data.Played = false;
        }

        if (!item.SupportsPositionTicksResume)
        {
            position = 0;
        }

        data.PlaybackPositionTicks = position;
        return played;
    }

    private void LogOutcome(
        BaseItem item,
        UserItemData data,
        long? reportedPositionTicks,
        long runtimeTicks,
        decimal? percent,
        Resolved resolved,
        bool custom,
        bool result,
        bool debug)
    {
        string decision = percent is null
            ? "no runtime, marked as played"
            : percent < resolved.Min
                ? "below minimum, progress NOT saved"
                : result
                    ? "marked as played"
                    : "in progress, progress saved";

        string handledBy = custom ? "plugin" : "Jellyfin (same values as server)";
        string percentText = percent is null
            ? "n/a"
            : percent.Value.ToString("0.0", CultureInfo.InvariantCulture) + "%";

        if (debug)
        {
            _logger.LogInformation(
                "[Check] {ItemName} [{Library}] position {Percent} ({Position}/{Runtime} ticks) | " +
                "minimum {Min}% ({MinSource}) | played after {Max}% ({MaxSource}) | handled by {HandledBy} | " +
                "result: {Decision} (Played={Played}, SavedPositionTicks={Saved})",
                item.Name,
                resolved.Library,
                percentText,
                reportedPositionTicks?.ToString(CultureInfo.InvariantCulture) ?? "end",
                runtimeTicks,
                resolved.Min,
                resolved.MinSource,
                resolved.Max,
                resolved.MaxSource,
                handledBy,
                decision,
                data.Played,
                data.PlaybackPositionTicks);

            return;
        }

        // Always-on trail: one line each time an item's decision changes.
        if (_lastDecision.TryGetValue(item.Id, out var previous) && previous == decision)
        {
            return;
        }

        if (_lastDecision.Count >= MaxCacheEntries)
        {
            _lastDecision.Clear();
        }

        _lastDecision[item.Id] = decision;

        _logger.LogInformation(
            "Playback check: {ItemName} [{Library}] at {Percent} -> {Decision}. " +
            "Minimum {Min}% ({MinSource}), played after {Max}% ({MaxSource}), handled by {HandledBy}.",
            item.Name,
            resolved.Library,
            percentText,
            decision,
            resolved.Min,
            resolved.MinSource,
            resolved.Max,
            resolved.MaxSource,
            handledBy);
    }

    private void LogConfigurationIfChanged(PluginConfiguration configuration, int serverMin, int serverMax)
    {
        string overrides = configuration.LibraryThresholds.Count == 0
            ? "(none)"
            : string.Join(
                ", ",
                configuration.LibraryThresholds.Select(
                    e => $"{e.LibraryId:N}: minimum {e.MinimumPercentage}%, played after {e.Threshold}%"));

        string signature =
            $"{configuration.DefaultLibraryThreshold}|{configuration.DefaultMinimumPercentage}|{serverMin}|{serverMax}|{overrides}";

        if (signature == _lastConfigurationSignature)
        {
            return;
        }

        _lastConfigurationSignature = signature;

        _logger.LogInformation(
            "Per-Library Playback Thresholds: configuration in effect. Plugin defaults: minimum {DefaultMin}% " +
            "(0 = Jellyfin's {ServerMin}%), played after {DefaultMax}%. Jellyfin: minimum {ServerMin2}%, " +
            "played after {ServerMax}%. Library overrides: {Overrides}",
            configuration.DefaultMinimumPercentage,
            serverMin,
            configuration.DefaultLibraryThreshold,
            serverMin,
            serverMax,
            overrides);
    }

    private Resolved Resolve(PluginConfiguration? configuration, BaseItem item, int serverMin, int serverMax)
    {
        if (configuration is null)
        {
            return new Resolved(serverMax, serverMin, "Jellyfin", "Jellyfin", "unknown");
        }

        var (libraryId, libraryName) = GetLibrary(item);

        // Unknown or ambiguous (multi-library) items keep Jellyfin's settings.
        if (libraryId is null)
        {
            const string reason = "Jellyfin, library not resolved";
            return new Resolved(serverMax, serverMin, reason, reason, libraryName);
        }

        int max = serverMax;
        string maxSource = "Jellyfin";
        int min = serverMin;
        string minSource = "Jellyfin";

        if (IsValidMax(configuration.DefaultLibraryThreshold))
        {
            max = configuration.DefaultLibraryThreshold;
            maxSource = "plugin default";
        }

        if (configuration.DefaultMinimumPercentage > 0 && IsValidMin(configuration.DefaultMinimumPercentage))
        {
            min = configuration.DefaultMinimumPercentage;
            minSource = "plugin default";
        }

        var entry = configuration.LibraryThresholds.FirstOrDefault(e => e.LibraryId == libraryId.Value);
        if (entry is not null)
        {
            if (IsValidMax(entry.Threshold))
            {
                max = entry.Threshold;
                maxSource = "library override";
            }

            if (entry.MinimumPercentage > 0 && IsValidMin(entry.MinimumPercentage))
            {
                min = entry.MinimumPercentage;
                minSource = "library override";
            }
        }

        // The minimum must stay below the completion threshold.
        if (min >= max)
        {
            min = Math.Max(0, Math.Min(serverMin, max - 1));
            minSource = "adjusted, must be below played-after";
        }

        return new Resolved(max, min, maxSource, minSource, libraryName);
    }

    private (Guid? Id, string Name) GetLibrary(BaseItem item)
    {
        var now = DateTime.UtcNow;

        if (_libraryCache.TryGetValue(item.Id, out var cached) && cached.ExpiresUtc > now)
        {
            return (cached.LibraryId, cached.LibraryName);
        }

        var folders = _libraryManager.Value.GetCollectionFolders(item);

        Guid? libraryId = folders.Count == 1 ? folders[0].Id : null;
        string libraryName = folders.Count switch
        {
            1 => folders[0].Name,
            0 => "no library found",
            _ => "several libraries"
        };

        if (_libraryCache.Count >= MaxCacheEntries)
        {
            _libraryCache.Clear();
        }

        _libraryCache[item.Id] = (libraryId, libraryName, now + CacheLifetime);
        return (libraryId, libraryName);
    }

    private static bool IsValidMax(int value) => value is >= 1 and <= 99;

    private static bool IsValidMin(int value) => value is >= 0 and <= 98;
}
