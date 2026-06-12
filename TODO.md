# Thesis Evaluation TODO

Use this as the active raw-material checklist for the thesis. Raw evidence goes in `thesis_eval_raw/06_11/` unless an OS-specific folder is stated. Thesis-ready, passed material goes in `complete_material/`.

Last updated: 2026-06-12 after promoting the new MR sync trace, recording FPS/sync trace, and Ubuntu/Linux data.

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
| `[DONE - PROMOTED]` | Demo source video, topic clips, screenshots, captions draft | `complete_material/demo_video_06_11/` | Provides thesis figures, README media source, and demo evidence. Needs final short teaser selection and human caption review before publication. |
| `[DONE - CAVEAT]` | Headset backend latency trace | `complete_material/latency_rates/headset_backend_latency_06_11/` | Numeric live Quest input to ROS backend command latency. Caveat: ROS/container arrival-time latency after receiver publication, not optical end-to-end display latency. |
| `[DONE - CAVEAT]` | RedCube MR object-sync visual alignment | `complete_material/sync_precision/redcube_mr_sync_06_11/` | Usable object sync evidence. RedCube mean motion error is about 2-3 mm; static alignment is near zero. Old EE absolute alignment rows are not usable. |
| `[DONE - CAVEAT]` | RedCube MR object-sync rerun | `complete_material/sync_precision/redcube_mr_sync_06_12/` | Static RedCube Gazebo-to-Unity visual alignment is effectively numerical precision. This run had no moving RedCube samples, so use it as static sync evidence. |
| `[DONE - CAVEAT]` | Wrist-camera recording file verification | `complete_material/recording/recording_file_verification_06_11/` | Confirms wrist-camera recording occurred and estimates capture rate. Does not measure Unity FPS or sync latency. |
| `[DONE - CAVEAT]` | Recording-on FPS and sync-latency trace | `complete_material/latency_rates/recording_fps_sync_06_12/` and `complete_material/plots/06_12_evaluation/` | Provides Quest FPS during recording, RedCube moving/still visual sync error, and object Gazebo-to-Unity visual onset latency. EE event rows remain debug-only unless filtered. |
| `[DONE - CAVEAT]` | Mac no-headset backend scripted batch | `complete_material/latency_rates/no_headset_backend_06_11/` and raw review in `thesis_eval_raw/06_11/scripted_backend_checks/no_headset_backend_results_review.md` | Topic rates, haptic idle baseline, Servo response, mapping sanity, and synthetic backend support evidence. Synthetic receiver is debug-only, not the primary controller eval. |
| `[DONE - CAVEAT]` | Ubuntu/Linux setup, dynamic performance, topic rates, and RTF isolation | `complete_material/portability/linux_ubuntu_06_12/` and `complete_material/plots/06_12_evaluation/` | Usable as a portability/performance case study. Do not present as a strict OS benchmark because hardware and Docker stacks differ. |
| `[DONE - PROMOTED]` | 06-12 derived thesis tables/plots | `complete_material/plots/06_12_evaluation/` and `complete_material/evaluation_update_06_12.md` | Thesis-ready CSV/SVG summaries for recording FPS, RedCube sync, macOS-vs-Ubuntu RTF/CPU, and Ubuntu isolation. |

## Remaining Material Gaps

Priority order:

1. `[TODO]` Human-reviewed final captions and README teaser clips from the promoted demo media.
2. `[TODO]` Cable insertion precision trial rows.
3. `[TODO]` Pick/place trial rows and optional dual-arm handoff rows.
4. `[OPTIONAL]` Windows/WSL portability evidence.
5. `[OPTIONAL]` New EE visual alignment trace if the thesis needs quantitative EE visual alignment rather than object alignment.
6. `[OPTIONAL]` Extra screenshots only if the current UI changed after the promoted demo video.
7. `[OPTIONAL]` Repeat recording FPS/sync-latency trace if the final Quest build or recorder settings change.
8. `[OPTIONAL]` Repeat Linux/Ubuntu performance on the final lab machine/GPU configuration if the current remote workstation is not the intended deployment target.

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

### 6. Cable Insertion Precision Trials

Status: `[TODO]`

Save rows to:

```text
thesis_eval_raw/06_11/cable_insertion/cable_insertion_trials.csv
```

Minimum thesis-safe set:

- Fixed receiver box first.
- Clearances if time is short: 2.0, 1.0, and 0.5 mm per side.
- Full set if time allows: 2.0, 1.5, 1.0, 0.5, 0.2 mm per side.
- 5 attempts per clearance if possible.
- Record success, completion time, drops, resets, arm used, and notes.

Why: this is the main precision/manipulation test for the thesis.

### 7. Pick/Place And Dual-Arm Functional Trials

Status: `[TODO]`

Save pick/place rows to:

```text
thesis_eval_raw/06_11/pick_place_control_modes/pick_place_trials.csv
```

Minimum:

- 10 MR hand-pose teleop pick/place trials.
- Optional: 5 keyboard and 5 gamepad/thumbstick baseline trials if time allows.
- Optional: 5 dual-arm handoff attempts if you want a dedicated dual-arm functionality table.

Why: this gives a simpler manipulation baseline and supports the functionality-evaluation section.

### 8. Screenshots And Demo Videos

Status: `[DONE - PROMOTED for source material]`, `[TODO for final README teasers and human caption pass]`

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

## Remote Ubuntu/Linux Tests

Status: `[DONE - CAVEAT]`

Use these to support portability and performance comparison. Present them as a platform case study, not as a strict OS benchmark, because the Linux hardware/Docker stack is not normalized against the Mac.

Path casing note: the remote branch currently contains both `thesis_eval_raw/Ubuntu_data` placeholder paths and `thesis_eval_raw/ubuntu_data` result paths. On this Mac's case-insensitive filesystem they appear as one visible `Ubuntu_data` folder, while lowercase `ubuntu_data` also resolves in shell commands. Future cleanup should standardize this to one spelling before more cross-platform data is committed.

### 1. Linux Setup Snapshot

On Linux:

```bash
cd ~/ros_unity_project/ros_backend1.1
cp .env.example .env
./scripts/backend11_lifecycle.sh safe_down || true
./scripts/backend11_lifecycle.sh up_container_build
./scripts/backend11_lifecycle.sh build_ws
python3 scripts/test_tools/eval_scripts/12_cross_platform_backend_bringup_check.py --output-root ../thesis_eval_raw/ubuntu_data/setup/linux_bringup_snapshot
```

Save output into the Git-synced Ubuntu data drop folder:

```text
thesis_eval_raw/ubuntu_data/setup/linux_bringup_snapshot/
```

After reviewing it on the Mac, copy the accepted subset into:

```text
complete_material/portability/linux_ubuntu_06_12/setup/
```

### 2. Linux Dynamic Performance

On Linux:

```bash
cd ~/ros_unity_project/ros_backend1.1
DURATION=60 WARMUP=8 ./scripts/test_tools/performance_test_scripts/run_dynamic_backend_performance_linux.sh
./scripts/backend11_lifecycle.sh safe_down
```

Save results into:

```text
thesis_eval_raw/ubuntu_data/runtime_performance/linux_performance/
```

After reviewing it on the Mac, copy the accepted subset into:

```text
complete_material/portability/linux_ubuntu_06_12/runtime_performance/
```

Accepted result location:

```text
complete_material/portability/linux_ubuntu_06_12/runtime_performance/
complete_material/plots/06_12_evaluation/mac_linux_dynamic_performance_summary.csv
complete_material/plots/06_12_evaluation/mac_linux_rtf_bar.svg
complete_material/plots/06_12_evaluation/mac_linux_cpu_bar.svg
```

Why: compares Mac Docker performance against the remote Ubuntu workstation as a deployment case study. In the collected run, Ubuntu RTF was lower than the Mac run, which means the result should be interpreted as "this specific Ubuntu workstation/container setup" rather than "Linux is slower."

### 3. Linux Topic Rates

On Linux:

```bash
cd ~/ros_unity_project/ros_backend1.1
./scripts/backend11_lifecycle.sh bringup_dual
python3 scripts/test_tools/eval_scripts/03_ros_topic_rate_audit.py --duration 20 --output-root ../thesis_eval_raw/ubuntu_data/latency_rates/linux_topic_rates
./scripts/backend11_lifecycle.sh safe_down
```

Accepted result location:

```text
complete_material/portability/linux_ubuntu_06_12/topic_rates/
```

Linux topic-rate summary: controller input, joint velocity commands, object sync, and haptic topics were near `60 Hz`. Target-twist and Servo twist topics were much lower in wall-clock time because the Gazebo simulation RTF was low and those stages are tied to simulation-time behavior.

### 4. Linux RTF Isolation Matrix

Status: `[DONE - CAVEAT]`

Accepted material:

```text
complete_material/portability/linux_ubuntu_06_12/isolation_matrix/
complete_material/plots/06_12_evaluation/linux_rtf_isolation_summary.csv
complete_material/plots/06_12_evaluation/linux_isolation_rtf_bar.svg
complete_material/plots/06_12_evaluation/linux_isolation_cpu_bar.svg
```

Interpretation: Gazebo-only headless already ran below real time on the remote Ubuntu workstation (`~0.275` mean RTF). Adding Servo caused a smaller drop, while starting the full stack dropped mean RTF to about `0.194-0.196`. This suggests the main performance bottleneck in this setup is the combined full-stack Docker/Gazebo workload, not the synthetic hand-motion generator alone.

## Windows/WSL Tests

Status: `[OPTIONAL]`

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
python3 scripts/test_tools/eval_scripts/12_cross_platform_backend_bringup_check.py --output-root ../thesis_eval_raw/Windows_data/portability/windows_wsl_bringup_snapshot
./scripts/backend11_lifecycle.sh safe_down
```

Optional if time allows:

```bash
./scripts/backend11_lifecycle.sh bringup_dual
python3 scripts/test_tools/eval_scripts/03_ros_topic_rate_audit.py --duration 20 --output-root ../thesis_eval_raw/Windows_data/latency_rates/windows_wsl_topic_rates
./scripts/backend11_lifecycle.sh safe_down
```

After reviewing it on the Mac, copy accepted evidence to:

```text
complete_material/portability/windows_wsl_bringup_snapshot/
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
- `[DONE - PROMOTED]` Demo source video, topic clips, and screenshots.
- `[DONE - CAVEAT]` Camera recording file verification.

Still collect before thesis writing locks:

- `[DONE - CAVEAT]` Linux setup snapshot, dynamic performance, topic rates, and RTF isolation matrix.
- `[DONE - CAVEAT]` Recording FPS/sync trace with `/unity_eval/recording_state` and `/unity_eval/fps_sample`.
- `[TODO]` 10 pick/place trials.
- `[TODO]` Cable insertion trials for 2.0, 1.0, and 0.5 mm clearances.
- `[TODO]` Final 5-8 second README teaser clips and human-reviewed captions.
- `[OPTIONAL]` Windows/WSL bringup snapshot.
- `[OPTIONAL]` New EE visual alignment trace.
