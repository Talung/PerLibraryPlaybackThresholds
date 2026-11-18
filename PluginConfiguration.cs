using System;
using System.Collections.Generic;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.PerLibraryPlaybackThresholds;

public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the default "mark as played after" percentage (1-99).
    /// </summary>
    public int DefaultLibraryThreshold { get; set; } = 90;

    /// <summary>
    /// Gets or sets the default minimum percentage below which progress is not saved.
    /// 0 means "use Jellyfin's own setting".
    /// </summary>
    public int DefaultMinimumPercentage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether verbose playback diagnostics are logged.
    /// </summary>
    public bool EnableDiagnosticLogging { get; set; }

    public List<LibraryThresholdEntry> LibraryThresholds { get; set; } = new();
}

public class LibraryThresholdEntry
{
    public Guid LibraryId { get; set; }

    /// <summary>
    /// Gets or sets the "mark as played after" percentage (1-99).
    /// </summary>
    public int Threshold { get; set; }

    /// <summary>
    /// Gets or sets the minimum percentage below which progress is not saved.
    /// 0 means "use the plugin default".
    /// </summary>
    public int MinimumPercentage { get; set; }
}
