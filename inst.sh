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

