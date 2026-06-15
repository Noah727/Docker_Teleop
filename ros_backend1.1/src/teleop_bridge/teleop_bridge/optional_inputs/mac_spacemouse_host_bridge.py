#!/usr/bin/env python3
"""macOS host-side SpaceMouse bridge.

Docker Desktop for macOS does not expose USB HID devices to Linux containers
like a native Ubuntu host does. This script runs on macOS, reads the
3Dconnexion SpaceMouse through hidapi, and forwards velocity packets over TCP
to `spacemouse_tcp_target_bridge.py` running inside the ROS container.
"""

import argparse
from collections import defaultdict
import json
import math
import socket
import sys
import time

try:
    import hid  # type: ignore
except ImportError:  # pragma: no cover - host dependency
    hid = None


def parse_vec3(text, name):
    parts = [p.strip() for p in str(text).split(",")]
    if len(parts) != 3:
        raise argparse.ArgumentTypeError(f"{name} must be formatted as x,y,z")
    try:
        return [float(p) for p in parts]
    except ValueError as exc:
        raise argparse.ArgumentTypeError(f"{name} must contain numeric values") from exc


def parse_axis_map(text):
    axes = [p.strip().lower() for p in str(text).split(",")]
    if len(axes) != 3 or any(axis not in ("x", "y", "z") for axis in axes):
        raise argparse.ArgumentTypeError("axis map must contain exactly three values from x,y,z")
    return axes


def i16(lo, hi):
    return int.from_bytes(bytes([lo & 0xFF, hi & 0xFF]), byteorder="little", signed=True)


def unpack_vec3(payload, offset=0):
    if len(payload) < offset + 6:
        return None
    return [
        i16(payload[offset + 0], payload[offset + 1]),
        i16(payload[offset + 2], payload[offset + 3]),
        i16(payload[offset + 4], payload[offset + 5]),
    ]


def map_axes(raw, axes):
    idx = {"x": 0, "y": 1, "z": 2}
    return [raw[idx[axis]] for axis in axes]


def normalize_raw(raw, full_scale, deadband):
    values = []
    for value in raw:
        normalized = max(-1.0, min(1.0, float(value) / full_scale))
        values.append(0.0 if abs(normalized) < deadband else normalized)
    return values


def vec_mul(a, b, c):
    return [float(x) * float(y) * float(z) for x, y, z in zip(a, b, c)]


def format_device(info):
    vendor = int(info.get("vendor_id", 0))
    product = int(info.get("product_id", 0))
    manufacturer = str(info.get("manufacturer_string") or "").strip()
    product_name = str(info.get("product_string") or "").strip()
    return f"0x{vendor:04x}:0x{product:04x} {manufacturer} {product_name}".strip()


def enumerate_matches(args):
    if hid is None:
        raise RuntimeError(
            "Python hidapi is not installed on macOS. Install it with: "
            "python3 -m pip install --user hidapi"
        )

    matches = []
    devices = []
    for info in hid.enumerate():
        devices.append(info)
        vendor = int(info.get("vendor_id", 0))
        product = int(info.get("product_id", 0))
        product_name = str(info.get("product_string") or "")
        manufacturer = str(info.get("manufacturer_string") or "")
        if args.vendor_id and vendor != args.vendor_id:
            continue
        if args.product_id and product != args.product_id:
            continue
        haystack = f"{manufacturer} {product_name}".lower()
        if args.name_filter and args.name_filter.lower() not in haystack and "3dconnexion" not in haystack:
            continue
        matches.append(info)
    return matches, devices


def open_device(args):
    matches, devices = enumerate_matches(args)
    if not matches:
        visible = "; ".join(format_device(d) for d in devices) if devices else "none"
        raise RuntimeError(f"No SpaceMouse HID device found. Visible HID devices: {visible}")
    if args.device_index < 0 or args.device_index >= len(matches):
        raise RuntimeError(
            f"SpaceMouse device index {args.device_index} is out of range; "
            f"{len(matches)} matching HID interface(s) are available"
        )
    info = matches[args.device_index]
    dev = hid.device()
    dev.open_path(info["path"])
    dev.set_nonblocking(True)
    return dev, info


class BridgeClient:
    def __init__(self, args):
        self.args = args
        self.sock = None
        self.last_connect_attempt = 0.0

    def connect_if_needed(self):
        if self.sock is not None:
            return True
        now = time.monotonic()
        if now - self.last_connect_attempt < self.args.reconnect_sec:
            return False
        self.last_connect_attempt = now
        try:
            sock = socket.create_connection((self.args.host, self.args.port), timeout=1.0)
            sock.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
            self.sock = sock
            print(f"[ok] Connected to SpaceMouse TCP target bridge at {self.args.host}:{self.args.port}", flush=True)
            return True
        except OSError as exc:
            print(f"[warn] Waiting for TCP target bridge {self.args.host}:{self.args.port}: {exc}", flush=True)
            return False

    def send(self, packet):
        if not self.connect_if_needed():
            return False
        data = (json.dumps(packet, separators=(",", ":")) + "\n").encode("utf-8")
        try:
            self.sock.sendall(data)
            return True
        except OSError as exc:
            print(f"[warn] SpaceMouse TCP send failed; reconnecting: {exc}", flush=True)
            try:
                self.sock.close()
            except OSError:
                pass
            self.sock = None
            return False

    def close(self):
        if self.sock is not None:
            try:
                self.sock.close()
            except OSError:
                pass
            self.sock = None


def packet_from_state(args, translation_raw, rotation_raw, last_motion_time, gripper_cmd, seq):
    stale = (time.monotonic() - last_motion_time) > args.stale_timeout_sec
    translation = [0.0, 0.0, 0.0] if stale else normalize_raw(translation_raw, args.raw_full_scale, args.deadband)
    rotation = [0.0, 0.0, 0.0] if stale else normalize_raw(rotation_raw, args.raw_full_scale, args.deadband)
    linear = vec_mul(map_axes(translation, args.linear_axis_map), args.linear_speed_xyz, args.linear_sign_xyz)
    angular = vec_mul(map_axes(rotation, args.angular_axis_map), args.angular_speed_xyz, args.angular_sign_xyz)
    return {
        "source": "mac_spacemouse_host_bridge",
        "seq": seq,
        "timestamp": time.time(),
        "tracked": True,
        "linear": linear,
        "angular": angular,
        "gripper_cmd": int(gripper_cmd),
    }


def run_synthetic(args):
    client = BridgeClient(args)
    rate_dt = 1.0 / max(1.0, args.publish_rate_hz)
    seq = 0
    started = time.monotonic()
    print("[info] Running synthetic SpaceMouse TCP test.", flush=True)
    try:
        while time.monotonic() - started < args.synthetic_duration_sec:
            phase = time.monotonic() - started
            direction = 1.0 if math.sin(phase * math.pi * 2.0 / max(0.1, args.synthetic_period_sec)) >= 0 else -1.0
            packet = {
                "source": "mac_spacemouse_host_bridge_synthetic",
                "seq": seq,
                "timestamp": time.time(),
                "tracked": True,
                "linear": [args.synthetic_speed * direction, 0.0, 0.0],
                "angular": [0.0, 0.0, 0.0],
                "gripper_cmd": 0,
            }
            client.send(packet)
            seq += 1
            time.sleep(rate_dt)
    finally:
        client.send(
            {
                "source": "mac_spacemouse_host_bridge_synthetic",
                "seq": seq,
                "timestamp": time.time(),
                "tracked": False,
                "linear": [0.0, 0.0, 0.0],
                "angular": [0.0, 0.0, 0.0],
                "gripper_cmd": 0,
            }
        )
        client.close()


def run_bridge(args):
    dev, info = open_device(args)
    print(f"[ok] Opened SpaceMouse HID device: {format_device(info)}", flush=True)

    client = BridgeClient(args)
    rate_dt = 1.0 / max(1.0, args.publish_rate_hz)
    translation_raw = [0.0, 0.0, 0.0]
    rotation_raw = [0.0, 0.0, 0.0]
    last_motion_time = 0.0
    buttons = 0
    last_buttons = 0
    gripper_closed = False
    gripper_cmd = 0
    seq = 0
    last_log = 0.0
    report_counts = defaultdict(int)

    try:
        while True:
            while True:
                data = dev.read(64)
                if not data:
                    break
                report_id = int(data[0])
                payload = data[1:]
                report_counts[report_id] += 1
                if report_id == 1 and len(payload) >= 12:
                    # Some SpaceMouse HID interfaces combine translation and rotation
                    # in one motion report instead of using separate report IDs 1 and 2.
                    translation_raw = unpack_vec3(payload, 0) or translation_raw
                    rotation_raw = unpack_vec3(payload, 6) or rotation_raw
                    last_motion_time = time.monotonic()
                elif report_id in (1, 2) and len(payload) >= 6:
                    vals = unpack_vec3(payload, 0)
                    if vals is None:
                        continue
                    if report_id == 1:
                        translation_raw = vals
                    elif report_id == 2:
                        rotation_raw = vals
                    last_motion_time = time.monotonic()
                elif report_id == 3 and payload:
                    buttons = 0
                    for i, value in enumerate(payload[:4]):
                        buttons |= int(value) << (8 * i)
                    mask = 1 << args.gripper_button_index
                    if (buttons & mask) and not (last_buttons & mask):
                        gripper_closed = not gripper_closed
                        gripper_cmd = 1 if gripper_closed else -1
                        state = "closing" if gripper_closed else "opening"
                        print(f"[info] SpaceMouse gripper toggle: {state}", flush=True)
                    last_buttons = buttons

            packet = packet_from_state(args, translation_raw, rotation_raw, last_motion_time, gripper_cmd, seq)
            client.send(packet)
            seq += 1

            now = time.monotonic()
            if now - last_log > args.log_period_sec:
                last_log = now
                lin = packet["linear"]
                ang = packet["angular"]
                counts = ",".join(f"{k}:{report_counts[k]}" for k in sorted(report_counts))
                print(
                    f"[info] seq={seq} lin=({lin[0]:.3f},{lin[1]:.3f},{lin[2]:.3f}) "
                    f"ang=({ang[0]:.3f},{ang[1]:.3f},{ang[2]:.3f}) gripper_cmd={gripper_cmd} "
                    f"reports={counts or 'none'}",
                    flush=True,
                )
            time.sleep(rate_dt)
    except KeyboardInterrupt:
        print("\n[info] Stopping mac SpaceMouse host bridge.", flush=True)
    except OSError as exc:
        print(f"\n[warn] Stopping mac SpaceMouse host bridge after HID read error: {exc}", flush=True)
    finally:
        try:
            dev.close()
        except Exception:
            pass
        client.close()


def build_arg_parser():
    parser = argparse.ArgumentParser(description="macOS SpaceMouse HID to ROS-container TCP bridge")
    parser.add_argument("--host", default="127.0.0.1", help="TCP target bridge host")
    parser.add_argument("--port", type=int, default=5036, help="TCP target bridge host port")
    parser.add_argument("--vendor-id", type=lambda s: int(s, 0), default=0x256F)
    parser.add_argument("--product-id", type=lambda s: int(s, 0), default=0)
    parser.add_argument("--name-filter", default="SpaceMouse")
    parser.add_argument("--device-index", type=int, default=0, help="Index among matching HID interfaces from --detect-only")
    parser.add_argument("--publish-rate-hz", type=float, default=60.0)
    parser.add_argument("--raw-full-scale", type=float, default=350.0)
    parser.add_argument("--deadband", type=float, default=0.06)
    parser.add_argument("--stale-timeout-sec", type=float, default=0.25)
    parser.add_argument("--reconnect-sec", type=float, default=1.0)
    parser.add_argument("--linear-speed-xyz", type=lambda s: parse_vec3(s, "linear-speed-xyz"), default=[0.35, 0.35, 0.30])
    parser.add_argument("--linear-sign-xyz", type=lambda s: parse_vec3(s, "linear-sign-xyz"), default=[-1.0, -1.0, -1.0])
    parser.add_argument("--linear-axis-map", type=parse_axis_map, default=["x", "y", "z"])
    parser.add_argument("--angular-speed-xyz", type=lambda s: parse_vec3(s, "angular-speed-xyz"), default=[1.4, 1.4, 1.4])
    parser.add_argument("--angular-sign-xyz", type=lambda s: parse_vec3(s, "angular-sign-xyz"), default=[1.0, 1.0, 1.0])
    parser.add_argument("--angular-axis-map", type=parse_axis_map, default=["x", "y", "z"])
    parser.add_argument("--gripper-button-index", type=int, default=0)
    parser.add_argument("--log-period-sec", type=float, default=1.0)
    parser.add_argument("--detect-only", action="store_true", help="List matching HID devices and exit")
    parser.add_argument("--synthetic", action="store_true", help="Do not read HID; send synthetic TCP test packets")
    parser.add_argument("--synthetic-duration-sec", type=float, default=3.0)
    parser.add_argument("--synthetic-period-sec", type=float, default=1.0)
    parser.add_argument("--synthetic-speed", type=float, default=0.18)
    return parser


def main():
    parser = build_arg_parser()
    args = parser.parse_args()

    if args.synthetic:
        run_synthetic(args)
        return 0

    try:
        matches, devices = enumerate_matches(args)
    except RuntimeError as exc:
        print(f"[error] {exc}", file=sys.stderr)
        return 2

    if args.detect_only:
        print(f"visible_hid_devices={len(devices)}")
        match_index = 0
        for info in devices:
            if info in matches:
                prefix = f"MATCH[{match_index}]"
                match_index += 1
            else:
                prefix = "        "
            print(f"{prefix} {format_device(info)}")
        return 0 if matches else 1

    try:
        run_bridge(args)
    except RuntimeError as exc:
        print(f"[error] {exc}", file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
