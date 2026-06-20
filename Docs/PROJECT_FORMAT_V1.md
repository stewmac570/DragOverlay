# DragOverlay project format v1

DragOverlay project files use the `.dragoverlay` extension and are ZIP packages. Version 1 stores original input files without modification so later DragOverlay versions can reparse them using improved loaders.

Issue #85 defines the manifest only. ZIP save/load behavior is implemented separately.

## Package layout

```text
project.dragoverlay
├─ project.json
├─ logs/
│  ├─ castle-slot-1.csv
│  └─ racebox-slot-1.csv
└─ tunes/
   └─ castle-slot-1.dat
```

Package paths:

- are relative to the package root;
- use `/` separators;
- cannot contain empty, `.` or `..` segments;
- cannot be absolute paths, drive-qualified paths, or backslash paths;
- use stable slot-based names to avoid collisions.

## Manifest

`project.json` is UTF-8 JSON. The current schema version is defined by `ProjectFormat.CurrentSchemaVersion`.

Unknown JSON properties are ignored for forward compatibility. A schema version newer than the application supports is rejected as `UnsupportedVersion`; it is not silently treated as version 1.

The manifest records:

- application build and UTC creation time;
- Drag or Speed mode;
- channel visibility;
- every occupied Castle and RaceBox slot;
- original display filename and package source path;
- run visibility and time-shift offset in milliseconds;
- optional Castle tune path;
- manual controller-style radio settings.

Castle UI slots 1–3 use plot slots 1–3. RaceBox UI slots 1–3 use plot slots 4–6.

## Example `project.json`

```json
{
  "schemaVersion": 1,
  "appBuild": "1.13",
  "createdUtc": "2026-06-20T02:00:00+00:00",
  "runMode": "Drag",
  "channelVisibility": {
    "RPM": true,
    "Throttle %": true,
    "RaceBox Speed": false
  },
  "runs": [
    {
      "sourceType": "Castle",
      "uiSlot": 1,
      "plotSlot": 1,
      "displayFileName": "castle-run.csv",
      "sourcePath": "logs/castle-slot-1.csv",
      "isVisible": true,
      "timeShiftMs": 42.5,
      "tunePath": "tunes/castle-slot-1.dat",
      "radioSettings": {
        "profileName": "Race",
        "throttleSpeedMode": "Mode 3",
        "mode": 3,
        "speedType": "Normal",
        "highTurnPercent": 68,
        "highReturnPercent": 100,
        "point2Percent": 60,
        "middleTurnPercent": 36,
        "middleReturnPercent": 100,
        "point1Percent": 19,
        "lowTurnPercent": 100,
        "lowReturnPercent": 100
      }
    },
    {
      "sourceType": "RaceBox",
      "uiSlot": 1,
      "plotSlot": 4,
      "displayFileName": "racebox-run.csv",
      "sourcePath": "logs/racebox-slot-1.csv",
      "isVisible": false,
      "timeShiftMs": -15.0
    }
  ]
}
```

## Version 1 exclusions

Version 1 does not require derived plot data, parsed telemetry caches, screenshots, or original absolute disk paths.
