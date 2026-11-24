# Configuration Schema (Schema)

The configuration file is located at:
`%LOCALAPPDATA%\TaskbarEmberOverlay\settings.json`

## JSON Schema Specification

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "TaskbarEmberOverlaySettings",
  "type": "object",
  "properties": {
    "targetFps": {
      "type": "integer",
      "minimum": 10,
      "maximum": 60,
      "default": 30,
      "description": "Target frame rate cap for procedural ember animation."
    },
    "edgeThickness": {
      "type": "number",
      "minimum": 1.0,
      "maximum": 12.0,
      "default": 3.5,
      "description": "Perimeter band thickness in WPF DIPs."
    },
    "intensity": {
      "type": "number",
      "minimum": 0.1,
      "maximum": 2.0,
      "default": 1.0,
      "description": "Overall brightness and glow multiplier."
    },
    "reducedMotion": {
      "type": ["boolean", "null"],
      "default": null,
      "description": "Force static mode (true), force animation (false), or auto-detect Windows settings (null)."
    },
    "showSparks": {
      "type": "boolean",
      "default": true,
      "description": "Whether to render drifting sparks within the edge band."
    },
    "showCracks": {
      "type": "boolean",
      "default": true,
      "description": "Whether to render molten crackle line segments along the edge."
    },
    "glowRadius": {
      "type": "number",
      "minimum": 0.0,
      "maximum": 10.0,
      "default": 3.0,
      "description": "Blur/glow radius along the edge cracks."
    },
    "primaryColor": {
      "type": "string",
      "pattern": "^#[0-9A-Fa-f]{6}$",
      "default": "#FF5722",
      "description": "Hex color for ember orange."
    },
    "hotColor": {
      "type": "string",
      "pattern": "^#[0-9A-Fa-f]{6}$",
      "default": "#FFE082",
      "description": "Hex color for hot molten crack cores."
    },
    "baseRedColor": {
      "type": "string",
      "pattern": "^#[0-9A-Fa-f]{6}$",
      "default": "#B71C1C",
      "description": "Hex color for deep ember red ambient."
    }
  },
  "additionalProperties": false
}
```

## Storage & Fallback Rules
- If file does not exist, default settings are generated and saved atomically.
- If JSON parsing fails or values are out of bounds, robust clamped fallbacks are used without crashing.
- No registry entries are written or modified.
