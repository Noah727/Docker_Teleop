# Dynamic Backend Performance: noVNC and Headed Gazebo

## Why This Test Was Run

This test isolates backend simulation/rendering overhead from headset usability. The teleoperation system depends on Gazebo running close to real time while MoveIt Servo, object synchronization, haptics, and the TCP/ROS bridge are active. If desktop services or Gazebo GUI rendering consume too much CPU, the robot can feel delayed even when the Unity controller stream is healthy.

The synthetic hand generator replaces the headset for this test. It publishes repeatable Quest-like hand poses while teleop is engaged, using a large vertical oscillation intended to push the robot into the workspace/table region. That makes the measurement closer to a loaded manipulation case than an idle simulation measurement.

## Test Conditions

| Condition | Gazebo | Desktop/noVNC | Dynamic input |
|---|---:|---:|---|
| Plain backend | headless | disabled | both:line_y, amp=(0.0,0.55,0.0), period=8.0s |
| noVNC only | headless | enabled; no browser connection required | both:line_y, amp=(0.0,0.55,0.0), period=8.0s |
| noVNC + headed Gazebo | headed | enabled | both:line_y, amp=(0.0,0.55,0.0), period=8.0s |

## Summary Table

| Condition | Mean RTF | Min RTF | Max RTF | Mean CPU % | Min CPU % | Max CPU % |
|---|---:|---:|---:|---:|---:|---:|
| Plain backend | 0.631 | 0.546 | 0.670 | 426.775 | 413.200 | 540.150 |
| noVNC only | 0.641 | 0.616 | 0.666 | 423.522 | 410.030 | 528.670 |
| noVNC + headed Gazebo | 0.519 | 0.458 | 0.537 | 650.358 | 632.070 | 725.470 |

## Interpretation

In this run, `noVNC only` had the highest mean RTF and `noVNC + headed Gazebo` had the lowest mean RTF. The useful interpretation is not only the mean value, but also how stable the RTF trace is under dynamic robot motion. A lower or more variable RTF means Gazebo is falling behind wall time, which directly increases perceived robot sluggishness.

CPU percentage is Docker container CPU from `docker stats`, so values can exceed 100% on multi-core systems. This is still useful for comparing relative backend load across conditions on the same host.

## Plots

- `dynamic_cpu_load_timeseries.svg`: time versus Docker CPU load.
- `dynamic_rtf_timeseries.svg`: time versus 1-second-windowed real-time factor.

Raw run directory: `thesis_eval_raw/06_11/logs/backend_eval_runs/20260611_175610_dynamic_novnc_headed_performance`
