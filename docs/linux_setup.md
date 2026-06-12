# Linux Setup

This guide records the Ubuntu setup path used on Noah's Linux workstation and the fixes needed to make the ROS/Gazebo backend build on x86_64 Linux.

Read these first when taking over the project:

```bash
sed -n '1,220p' AGENTS.md
sed -n '1,220p' docs/across_platform_agent.md
```

## Confirmed Host

This setup was exercised on:

```text
OS: Ubuntu 24.04.4 LTS
Kernel: 6.17.0-23-generic
Architecture: x86_64
CPU: Intel Xeon W-2295, 18 cores / 36 threads
RAM: 125 GiB
Docker: 29.5.0
Docker Compose: v5.1.3
Git LFS: 3.4.1
ADB: not installed during this documentation pass
```

Backend status from the first Ubuntu setup pass:

- `git lfs install` succeeded.
- Docker was able to build and start `motion_planner_11` after the Dockerfile fixes below.
- `./scripts/backend11_lifecycle.sh build_ws` completed successfully and built the ROS workspace packages.
- Only setuptools deprecation warnings were seen during the successful workspace build.
- Quest USB / `adb reverse` was not tested yet on this machine.

## Required Linux Fixes

Two Dockerfile details were required on this x86_64 Ubuntu machine:

1. Use the multi-architecture ROS base image:

```dockerfile
FROM ros:humble-ros-base
```

The previous ARM-specific image failed on x86_64 with a manifest/platform error:

```text
no match for platform in manifest
```

2. Make apt noninteractive before package installation:

```dockerfile
ENV DEBIAN_FRONTEND=noninteractive \
    TZ=Etc/UTC
```

Without this, the image build can stop at an interactive `keyboard-configuration` / timezone prompt.

These fixes are currently applied in:

```text
ros_backend1.1/Dockerfile
```

Keep them unless intentionally building only for an ARM host.

## Install Host Tools

Install the baseline Ubuntu packages:

```bash
sudo apt update
sudo apt install -y \
  git \
  git-lfs \
  curl \
  ca-certificates \
  gnupg \
  lsb-release \
  android-tools-adb

git lfs install
```

Check versions:

```bash
git --version
git lfs version
adb version
```

`android-tools-adb` is needed for Quest wired mode. If `adb version` is not found, install the package above before testing the headset path.

## Install Docker

If Docker is not already installed, use Docker's official Ubuntu repository:

```bash
sudo install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | \
  sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
sudo chmod a+r /etc/apt/keyrings/docker.gpg

echo \
  "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu \
  $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | \
  sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

sudo apt update
sudo apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
```

Allow the current user to run Docker without `sudo`:

```bash
sudo usermod -aG docker "$USER"
newgrp docker
```

Verify Docker:

```bash
docker --version
docker compose version
docker run --rm hello-world
```

## Clone And Pull LFS Assets

Use SSH after adding the machine's public SSH key to GitHub:

```bash
cd /home/noah
git clone git@github.com:su-idr-lab/ros_unity_project.git
cd ros_unity_project
git lfs pull
```

When GitHub asks to trust the host key, type the full word:

```text
yes
```

Do not answer only `y`; OpenSSH will keep asking until it receives `yes`, `no`, or the fingerprint.

If SSH fails with:

```text
Permission denied (publickey)
```

confirm the key is loaded and registered with GitHub:

```bash
ssh -T git@github.com
ssh-add -l
```

Expected repository shape:

```text
/home/noah/ros_unity_project
  AGENTS.md
  docs/
  ros_backend1.1/
  UnityApp/
```

If the clone accidentally creates `/home/noah/ros_unity_project/ros_unity_project`, move into the nested folder, verify its `.git` is the real repo, then move contents up or reclone cleanly. Do not delete a nested folder until `git status --short --branch` works from `/home/noah/ros_unity_project`.

## Configure Backend

From the backend folder:

```bash
cd /home/noah/ros_unity_project/ros_backend1.1
cp .env.example .env
./scripts/backend11_lifecycle.sh mode_wired
```

The wired-mode `.env` should bind local ports only to localhost:

```text
ROS_TCP_HOST_BIND=127.0.0.1
ROS_TCP_HOST_PORT=10001
ROS_TCP_UNITY_PORT=10001
QUEST_TCP_HOST_BIND=127.0.0.1
QUEST_TCP_HOST_PORT=5026
QUEST_TCP_UNITY_PORT=5026
ENABLE_DESKTOP=0
ENABLE_NOVNC=0
SIM_HEADLESS=1
```

## Build Container And Workspace

Use a clean container start when setting up or after Dockerfile changes:

```bash
cd /home/noah/ros_unity_project/ros_backend1.1
./scripts/backend11_lifecycle.sh safe_down
./scripts/backend11_lifecycle.sh up_container_build
./scripts/backend11_lifecycle.sh build_ws
./scripts/backend11_lifecycle.sh status
```

Expected:

- Container `motion_planner_11` is running.
- Ports include host `127.0.0.1:5026 -> container 5005` and `127.0.0.1:10001 -> container 10000`.
- `build_ws` finishes without compile errors.

Useful checks:

```bash
docker ps --filter "name=motion_planner_11"
docker logs motion_planner_11 --tail 100
```

If the Docker build looks quiet while exporting layers, wait. Large ROS images can spend a while in the final export step.

## Bring Up Backend

Default backend bringup is headless Gazebo:

```bash
cd /home/noah/ros_unity_project/ros_backend1.1
./scripts/backend11_lifecycle.sh bringup_dual
./scripts/backend11_lifecycle.sh status
```

`bringup_dual` starts the container if needed, builds/checks the workspace, generates the dual-arm world, starts Gazebo, starts MoveIt Servo, starts the Quest TCP receiver, starts dual Part 2/3 control nodes, and starts Part 4 sync/haptics/task services.

Important logs:

```bash
docker exec motion_planner_11 bash -lc 'tail -n 80 /tmp/run_dual_arm_tabletop_sim.log'
docker exec motion_planner_11 bash -lc 'tail -n 80 /tmp/servo_dual_gz.log'
docker exec motion_planner_11 bash -lc 'tail -n 80 /tmp/qcr.log'
docker exec motion_planner_11 bash -lc 'tail -n 80 /tmp/dual_part4_tcp_endpoint.log'
docker exec motion_planner_11 bash -lc 'tail -n 80 /tmp/dual_part4_cube_pose.log'
```

Use headed Gazebo/noVNC only when visual debugging is needed:

```bash
ENABLE_DESKTOP=1 ENABLE_NOVNC=1 SIM_HEADLESS=0 ./scripts/backend11_lifecycle.sh up_container_build
SIM_HEADLESS=0 ./scripts/backend11_lifecycle.sh start_dual_sim
```

## Quest Wired Mode

Install `adb` first:

```bash
sudo apt install -y android-tools-adb
adb version
```

Connect the Quest by USB and accept the USB debugging prompt in the headset:

```bash
adb devices
```

Expected:

```text
<device_id>    device
```

Enable reverse tunnels:

```bash
cd /home/noah/ros_unity_project/ros_backend1.1
./scripts/backend11_lifecycle.sh wired_on
./scripts/backend11_lifecycle.sh wired_status
adb reverse --list
```

Expected tunnels:

```text
tcp:5026 tcp:5026
tcp:10001 tcp:10001
```

Quest/Unity wired settings:

```text
Hand/controller TCP target IP: 127.0.0.1
Hand/controller TCP target port: 5026
ROS Settings IP: 127.0.0.1
ROS Settings port: 10001
```

## Scripted Tests

The scripted test tools are under:

```text
ros_backend1.1/scripts/test_tools/
```

Backend status and cross-platform report:

```bash
cd /home/noah/ros_unity_project/ros_backend1.1
python3 scripts/test_tools/eval_scripts/12_cross_platform_backend_bringup_check.py
```

To let the report run a backend bringup first:

```bash
python3 scripts/test_tools/eval_scripts/12_cross_platform_backend_bringup_check.py --bringup
```

Topic-rate audit after `bringup_dual`:

```bash
python3 scripts/test_tools/eval_scripts/03_ros_topic_rate_audit.py --duration 20
```

Short dynamic backend performance smoke test:

```bash
DURATION=20 WARMUP=5 ./scripts/test_tools/performance_test_scripts/run_dynamic_backend_performance_linux.sh
```

Full dynamic backend performance test:

```bash
./scripts/test_tools/performance_test_scripts/run_dynamic_backend_performance_linux.sh
```

The performance wrapper compares:

- Plain headless backend.
- noVNC enabled with headless Gazebo.
- noVNC enabled with headed Gazebo.

Outputs default into:

```text
thesis_eval_raw/MM_DD/runtime_performance/
thesis_eval_raw/MM_DD/logs/backend_eval_runs/
```

## Useful ROS Checks

After `bringup_dual`:

```bash
CONTAINER=motion_planner_11
ROS_ENV='source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash'

docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic list | sort | sed -n '1,120p'"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic hz /joint_states"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic hz /left_arm/received_pose_states"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic hz /right_arm/received_pose_states"
```

`/left_arm/received_pose_states` and `/right_arm/received_pose_states` publish only when live Quest packets or the synthetic hand generator are active.

## Troubleshooting

Git LFS:

- Run `git lfs install` before clone or before `git lfs pull`.
- If LFS objects appear as tiny pointer files, run `git lfs pull` from the repo root.

GitHub SSH:

- Answer the host authenticity prompt with the full word `yes`.
- For `Permission denied (publickey)`, add the public SSH key to GitHub and test with `ssh -T git@github.com`.

Docker permissions:

- If Docker says permission denied on `/var/run/docker.sock`, add the user to the `docker` group and start a new shell:

```bash
sudo usermod -aG docker "$USER"
newgrp docker
```

Docker image platform:

- On x86_64, `FROM arm64v8/ros:humble-ros-base` fails. Use `FROM ros:humble-ros-base`.

Interactive apt prompt during image build:

- Add `DEBIAN_FRONTEND=noninteractive` and `TZ=Etc/UTC` before `apt-get install` in the Dockerfile.

ADB:

- If `adb devices` shows `unauthorized`, accept the debugging prompt inside the Quest.
- If no device appears, try another USB cable/port and restart the adb server with:

```bash
adb kill-server
adb start-server
adb devices
```

Sandboxed agent sessions:

- In managed Codex sandboxes, `git status` can fail after LFS pulls because Git LFS tries to write temporary files under `.git/lfs/tmp`, while `.git` is read-only to the sandbox. This is a sandbox artifact. Use a normal terminal or an escalated command for repository status checks.

## Report Template

When finishing a Linux bringup/test pass, report:

```text
OS:
Kernel:
CPU:
GPU:
RAM:
Docker version:
Docker compose version:
Git LFS version:
ADB version:
Repo commit:
Container build: pass/fail
build_ws: pass/fail
bringup_dual: pass/fail
Quest wired adb reverse: pass/fail/not tested
Scripted tests run:
Primary failure:
Relevant logs:
```
