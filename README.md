Used ChatGPT to create this.

Effort in patience.... holy crap it is bad.

# Build Procedure
```sh
docker run --rm -v "$PWD:/src" -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet build -v minimal
```

# Install Procedure
```sh
cd /appdata/jellyfin-plugin-dev

mkdir -p /appdata/jellyfin-plugin-staging/PerLibraryPlaybackThresholds

cp bin/Debug/net10.0/PerLibraryPlaybackThresholds.dll \
  /appdata/jellyfin-plugin-staging/PerLibraryPlaybackThresholds/

chown -R 1000:1000 /appdata/jellyfin-plugin-staging/PerLibraryPlaybackThresholds/

mkdir -p /appdata/jellyfin/config/plugins/PerLibraryPlaybackThresholds

cp /appdata/jellyfin-plugin-staging/PerLibraryPlaybackThresholds/PerLibraryPlaybackThresholds.dll \
  /appdata/jellyfin/config/plugins/PerLibraryPlaybackThresholds/

chown -R 1000:1000 /appdata/jellyfin/config/plugins/PerLibraryPlaybackThresholds/

docker restart jellyfin
```

# Install onto main system
## Send to Shared
```sh
cp -rv /appdata/jellyfin-plugin-staging/PerLibraryPlaybackThresholds /mnt/youtube
```

## proxmox cp to main Jellyfin
```sh
cp -rv /rpool/dockerdata/jellyfin/config/plugins/PerLibraryPlaybackThresholds /tank/temp/
cp -v /tank/media/downloads/youtube/PerLibraryPlaybackThresholds/PerLibraryPlaybackThresholds.dll /rpool/dockerdata/jellyfin/config/plugins/PerLibraryPlaybackThresholds/
chown -Rv talung:talung /rpool/dockerdata/jellyfin/config/plugins/PerLibraryPlaybackThresholds
rm /rpool/dockerdata/jellyfin/config/plugins/PerLibraryPlaybackThresholds/meta.json
docker restart jellyfin
```

# Log Checker
```sh 
docker logs -f -n100 jellyfin | grep -E "PerLibraryPlaybackThresholds|Per-Library"
```