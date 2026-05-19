# Controller Buttons

This is the current headset/controller button map for `ros_backend1.0`.

## Right Controller

- `Grip hold`: engage robot teleop. The robot only follows your hand while this is held.
- `Trigger tap`: toggle gripper open / close.
- `A hold`: rotation mode for hand-pose control.
- `B tap`: reset robot and table objects.
- `Thumbstick press`: clutch / pause hand following. Hold it to move your hand without moving the robot; release it to reset the hand reference at the new hand position.

## Left Controller

- `X tap`: start / stop wrist-camera recording.
- `Y tap`: switch between hand-pose mode and thumbstick/gamepad mode.

## Thumbstick / Gamepad Mode

- `Left stick Y`: forward / back.
- `Left stick X`: left / right.
- `Left trigger`: move up.
- `Left grip`: move down.
- `Right stick Y`: rotate around robot angular Y.
- `Right stick X`: rotate around robot angular Z.

## Floating Control Panel

- Release `Right Grip` so teleop is not engaged.
- Point the left controller at the floating panel.
- Hold `Left Trigger` and move the controller to drag the panel.
- Release `Left Trigger` to drop the panel.

## In-Scene Table Board

The Unity scene has a `TeleopInstructionBoard` component on `NetworkSender`.

It creates a real scene object named `Teleop_Button_Instructions` near the front edge of the table, facing the player's default position. If you want to move it manually, open the hierarchy after Unity reloads scripts and edit `Teleop_Button_Instructions`.

If the board does not appear, select `NetworkSender`, find `Teleop Instruction Board`, and enable `Rebuild Instruction Board From Settings`.
