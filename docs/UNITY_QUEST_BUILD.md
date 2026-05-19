# Unity Quest 3 Rebuild Guide

This guide explains how another developer can clone the repository, open the Unity project, rebuild the Quest 3 app, and run it against the ROS backend.

## Repository Size Notes

The full local `UnityApp/` folder can become several GB after Unity opens it because Unity creates generated folders such as `Library/`, `Temp/`, and local build outputs.

The pushed Git-tracked Unity content is much smaller:

```text
Tracked UnityApp files: about 94 MB
UnityApp Git LFS files: about 91 MB
```

Do not commit generated Unity folders or APK builds. They are ignored by `.gitignore`.

## Requirements

- Unity Hub.
- Unity `6000.2.10f1`.
- Unity Android Build Support module.
- Unity Android SDK & NDK Tools module.
- Unity OpenJDK module.
- Meta Quest 3 with Developer Mode enabled.
- `adb` available on the host machine.
- Git LFS.
- Docker backend from `ros_backend1.0/`.

## Clone The Project

Use SSH if you have GitHub SSH access:

```bash
git lfs install
git clone git@github.com:su-idr-lab/ros_unity_project.git
cd ros_unity_project
git lfs pull
```

If Git LFS is not installed or `git lfs pull` is skipped, some meshes/textures may appear as tiny pointer files and Unity imports will be broken.

## Open In Unity

1. Open Unity Hub.
2. Select `Add project from disk`.
3. Choose:

```text
ros_unity_project/UnityApp
```

4. Open with Unity `6000.2.10f1`.
5. Let Unity restore packages and rebuild the local `Library/` folder.

The first import can take a while. This is normal and should not be committed.

## Important Project Settings

Current Unity project identity:

```text
Company: NoahLi
Product: HandTrackingUnity
Android package ID: com.noahli.handtrackingunity
```

Current active build scene:

```text
Assets/Scenes/Ur5e_Working 1.unity
```

Current build profile asset:

```text
Assets/Settings/Build Profiles/Quest3_4.2.asset
```

## Build For Quest 3

1. Connect the Quest 3 by USB.
2. Accept the headset USB debugging prompt.
3. Confirm the device is visible:

```bash
adb devices
```

Expected output should include a connected device, not `unauthorized`.

4. In Unity, open:

```text
File > Build Profiles
```

5. Select Android / Quest build profile.
6. Confirm the active scene includes:

```text
Assets/Scenes/Ur5e_Working 1.unity
```

7. Use `Build And Run` to install directly to the Quest.

## Backend For Wired Quest Mode

For the usual wired development mode, start the backend before running the app:

```bash
cd ros_backend1.0
cp .env.example .env
./scripts/backend10_lifecycle.sh bringup_wired
```

Wired mode uses `adb reverse`, so the Quest app connects to its own loopback address:

```text
Quest app target: 127.0.0.1:5026
Unity ROS-TCP target: 127.0.0.1:10001
```

The USB tunnel forwards those connections back to the host backend.

## Runtime Verification

Use logs when testing a deployed Quest build:

```bash
adb logcat -c
adb logcat Unity:I '*:S'
```

Useful one-shot capture after reproducing a bug:

```bash
adb logcat -d Unity:I '*:S' > quest_unity_log.txt
```

Check the backend separately:

```bash
cd ros_backend1.0
./scripts/backend10_lifecycle.sh status
```

## Recording Data Path On Quest

The app package ID is:

```text
com.noahli.handtrackingunity
```

Wrist-camera recordings are expected under:

```text
/storage/emulated/0/Android/data/com.noahli.handtrackingunity/files/GripperCameraRecordings
```

Pull recordings to the host:

```bash
adb pull "/storage/emulated/0/Android/data/com.noahli.handtrackingunity/files/GripperCameraRecordings" ./GripperCameraRecordings
```

## Common Problems

- Missing robot meshes or textures: run `git lfs pull`.
- Unity opens with the wrong version: install/use Unity `6000.2.10f1`.
- Android build unavailable: install Android Build Support, SDK/NDK Tools, and OpenJDK through Unity Hub.
- `adb devices` shows `unauthorized`: accept the USB debugging prompt inside the headset.
- App cannot connect in wired mode: restart `bringup_wired` and confirm the Quest is connected by USB.
- Robot does not move: confirm backend status, Quest logs, and controller state in the in-app panel.
- Recording path looks empty: make sure the currently installed app uses package ID `com.noahli.handtrackingunity`.

## Development Workflow

Keep `main` stable. Use feature branches for app changes:

```bash
git checkout -b feature/my-unity-change
```

After testing:

```bash
git add UnityApp docs
git commit -m "Describe Unity change"
git push -u origin feature/my-unity-change
```

Merge back to `main` only after the Quest build is tested.
