# Wired / Wireless Setup (ros_backend1.0)

This guide explains both connection modes for the Quest/Unity app.

There are 2 TCP channels in this project:

1. Hand pose stream
- Unity `HandPoseSender`
- Quest/Unity -> backend receiver
- default port: `5026` on host -> `5005` in container

2. Unity ROS-TCP connector
- Unity `ROSConnection`
- Unity subscribers/publishers for joint states, cube sync, TF, etc.
- default port: `10001` on host -> `10000` in container

## Quick recommendation

- Use `wired` when you want the easiest and most stable Quest connection.
- Use `wireless` when you want the headset untethered.

## Ports used

- Hand TCP: host `5026` -> container `5005`
- ROS-TCP: host `10001` -> container `10000`

## Backend mode commands

From `ros_backend1.0`:

```bash
./scripts/backend10_lifecycle.sh mode_wired
./scripts/backend10_lifecycle.sh mode_wireless
./scripts/backend10_lifecycle.sh mode_status
```

After changing mode, restart the container so Docker applies the new port bindings:

```bash
./scripts/backend10_lifecycle.sh restart_container
```

## Wired mode

### How it works

Wired mode uses `adb reverse` over USB.

Path for hand data:

`Quest 127.0.0.1:5026`
-> `adb reverse`
-> `host 127.0.0.1:5026`
-> `Docker`
-> `container 5005`

Path for Unity ROS-TCP:

`Quest 127.0.0.1:10001`
-> `adb reverse`
-> `host 127.0.0.1:10001`
-> `Docker`
-> `container 10000`

### One-time host setup

Install `adb`.

Ubuntu:

```bash
sudo apt update
sudo apt install -y adb
```

macOS with Homebrew:

```bash
brew install android-platform-tools
```

Verify:

```bash
which adb
adb version
```

### Quest setup

- enable Developer Mode
- connect headset by USB
- accept USB debugging prompt in headset

Verify:

```bash
adb devices
```

Expected:

```text
List of devices attached
<serial>    device
```

### Switch backend to wired mode

```bash
cd /Users/noahli/ros_unity_project/ros_backend1.0
./scripts/backend10_lifecycle.sh mode_wired
./scripts/backend10_lifecycle.sh restart_container
```

### Enable the USB tunnels

```bash
./scripts/backend10_lifecycle.sh wired_on
./scripts/backend10_lifecycle.sh wired_status
```

Expected `adb reverse --list` entries:

```text
tcp:5026 tcp:5026
tcp:10001 tcp:10001
```

### Unity settings for wired mode

`HandPoseSender`:
- `targetIP = 127.0.0.1`
- `targetPort = 5026`

`ROS Settings` / `ROSConnection`:
- `ROS IP Address = 127.0.0.1`
- `ROS Port = 10001`

Current defaults in the project already match wired mode:
- `/Users/noahli/ros_unity_project/UnityApp/Assets/Scripts/HandPoseSender.cs`
- `/Users/noahli/ros_unity_project/UnityApp/Assets/Resources/ROSConnectionPrefab.prefab`

Rebuild and deploy the Quest app after changing connection settings.

### Bringup for wired mode

Fast path:

```bash
./scripts/backend10_lifecycle.sh bringup_wired
```

Or step by step:

```bash
./scripts/backend10_lifecycle.sh up_container
./scripts/backend10_lifecycle.sh wired_on
./scripts/backend10_lifecycle.sh build_ws
./scripts/backend10_lifecycle.sh start_sim
./scripts/backend10_lifecycle.sh start_servo
./scripts/backend10_lifecycle.sh start_receiver
./scripts/backend10_lifecycle.sh start_part23
./scripts/backend10_lifecycle.sh start_part4
```

## Wireless mode

### How it works

Wireless mode uses your LAN/Wi-Fi network.

Path for hand data:

`Quest <HostLANIP>:5026`
-> `host LAN interface`
-> `Docker`
-> `container 5005`

Path for Unity ROS-TCP:

`Quest <HostLANIP>:10001`
-> `host LAN interface`
-> `Docker`
-> `container 10000`

### Switch backend to wireless mode

```bash
cd /Users/noahli/ros_unity_project/ros_backend1.0
./scripts/backend10_lifecycle.sh mode_wireless
./scripts/backend10_lifecycle.sh restart_container
./scripts/backend10_lifecycle.sh wired_off
```

The `wired_off` step removes any old USB reverse rules so they do not confuse testing.

### Find the host LAN IP

macOS:

```bash
ipconfig getifaddr en0
```

Ubuntu:

```bash
hostname -I
```

Use the LAN IP address that the Quest can reach, for example `192.168.1.199`.

### Unity settings for wireless mode

`HandPoseSender`:
- `targetIP = <HostLANIP>`
- `targetPort = 5026`

`ROS Settings` / `ROSConnection`:
- `ROS IP Address = <HostLANIP>`
- `ROS Port = 10001`

### Notes for wireless mode

- Quest and host must be on the same network
- host firewall must allow inbound TCP on `5026` and `10001`
- IP can change when you move networks

### Bringup for wireless mode

```bash
./scripts/backend10_lifecycle.sh bringup_all
```

## How to switch between modes

### Wired -> Wireless

1. Switch backend bind mode:

```bash
./scripts/backend10_lifecycle.sh mode_wireless
./scripts/backend10_lifecycle.sh restart_container
```

2. Remove USB tunnels:

```bash
./scripts/backend10_lifecycle.sh wired_off
```

3. In Unity / Quest app:
- change `HandPoseSender.targetIP` to `<HostLANIP>`
- change `ROS IP Address` to `<HostLANIP>`
- keep ports `5026` and `10001`
- rebuild and redeploy app

### Wireless -> Wired

1. Switch backend bind mode:

```bash
./scripts/backend10_lifecycle.sh mode_wired
./scripts/backend10_lifecycle.sh restart_container
```

2. Connect Quest by USB and enable reverse tunnels:

```bash
./scripts/backend10_lifecycle.sh wired_on
./scripts/backend10_lifecycle.sh wired_status
```

3. In Unity / Quest app:
- set `HandPoseSender.targetIP = 127.0.0.1`
- set `ROS IP Address = 127.0.0.1`
- keep ports `5026` and `10001`
- rebuild and redeploy app

## Verification checklist

### Verify backend mode

```bash
./scripts/backend10_lifecycle.sh mode_status
```

### Verify hand receiver

```bash
docker exec motion_planner_10 bash -lc "tail -f /tmp/qcr.log"
```

### Verify hand topic

```bash
docker exec motion_planner_10 bash -lc "source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash && ros2 topic echo /received_pose_states"
```

### Verify ROS-TCP bridge port

```bash
docker port motion_planner_10
```

### Verify wired ADB state

```bash
adb devices
adb reverse --list
```

## Common issues

### `adb devices` shows nothing

- check USB cable
- check Quest Developer Mode
- accept USB debugging prompt
- on Ubuntu, you may need udev rules

Useful commands:

```bash
adb kill-server
adb start-server
adb devices
```

Ubuntu helper package:

```bash
sudo apt install -y android-sdk-platform-tools-common
sudo udevadm control --reload-rules
sudo udevadm trigger
```

### Wireless mode connects unreliably

Typical causes:
- wrong host LAN IP
- Quest on different Wi-Fi/VLAN
- host firewall blocking `5026` or `10001`

### Wired mode hand data works but ROS sync does not

Check that both reverse rules exist:

```bash
adb reverse --list
```

You need both:
- `tcp:5026 tcp:5026`
- `tcp:10001 tcp:10001`
