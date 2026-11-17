using System;
using System.Collections.Generic;
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

public sealed class PerLibraryUserDataManager : IUserDataManager
{
    private readonly IUserDataManager _original;
    private readonly IServerConfigurationManager _serverConfiguration;
    private readonly Lazy<ILibraryManager> _libraryManager;
    private readonly ILogger<PerLibraryUserDataManager> _logger;

    public PerLibraryUserDataManager(
        IUserDataManager original,
        IServiceProvider serviceProvider,
        IServerConfigurationManager serverConfiguration,
        ILogger<PerLibraryUserDataManager> logger)
    {
        _original = original;
        _serverConfiguration = serverConfiguration;
        _logger = logger;

	_logger.LogInformation(
    "Per-Library Playback Thresholds v0.1.2.0: PerLibraryUserDataManager instantiated.");

        _libraryManager = new Lazy<ILibraryManager>(
            () => serviceProvider.GetRequiredService<ILibraryManager>());
    }

    public event EventHandler<UserDataSaveEventArgs>? UserDataSaved
    {
        add => _original.UserDataSaved += value;
        remove => _original.UserDataSaved -= value;
    }

    public void SaveUserData(
        User user,
        BaseItem item,
        UserItemData userData,
        UserDataSaveReason reason,
        CancellationToken cancellationToken)
        => _original.SaveUserData(user, item, userData, reason, cancellationToken);

    public void SaveUserData(
        User user,
        BaseItem item,
        UpdateUserItemDataDto userDataDto,
        UserDataSaveReason reason)
        => _original.SaveUserData(user, item, userDataDto, reason);

    public UserItemData? GetUserData(User user, BaseItem item)
        => _original.GetUserData(user, item);

    public UserItemDataDto? GetUserDataDto(BaseItem item, User user)
        => _original.GetUserDataDto(item, user);

    public UserItemDataDto? GetUserDataDto(
        BaseItem item,
        BaseItemDto? itemDto,
        User user,
        DtoOptions options)
        => _original.GetUserDataDto(item, itemDto, user, options);

    public Dictionary<Guid, UserItemData> GetUserDataBatch(
        IReadOnlyList<BaseItem> items,
        User user)
        => _original.GetUserDataBatch(items, user);

    public VersionResumeData? GetResumeUserData(User user, BaseItem item)
        => _original.GetResumeUserData(user, item);

    public IReadOnlyDictionary<Guid, VersionResumeData> GetResumeUserDataBatch(
        IReadOnlyList<BaseItem> items,
        User user)
        => _original.GetResumeUserDataBatch(items, user);

    public void ResetPlaybackStreamSelections(User user, BaseItem item)
        => _original.ResetPlaybackStreamSelections(user, item);


    public bool UpdatePlayState(
        BaseItem item,
        UserItemData data,
        long? reportedPositionTicks)
    {
        var configuration = _serverConfiguration.Configuration;

        var maximumResumePct = configuration.MaxResumePct;

        if (item is Video)
        {
            maximumResumePct = GetLibraryThreshold(
                item,
                maximumResumePct);
        }

        long runTimeTicksForPlayState = item.GetRunTimeTicksForPlayState();
        long num = reportedPositionTicks ?? runTimeTicksForPlayState;
        bool flag = runTimeTicksForPlayState > 0;

        _logger.LogInformation(
            "Playback threshold diagnostic: Item={ItemName}, ItemId={ItemId}, " +
            "Type={ItemType}, ReportedPositionTicks={PositionTicks}, " +
            "RuntimeTicks={RuntimeTicks}, GlobalThreshold={GlobalThreshold}, " +
            "SelectedThreshold={SelectedThreshold}",
            item.Name,
            item.Id,
            item.GetType().Name,
            reportedPositionTicks,
            runTimeTicksForPlayState,
            configuration.MaxResumePct,
            maximumResumePct);

        bool result = false;

        if (num > 0 && flag && item is not AudioBook && item is not Book)
        {
            decimal percentage =
                decimal.Divide(num, runTimeTicksForPlayState) * 100m;

            if (percentage < configuration.MinResumePct)
            {
                num = 0L;
            }
            else if (
                percentage > maximumResumePct ||
                num >= runTimeTicksForPlayState - 10000000)
            {
                num = 0L;
                result = data.Played = true;
            }
            else if (
                TimeSpan.FromTicks(runTimeTicksForPlayState).TotalSeconds
                < configuration.MinResumeDurationSeconds)
            {
                num = 0L;
                result = data.Played = true;
            }
        }
        else if (num > 0 && flag && item is AudioBook)
        {
            double totalMinutes = TimeSpan.FromTicks(num).TotalMinutes;

            double remainingMinutes =
                TimeSpan.FromTicks(runTimeTicksForPlayState - num).TotalMinutes;

            if (totalMinutes < configuration.MinAudiobookResume)
            {
                num = 0L;
            }
            else if (
                remainingMinutes < configuration.MaxAudiobookResume ||
                num >= runTimeTicksForPlayState)
            {
                num = 0L;
                result = data.Played = true;
            }
        }
        else if (!flag)
        {
            result = data.Played = true;
            num = 0L;
        }

        if (!item.SupportsPlayedStatus)
        {
            num = 0L;
            data.Played = false;
        }

        if (!item.SupportsPositionTicksResume)
        {
            num = 0L;
        }

        data.PlaybackPositionTicks = num;

        decimal? finalPercentage = runTimeTicksForPlayState > 0
            ? (decimal?)num * 100m / runTimeTicksForPlayState
            : null;

        _logger.LogInformation(
            "Playback threshold result: Item={ItemName}, " +
            "PositionPercent={PositionPercent}, Played={Played}, " +
            "PlaybackPositionTicks={FinalPositionTicks}, ReturnValue={ReturnValue}",
            item.Name,
            finalPercentage,
            data.Played,
            num,
            result);

        return result;
    }


    private int GetLibraryThreshold(BaseItem item, int globalThreshold)
    {
        try
        {
            var libraries = _libraryManager.Value.GetCollectionFolders(item);

            // Ambiguous or unresolved library: retain Jellyfin's global setting.
            if (libraries.Count != 1)
            {
                return globalThreshold;
            }

            var pluginConfiguration = Plugin.Instance?.Configuration;

            if (pluginConfiguration is null)
            {
                return globalThreshold;
            }

            var libraryThresholdEntry = pluginConfiguration.LibraryThresholds
                .FirstOrDefault(entry => entry.LibraryId == libraries[0].Id);

            if (libraryThresholdEntry is not null)
            {
                return IsValidThreshold(libraryThresholdEntry.Threshold)
                    ? libraryThresholdEntry.Threshold
                    : globalThreshold;
            }

            int defaultThreshold =
                pluginConfiguration.DefaultLibraryThreshold;

            return IsValidThreshold(defaultThreshold)
                ? defaultThreshold
                : globalThreshold;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unable to determine playback threshold for item {ItemId}. Using global threshold.",
                item.Id);

            return globalThreshold;
        }
    }

    private static bool IsValidThreshold(int threshold)
        => threshold is >= 0 and <= 100;
}

