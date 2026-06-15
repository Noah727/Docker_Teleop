# Linux Agent RTF Isolation Handoff

This note defines the cross-platform RTF isolation test. It was first run on the Linux workstation on 2026-06-12 to explain why Ubuntu RTF was much lower than the Mac result.

## Linux Result

Linux output folder:

```text
thesis_eval_raw/ubuntu_data/isolation_matrix/20260612_114727_linux_rtf_isolation
```

Linux summary:

| Condition | Stack | RTF mean | CPU mean |
| --- | --- | ---: | ---: |
| `sim_only_headless` | Dual-arm Gazebo sim only | 0.275 | 160.8% |
| `sim_plus_servo` | Gazebo plus dual MoveIt Servo | 0.255 | 176.8% |
| `full_idle` | Full backend stack, no synthetic hand motion | 0.194 | 562.4% |
| `full_dynamic` | Full backend stack plus synthetic dual-hand motion | 0.196 | 548.8% |

Interpretation from the Linux run:

- Gazebo-only is already low on this Linux host.
- Dual MoveIt Servo adds modest overhead.
- The large drop happens when the full always-on backend services start.
- Synthetic dual-hand motion does not materially worsen RTF beyond full idle in this run.

The Linux run used the local x86_64 Dockerfile fix on the workstation. That Dockerfile change is intentionally not pushed because the main development machine is the Mac.

## Metric To Compare

Compare `rtf_window.mean` from each condition's `summary.json`.

Also report:

- `rtf_window.min`
- `rtf_window.max`
- `cpu_percent.mean`
- `cpu_percent.max`
- host CPU/GPU/RAM
- Docker version
- current commit

Use `rtf_instant` only for debugging. The 1-second `rtf_window` metric is less noisy and is the number used in the table above.

## Mac Agent Task

Run the same matrix on the Mac from a clean checkout of `main`. Do not apply or push the Linux x86_64 Dockerfile change unless the user explicitly asks for it.

Suggested output folder:

```text
thesis_eval_raw/mac_data/isolation_matrix/YYYYMMDD_HHMMSS_mac_rtf_isolation
```

Use absolute paths for `--output-root`.

## Matrix Definition

All conditions use:

```text
duration: 45 seconds
interval: 1 second
rtf window: 1 second
desktop: disabled
noVNC: disabled
Gazebo: headless
```

Condition definitions:

1. `sim_only_headless`
   - Start only the container and dual-arm headless Gazebo.
   - Do not start Servo, receiver, mappers, Part 4, or synthetic hands.

2. `sim_plus_servo`
   - Add dual MoveIt Servo on top of the same Gazebo run.

3. `full_idle`
   - Add receiver, dual Part 2/3 mapper/control nodes, and Part 4 sync/haptics/task services.
   - Do not run synthetic hands.

4. `full_dynamic`
   - Start the synthetic dual-hand generator with:

```text
arm=both
pattern=line_y
duration_sec=52
period_sec=8
amplitude_x=0.0
amplitude_y=0.55
amplitude_z=0.0
```

## Commands

From repo root:

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh safe_down
ENABLE_DESKTOP=0 ENABLE_NOVNC=0 SIM_HEADLESS=1 ./scripts/backend11_lifecycle.sh up_container
SIM_HEADLESS=1 ./scripts/backend11_lifecycle.sh start_dual_sim
sleep 10
python3 scripts/test_tools/eval_scripts/01_backend_rtf_cpu_monitor.py \
  --duration 45 \
  --interval 1 \
  --rtf-window-sec 1.0 \
  --output-root /ABS/PATH/TO/thesis_eval_raw/mac_data/isolation_matrix/YYYYMMDD_HHMMSS_mac_rtf_isolation/sim_only_headless

./scripts/backend11_lifecycle.sh start_dual_servo
sleep 10
python3 scripts/test_tools/eval_scripts/01_backend_rtf_cpu_monitor.py \
  --duration 45 \
  --interval 1 \
  --rtf-window-sec 1.0 \
  --output-root /ABS/PATH/TO/thesis_eval_raw/mac_data/isolation_matrix/YYYYMMDD_HHMMSS_mac_rtf_isolation/sim_plus_servo

./scripts/backend11_lifecycle.sh start_receiver
./scripts/backend11_lifecycle.sh start_dual_part23
./scripts/backend11_lifecycle.sh start_dual_part4
sleep 10
python3 scripts/test_tools/eval_scripts/01_backend_rtf_cpu_monitor.py \
  --duration 45 \
  --interval 1 \
  --rtf-window-sec 1.0 \
  --output-root /ABS/PATH/TO/thesis_eval_raw/mac_data/isolation_matrix/YYYYMMDD_HHMMSS_mac_rtf_isolation/full_idle

DEBUG_ARM=both \
DEBUG_PATTERN=line_y \
DEBUG_DURATION_SEC=52 \
DEBUG_PERIOD_SEC=8 \
DEBUG_AMPLITUDE_X=0.0 \
DEBUG_AMPLITUDE_Y=0.55 \
DEBUG_AMPLITUDE_Z=0.0 \
./scripts/backend11_lifecycle.sh debug_hand_start
sleep 5
python3 scripts/test_tools/eval_scripts/01_backend_rtf_cpu_monitor.py \
  --duration 45 \
  --interval 1 \
  --rtf-window-sec 1.0 \
  --output-root /ABS/PATH/TO/thesis_eval_raw/mac_data/isolation_matrix/YYYYMMDD_HHMMSS_mac_rtf_isolation/full_dynamic

./scripts/backend11_lifecycle.sh debug_hand_stop
./scripts/backend11_lifecycle.sh safe_down
```

## Report Format

Create a summary table with this shape:

```text
condition,rtf_window_mean,rtf_window_min,rtf_window_max,cpu_mean_percent,cpu_max_percent,raw_summary
```

Then answer:

- Is Mac Gazebo-only already much faster than Linux?
- Does Servo cause a large drop on Mac?
- Does the full idle stack cause the same large drop seen on Linux?
- Does synthetic hand motion change RTF meaningfully beyond full idle?

The answer to those four questions will identify whether the platform gap is mostly Gazebo physics, backend service load, Docker virtualization, or a non-comparable test setup.
