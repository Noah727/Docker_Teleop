# Project TODO: Thesis Evaluation vs GitHub Demo

Use this as the active checklist for two separate jobs:

- Thesis writing/evaluation: collect defensible raw data, plots, and trial CSVs. Raw evidence goes in `thesis_eval_raw/06_11/` unless an OS-specific folder is stated. Thesis-ready, passed material goes in `complete_material/`.
- GitHub repo demo construction: prepare short public-facing clips/screenshots/captions for the README demo section. Working media lives in ignored `demo_media/`; curated source copies live in ignored `complete_material/demo_video_06_11/`; final published links go into the root `README.md`.

Important split: the long Quest demo video collected on 2026-06-11 belongs primarily to GitHub demo construction. It can provide screenshots or illustrative figures for the thesis, but it is not a substitute for the time-boxed thesis trial rows.

Last updated: 2026-06-12 after promoting the new MR sync trace, recording FPS/sync trace, Ubuntu/Linux data, and Windows/WSL backend portability data.

## Status Legend

- `[DONE - PROMOTED]`: accepted into `complete_material/` and usable in the thesis.
- `[DONE - CAVEAT]`: usable, but the limitation must be stated when writing the thesis.
- `[TODO]`: still needs collection, rerun, or human review.
- `[OPTIONAL]`: useful if time allows, but not required for the minimum dataset.
- `[DO NOT USE AS PASS RESULT]`: keep as raw/debug history, but do not present as a successful thesis result.
- `[DEBUG ONLY]`: useful for development/debugging, but not a primary thesis evaluation result.

## Current Complete Material Snapshot

You deleted the old `06_10` raw folder, so the final material set is now treated as a clean `06_11` collection.

Start every writing pass from:

```text
complete_material/material_verification_06_11.md
complete_material/README.md
```

Accepted into `complete_material/`:

| Status | Material | Accepted Location | Why It Matters |
| --- | --- | --- | --- |
| `[DONE - PROMOTED]` | Current setup snapshot | `complete_material/setup/mac_setup_snapshot.txt` | Fills the tested-environment table and ties results to the exact Mac/project state. |
| `[DONE - PROMOTED]` | Backend status after bringup | `complete_material/setup/backend_status_after_bringup.txt` | Documents that the backend lifecycle was up during the setup snapshot. |
| `[DONE - PROMOTED]` | Current-code Mac dynamic backend performance | `complete_material/runtime_performance/` | Supports the headless/noVNC/headed performance comparison with RTF and CPU plots. |
| `[DONE - PROMOTED]` | Task profile/SDF/Unity JSON consistency | `complete_material/scripted_backend_checks/06_11_offline_validation/` | Confirms generated task objects agree across task YAML, Gazebo SDF, and Unity `active_task.json`. |
| `[DONE - PROMOTED]` | Unity saved-scene smoke check | `complete_material/scripted_backend_checks/06_11_offline_validation/` | Confirms the saved dual-arm Unity scene contains expected generated task/sync names. |
| `[DONE - PROMOTED]` | Mac platform bringup snapshot | `complete_material/portability/mac_bringup_snapshot/` | Gives a macOS host/tooling report for portability. |
| `[DONE - PROMOTED]` | Demo source video, topic clips, screenshots, captions draft | `complete_material/demo_video_06_11/` | GitHub README demo source material. Thesis may reuse screenshots as illustrative figures, but this is not trial-performance evidence. Needs final short teaser selection and human caption review before publication. |
| `[DONE - CAVEAT]` | Headset backend latency trace | `complete_material/latency_rates/headset_backend_latency_06_11/` | Numeric live Quest input to ROS backend command latency. Caveat: ROS/container arrival-time latency after receiver publication, not optical end-to-end display latency. |
| `[DONE - CAVEAT]` | RedCube MR object-sync visual alignment | `complete_material/sync_precision/redcube_mr_sync_06_11/` | Usable object sync evidence. RedCube mean motion error is about 2-3 mm; static alignment is near zero. Old EE absolute alignment rows are not usable. |
| `[DONE - CAVEAT]` | RedCube MR object-sync rerun | `complete_material/sync_precision/redcube_mr_sync_06_12/` | Static RedCube Gazebo-to-Unity visual alignment is effectively numerical precision. This run had no moving RedCube samples, so use it as static sync evidence. |
| `[DONE - CAVEAT]` | Wrist-camera recording file verification | `complete_material/recording/recording_file_verification_06_11/` | Confirms wrist-camera recording occurred and estimates capture rate. Does not measure Unity FPS or sync latency. |
| `[DONE - CAVEAT]` | Recording-on FPS and sync-latency trace | `complete_material/latency_rates/recording_fps_sync_06_12/` and `complete_material/plots/06_12_evaluation/` | Provides Quest FPS during recording, RedCube moving/still visual sync error, and object Gazebo-to-Unity visual onset latency. EE event rows remain debug-only unless filtered. |
| `[DONE - CAVEAT]` | Mac no-headset backend scripted batch | `complete_material/latency_rates/no_headset_backend_06_11/` and raw review in `thesis_eval_raw/06_11/scripted_backend_checks/no_headset_backend_results_review.md` | Topic rates, haptic idle baseline, Servo response, mapping sanity, and synthetic backend support evidence. Synthetic receiver is debug-only, not the primary controller eval. |
| `[DONE - CAVEAT]` | Ubuntu/Linux setup, dynamic performance, topic rates, and RTF isolation | `complete_material/portability/linux_ubuntu_06_12/` and `complete_material/plots/06_12_evaluation/` | Usable as a portability/performance case study. Do not present as a strict OS benchmark because hardware and Docker stacks differ. |
| `[DONE - CAVEAT]` | Windows/WSL backend portability and scripted checks | `complete_material/portability/windows_wsl_06_12/` and `complete_material/plots/06_12_evaluation/` | Usable as no-headset Windows/WSL backend portability evidence. ADB was not on PATH, so Quest wired transport and headset traffic were not validated. |
| `[DONE - PROMOTED]` | 06-12 derived thesis tables/plots | `complete_material/plots/06_12_evaluation/` and `complete_material/evaluation_update_06_12.md` | Thesis-ready CSV/SVG summaries for recording FPS, RedCube sync, macOS-vs-Ubuntu RTF/CPU, and Ubuntu isolation. |

## Remaining Thesis Material Gaps

Priority order:

1. `[TODO]` Time-boxed cable insertion throughput rows.
2. `[TODO]` Time-boxed pick/place, dual-arm simultaneous pick/place, and dual-arm handoff rows.
3. `[OPTIONAL]` New EE visual alignment trace if the thesis needs quantitative EE visual alignment rather than object alignment.
4. `[OPTIONAL]` Extra screenshots only if the current UI changed after the promoted demo video.
5. `[OPTIONAL]` Repeat recording FPS/sync-latency trace if the final Quest build or recorder settings change.
6. `[OPTIONAL]` Repeat Linux/Ubuntu performance on the final lab machine/GPU configuration if the current remote workstation is not the intended deployment target.
7. `[OPTIONAL]` Repeat Windows/WSL test with ADB installed if the thesis needs Windows wired-Quest evidence rather than backend-only portability.

## Remaining GitHub Demo / README Gaps

These are separate from the thesis trial dataset.

1. `[TODO]` Human-review the demo transcript/captions.
2. `[TODO]` Select 6-10 public-facing README teaser clips from the promoted demo media.
3. `[TODO]` Export final 5-8 second H.264 MP4 teasers.
4. `[TODO]` Upload teasers to GitHub Releases, YouTube, Drive, or lab storage.
5. `[TODO]` Replace the root `README.md` demo table `TODO` links with hosted URLs.
6. `[OPTIONAL]` Add a short GIF fallback only for one hero clip if GitHub rendering needs it.

Do not spend thesis time trying to turn the following old runs into pass results unless the underlying scripts are fixed first:

- `[DO NOT USE AS PASS RESULT]` Gripper timing/symmetry run: the script did not exercise the current coupled gripper path.
- `[DO NOT USE AS PASS RESULT]` Reset reliability stress run: it reported object-pose failures, even though the user-facing reset button worked during headset testing.
- `[DEBUG ONLY]` Synthetic receiver: useful as backend debug/supporting evidence, but real controller/headset, keyboard, or gamepad modes are better thesis-facing controller evaluations.
- `[DO NOT USE AS PASS RESULT]` Old EE absolute alignment rows from current MR sync traces: mixed reference point issue.
- `[DO NOT USE AS PASS RESULT]` Old recording FPS trace runs that ended `max_wait_elapsed_without_recording`: use the 2026-06-12 trace in `complete_material/latency_rates/recording_fps_sync_06_12/` instead.

## Output Folder Rule

Most eval scripts do not overwrite old runs. They create timestamped folders such as:

```text
20260611_213433_mr_sync_visual_latency_trace/
```

When rerunning, inspect the newest timestamped folder, then copy only the clean accepted output into `complete_material/` and add a note to `complete_material/README.md`.

## Current Scripted Tests Runnable On This Mac

### Already Run Without Backend

Status: `[DONE - PROMOTED]`

These passed and were copied to `complete_material/scripted_backend_checks/06_11_offline_validation/` and `complete_material/portability/mac_bringup_snapshot/`.

```bash
cd /Users/noahli/ros_unity_project/ros_backend1.1
python3 scripts/test_tools/eval_scripts/09_task_profile_sdf_unity_consistency_test.py --output-root ../thesis_eval_raw/06_11/scripted_backend_checks
python3 scripts/test_tools/eval_scripts/10_unity_editor_sync_smoke.py --output-root ../thesis_eval_raw/06_11/scripted_backend_checks
python3 scripts/test_tools/eval_scripts/12_cross_platform_backend_bringup_check.py --output-root ../thesis_eval_raw/06_11/portability/mac_bringup_snapshot
```

### Already Run After Backend Bringup, No Headset

Status: `[DONE - CAVEAT]`

The usable outputs were copied to `complete_material/latency_rates/no_headset_backend_06_11/`. Rerun only if backend code changes or if you specifically fix the gripper/reset test scripts.

Bringup command:

```bash
cd /Users/noahli/ros_unity_project/ros_backend1.1
./scripts/backend11_lifecycle.sh bringup_dual
```

Topic-rate audit:

```bash
python3 scripts/test_tools/eval_scripts/03_ros_topic_rate_audit.py --duration 20 --output-root ../thesis_eval_raw/06_11/latency_rates/topic_rate_audit
```

Haptic topic-logic monitor:

```bash
python3 scripts/test_tools/eval_scripts/11_haptic_publisher_topic_logic_test.py --duration 20 --output-root ../thesis_eval_raw/06_11/latency_rates/haptic_logic
```

Servo response support test:

```bash
python3 scripts/test_tools/eval_scripts/05_servo_step_sine_response_test.py --arm both --pattern circle_xy --duration 12 --output-root ../thesis_eval_raw/06_11/latency_rates/servo_response
./scripts/backend11_lifecycle.sh start_receiver
```

Mapping sanity support test:

```bash
python3 scripts/test_tools/eval_scripts/06_mapping_axis_sign_sanity_test.py --output-root ../thesis_eval_raw/06_11/latency_rates/mapping_sanity
./scripts/backend11_lifecycle.sh start_receiver
```

Keep these as raw/debug unless fixed:

```bash
python3 scripts/test_tools/eval_scripts/07_gripper_timing_symmetry_test.py --cycles 4 --hold 1.5 --output-root ../thesis_eval_raw/06_11/sync_precision/gripper_timing
python3 scripts/test_tools/eval_scripts/08_reset_reliability_test.py --disturb --settle 4 --output-root ../thesis_eval_raw/06_11/sync_precision/reset_reliability
```

## Headset / Quest Tests On Mac

Most human/headset material can be collected on the Mac. The lab computer is mainly needed for the cross-platform/performance comparison.

### 1. Rebuild/Deploy Quest App

Status: `[DONE for the 2026-06-12 recording trace]`, `[TODO only if repeating on a new Quest build]`

Purpose: the newest logging requires the Quest build to include:

- `MREvaluationTracePublisher`
- `RecordingPerformanceTraceLogger`

If the app was built before these scripts existed, the ROS trace topics below will be missing. The 2026-06-12 recording trace confirms the deployed app used for that run did publish the required FPS/recording-state data.

### 2. Headset Smoke Test

Status: `[PARTLY DONE informally, keep notes if repeated]`

Save notes to:

```text
thesis_eval_raw/06_11/notes.md
```

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

Status: `[DONE - CAVEAT]`

Accepted material:

```text
complete_material/latency_rates/headset_backend_latency_06_11/
```

Rerun only if the controller pipeline changes:

```bash
cd /Users/noahli/ros_unity_project/ros_backend1.1
python3 scripts/test_tools/eval_scripts/14_headset_backend_latency_trace.py --duration 30 --output-root ../thesis_eval_raw/06_11/latency_rates/headset_backend_latency
```

Procedure if rerun:

- Hold grip to engage one arm.
- Keep still for 2 seconds.
- Make 5 short sharp controller movements.
- Repeat for the other arm.

Primary files:

- `summary.json`
- `topic_rates.csv`
- `hop_delays_ms.csv`
- `motion_events.csv`

Thesis wording caveat: this measures ROS/container pipeline latency after receiver publication, not camera-observed end-to-end display latency.

### 4. MR Sync And Visual Alignment Trace

Status: `[DONE - CAVEAT for RedCube]`, `[OPTIONAL for EE visual alignment]`

Accepted RedCube object-sync material:

```text
complete_material/sync_precision/redcube_mr_sync_06_11/
```

RedCube sync is usable. The current old EE absolute alignment numbers are not usable because they compare mixed reference points. If EE visual alignment becomes important, rebuild/deploy the newest app and rerun after confirming expanded Unity visual trace topics.

Topic check before an EE rerun:

```bash
docker exec motion_planner_11 bash -lc 'source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash && ros2 topic list | grep -E "Sync_RedCube_visual|tool0_visual|wrist_3_visual|hande_end_visual"'
```

Rerun command:

```bash
cd /Users/noahli/ros_unity_project/ros_backend1.1
python3 scripts/test_tools/eval_scripts/15_mr_sync_visual_latency_trace.py --duration 30 --object-id Sync_RedCube --output-root ../thesis_eval_raw/06_11/sync_precision/mr_sync_trace
```

Procedure if rerun:

- Keep the red cube and grippers visible in MR.
- Move one arm briefly.
- If possible, lightly grasp or nudge the red cube.

Primary files:

- `pose_alignment_samples.csv`
- `pose_alignment_summary.csv`
- `motion_events.csv`
- `pose_samples.csv`
- `topic_rates.csv`

### 5. Recording FPS And Sync-Latency Trace

Status: `[DONE - CAVEAT]`

Accepted material:

```text
complete_material/latency_rates/recording_fps_sync_06_12/
complete_material/plots/06_12_evaluation/recording_fps_summary.csv
complete_material/plots/06_12_evaluation/recording_sync_latency_summary.csv
complete_material/plots/06_12_evaluation/recording_fps_timeseries.svg
complete_material/plots/06_12_evaluation/recording_redcube_error_timeseries_mm.svg
```

Key result: recording-on FPS averaged about `40.3 FPS` with a 5th percentile of about `28.8 FPS`. The clearer late RedCube manipulation sequence showed about `53.6 ms` mean Gazebo-to-Unity visual onset latency over 5 object events. Treat the raw EE event rows as debug-only until manually filtered because the event detector includes outliers.

Rerun this only if the Quest build, camera recording settings, or sync logger changes.

Confirm topics first:

```bash
docker exec motion_planner_11 bash -lc 'source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash && ros2 topic list | grep -E "recording_state|fps_sample"'
```

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

Why this matters: this is the direct evidence for whether camera recording reduces Quest-side FPS or changes visual synchronization behavior.

### 6. Cable Insertion Throughput Trials

Status: `[TODO]`

Use the new fixed-duration protocol:

```text
thesis_eval_raw/06_11/timeboxed_trial_protocol.md
```

Save rows to:

```text
thesis_eval_raw/06_11/cable_insertion/timeboxed_insertion_trials.csv
```

Minimum thesis-safe set:

- Fixed receiver box first.
- Run `60 s` per clearance.
- Full fixed-box set: 2.0, 1.5, 1.0, 0.5, 0.2 mm per side.
- Movable receiver box if time allows: run all five clearances, or at least 2.0, 1.0, and 0.5 mm per side.
- Count successful insertions, attempts, partial insertions, drops, resets, and Servo/collision stops.
- After a success, withdraw the cable and attempt again within the same `60 s` window.

Why: this is the main precision/manipulation test for the thesis. The one-minute design gives a clean throughput metric and avoids ambiguous failed-attempt durations.

### 7. Pick/Place And Dual-Arm Throughput Trials

Status: `[TODO]`

Save pick/place rows to:

```text
thesis_eval_raw/06_11/pick_place_control_modes/timeboxed_pick_place_trials.csv
```

Save dual-arm rows to:

```text
thesis_eval_raw/06_11/dual_arm_demo/timeboxed_dual_arm_trials.csv
```

Minimum:

- Single-arm pick/place: `3` one-minute MR hand-pose teleop runs.
- Optional single-arm baselines: `3` one-minute keyboard runs and `3` one-minute gamepad/thumbstick runs if time allows.
- Dual-arm simultaneous pick/place: `3` one-minute runs.
- Dual-arm air handoff: `3` one-minute runs if stable.
- Count successful task cycles, attempts, drops, resets, and Servo/collision stops.

Why: this gives a simpler manipulation baseline and supports the functionality-evaluation section. The dual-arm rows show whether two independent arms improve throughput or enable tasks that single-arm control cannot do cleanly.

### 8. GitHub Demo Construction

Status: `[DONE - PROMOTED for source material]`, `[TODO for final README teasers and human caption pass]`

This is GitHub README/demo work, not thesis trial collection. The clips can supply optional thesis figures, but they should not be counted as quantitative trial evidence.

Promoted source material:

```text
complete_material/demo_video_06_11/source/com.noahli.ROSUNITY-20260611-214332-1.mp4
complete_material/demo_video_06_11/clips_v2/
complete_material/demo_video_06_11/screenshots_v2/
complete_material/demo_video_06_11/transcripts/demo_full_small_en.cleaned.*
```

Still do:

- Human-review `complete_material/demo_video_06_11/transcripts/demo_full_small_en.cleaned.srt` and `.txt`.
- Select 6-10 short README clips, each about 5-8 seconds.
- Export final teaser clips as H.264 MP4 into:

```text
complete_material/demo_video_06_11/readme_teasers/
```

Recommended teaser topics:

- MR workspace placement/drag/rotation.
- Left arm teleop.
- Right arm teleop.
- Dual-arm handoff or coordinated movement.
- Pick/place grasp.
- Cable insertion.
- Camera/control panel recording.

Use the existing v2 clips as source; no need to re-record unless the UI changed significantly.

## Cross-Platform Ubuntu And Windows Tests

Use these to support portability and performance discussion. Present them as platform case studies, not strict operating-system benchmarks, because CPU, GPU, memory, Docker, virtualization, cooling, and driver stacks are not normalized.

### Matched Platform Checklist

| Test block | Ubuntu/Linux | Windows/WSL | Purpose |
| --- | --- | --- | --- |
| Hardware/context snapshot | `[TODO]` add CPU/GPU/RAM snapshot | `[TODO]` add Windows host + WSL hardware snapshot | Gives context for RTF/CPU results. |
| Backend bringup snapshot | `[DONE - CAVEAT]` | `[DONE - CAVEAT]` | Confirms Docker/backend can start. |
| Dynamic RTF/CPU performance | `[DONE - CAVEAT]` | `[TODO]` | Main cross-platform performance case-study result. |
| ROS topic-rate audit | `[DONE - CAVEAT]` | `[DONE - CAVEAT]` | Confirms core ROS streams publish and shows sim-time effects. |
| RTF isolation matrix | `[DONE - CAVEAT]` | `[OPTIONAL]` | Breaks down Gazebo-only, Servo, full-stack bottlenecks. |
| Quest/ADB wired validation | `[OPTIONAL]` | `[OPTIONAL]` | Only needed if the thesis claims wired Quest transport on those hosts. |

### Ubuntu/Linux Section

Status: `[DONE - CAVEAT]` for bringup, dynamic performance, topic rates, and RTF isolation. Hardware/context snapshot still needs CPU/GPU/RAM details if we want a stronger setup table.

Path casing note: the remote branch currently contains both `thesis_eval_raw/Ubuntu_data` placeholder paths and `thesis_eval_raw/ubuntu_data` result paths. On this Mac's case-insensitive filesystem they appear as one visible `Ubuntu_data` folder, while lowercase `ubuntu_data` also resolves in shell commands. Future cleanup should standardize this to one spelling before more cross-platform data is committed.

#### Ubuntu Hardware / Context Snapshot

Status: `[TODO]`

Run on Ubuntu/Linux:

```bash
cd ~/ros_unity_project/ros_backend1.1
mkdir -p ../thesis_eval_raw/ubuntu_data/setup
{
  date
  hostname
  uname -a
  lscpu
  free -h
  (command -v lspci >/dev/null && lspci | grep -Ei 'vga|3d|display' || true)
  (command -v nvidia-smi >/dev/null && nvidia-smi || true)
  docker version
  docker info
} > ../thesis_eval_raw/ubuntu_data/setup/linux_hardware_snapshot.txt 2>&1
```

Use this mainly for CPU model/core count, RAM, GPU model, kernel, and Docker version.

#### Ubuntu Backend Bringup Snapshot

Status: `[DONE - CAVEAT]`

Command used:

```bash
cd ~/ros_unity_project/ros_backend1.1
cp .env.example .env
./scripts/backend11_lifecycle.sh safe_down || true
./scripts/backend11_lifecycle.sh up_container_build
./scripts/backend11_lifecycle.sh build_ws
python3 scripts/test_tools/eval_scripts/12_cross_platform_backend_bringup_check.py --output-root ../thesis_eval_raw/ubuntu_data/setup/linux_bringup_snapshot
```

Accepted location:

```text
complete_material/portability/linux_ubuntu_06_12/setup/
```

#### Ubuntu Dynamic RTF/CPU Performance

Status: `[DONE - CAVEAT]`

Command used:

```bash
cd ~/ros_unity_project/ros_backend1.1
DURATION=60 WARMUP=8 ./scripts/test_tools/performance_test_scripts/run_dynamic_backend_performance_linux.sh
./scripts/backend11_lifecycle.sh safe_down
```

Accepted locations:

```text
complete_material/portability/linux_ubuntu_06_12/runtime_performance/
complete_material/plots/06_12_evaluation/mac_linux_dynamic_performance_summary.csv
complete_material/plots/06_12_evaluation/mac_linux_rtf_bar.svg
complete_material/plots/06_12_evaluation/mac_linux_cpu_bar.svg
```

Interpretation: this compares Mac Docker performance against the remote Ubuntu workstation as a deployment case study. In the collected run, Ubuntu RTF was lower than the Mac run, so write it as "this specific Ubuntu workstation/container setup" rather than "Linux is slower."

#### Ubuntu Topic Rates

Status: `[DONE - CAVEAT]`

Command used:

```bash
cd ~/ros_unity_project/ros_backend1.1
./scripts/backend11_lifecycle.sh bringup_dual
python3 scripts/test_tools/eval_scripts/03_ros_topic_rate_audit.py --duration 20 --output-root ../thesis_eval_raw/ubuntu_data/latency_rates/linux_topic_rates
./scripts/backend11_lifecycle.sh safe_down
```

Accepted location:

```text
complete_material/portability/linux_ubuntu_06_12/topic_rates/
```

Summary: controller input, joint velocity commands, object sync, and haptic topics were near `60 Hz`. Target-twist and Servo twist topics were lower in wall-clock time because the Gazebo simulation RTF was low and those stages are tied to simulation-time behavior.

#### Ubuntu RTF Isolation Matrix

Status: `[DONE - CAVEAT]`

Accepted material:

```text
complete_material/portability/linux_ubuntu_06_12/isolation_matrix/
complete_material/plots/06_12_evaluation/linux_rtf_isolation_summary.csv
complete_material/plots/06_12_evaluation/linux_isolation_rtf_bar.svg
complete_material/plots/06_12_evaluation/linux_isolation_cpu_bar.svg
```

Interpretation: Gazebo-only headless already ran below real time on the remote Ubuntu workstation (`~0.275` mean RTF). Adding Servo caused a smaller drop, while starting the full stack dropped mean RTF to about `0.194-0.196`.

### Windows/WSL Section

Status: `[PARTIAL - CAVEAT]`. Bringup, topic rates, and no-headset backend checks are collected. To match Ubuntu, Windows still needs dynamic RTF/CPU performance and preferably a hardware/context snapshot.

Expected setup:

- Windows 11.
- WSL2 Ubuntu.
- Docker Desktop with WSL integration enabled.
- Repo cloned inside the WSL filesystem, not under `/mnt/c`, for better I/O.

#### Windows Host + WSL Hardware / Context Snapshot

Status: `[TODO]`

Run from PowerShell on the Windows host:

```powershell
cd PATH\TO\ros_unity_project
New-Item -ItemType Directory -Force thesis_eval_raw\Windows_data\setup | Out-Null
Get-Date | Out-File thesis_eval_raw\Windows_data\setup\windows_host_hardware_snapshot.txt
Get-CimInstance Win32_Processor | Format-List * | Out-File thesis_eval_raw\Windows_data\setup\windows_host_hardware_snapshot.txt -Append
Get-CimInstance Win32_VideoController | Format-List * | Out-File thesis_eval_raw\Windows_data\setup\windows_host_hardware_snapshot.txt -Append
Get-CimInstance Win32_ComputerSystem | Format-List * | Out-File thesis_eval_raw\Windows_data\setup\windows_host_hardware_snapshot.txt -Append
Get-CimInstance Win32_OperatingSystem | Format-List * | Out-File thesis_eval_raw\Windows_data\setup\windows_host_hardware_snapshot.txt -Append
```

Run from WSL:

```bash
cd ~/ros_unity_project/ros_backend1.1
mkdir -p ../thesis_eval_raw/Windows_data/setup
{
  date
  hostname
  uname -a
  lscpu
  free -h
  docker version
  docker info
} > ../thesis_eval_raw/Windows_data/setup/windows_wsl_hardware_snapshot.txt 2>&1
```

Use this mainly for CPU model/core count, RAM, GPU model, WSL kernel, Windows version, and Docker version.

#### Windows Backend Bringup Snapshot

Status: `[DONE - CAVEAT]`

Collected material:

```text
thesis_eval_raw/Windows_data/portability/windows_wsl_bringup_snapshot/
complete_material/portability/windows_wsl_06_12/portability/windows_wsl_bringup_snapshot/
complete_material/plots/06_12_evaluation/windows_wsl_platform_case_study_summary.csv
```

Caveat: `adb` was not on PATH, so this does not validate Quest wired USB reverse tunnels, headset app traffic, MR visual synchronization, or camera recording on Windows.

#### Windows Topic Rates

Status: `[DONE - CAVEAT]`

Collected material:

```text
thesis_eval_raw/Windows_data/latency_rates/windows_wsl_topic_rates/
complete_material/plots/06_12_evaluation/windows_wsl_topic_rate_groups.csv
complete_material/plots/06_12_evaluation/windows_wsl_topic_rate_groups.svg
```

Summary: receiver pose input, Gazebo velocity commands, Unity object sync, and haptic amplitude streams were near `60 Hz`. Target-twist and Servo delta-twist streams were about `7.8 Hz` in wall-clock time during this WSL run. The `/clock` topic rate is not the same as Gazebo real-time factor.

#### Windows Dynamic RTF/CPU Performance

Status: `[TODO]`

Run from PowerShell:

```powershell
cd PATH\TO\ros_unity_project
.\ros_backend1.1\scripts\test_tools\performance_test_scripts\run_dynamic_backend_performance_windows.ps1 -Duration 60 -Warmup 8
```

Expected output:

```text
thesis_eval_raw/Windows_data/runtime_performance/
```

After pulling/reviewing on the Mac, promote the accepted subset to:

```text
complete_material/portability/windows_wsl_06_12/runtime_performance/
complete_material/plots/06_12_evaluation/windows_wsl_dynamic_performance_summary.csv
```

#### Windows RTF Isolation Matrix

Status: `[OPTIONAL]`

Run if we want a fully matched Linux-style bottleneck breakdown:

- Gazebo-only headless.
- Gazebo plus Servo.
- Full backend idle.
- Full backend dynamic.

Save to:

```text
thesis_eval_raw/Windows_data/isolation_matrix/
complete_material/portability/windows_wsl_06_12/isolation_matrix/
```

## After Each Test Block

1. Leave raw output in `thesis_eval_raw/06_11/`, `thesis_eval_raw/ubuntu_data/`, or `thesis_eval_raw/Windows_data/`.
2. Copy only clean/passed material into `complete_material/`.
3. Add a short note to `complete_material/README.md` saying what was accepted.
4. If a test failed, keep the raw logs but do not use it as headline thesis evidence.
5. Update this file by changing `[TODO]` to `[DONE - PROMOTED]`, `[DONE - CAVEAT]`, or `[DO NOT USE AS PASS RESULT]` instead of deleting the task.

## Minimum Dataset If Time Gets Tight

Already finished:

- `[DONE - PROMOTED]` Current Mac setup snapshot.
- `[DONE - PROMOTED]` Current-code Mac performance results.
- `[DONE - CAVEAT]` One headset backend latency trace.
- `[DONE - CAVEAT]` RedCube MR object sync/alignment trace.
- `[DONE - CAVEAT]` Camera recording file verification.

Still collect before thesis writing locks:

- `[DONE - CAVEAT]` Linux setup snapshot, dynamic performance, topic rates, and RTF isolation matrix.
- `[DONE - CAVEAT]` Recording FPS/sync trace with `/unity_eval/recording_state` and `/unity_eval/fps_sample`.
- `[TODO]` Time-boxed pick/place throughput trials.
- `[TODO]` Time-boxed cable insertion throughput trials for fixed receiver all five clearances, plus movable receiver if time allows.
- `[TODO]` Time-boxed dual-arm simultaneous pick/place and air-handoff trials.
- `[OPTIONAL]` Repeat Windows/WSL with ADB installed if Windows wired-Quest transport evidence is needed.
- `[OPTIONAL]` New EE visual alignment trace.

Still build for GitHub repo presentation:

- `[DONE - PROMOTED for source material]` Demo source video, topic clips, and screenshots.
- `[TODO]` Final 5-8 second README teaser clips and human-reviewed captions.
