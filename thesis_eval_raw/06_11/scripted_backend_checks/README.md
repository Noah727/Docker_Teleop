# 2026-06-11 Offline Scripted Backend Checks

These checks were run without the Quest headset and without a live Gazebo/ROS container.

## Passed Checks

| Check | Output Folder | Result | Notes |
| --- | --- | --- | --- |
| Task profile/SDF/Unity JSON consistency | `20260611_190718_task_profile_sdf_unity_consistency/` | Pass | 11 generated task objects checked, 0 failures. This is the accepted current-code rerun copied to `complete_material/`. |
| Unity saved-scene object presence smoke test | `20260611_190718_unity_editor_sync_smoke/` | Pass | 16 expected names checked, 0 missing. This is the accepted current-code rerun copied to `complete_material/`. |
| Earlier same-day consistency run | `20260611_150550_*` | Pass | Kept as raw history; use the `190718` folders as the thesis-ready accepted results. |

## Thesis Use

Use these as supporting evidence for the scripted backend consistency checks and task-profile pipeline. They do not replace headset trials, live ROS topic-rate measurements, or visible MR latency tests.
