using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.PerLibraryPlaybackThresholds;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    private const string PluginVersion = "0.1.6.0";

    private readonly ILogger<Plugin> _logger;
    private readonly ISessionManager _sessionManager;
    private readonly ILibraryManager _libraryManager;

    public static Plugin? Instance { get; private set; }

    public Plugin(
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer,
        ILogger<Plugin> logger,
        ISessionManager sessionManager,
        ILibraryManager libraryManager)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;

        _logger = logger;
        _sessionManager = sessionManager;
        _libraryManager = libraryManager;

        _sessionManager.PlaybackStart += OnPlaybackStart;
        _sessionManager.PlaybackProgress += OnPlaybackProgress;
        _sessionManager.PlaybackStopped += OnPlaybackStopped;

        _logger.LogInformation(
            "Per-Library Playback Thresholds v{Version}: diagnostic listeners registered.",
            PluginVersion);
    }

    public override string Name => "Per-Library Playback Thresholds";

    public override Guid Id =>
        Guid.Parse("a8c6f8e2-7d31-4b95-9c42-1e5f6a0b3d87");



    public IEnumerable<PluginPageInfo> GetPages()
    {
        yield return new PluginPageInfo
        {
            Name = Name,
            EmbeddedResourcePath =
                $"{GetType().Namespace}.Pages.configPage.html"
        };
    }



    private void OnPlaybackStart(
        object? sender,
        PlaybackProgressEventArgs e)
    {
        LogPlaybackEvent("START", e);
    }

    private void OnPlaybackProgress(
        object? sender,
        PlaybackProgressEventArgs e)
    {
        LogPlaybackEvent("PROGRESS", e);
    }

    private void OnPlaybackStopped(
        object? sender,
        PlaybackStopEventArgs e)
    {
        LogPlaybackEvent("STOP", e, e.PlayedToCompletion);
    }

    private void LogPlaybackEvent(
        string eventName,
        PlaybackProgressEventArgs e,
        bool? playedToCompletion = null)
    {
        try
        {
            var item = e.Item;

            if (item is null)
            {
                _logger.LogInformation(
                    "[PlaybackDiagnostic] {Event}: No library item supplied.",
                    eventName);

                return;
            }

            var users = e.Users is { Count: > 0 }
                ? string.Join(", ", e.Users.Select(
                    u => $"{u.Username} ({u.Id})"))
                : "(none)";

            var libraries = _libraryManager.GetCollectionFolders(item);

            var libraryNames = libraries.Count > 0
                ? string.Join(", ", libraries.Select(
                    f => $"{f.Name} ({f.Id})"))
                : "(unresolved)";

            var positionTicks = e.PlaybackPositionTicks;
            var runtimeTicks = item.RunTimeTicks;

            var percentage = positionTicks.HasValue
                && runtimeTicks.HasValue
                && runtimeTicks.Value > 0
                    ? $"{(decimal)positionTicks.Value * 100m / runtimeTicks.Value:0.00}%"
                    : "n/a";

            var completion = playedToCompletion.HasValue
                ? playedToCompletion.Value.ToString()
                : "n/a";

            _logger.LogInformation(
                "[PlaybackDiagnostic] {Event} | " +
                "Item={ItemName} | ItemId={ItemId} | Type={ItemType} | " +
                "Libraries={Libraries} | Users={Users} | " +
                "PositionTicks={PositionTicks} | RuntimeTicks={RuntimeTicks} | " +
                "PositionPercent={Percentage} | " +
                "PlayedToCompletion={Completion} | " +
                "Automated={Automated} | Paused={Paused} | " +
                "Client={Client} | Device={Device} | PlaySession={PlaySession}",
                eventName,
                item.Name,
                item.Id,
                item.GetType().Name,
                libraryNames,
                users,
                positionTicks?.ToString() ?? "null",
                runtimeTicks?.ToString() ?? "null",
                percentage,
                completion,
                e.IsAutomated,
                e.IsPaused,
                e.ClientName ?? "(unknown)",
                e.DeviceName ?? "(unknown)",
                e.PlaySessionId ?? "(unknown)");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[PlaybackDiagnostic] Error handling {Event} event.",
                eventName);
        }
    }
}
