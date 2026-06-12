# Headset Trace Results Review

Generated from the current `06_11` trace outputs.

## Key Findings

- Headset backend latency traces produced events and can be used after filtering obvious outliers/teleop discontinuities.
- MR object sync for `Sync_RedCube` looks usable in the two runs: mean position error is about 2-3 mm; max observed error is about 2-3.5 cm during motion.
- EE visual alignment summaries currently show large constant position offsets around 0.39-0.43 m. Debugging found this was a mixed-source/reference-point comparison: the script compared backend mapper TF-derived `actual_ee_pose` against Unity `tool0` visuals, while object sync uses Gazebo-authoritative dynamic poses. The trace scripts now include extra Unity `wrist_3` and `robotiq_hande_end` visual topics so the next run can identify whether the remaining error is a reference-point issue or a robot visual joint/base mismatch. Do not use the old EE absolute alignment numbers as thesis results.
- Recording FPS trace did not capture Unity FPS because `/unity_eval/recording_state` and `/unity_eval/fps_sample` were not published by the deployed app. The pulled JPG sequence verifies camera recording occurred.

## Summary Table

| Category | Run | Metric | Samples | Mean | Min | Max | Source |
| --- | --- | --- | ---: | ---: | ---: | ---: | --- |
| headset_backend_latency | 20260611_212422_headset_backend_latency_trace | target_latency_ms | 23 | 23.1587 | 0.296333 | 164.504 | `thesis_eval_raw/06_11/latency_rates/headset_backend_latency/20260611_212422_headset_backend_latency_trace/summary.json` |
| headset_backend_latency | 20260611_212422_headset_backend_latency_trace | servo_latency_ms | 23 | 19.2534 | 0.0665 | 115.132 | `thesis_eval_raw/06_11/latency_rates/headset_backend_latency/20260611_212422_headset_backend_latency_trace/summary.json` |
| headset_backend_latency | 20260611_212422_headset_backend_latency_trace | joint_cmd_latency_ms | 23 | 5.69743 | 0.046917 | 83.5702 | `thesis_eval_raw/06_11/latency_rates/headset_backend_latency/20260611_212422_headset_backend_latency_trace/summary.json` |
| headset_backend_latency | 20260611_212603_headset_backend_latency_trace | target_latency_ms | 25 | 15.934 | 1.62567 | 39.9651 | `thesis_eval_raw/06_11/latency_rates/headset_backend_latency/20260611_212603_headset_backend_latency_trace/summary.json` |
| headset_backend_latency | 20260611_212603_headset_backend_latency_trace | servo_latency_ms | 24 | 18.7014 | 1.70579 | 68.6554 | `thesis_eval_raw/06_11/latency_rates/headset_backend_latency/20260611_212603_headset_backend_latency_trace/summary.json` |
| headset_backend_latency | 20260611_212603_headset_backend_latency_trace | joint_cmd_latency_ms | 27 | 17.7301 | 0.690584 | 184.378 | `thesis_eval_raw/06_11/latency_rates/headset_backend_latency/20260611_212603_headset_backend_latency_trace/summary.json` |
| mr_sync_alignment | 20260611_212724_mr_sync_visual_latency_trace | Sync_RedCube_gazebo_to_unity_visual | 2855 | 0.0020934 | 1.70612e-08 | 0.020903 | `thesis_eval_raw/06_11/sync_precision/mr_sync_trace/20260611_212724_mr_sync_visual_latency_trace/summary.json` |
| mr_sync_alignment | 20260611_212724_mr_sync_visual_latency_trace | left_ee_sim_to_unity_visual | 1640 | 0.43144 | 0.162901 | 0.471438 | `thesis_eval_raw/06_11/sync_precision/mr_sync_trace/20260611_212724_mr_sync_visual_latency_trace/summary.json` |
| mr_sync_alignment | 20260611_213433_mr_sync_visual_latency_trace | Sync_RedCube_gazebo_to_unity_visual | 2902 | 0.00295892 | 6.32271e-09 | 0.0353278 | `thesis_eval_raw/06_11/sync_precision/mr_sync_trace/20260611_213433_mr_sync_visual_latency_trace/summary.json` |
| mr_sync_alignment | 20260611_213433_mr_sync_visual_latency_trace | right_ee_sim_to_unity_visual | 2044 | 0.390649 | 0.297387 | 0.462912 | `thesis_eval_raw/06_11/sync_precision/mr_sync_trace/20260611_213433_mr_sync_visual_latency_trace/summary.json` |
| mr_sync_alignment | 20260611_213433_mr_sync_visual_latency_trace | left_ee_sim_to_unity_visual | 1691 | 0.399844 | 0.294358 | 0.466987 | `thesis_eval_raw/06_11/sync_precision/mr_sync_trace/20260611_213433_mr_sync_visual_latency_trace/summary.json` |
| recording_file_verification | recording_file_verification_20260611_215347 | estimated_capture_hz | 1583 | 4.67308 | 0.192 | 0.257 | `thesis_eval_raw/06_11/latency_rates/recording_fps_sync_trace/recording_file_verification_20260611_215347/summary.json` |

## Red-Cube Alignment Interpretation

The `Sync_RedCube` mean position error of about 2-3 mm is not a static spatial offset. Re-analysis of `pose_alignment_samples.csv` showed that when the cube was effectively still, mean error was about 0.006-0.013 mm, with many samples near numerical zero. During movement, mean error rose to about 4-9 mm, with larger maxima during faster motion.

This points to asynchronous sampling and display/update latency rather than a fixed code offset. The script pairs the latest Gazebo-authoritative object pose with the latest Unity visual pose using ROS/container arrival time. Since Gazebo sync, ROS-TCP delivery, Unity `Update()`, and the Unity eval publisher are not the same clocked process, a moving object can show apparent position error equal to motion during the residual delay. This is expected measurement behavior for moving objects, while the near-zero still-object error indicates the coordinate conversion and object profile alignment are correct.
