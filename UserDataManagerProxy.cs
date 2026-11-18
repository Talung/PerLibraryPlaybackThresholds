using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.PerLibraryPlaybackThresholds;

/// <summary>
/// Forwards every <see cref="IUserDataManager"/> member to Jellyfin's implementation and
/// intercepts only <c>UpdatePlayState</c>. Because it is a dynamic proxy, members added to the
/// interface in future Jellyfin releases need no changes here.
/// </summary>
public class UserDataManagerProxy : DispatchProxy
{
    private IUserDataManager? _original;
    private PlaybackThresholdService? _service;

    internal void Initialize(IUserDataManager original, PlaybackThresholdService service)
    {
        _original = original;
        _service = service;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod is null || _original is null)
        {
            throw new InvalidOperationException("UserDataManagerProxy has not been initialised.");
        }

        try
        {
            if (_service is not null
                && targetMethod.Name == nameof(IUserDataManager.UpdatePlayState)
                && args is { Length: 3 }
                && args[0] is BaseItem item
                && args[1] is UserItemData data)
            {
                return _service.UpdatePlayState(_original, item, data, (long?)args[2]);
            }

            return targetMethod.Invoke(_original, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw; // Unreachable; satisfies the compiler.
        }
    }
}
