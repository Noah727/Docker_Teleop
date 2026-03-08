# Teleop System Implementation Roadmap (v2)

Last updated: March 5, 2026

## Goal

Deliver a robust VR teleoperation stack that supports:
- Stable operation (no random RX 0.0 failures)
- Easy tuning of mapping/reset/environment
- Simulation + real robot operation
- Wired and wireless transport options
- Operator feedback (camera + haptics)
- Scale-up to dual-arm control

## Priority Order

1. Stabilize runtime + tuning workflow
2. Improve reset behavior
3. Add gripper camera + dataset logging
4. Add wired mode transport
5. Standardize multi-computer deployment (wired/wireless)
6. Add operator UI for config
7. Simulation-to-real rollout
8. Add haptic feedback loop
9. Add second arm

---

## Phase 0: Foundations (Do First)

### 0.1 Parameter profile system
- Keep all tunables in YAML profiles:
  - mapping axes/signs
  - scale/offset
  - workspace clamp
  - reset home/kick behavior
  - transport mode (wifi/wired)
- Add profile files:
  - `profiles/local_wifi.yaml`
  - `profiles/local_wired.yaml`
  - `profiles/laptop_wifi.yaml`
  - `profiles/laptop_wired.yaml`
- Add script:
  - `apply_profile.sh <profile>`
  - applies params + restarts required nodes only.

### 0.2 Health checks script
- Add `health_check.sh` that checks:
  - container exists/running
  - UDP/TCP port mapping
  - receiver process and bind
  - topic freshness (`/received_pose_states`, `/target_twist_states`)
  - TF availability (`base_link <- tool0`)
- Output PASS/FAIL with next action.

### Acceptance
- One command confirms stack health before demos.
- Switching profiles does not require full manual restart.

---

## Phase 1: Mapping/Offset Tuning + Auto Tuning

### 1.1 Manual tuning workflow (immediate)
- Keep current mapper architecture.
- Add a structured tuning checklist:
  - axis/sign first
  - scale second
  - offset third
  - angular gain/deadband last
- Add a repeatable test trajectory and expected response criteria.

### 1.2 Automatic tuning (environment-aware)
- Implement calibration routine with paired samples:
  - sample N hand poses in Unity frame
  - sample corresponding EE poses in robot frame
- Solve affine map:
  - `x_robot = A * x_unity + b`
- Store result to profile YAML.
- Optional: per-environment profiles (desk A, desk B).

### 1.3 Environment change handling
- Add `environment_id` in profile selection.
- Optional advanced mode:
  - detect a reference marker/object and auto-select profile.

### Acceptance
- After calibration, position error improves vs baseline.
- Re-running calibration in changed environment restores accuracy quickly.

---

## Phase 2: Reset (B Button) Improvements

### Problem
Current reset can joggle robot due to abrupt kick and control switching.

### Plan
- Replace hard kick with state-machine reset:
  1. hold servo + zero target twist
  2. smooth joint trajectory to pre-home waypoint
  3. smooth trajectory to home pose
  4. reopen gripper
  5. reset cube pose
  6. release to teleop
- Add jerk/velocity limits and timeout fallback.
- Add anti-retrigger lockout while reset active.

### Acceptance
- No visible jerks during reset.
- After reset, teleop resumes immediately and reliably.

---

## Phase 3: Gripper Camera + Data Logging

### 3.1 Simulation camera
- Attach camera sensor to wrist/tool link in Gazebo.
- Publish:
  - `/ee_camera/image_raw`
  - `/ee_camera/camera_info`
  - optional `/ee_camera/depth/image_raw`

### 3.2 Dataset recording
- Create recorder launch/script using `rosbag2`:
  - images
  - `/joint_states`
  - `/tf`
  - `/received_pose_states`
  - `/target_twist_states`
  - gripper command topic
- Save run metadata JSON:
  - operator id
  - profile used
  - task id
  - timestamps

### Acceptance
- Camera stream visible during teleop.
- One command starts/stops synchronized data recording.

---

## Phase 4: Wired Transport Mode

### 4.1 Transport abstraction
- Define sender/receiver transport interface:
  - `wifi_udp`
  - `wired_tcp`
- Keep message schema unchanged.

### 4.2 Wired implementation path
- Preferred short-term: USB + ADB tunnel + TCP stream.
- Move hand/control stream from UDP to TCP when wired mode selected.
- Keep wireless UDP as default fallback.

### 4.3 Runtime mode selection
- Add `transport_mode` parameter/profile key.
- Add startup script that configures correct tunnel and ports.

### Acceptance
- User can switch between wired and wireless without code edits.
- Wired mode works when Wi-Fi is unstable or unavailable.

---

## Phase 5: Multi-Computer Deployment Matrix

## 5.1 Clarified dimensions
- Dimension A: Host machine
  - current Mac
  - laptop
- Dimension B: Transport
  - wireless
  - wired
- Dimension C: OS
  - macOS
  - Linux
  - Windows (later)

### 5.2 Support matrix
- Local Mac + wireless
- Local Mac + wired
- Laptop + wireless
- Laptop + wired

Each combination gets:
- required prerequisites
- exact startup commands
- firewall/network checks
- recovery steps

### 5.3 Public network constraints
- Same-LAN direct UDP/TCP is reliable only if network allows peer traffic.
- School/public Wi-Fi often blocks client-to-client.
- For internet/public-network operation, add relay architecture:
  - WebRTC (STUN/TURN) or secure relay server
  - authenticated session pairing

### Acceptance
- Reproducible bringup on at least 2 machines in both transport modes.
- Clear failover guide for blocked networks.

---

## Phase 6: Operator UI (Parameter + Environment Manager)

### Plan
- Build lightweight web UI (recommended):
  - backend: ROS2 service/param bridge
  - frontend: profile selector + sliders + buttons
- Controls:
  - axis/sign map
  - scale/offset/clamps
  - reset home pose
  - environment object pose/size
  - transport mode (wired/wireless)
- Actions:
  - save profile
  - apply profile
  - restart required nodes only
  - health check

### Acceptance
- User can retune system without editing YAML manually.
- UI changes apply within one workflow step.

---

## Phase 7: Simulation to Real Robot

### 7.1 Safety-first migration
- Keep teleop mapper unchanged; swap execution backend.
- Use UR driver + controller interfaces on real robot.
- Set strict speed/acceleration/workspace limits.
- Add deadman/enable gating and E-stop integration.

### 7.2 Calibration and validation
- Tool frame and base frame calibration.
- Validate low-speed tracking first.
- Validate gripper open/close independently.
- Add remote camera stream for operator view if not colocated.

### Acceptance
- Controlled real-robot motion with same teleop semantics as sim.
- Safety checks pass before full-speed trials.

---

## Phase 8: Haptic Feedback

### Plan
- Add haptic command topic from ROS -> Unity.
- Compute vibration intensity from confidence/error signals:
  - high target-vs-EE error
  - commanded motion high but EE velocity low
  - optional force/torque/contact signal when available
- Add deadband and rate limiting to avoid noisy vibration.

### Note on your current idea
- Your stale/error-based method is a good start.
- Improve it by combining:
  - position error magnitude
  - motion stall detection
  - contact signal (if available)

### Acceptance
- Operator receives meaningful vibration when robot stalls/contacts.
- False positives are low during free motion.

---

## Phase 9: Add Second Arm

### Plan
- Extend simulation to dual-arm model.
- Namespaced topics and controllers per arm.
- Add control mode:
  - single-arm select
  - dual-arm synchronized
- Expand mapping profile to include per-arm settings.

### Acceptance
- Independent control of arm A/arm B.
- Optional coordinated bimanual tasks in sim.

---

## Suggested Execution Schedule

## Sprint A (1-2 weeks)
- Phase 0 + Phase 1.1 + Phase 2

## Sprint B (1 week)
- Phase 3 + Phase 4

## Sprint C (1-2 weeks)
- Phase 5 + Phase 6

## Sprint D (2+ weeks)
- Phase 7 + Phase 8 + Phase 9

---

## Immediate Next 5 Tasks (Concrete)

1. Add profile files + `apply_profile.sh`.
2. Implement `health_check.sh`.
3. Refactor reset manager to smooth trajectory reset.
4. Add gripper camera sensor + rosbag recorder script.
5. Add transport mode abstraction (`wifi_udp` + `wired_tcp`).

