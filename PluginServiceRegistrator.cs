using System;
using System.Linq;
using System.Reflection;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.PerLibraryPlaybackThresholds;

public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(
        IServiceCollection serviceCollection,
        IServerApplicationHost applicationHost)
    {
        var originalDescriptor = serviceCollection.LastOrDefault(
            descriptor => descriptor.ServiceType == typeof(IUserDataManager));

        if (originalDescriptor?.ImplementationType is null)
        {
            // Leave Jellyfin's default behaviour untouched instead of failing plugin load.
            Console.WriteLine(
                "Per-Library Playback Thresholds: IUserDataManager registration is not replaceable; skipping.");
            return;
        }

        var originalImplementationType = originalDescriptor.ImplementationType;

        serviceCollection.Remove(originalDescriptor);

        serviceCollection.AddSingleton<IUserDataManager>(serviceProvider =>
        {
            var original = (IUserDataManager)ActivatorUtilities.CreateInstance(
                serviceProvider,
                originalImplementationType);

            var service = new PlaybackThresholdService(
                serviceProvider.GetRequiredService<IServerConfigurationManager>(),
                serviceProvider,
                serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger<PlaybackThresholdService>());

            var proxy = DispatchProxy.Create<IUserDataManager, UserDataManagerProxy>();
            ((UserDataManagerProxy)(object)proxy).Initialize(original, service);

            return proxy;
        });
    }
}
