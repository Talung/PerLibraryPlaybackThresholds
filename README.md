# Jellyfin Per-Library Playback Thresholds Plugin

A Jellyfin plugin that allows you to set custom playback thresholds for individual libraries, giving you fine-grained control over when videos are marked as played and at what minimum progress is saved.

## Description

This plugin enables users to customize Jellyfin's default playback behavior on a per-library basis. By default, Jellyfin marks videos as played after 90% of playback completion and saves progress only if the user has watched at least 15% of the video. This plugin allows you to override these settings for each library individually.

## Features

- Set custom "mark as played after" percentage (1-99%) per library
- Set custom "minimum progress to save" percentage (1-98%) per library  
- Default settings that apply to libraries without specific overrides
- Debug logging to help troubleshoot playback behavior
- Works with video libraries only (other media types use Jellyfin's default behavior)

## Installation

### Prerequisites
- Jellyfin Server 12.4 or later
- .NET 10.0 runtime

### Steps
1. Download the latest release from the [GitHub releases page](https://github.com/your-repo/per-library-playback-thresholds/releases)
2. Copy the plugin files to your Jellyfin plugins directory:
   - On Linux: `/var/lib/jellyfin/plugins/`
   - On Windows: `C:\ProgramData\Jellyfin\Server\plugins\`
3. Restart Jellyfin server
4. Navigate to Dashboard → Plugins → Per-Library Playback Thresholds
5. Configure settings for your libraries

## Usage

### Configuration Options

1. **Defaults Section**
   - **Minimum progress to save (%)**: The minimum percentage of a video that must be watched before progress is saved (default: 15%)
   - **Mark as played after (%)**: The percentage at which a video is marked as played (default: 90%)

2. **Libraries Section**  
   - Each library can have its own custom settings
   - Enable "Override" to customize settings for that specific library
   - Set custom minimum and mark-as-played percentages

### Example Settings

```
Default Settings:
- Minimum progress to save: 15%
- Mark as played after: 90%

Library A (Custom):
- Minimum progress to save: 20%
- Mark as played after: 85%

Library B (Custom):
- Minimum progress to save: 10%
- Mark as played after: 95%
```

## Debug Logging

To enable debug logging:
1. In the plugin configuration page, check "Enable Debug Logging"
2. Restart Jellyfin server
3. Check the Jellyfin logs for detailed playback diagnostics
4. Debug information includes:
   - Playback start/progress/stop events
   - Library resolution details
   - Threshold decisions made by the plugin
   - User data saved vs. what Jellyfin has stored

## Troubleshooting

### Common Issues

1. **Settings Not Saving**
   - Ensure Jellyfin has write permissions to the configuration directory
   - Check that no other plugin is conflicting with IUserDataManager registration

2. **Library Settings Not Applying**
   - Verify that libraries are properly configured in Jellyfin
   - Ensure items belong to only one library (multi-library items use Jellyfin's default behavior)

3. **Performance Issues**
   - If experiencing performance problems, disable debug logging
   - Check the plugin cache size limitations

## Compatibility

This plugin is compatible with Jellyfin Server version 10.8 and later.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License.
