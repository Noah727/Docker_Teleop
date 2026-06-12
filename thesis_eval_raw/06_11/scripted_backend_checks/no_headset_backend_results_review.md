# No-Headset Backend Script Review - 2026-06-11

This review covers the `Runnable On Mac After Backend Is Up, No Headset Needed` script batch.

## Accepted / Thesis-Usable With Notes

### ROS Topic-Rate Audit

Folder:

```text
thesis_eval_raw/06_11/latency_rates/topic_rate_audit/20260611_202149_ros_topic_rate_audit/
```

Key observations:

| Topic group | Observed rate | Notes |
| --- | ---: | --- |
| `/left_arm/received_pose_states`, `/right_arm/received_pose_states` | ~60.0 Hz | Input stream target is met. |
| `/left_joint_group_velocity_controller/commands`, `/right_joint_group_velocity_controller/commands` | ~60.0 Hz | Joint velocity command stream target is met. |
| `/unity_sync/Sync_RedCube_pose`, `/unity_sync/Sync_CableRod_pose` | ~60.0 Hz | Object sync topics publish at target rate. |
| Haptic amplitude topics | ~60.0 Hz | Haptic status streams publish at target rate. |
| `/task_manager/status`, `/task_manager/active_task_manifest` | ~1.0 Hz | Expected slow status/manifest cadence. |
| `/joint_states` | ~76.9 Hz | One combined robot/simulation joint-state topic, not per arm. |
| `/left_arm/target_twist_states`, `/right_arm/target_twist_states` | ~39.1 Hz | Below 60 Hz in wall-clock time. Interpret together with Gazebo RTF/sim-time behavior. |
| `/left_arm/servo_node/delta_twist_cmds`, `/right_arm/servo_node/delta_twist_cmds` | ~39.1 Hz | Same caveat as target twist topics. |

Use this result in the latency/rate section, with the caveat that some sim-time-driven topics appear below 60 Hz in wall-clock measurement when Gazebo RTF is below 1.

### Haptic Topic Logic Idle Baseline

Folder:

```text
thesis_eval_raw/06_11/latency_rates/haptic_logic/20260611_202327_haptic_topic_logic/
```

Result:

- Left haptic topic: 1201 messages, max amplitude 0.0, no nonzero messages.
- Right haptic topic: 1200 messages, max amplitude 0.0, no nonzero messages.

Use this only as an idle/no-contact false-positive baseline. It does not validate contact pulse behavior because no contact was generated during the test.

### Synthetic Receiver Test - First Run

Folder:

```text
thesis_eval_raw/06_11/latency_rates/synthetic_receiver/20260611_202354_synthetic_hand_receiver/
```

Result:

- Received-pose topics: ~60.0 Hz for both arms.
- Target-twist topics: ~53.8 Hz for both arms.
- Script return code: 0.

Use this as the cleaner synthetic receiver result. The max gap around 0.338 s suggests startup/transition disturbance, so avoid overclaiming strict jitter performance from this run alone.

### Servo Response Test

Folder:

```text
thesis_eval_raw/06_11/latency_rates/servo_response/20260611_202558_servo_response/
```

Result:

- Script return code: 0.
- Both arms received active target messages.
- Servo status stayed `0` with scale factor 1.0.
- Joint command messages were produced for both arms.

Use this as evidence that synthetic hand motion reaches Servo and produces joint commands without collision scaling during this scripted motion.

### Mapping Axis/Sign Sanity Test

Folder:

```text
thesis_eval_raw/06_11/latency_rates/mapping_sanity/20260611_202616_mapping_axis_sign/
```

Result:

- X/Y/Z synthetic motion cases all returned code 0.

Use this as a lightweight automated sanity check only. It does not replace headset-based human direction-feel testing.

## Not Accepted As Passing Results / Script Limitations

### Synthetic Receiver Test - Second Run

Folder:

```text
thesis_eval_raw/06_11/latency_rates/synthetic_receiver/20260611_202539_synthetic_hand_receiver/
```

Issue:

- Received-pose topics stayed ~60 Hz, but target-twist topics dropped to ~3.7 Hz.

Do not use this as the headline synthetic receiver result. Keep it as raw history/debug evidence.

### Gripper Timing/Symmetry Test

Folder:

```text
thesis_eval_raw/06_11/sync_precision/gripper_timing/20260611_202222_gripper_timing_symmetry/
```

Issue:

- Finger joint samples stayed effectively constant near zero throughout all open/close events.
- Joint ranges were near floating-point noise rather than real gripper motion.

Interpretation:

- This test did not actually validate the current coupled gripper behavior. It likely published to a lower-level position-controller topic that did not move the gripper under the current coupled-controller setup.

Do not use this as gripper timing evidence. This is a test-script limitation, not evidence that the headset trigger path is broken. Rework the test to publish `TargetTwistStates.gripper_cmd` into `/left_arm/target_twist_states` and `/right_arm/target_twist_states`, then let `coupled_gripper_controller` command the low-level position controller exactly like the live system.

### Reset Reliability

Important interpretation: this was a stress/exact-pose verification test, not the same as the normal user-facing reset check. The script first disturbs every generated `Sync_*` model by `+0.08 m` in X and `+0.08 m` in Z, then compares final poses against generated SDF spawn poses. Your headset reset can still work normally even if this strict test flags objects that settle physically to a different Z height.

Folders:

```text
thesis_eval_raw/06_11/sync_precision/reset_reliability/20260611_202311_reset_reliability/
thesis_eval_raw/06_11/sync_precision/reset_reliability/20260611_202421_reset_reliability/
```

Results:

- First run: 7 failures / 11 objects.
- Second run: 2 failures / 11 objects.

Second-run failures:

| Object | Error | Main mismatch |
| --- | ---: | --- |
| `Sync_CableRod` | ~0.1006 m | Z observed around 0.0039 instead of expected 0.1045. |
| `Sync_GreenCube` | ~0.1008 m | Z observed around 0.0192 instead of expected 0.1200. |

Interpretation:

- The second run shows many objects resetting correctly, but not all. This is not a passing reset-reliability result.
- The failures are mostly vertical settling/reset-position mismatches, not total sync loss.

Do not use this as a pass result. Use it as evidence that the reset evaluator needs improvement, or that the reset test must distinguish intended physical settled pose from generated spawn pose. A better future test should compare against the post-reset settled pose after normal control-panel reset, not only the raw SDF spawn pose after forcibly disturbing every object.

## Accepted Copy Location

Clean/supportable outputs were copied into:

```text
complete_material/latency_rates/no_headset_backend_06_11/
```

The failed/caveated reset and gripper timing runs were intentionally not copied into `complete_material/` as accepted results.
