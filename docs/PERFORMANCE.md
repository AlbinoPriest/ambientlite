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
- GPU downsampling to a reused 256 × 128 surface, with three nonblocking readback buffers.
- Adjustable capture target, default 20 fps.
- Serialized USB writes and suppression of duplicate screen-colour frames.
- Static mode releases capture and waits for setting changes.
- Tray controls are created on demand, with no polling loop or continuous animation.

A 20 fps target typically produced about 16–18 captures per second on the tested setup. Windows scheduling and capture cost affect the actual rate.

## Desktop smoothness

The original GDI path used `StretchBlt` to read and shrink the desktop on each sampling tick. Low average CPU usage did not mean that these calls were short: the desktop could visibly hitch while windows were dragged. Version 0.3.0 introduces DXGI Desktop Duplication and GPU downsampling as the default. CPU readback uses `D3D11_MAP_FLAG_DO_NOT_WAIT`; busy buffers are skipped instead of forcing the CPU and GPU to synchronize.

A local capture-only comparison on the tested 3440 × 1440 display, with a 20 fps target and separate ten-second samples, measured:

| Capture path | Median call | 95th percentile | Longest call | CPU across 24 logical processors |
|---|---:|---:|---:|---:|
| GDI compatibility | 19.158 ms | 23.210 ms | 27.972 ms | 0.51% |
| GPU capture | 0.137 ms | 0.211 ms | 0.872 ms | 0.01% |

These timings include the capture poll and edge averaging, exclude one-time initialization, and are **not display frame times or measured input latency**. The samples were sequential desktop observations, not a controlled identical animation. The GPU sample had 80 completed readbacks and 81 busy-buffer skips; it reused the last completed image on other lighting ticks. That is intentional: a busy GPU can delay LED colours by one or more sampling intervals instead of blocking capture.

The GPU path adds a device, desktop-sized GPU input texture, small output texture, and staging buffers. It trades some memory and possible lighting latency for shorter CPU capture calls. Its first initialization measured about 218 ms in this sample, versus 30 ms for GDI. These console-only memory/CPU observations should not be substituted for measurements of the full WPF app.

The fallback remains available as **Compatibility capture** in Strip setup, including for rotated displays. It is optional and off by default. The source uses only Windows graphics APIs and adds no third-party runtime or packages.

The implementation follows Microsoft's [Desktop Duplication guidance](https://learn.microsoft.com/en-us/windows/win32/direct3ddxgi/desktop-dup-api), [frame lifetime guidance](https://learn.microsoft.com/en-us/windows/win32/api/dxgi1_2/nf-dxgi1_2-idxgioutputduplication-releaseframe), and [nonblocking Map semantics](https://learn.microsoft.com/en-us/windows/win32/api/d3d11/ne-d3d11-d3d11_map_flag).

## Comparing fairly

Use the same video, display configuration, window/fullscreen mode, brightness, and approximate lighting update rate. Run one controller app at a time. Let startup activity settle, then sample all of each app's processes for equal durations. Record CPU, working set, private memory, and achieved frame rate separately.

A short test cannot establish long-run memory stability, end-to-end latency, or every game's capture compatibility.
