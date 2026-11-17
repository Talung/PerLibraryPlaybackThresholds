using System;
using System.Collections.Generic;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.PerLibraryPlaybackThresholds;

public class PluginConfiguration : BasePluginConfiguration
{
    public int DefaultLibraryThreshold { get; set; } = 90;

    public List<LibraryThresholdEntry> LibraryThresholds { get; set; } = new();
}

public class LibraryThresholdEntry
{
    public Guid LibraryId { get; set; }

    public int Threshold { get; set; }
}
