# Ubuntu Data Drop

Use this folder for evaluation data collected on the remote Ubuntu/Linux machine.

Suggested workflow:

1. Pull the latest repo on Ubuntu.
2. Run the scripted tests from `ros_backend1.1`.
3. Save or copy Ubuntu outputs into this folder.
4. Commit and push from Ubuntu.
5. Pull on the Mac to retrieve the data.

Recommended subfolders:

- `setup/`: Ubuntu host and backend setup snapshots.
- `runtime_performance/`: headless/headed/noVNC RTF and CPU runs.
- `latency_rates/`: topic-rate and backend-latency outputs.
- `sync_precision/`: reset/sync validation outputs if run on Ubuntu.
- `logs/`: supporting logs that are small enough to commit.

Avoid committing large videos, rosbags, `.db3`, `.mcap`, `.mp4`, or `.mov` files unless they are intentionally hosted through a release or external storage.
