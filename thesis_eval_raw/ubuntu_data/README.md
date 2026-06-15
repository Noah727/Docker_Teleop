# Ubuntu Data Drop

This lowercase directory is the canonical Ubuntu/Linux evaluation data folder.
The old `thesis_eval_raw/Ubuntu_data/` scaffold was removed to avoid duplicate
paths that differ only by case.

Use this folder for evaluation data collected on the remote Ubuntu/Linux
machine.

Current collected sets:

- `setup/linux_bringup_snapshot/20260611_234112_cross_platform_bringup_check/`
- `runtime_performance/linux_performance/20260611_234406_dynamic_novnc_headed_performance/`
- `latency_rates/linux_topic_rates/20260611_235103_ros_topic_rate_audit/`
- `isolation_matrix/20260612_114727_linux_rtf_isolation/`

Avoid committing large videos, rosbags, `.db3`, `.mcap`, `.mp4`, or `.mov`
files unless they are intentionally hosted through a release or external
storage.
