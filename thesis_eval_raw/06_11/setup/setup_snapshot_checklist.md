# Setup Snapshot Checklist

Fill this once per host used on 06_11.

```bash
date
hostname
uname -a
sw_vers 2>/dev/null || true
lsb_release -a 2>/dev/null || true
docker version
docker compose version
git rev-parse HEAD
git status --short --branch
sed -n '1,80p' ros_backend1.1/.env
cat UnityApp/ProjectSettings/ProjectVersion.txt
adb devices 2>/dev/null || true
adb reverse --list 2>/dev/null || true
```

Save output to `thesis_eval_raw/06_11/setup/<host>_setup_snapshot.txt`.
