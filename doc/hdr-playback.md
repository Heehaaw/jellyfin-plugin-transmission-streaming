# HDR Playback

HDR Playback adds an **HDR conversion** option to Jellyfin's native player settings, beside Quality. It starts off for each movie or episode. Selecting it requests a normal
Jellyfin transcode and adds SDR-to-HDR inverse tone mapping to that same FFmpeg process. No alternate movie, source, generated library file, or second encoder is created.

This is synthesized HDR from SDR, not recovery of information missing from the source. Existing HDR sources stay on Jellyfin's normal playback path.

## Requirements and current scope

HDR Playback uses Jellyfin's existing encoding configuration. Conversion parameters are fixed; HDR selection is per playback.

- Jellyfin server and bundled web client 12.3, with jellyfin-ffmpeg 8.
- Linux with an AMD GPU, configured VA-API hardware encoding, HEVC encoding enabled, and working Vulkan/DRM interop plus libplacebo.
- A player profile explicitly advertising HEVC HDR10, and a user allowed to transcode video.
- Jellyfin must serve its bundled web assets. The menu extension recognizes the tested 12.3 asset structure; an unknown frontend leaves the menu untouched.

The webOS client uses the server web frontend. A C4 playback trial is still required before claiming that client's compatibility. Native clients with their own frontend do not
receive this menu automatically. The option reports an unavailable reason when the server, source, permissions, or player profile cannot support conversion.

## Build and review

```sh
dotnet run --file scripts/Package.cs -- --plugin hdr-playback
```

This produces `artifacts/HDRPlayback_0.0.0.0.zip`. It does not install or restart Jellyfin.

For a separate development server, stop it before installing:

```sh
dotnet run --file scripts/Install.cs -- --plugin hdr-playback --plugins-dir /path/to/development/plugins
```

Install either plugin independently or both. Existing Transmission configuration and persisted state retain their original identities. The new HDR plugin has its own GUID and
configuration identity. It has no Transmission dependency.

## Playback behavior

The player sends the selected mode on its native PlaybackInfo requests. The server validates source range, user permissions, client HDR support, and configured hardware before
forcing video transcoding. It retains native bitrate and resolution limits, selected source, audio and subtitle tracks, start position, cancellation, and transcode ownership.
Changing Quality keeps the current HDR selection. Changing items or stopping playback clears it. Seeking can restart native transcoding without losing HDR mode.

After native scaling and subtitle composition, the additional filters map VA-API frames through DRM into libplacebo, convert to BT.2020/PQ, then return 10-bit frames to the
same HEVC VA-API encoder. The output URL requests Main10 and the HLS master playlist advertises PQ. The session key includes the authenticated user, device, and native play-session
ID so simultaneous players do not share selection or reuse each other's output.

Any input resolved by Jellyfin can reach this path, including Transmission's guarded HTTP stream. HDR Playback does not call Transmission RPC or open incomplete torrent files.
Native streaming continues to own range requests and missing-piece waits. Unsupported encoders or filter graph layouts fail rather than emitting SDR labelled as HDR.

## Verification

```sh
dotnet test JellyfinPlugins.slnx -c Release --filter 'FullyQualifiedName~.Tests.'
node --test tests/HdrPlayback/Player/player.test.cjs
dotnet test tests-integration/HdrPlayback/HdrPlayback.IntegrationTests -c Release
dotnet test tests-application/HdrPlayback/Jellyfin.Plugin.HdrPlayback.ApplicationTests -c Release
```

Unit and integration checks cover eligibility, native command preservation, session isolation, quality fields, playlist signalling, and both plugin registration orders.
Disposable Jellyfin/Chromium tests exercise the actual frontend, native playback, reverse-proxy base paths, and coexistence. CPU-only container checks reject unavailable GPU
conversion; they cannot establish HDR display quality. A bounded synthetic encode has exercised the conversion chain on the Ryzen iGPU.

Before deployment, test HDR on/off and the TV's HDR indication, quality changes, seek, subtitles, stop, and simultaneous SDR playback on the intended C4. Compare image quality
with native SDR. No plugin installation on the home Jellyfin server is part of the uncommitted implementation review.

## Release

Use `hdr-v1.2.3` for HDR Playback and `v1.2.3` for Transmission Streaming. Each tag promotes that plugin's artifact from successful main CI. The combined catalog retains both
plugin records and their prior versions. Publication remains subject to the [release workflow](release-guidelines.md).
