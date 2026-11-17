
using System;
using System.Linq;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.PerLibraryPlaybackThresholds;

public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(
        IServiceCollection serviceCollection,
        IServerApplicationHost applicationHost)
    {
        Console.WriteLine(
            "Per-Library Playback Thresholds v0.1.6.0: RegisterServices invoked.");

        var originalDescriptor = serviceCollection.LastOrDefault(
            descriptor => descriptor.ServiceType == typeof(IUserDataManager));

        if (originalDescriptor?.ImplementationType is null)
        {
            Console.WriteLine(
                "Per-Library Playback Thresholds: Original IUserDataManager registration not found or has no implementation type.");

            throw new InvalidOperationException(
                "Could not locate Jellyfin's original IUserDataManager registration.");
        }

        var originalImplementationType = originalDescriptor.ImplementationType;

        serviceCollection.Remove(originalDescriptor);

        serviceCollection.AddSingleton<IUserDataManager>(serviceProvider =>
        {
            var original = (IUserDataManager)ActivatorUtilities.CreateInstance(
                serviceProvider,
                originalImplementationType);

            return ActivatorUtilities.CreateInstance<PerLibraryUserDataManager>(
                serviceProvider,
                original);
        });

        Console.WriteLine(
            "Per-Library Playback Thresholds: IUserDataManager replacement registered.");
    }
}
