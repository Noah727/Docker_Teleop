# 2026-05-24 MR Interaction Fixes

## Goals

- Make all floating/debug windows movable with a lower-right semi-transparent drag notch instead of a blue square.
- Make workspace dragging move the table, robot, and synchronized object group as one operational scene.
- Keep controller rays visible briefly after trigger interaction so the user can see the pointer in MR.
- Reduce wrist-camera recording stutter on Quest.
- Keep first-person robot-view work isolated enough to test from a clean scene later.

## Changes Made

- Added `UICornerDragHandle` as a shared UI helper for transparent lower-right notch handles.
- Updated the debug panel, instruction board, robot view window, and gripper-camera control panel to use the notch handle.
- Made window dragging retry the handle hit while the trigger is held, instead of only checking on the first trigger frame.
- Made the gripper-camera panel use the same retry behavior for dragging.
- Replaced the separate runtime debug, instruction, and gripper-camera floating windows with one `MRCentralControlPanel` that has Controls, Camera, and Debug pages.
- Updated workspace dragging to explicitly move operational roots (`ur5e`, `GameObjects_sync`) and teleport the robot articulation root while the workspace root moves.
- Split workspace manipulation into two handles: the blue inner skirt translates the workspace by attaching it to the end of the controller ray, while the outer grey circular ring rotates only the workspace yaw.
- Made the central control panel accept tab/button clicks from either controller, not only the left controller.
- Made the central control panel drag handle work with either controller.
- Changed left Y into an explicit hand-pose/gamepad control-mode switch. Unity now tracks the selected mode locally, sends it to ROS, and displays it on the debug/status panel.
- Added central-panel buttons for mode switching and reset requests, which starts moving system-level controls off the physical controller buttons.
- Added `HandPoseSender.sendRelativeToControlFrame` and configured the Gazebo replica scene to send controller poses in `GazeboWorkspace` local coordinates.
- Updated controller rays to stay visible for 5 seconds after trigger/grip interaction intent.
- Changed gripper-camera recording to target 5 Hz, cap Quest recording resolution at 640x360, use JPG by default, use async GPU readback, and drop frames when a capture is still pending.
- Created `GazeboReplica_FirstPerson_MR.unity` as a separate first-person test scene. It starts from the clean Gazebo replica scene and enables `RobotFirstPersonTestMode`.
- Changed Gazebo gripper simulation so both Hand-E finger joints are directly position-controlled in sim. The right finger is no longer a passive mimic joint in Gazebo, which should prevent free rail sliding when closed/contacted.

## Frame Fix

The fixed physical frame is the Quest/MR tracking world. The movable visual frame is `GazeboWorkspace`. The ROS servo frame stays `base_link`.

When the user rotates `GazeboWorkspace`, Unity visuals rotate but Gazebo/MoveIt still command the robot in `base_link`. If the controller pose is sent directly in Quest world coordinates, visual EE motion rotates away from hand motion. To compensate, the Unity app now expresses controller pose in `GazeboWorkspace` local coordinates before sending it to ROS. Gazebo commands remain in `base_link`, and the Unity visual workspace rotation maps the resulting robot motion back into the same direction as the user's MR hand motion.

## Workspace Handles

The workspace now has two separate MR manipulation handles:

- Blue inner skirt: translation only. When grabbed, the workspace follows the fixed-depth point at the end of the controller ray, preserving the initial grab offset.
- Grey outer circle: yaw rotation only. When grabbed, the workspace rotates around its vertical axis using the ray's horizontal angle around the workspace center.

This avoids the previous behavior where controller pose was applied 1:1, causing unwanted pitch/roll/rotation while trying to reposition the table.

## Recording Performance Notes

The previous capture path used synchronous `ReadPixels()` and PNG encoding/writing on the main Unity thread. On Quest, that can stall rendering every capture and make the scene feel like it is running at the recording cadence.

The new path reduces this by:

- Capturing at a real `targetRecordingHz = 5`.
- Using `maxPendingCaptures = 1`.
- Using `AsyncGPUReadback` when available.
- Saving JPG frames instead of PNG frames.
- Reducing default recording resolution to `640x360`.

## First-Person View Plan

Current first-person test setup:

- Scene: `Assets/Scenes/GazeboReplica_FirstPerson_MR.unity`.
- Runtime helper: `RobotFirstPersonTestMode`.
- It enables a robot-base view window named `RobotFirstPersonTestWindow`.
- It keeps the Gazebo replica workspace and central panel separate from the main working scene.

Next implementation step:

- Add the true end-effector-to-controller pilot behavior in this test scene only.
- Once it feels right, add a mode switch to the final MR scene.
