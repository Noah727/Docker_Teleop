# Linux RTF Isolation Matrix

Run date: 2026-06-12

This run isolates where the low Linux Gazebo real-time factor appears in the dual-arm backend stack. All conditions used the same container session and headless Gazebo/no-desktop mode. Each metric comes from `01_backend_rtf_cpu_monitor.py` with a 1-second RTF window.

## Summary

| Condition | Stack | RTF mean | CPU mean |
| --- | --- | ---: | ---: |
| `sim_only_headless` | Dual-arm Gazebo sim only | 0.275 | 160.8% |
| `sim_plus_servo` | Gazebo plus dual MoveIt Servo | 0.255 | 176.8% |
| `full_idle` | Full backend stack, no synthetic hand motion | 0.194 | 562.4% |
| `full_dynamic` | Full backend stack plus synthetic dual-hand motion | 0.196 | 548.8% |

The main drop is from `sim_plus_servo` to `full_idle`, not from the synthetic hand generator. Gazebo-only is already low on this Linux host, and the full always-on backend services add the biggest CPU increase.

## Raw Data

- `sim_only_headless/20260612_114911_backend_rtf_cpu/summary.json`
- `sim_plus_servo/20260612_115037_backend_rtf_cpu/summary.json`
- `full_idle/20260612_115216_backend_rtf_cpu/summary.json`
- `full_dynamic/20260612_115339_backend_rtf_cpu/summary.json`

The compact table is in `summary.csv`.
