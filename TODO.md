# Thesis Evaluation TODO

Use this as the active raw-material checklist for the thesis. Raw evidence goes in `thesis_eval_raw/06_11/`. Thesis-ready, passed material goes in `complete_material/`.

## Current Status

You deleted the old `06_10` raw folder, so the final material set should be treated as a clean `06_11` collection.

Already accepted into `complete_material/`:

| Status | Material | Accepted Location | Why It Matters |
| --- | --- | --- | --- |
| Done | Current setup snapshot | `complete_material/setup/mac_setup_snapshot.txt` | Fills the tested-environment table and ties results to the exact machine/project state. |
| Done | Backend status after bringup | `complete_material/setup/backend_status_after_bringup.txt` | Documents that the backend lifecycle was up during the setup snapshot. |
| Done | Current-code Mac dynamic backend performance | `complete_material/runtime_performance/` | Supports the headless/noVNC/headed performance comparison with RTF and CPU plots. |
| Done | Task profile/SDF/Unity JSON consistency | `complete_material/scripted_backend_checks/06_11_offline_validation/` | Confirms the generated task objects agree across task YAML, Gazebo SDF, and Unity `active_task.json`. |
| Done | Unity saved-scene smoke check | `complete_material/scripted_backend_checks/06_11_offline_validation/` | Confirms the saved dual-arm Unity scene contains the expected generated task/sync names. |
| Done | Mac platform bringup snapshot | `complete_material/portability/mac_bringup_snapshot/` | Gives a macOS host/tooling report for the portability section. |

Still missing for thesis-safe evaluation:

- Headset backend latency trace.
- MR visual sync/alignment trace.
- Recording-on FPS/sync-latency trace.
- Cable insertion precision trials.
- Pick/place task trials.
- Short demo videos and screenshots.
- Linux performance/portability evidence.
- Windows/WSL portability evidence.

## Current Scripted Tests Runnable On This Mac

The backend container was not running when this checklist was updated, so only offline checks were run automatically. Start the backend before running live ROS/Gazebo tests.

### Already Run Today Without Backend

These passed and were copied to `complete_material/`:

```bash
cd /Users/noahli/ros_unity_project/ros_backend1.1
python3 scripts/test_tools/eval_scripts/09_task_profile_sdf_unity_consistency_test.py --output-root ../thesis_eval_raw/06_11/scripted_backend_checks
python3 scripts/test_tools/eval_scripts/10_unity_editor_sync_smoke.py --output-root ../thesis_eval_raw/06_11/scripted_backend_checks
python3 scripts/test_tools/eval_scripts/12_cross_platform_backend_bringup_check.py --output-root ../thesis_eval_raw/06_11/portability/mac_bringup_snapshot
```

### Runnable On Mac After Backend Is Up, No Headset Needed

Run these when you are not actively doing headset trials. Some synthetic tests take over the receiver or move the robots.

```bash
cd /Users/noahli/ros_unity_project/ros_backend1.1
./scripts/backend11_lifecycle.sh bringup_dual
```

Topic-rate audit:

```bash
python3 scripts/test_tools/eval_scripts/03_ros_topic_rate_audit.py --duration 20 --output-root ../thesis_eval_raw/06_11/latency_rates/topic_rate_audit
```

Gripper timing/symmetry:

```bash
python3 scripts/test_tools/eval_scripts/07_gripper_timing_symmetry_test.py --cycles 4 --hold 1.5 --output-root ../thesis_eval_raw/06_11/sync_precision/gripper_timing
```

Reset reliability:

```bash
python3 scripts/test_tools/eval_scripts/08_reset_reliability_test.py --disturb --settle 4 --output-root ../thesis_eval_raw/06_11/sync_precision/reset_reliability
```

Haptic topic-logic monitor:

```bash
python3 scripts/test_tools/eval_scripts/11_haptic_publisher_topic_logic_test.py --duration 20 --output-root ../thesis_eval_raw/06_11/latency_rates/haptic_logic
```

Synthetic receiver/mapping/Servo tests. These can disturb live Quest control, so restart the receiver afterward:

```bash
python3 scripts/test_tools/eval_scripts/04_synthetic_hand_receiver_test.py --arm both --pattern line_x --duration 15 --output-root ../thesis_eval_raw/06_11/latency_rates/synthetic_receiver
./scripts/backend11_lifecycle.sh start_receiver

python3 scripts/test_tools/eval_scripts/05_servo_step_sine_response_test.py --arm both --pattern circle_xy --duration 12 --output-root ../thesis_eval_raw/06_11/latency_rates/servo_response
./scripts/backend11_lifecycle.sh start_receiver

python3 scripts/test_tools/eval_scripts/06_mapping_axis_sign_sanity_test.py --output-root ../thesis_eval_raw/06_11/latency_rates/mapping_sanity
./scripts/backend11_lifecycle.sh start_receiver
```

After each clean run, copy the useful output folder into the matching `complete_material/` subfolder and add one sentence to `complete_material/README.md`.

## Today On Mac With Quest Headset

Most human/headset material can be collected today on the Mac. The lab machine is only needed if you want stronger hardware comparison or a second latency confirmation.

### 1. Rebuild/Deploy The Quest App

Purpose: the newest logging requires the Quest build to include:

- `MREvaluationTracePublisher`
- `RecordingPerformanceTraceLogger`

If the app was built before these scripts existed, the ROS trace topics below will be missing.

### 2. Headset Smoke Test

Save notes to `thesis_eval_raw/06_11/notes.md`.

Check:

- Wired mode works.
- Left grip moves left arm only.
- Right grip moves right arm only.
- Triggers toggle grippers.
- Object sync works.
- Reset buttons do the right actions.
- Recording button visibly changes state.
- Cable insertion objects are visible before and after backend sync.

### 3. Headset Backend Latency Trace

Purpose: numeric backend pipeline latency from live Quest input to backend control topics.

```bash
cd /Users/noahli/ros_unity_project/ros_backend1.1
python3 scripts/test_tools/eval_scripts/14_headset_backend_latency_trace.py --duration 30 --output-root ../thesis_eval_raw/06_11/latency_rates/headset_backend_latency
```

While it runs:

- Hold grip to engage one arm.
- Keep still for 2 seconds.
- Make 5 short sharp controller movements.
- Repeat for the other arm.

Primary files:

- `summary.json`
- `topic_rates.csv`
- `hop_delays_ms.csv`
- `motion_events.csv`

### 4. MR Sync And Visual Alignment Trace

Purpose: numeric Gazebo-authoritative pose versus Unity/MR visual pose error, plus sync onset latency.

```bash
cd /Users/noahli/ros_unity_project/ros_backend1.1
python3 scripts/test_tools/eval_scripts/15_mr_sync_visual_latency_trace.py --duration 30 --object-id Sync_RedCube --output-root ../thesis_eval_raw/06_11/sync_precision/mr_sync_trace
```

While it runs:

- Keep the red cube and grippers visible in MR.
- Move one arm briefly.
- If possible, lightly grasp or nudge the red cube.

Primary files:

- `pose_alignment_samples.csv`
- `pose_alignment_summary.csv`
- `motion_events.csv`
- `pose_samples.csv`
- `topic_rates.csv`

This makes the old manual MR alignment row mostly optional. Keep screenshots as visual evidence, but use this trace as the numeric evidence when available.

### 5. Recording FPS And Sync-Latency Trace

Purpose: directly tests the lag/FPS drop when camera recording starts.

Run this before pressing record in the headset:

```bash
cd /Users/noahli/ros_unity_project/ros_backend1.1
python3 scripts/test_tools/eval_scripts/16_recording_fps_sync_latency_trace.py --max-wait-sec 120 --max-duration-sec 240 --post-recording-tail-sec 15 --output-root ../thesis_eval_raw/06_11/latency_rates/recording_fps_sync_trace
```

Procedure:

- Start the script.
- In the Quest app, start camera recording.
- Move one or both arms for 15-30 seconds.
- Stop recording.
- Keep the app running until the script exits 15 seconds later.

Primary files:

- `recording_fps_samples.csv`
- `recording_events.csv`
- `pose_alignment_summary.csv`
- `motion_events.csv`
- `topic_rates.csv`

Unity also writes a local Quest-side CSV under:

```text
Application.persistentDataPath/RecordingPerformanceLogs/
```

Retrieve with `adb pull` if needed. The ROS-side script output is usually enough for thesis plots.

### 6. Cable Insertion Precision Trials

Save rows to `thesis_eval_raw/06_11/cable_insertion/cable_insertion_trials.csv`.

Minimum thesis-safe set:

- Fixed receiver box first.
- Clearances: 2.0, 1.0, and 0.5 mm per side if time is short.
- Full set if time allows: 2.0, 1.5, 1.0, 0.5, 0.2 mm per side.
- 5 attempts per clearance if possible.
- Record success, completion time, drops, resets, arm used, and notes.

Why: this is the main precision/manipulation test for the thesis.

### 7. Pick/Place Trials

Save rows to `thesis_eval_raw/06_11/pick_place_control_modes/pick_place_trials.csv`.

Minimum:

- 10 MR hand-pose teleop trials.
- Optional: 5 keyboard and 5 gamepad/thumbstick baseline trials if time allows.

Why: gives a simpler manipulation baseline to compare with cable insertion.

### 8. Screenshots And Short Videos

Save screenshots to `thesis_eval_raw/06_11/screenshots/`.

Save videos to `thesis_eval_raw/06_11/videos/`.

Capture:

- Unity Editor hierarchy showing `GazeboWorkspace/TaskGroups/TaskGroup_Main/GameObjects_sync`.
- MR view of dual-arm workspace and task platform.
- Control panel Controls page.
- Control panel Camera page.
- One gripper-object alignment screenshot in MR.
- Gazebo/noVNC headed view if you need a simulation figure.
- 5-10 second clips: workspace drag/rotate, left arm teleop, right arm teleop, dual-arm movement, pick/place, cable insertion, camera recording state.

## Remote Ubuntu/Linux Tests

Use these to support portability and performance comparison. Present them as a platform case study unless the Linux hardware matches the Mac.

### 1. Linux Setup Snapshot

On Linux:

```bash
cd ~/ros_unity_project/ros_backend1.1
cp .env.example .env
./scripts/backend11_lifecycle.sh safe_down || true
./scripts/backend11_lifecycle.sh up_container_build
./scripts/backend11_lifecycle.sh build_ws
python3 scripts/test_tools/eval_scripts/12_cross_platform_backend_bringup_check.py --output-root ../thesis_eval_raw/Ubuntu_data/setup/linux_bringup_snapshot
```

Copy or save output into the Git-synced Ubuntu data drop folder:

```text
thesis_eval_raw/Ubuntu_data/setup/linux_bringup_snapshot/
```

After reviewing it on the Mac, copy the accepted subset into:

```text
complete_material/portability/linux_bringup_snapshot/
```

### 2. Linux Dynamic Performance

```bash
cd ~/ros_unity_project/ros_backend1.1
DURATION=60 WARMUP=8 ./scripts/test_tools/performance_test_scripts/run_dynamic_backend_performance_linux.sh
./scripts/backend11_lifecycle.sh safe_down
```

Copy or save results into the Git-synced Ubuntu data drop folder:

```text
thesis_eval_raw/Ubuntu_data/runtime_performance/linux_performance/
```

After reviewing it on the Mac, copy the accepted subset into:

```text
complete_material/portability/linux_performance/
```

Why: compares Mac Docker CPU-only performance against a Linux environment, especially if the lab machine has RTX/GPU support.

### 3. Linux Topic Rates

```bash
cd ~/ros_unity_project/ros_backend1.1
./scripts/backend11_lifecycle.sh bringup_dual
python3 scripts/test_tools/eval_scripts/03_ros_topic_rate_audit.py --duration 20 --output-root ../thesis_eval_raw/Ubuntu_data/latency_rates/linux_topic_rates
./scripts/backend11_lifecycle.sh safe_down
```

## Windows/WSL Tests

Do this later if a Windows machine is available. This is a portability case study, not a fair performance comparison unless the hardware is controlled.

Expected setup:

- Windows 11.
- WSL2 Ubuntu.
- Docker Desktop with WSL integration enabled.
- Repo cloned inside the WSL filesystem, not under `/mnt/c`, for better I/O.

Minimum evidence:

```bash
cd ~/ros_unity_project/ros_backend1.1
cp .env.example .env
./scripts/backend11_lifecycle.sh up_container_build
./scripts/backend11_lifecycle.sh build_ws
python3 scripts/test_tools/eval_scripts/12_cross_platform_backend_bringup_check.py --output-root ../thesis_eval_raw/06_11/portability/windows_wsl_bringup_snapshot
./scripts/backend11_lifecycle.sh safe_down
```

Optional if time allows:

```bash
./scripts/backend11_lifecycle.sh bringup_dual
python3 scripts/test_tools/eval_scripts/03_ros_topic_rate_audit.py --duration 20 --output-root ../thesis_eval_raw/06_11/latency_rates/windows_wsl_topic_rates
./scripts/backend11_lifecycle.sh safe_down
```

Save final copied evidence to:

```text
complete_material/portability/windows_wsl_bringup_snapshot/
```

## After Each Test Block

1. Leave raw output in `thesis_eval_raw/06_11/`.
2. Copy only clean/passed material into `complete_material/`.
3. Add a short note to `complete_material/README.md` saying what was accepted.
4. If a test failed, keep the raw logs but do not use it as headline thesis evidence.

## Minimum Dataset If Time Gets Tight

Collect only this:

- Current Mac setup snapshot and current-code performance results. Done.
- Linux setup snapshot and Linux dynamic performance.
- One headset backend latency trace.
- One MR sync/alignment trace.
- One recording FPS/sync trace.
- 10 pick/place trials.
- Cable insertion trials for 2.0, 1.0, and 0.5 mm clearances.
- Screenshots of MR workspace, control panel, Unity hierarchy, Gazebo scene, and one grasp alignment.
- Short demo clips for workspace placement, single-arm pick/place, dual-arm movement, cable insertion, and camera recording.
