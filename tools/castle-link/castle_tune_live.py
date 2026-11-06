"""Live Castle tune decoder.

Reads a USBPcap stream on stdin, saves it to capture.pcap, and every time the
Castle app writes a tune to the ESC, prints which bytes moved and what they
are believed to be. Everything printed also goes to live-log.txt so Claude can
read along while the recording runs.

Run through "Record Castle tune live.bat".
"""
import datetime
import os
import struct
import sys

NAMES = {
    0: "Data Log Enabled (low byte)", 1: "Data Log Enabled (high byte)",
    2: "Full Forward (high)", 3: "Full Forward (low)",
    4: "Full Reverse (high)", 5: "Full Reverse (low)",
    6: "Neutral (high)", 7: "Neutral (low)",
    8: "Boost RPM start (high, x10)", 9: "Boost RPM start (low, x10)",
    10: "Boost RPM end (high, x10)", 11: "Boost RPM end (low, x10)",
    15: "Sensorless Motor Timing",
    16: "Boost Timing Amount (low 7 bits) + throttle mode (top bit)",
    17: "Motor Type", 18: "Drag Brake (/255 x 100 %)", 19: "Drag Brake Ramp",
    20: "Punch Control %", 21: "Max Power %", 22: "Reverse Percentage %",
    23: "Brake Amount %", 24: "Reverse To Forward Brake %",
    25: "Throttle Dead Band (/1000 ms)", 26: "Brake Lockout Time (/128 s)",
    27: "Idle/Error Beep Disable", 28: "Start Power %", 29: "Reverse Type",
    30: "Motor Direction", 32: "Cutoff Voltage (/10 V)",
    33: "Motor Temperature Cutoff (deg C)", 34: "Auxiliary Wire Mode",
    35: "BEC Voltage (/10 V)", 36: "Sample Frequency",
    39: "Turbo Timing Amount", 40: "Turbo Activation Throttle %",
    41: "Turbo Acceleration", 42: "Turbo Deceleration",
    43: "RPM Limit (kRPM)", 44: "Start Power Ramp Time ms",
    45: "Start Power Hold Time ms",
}
TABLES = ["Forward Curve", "Brake Curve", "Reverse Curve", "Table 3 (unknown)",
          "Boost By Throttle"]

folder = sys.argv[1]
os.makedirs(folder, exist_ok=True)
raw = open(os.path.join(folder, "capture.pcap"), "wb", buffering=0)
log = open(os.path.join(folder, "live-log.txt"), "a", encoding="utf-8", buffering=1)


def say(text=""):
    stamp = datetime.datetime.now().strftime("%H:%M:%S")
    print(text, flush=True)
    log.write(f"{stamp} {text}\n")


def read_exact(n):
    buf = b""
    while len(buf) < n:
        chunk = sys.stdin.buffer.read(n - len(buf))
        if not chunk:
            return None
        raw.write(chunk)
        buf += chunk
    return buf


settings = {}          # block -> 8 bytes, as last read or written
curves = {}            # block -> 8 bytes
pending_set = {}       # settings blocks in the write in progress
pending_curve = {}     # curve blocks in the write in progress
write_count = 0
last_request = None
seen_link = False


def settings_bytes(blocks):
    if len(blocks) < 6:
        return None
    return b"".join(blocks[k] for k in range(6))


def finish_settings_write():
    global write_count
    write_count += 1
    before = settings_bytes(settings)
    after_blocks = dict(settings)
    after_blocks.update(pending_set)
    after = settings_bytes(after_blocks)
    say("")
    say(f"=== WRITE {write_count} to the ESC ===")
    if after is None:
        say("  (no full settings yet)")
    elif before is None:
        say("  No starting read was recorded, so here is everything:")
        for i, v in enumerate(after):
            say(f"  byte {i:2d} = {v:3d}   {NAMES.get(i, 'UNKNOWN')}")
    else:
        moved = [(i, a, b) for i, (a, b) in enumerate(zip(before, after)) if a != b]
        if not moved:
            say("  No settings bytes moved.")
        for i, a, b in moved:
            flag = "" if i in NAMES else "   <-- NEW, not mapped yet"
            say(f"  byte {i:2d}: {a:3d} -> {b:3d}   {NAMES.get(i, 'UNKNOWN')}{flag}")
    settings.update(pending_set)
    pending_set.clear()


def finish_curve_write():
    moved = sorted({k // 64 for k, v in pending_curve.items() if curves.get(k) != v})
    if moved:
        say("  Curves changed: " + ", ".join(TABLES[t] for t in moved))
    else:
        say("  No curves changed.")
    curves.update(pending_curve)
    pending_curve.clear()
    say("Ready for the next change.")


def on_host(frame):
    global last_request
    cmd = frame[1]
    block = frame[3] * 256 + frame[4]
    if cmd != 0x0d and cmd != 0x0e and pending_curve:
        finish_curve_write()
    if cmd == 0x0b:
        pending_set[block] = frame[5:]
    elif cmd == 0x0c:
        finish_settings_write()
    elif cmd == 0x0d:
        pending_curve[block] = frame[5:]
        if block == 319:
            finish_curve_write()
    last_request = frame


def on_device(frame):
    global seen_link
    if not seen_link:
        seen_link = True
        say("Castle Link seen. Recording is working.")
    req = last_request
    if req is None or frame[1] != req[1]:
        return
    block = req[3] * 256 + req[4]
    if req[1] == 0x06 and not pending_set:
        settings[block] = frame[5:]
    elif req[1] == 0x0e and not pending_curve:
        curves[block] = frame[5:]


header = read_exact(24)
if header is None:
    say("The USB recorder sent nothing. Is USBPcap installed?")
    sys.exit(1)
say("RECORDING. Leave this window open.")
say("Open the Castle app and connect (or reconnect) to the ESC.")
say("Then change ONE setting and write it to the ESC. Repeat.")
say("Close this window when you are finished.")

while True:
    rec = read_exact(16)
    if rec is None:
        break
    _, _, incl, _ = struct.unpack("<IIII", rec)
    packet = read_exact(incl)
    if packet is None:
        break
    if len(packet) < 27:
        continue
    header_len = struct.unpack("<H", packet[0:2])[0]
    endpoint = packet[21]
    transfer = packet[22]
    data = packet[header_len:]
    if transfer != 3 or len(data) != 13 or data[0] != 0x43:
        continue
    if endpoint & 0x80:
        on_device(data)
    else:
        on_host(data)
