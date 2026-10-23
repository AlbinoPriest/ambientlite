# Performance notes

AmbientLite began as a replacement for the supplied SyncLight app on a Windows 11 desktop with an i7-13700K, 32 GB RAM, and RTX 4080.

These are short local observations, not laboratory benchmarks. Scenes, app activity, sample duration, interface version, and capture settings varied. CPU percentages are normalized across all 24 logical processors. Working set includes shared resident pages and differs from private memory.

| App / sample | CPU | Working set | Context |
|---|---:|---:|---|
| SyncLight, all processes | 2.14% | 471.9 MB | 15-second background sample |
| Original WinForms AmbientLite | 0.55% | 116.7 MB | 15 seconds, syncing with settings open |
| Original AmbientLite, ambient-light test video | 1.13% | 92.6–132.2 MB | About 34 seconds; private memory 74.0–74.1 MB |
| AmbientLite WPF interface | 1.01% | 138.4 MB | 15 seconds, syncing with redesigned window open; 113.4 MB private memory |

The current release adds a colour wheel and tray flyout to that WPF interface. It has not received a controlled same-video comparison against SyncLight. The table does not establish a guaranteed reduction for every computer or workload.

An earlier console-only capture-and-USB diagnostic measured 0.54% CPU and 52.1 MB working set while sending 355 frames in 20 seconds. That excludes the GUI and is not a direct full-app comparison.

## Keeping the workload small

- One native process, without a browser runtime.
- A reused 256 × 128 capture surface.
- Adjustable capture target, default 20 fps.
- Serialized USB writes and suppression of duplicate screen-colour frames.
- Static mode releases capture and waits for setting changes.
- Tray controls are created on demand, with no polling loop or continuous animation.

A 20 fps target typically produced about 16–18 captures per second on the tested setup. Windows scheduling and capture cost affect the actual rate.

## Comparing fairly

Use the same video, display configuration, window/fullscreen mode, brightness, and approximate lighting update rate. Run one controller app at a time. Let startup activity settle, then sample all of each app's processes for equal durations. Record CPU, working set, private memory, and achieved frame rate separately.

A short test cannot establish long-run memory stability, end-to-end latency, or every game's capture compatibility.
