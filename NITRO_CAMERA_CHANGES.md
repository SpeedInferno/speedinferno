# Nitro Camera – Change Log

Project: **Inferno** (Unity, URP)
Date: 22 Nov 2026
Scope: camera behaviour during nitro, plus camera smoothness

---

## 1. Summary

| Item | Status |
|---|---|
| Changed file | `Assets/Scripts/Controller/NewCameraContoller.cs` |
| New files | None |
| Other scripts changed | None (`PlayerCarController.cs` is untouched) |
| Scene / prefab changes | None, apart from the new Inspector fields that appear on the camera rig |
| Compile status | Compiles with no errors |
| Play-mode test | **Not yet verified** – needs a manual check (see section 8) |

The camera now reacts to nitro in a way modelled on **Asphalt 9**. When nitro fires, it quickly pulls back and widens its field of view. While nitro runs, it holds a slightly closer distance. When nitro ends, it eases back. The camera also turns more smoothly and trails the car a little in turns and drifts.

---

## 2. Scene setup (for reference)

The changes depend on this existing hierarchy. Nothing in it was changed.

```
CameraPivot            <- has NewCameraContoller (the "camera rig", no parent)
 └─ Main Camera        <- local pos (-0.02, 1.50, -3.06), tilt 15°, FOV 60

FixedAltima            <- the player car (PlayerCarController, Rigidbody)
 ├─ camPivot           <- follow target, local pos (0, 0.35, -0.64)
 └─ camDefAnchor       <- start position, local pos (0, 0.35, -2.14)
```

- The rig follows `camPivot` every physics step (`FixedUpdate`, 50 Hz).
- Main Camera sits about 3 units behind the rig and tilts down 15°, so moving or rotating the rig moves the view around the car.
- Nitro state is read from `PlayerCarController.nitroStart`. It is `true` for as long as nitro is running.

---

## 3. How the camera behaves now

### Nitro timeline

```
Extra distance behind normal position
 1.6 ┤    ╭──╮
     │   ╱    ╲___
 1.0 ┤  ╱         ‾‾‾‾‾‾‾‾‾‾‾‾‾‾╲
     │ ╱                         ╲
 0.0 ┼╯────────────────────────────╲_______
     │  ↑      ↑                 ↑      ↑
     │ press  1 s           nitro ends  back to normal (~1 s later)
```

| Phase | What happens | Default timing |
|---|---|---|
| **Nitro pressed** | Camera pulls back quickly to **1.6** units behind normal. FOV widens from 60 to **72**. The motion eases out, so there is no hard stop. | Settles in about 0.3 s |
| **After 1 s, nitro still on** | Camera eases in to **1.0** unit behind normal and holds steady. FOV comes down to about **67.5**. | Eases over about 0.7 s |
| **Nitro ends** | Camera and FOV drift back to the normal position and 60. | About 1 s |
| **Nitro pressed again** | The sequence restarts from the pull-back. | – |

"Behind" means opposite the car's facing direction, kept level, so the camera never dips or rises during the pull-back.

### Steering, turns and drifts

The camera turns toward the car's **direction of travel** (its velocity) rather than snapping to the car's nose. In turns and drifts it trails slightly, so the car appears to rotate in the frame, as it does in Asphalt 9. Below about 7 km/h, or when reversing, it follows the car's facing direction instead.

---

## 4. Detailed changes in `NewCameraContoller.cs`

### 4.1 New fields

```csharp
private Rigidbody playerRB;   // car's Rigidbody, used for travel direction
private Camera mainCam;       // child Main Camera, used for field of view

[Header("Nitro Camera")]
[SerializeField] float nitroKickBackDistance = 1.6f;
[SerializeField] float nitroHoldDistance     = 1.0f;
[SerializeField] float nitroKickDuration     = 1.0f;
[SerializeField] float kickBackTime          = 0.12f;
[SerializeField] float settleTime            = 0.35f;
[SerializeField] float returnTime            = 0.45f;
[SerializeField] float nitroFovBoost         = 12f;

[Header("Camera Rotation")]
[SerializeField] float rotationFollowSpeed   = 6f;

private float nitroOffset = 0f, nitroOffsetVel = 0f, nitroTimer = 0f, baseFov = 60f;
private bool  wasNitroActive = false;
```

### 4.2 `Start()`

It now also caches the car's `Rigidbody` and the child `Camera`, and records the camera's starting FOV as `baseFov`. So if the FOV is changed on Main Camera, the nitro boost is added on top of the new value.

### 4.3 `FixedUpdate()`

| Before | After |
|---|---|
| `Lerp` toward `cameraPivot.transform.position` | `Lerp` toward `fun_GetNitroTargetPosition()` (the pivot, moved back by the nitro offset) |
| `transform.LookAt(cameraPivot.transform.position)` | `fun_UpdateRotation()` – smooth turn toward travel direction |
| – | `fun_UpdateFov()` – field-of-view boost |

### 4.4 `fun_changeCameraLook()`: the one change to existing logic

```csharp
if(playerScript.nitroStart)
    currentMaxSmoothTime = 0.85f;   // was 0.43f
```

**Why:** at 0.43, the camera lagged far behind the car during nitro. The amount depended on speed; at 300 km/h it was more than 2 units. That lag stacked on top of the new controlled pull-back, so the motion was uneven and the distance was hard to predict. At 0.85, only a light speed lag remains, and the pull-back is controlled by the new settings. The other branches (speed > 5 and speed < 5) are unchanged.

### 4.5 New method: `fun_GetNitroTargetPosition()`

1. Detects the moment nitro turns on and resets a timer.
2. Picks a target distance and smoothing time:
   - nitro on, less than 1 s → `nitroKickBackDistance` with `kickBackTime`
   - nitro on, 1 s or more → `nitroHoldDistance` with `settleTime`
   - nitro off → `0` with `returnTime`
3. Moves the current distance toward the target with `Mathf.SmoothDamp`. This gives eased starts and stops, with no linear slide and no overshoot.
4. Returns `camPivot` position + (level backward direction of the car × distance).

### 4.6 New method: `fun_UpdateRotation()`

This replaces `transform.LookAt(cameraPivot...)`.

**Why:** the rig sits almost on top of `camPivot`; the distance measured in play mode was 0.0007 units. Aiming at a point that close gives an unreliable direction, so the camera angle could shake. The new method:
- uses the car's velocity direction when it is moving forward faster than about 7 km/h, and otherwise the car's facing direction;
- keeps the direction level;
- turns toward it smoothly, with frame-rate-independent smoothing controlled by `rotationFollowSpeed`.

### 4.7 New method: `fun_UpdateFov()`

FOV = `baseFov + nitroFovBoost × (current pull-back ÷ nitroKickBackDistance)`

The FOV follows the pull-back exactly, so widening and distance always move together. This is the main source of the speed effect in Asphalt-style cameras.

---

## 5. Inspector settings (on `CameraPivot`)

| Setting | Default | What it does | To make it… |
|---|---|---|---|
| `nitroKickBackDistance` | 1.6 | Extra distance behind normal right after pressing nitro | Bigger pull-back → raise |
| `nitroHoldDistance` | 1.0 | Extra distance kept while nitro keeps running | Stay further out → raise |
| `nitroKickDuration` | 1.0 s | How long the full pull-back lasts before easing in | Longer kick → raise |
| `kickBackTime` | 0.12 | Smoothing of the pull-back (≈ settles in 2.5× this) | Snappier → lower |
| `settleTime` | 0.35 | Smoothing when easing from pull-back to hold | Slower ease-in → raise |
| `returnTime` | 0.45 | Smoothing when returning after nitro | Slower return → raise |
| `nitroFovBoost` | 12° | Extra FOV at full pull-back | Stronger speed effect → raise (stay under ~18 to avoid distortion) |
| `rotationFollowSpeed` | 6 | How quickly the camera turns to follow the car | Tighter follow → raise; more drift swing → lower |

These are saved with the scene, so values changed in the Inspector override the defaults in code.

---

## 6. Reference used

The behaviour is based on a side-by-side clip of **Asphalt 9 / Asphalt 8 / NFS No Limits** (15 s, 60 fps). Frames from the Asphalt 9 nitro moments, around 1–4 s and 10–13 s, showed:

- the car shrinks in the frame within about 0.3 s of nitro firing: a fast, eased pull-back;
- the road and edges spread out at the same time: the FOV widens;
- the camera holds steady while nitro runs, with no jitter;
- the camera returns slowly, over about 1 s, after nitro ends;
- in drifts the camera trails the direction of travel, so the car turns in the frame.

The original request, quick pull-back and then a partial return after 1 s while nitro is still on, is kept through `nitroKickDuration` and `nitroHoldDistance`.

---

## 7. Change history

| # | Change | Result |
|---|---|---|
| 1 | Added nitro pull-back: 3 units at press, 1.2 units after 1 s, return at nitro end. Linear easing. | Superseded by #2 |
| 2 | Reworked for Asphalt 9 feel: smaller eased pull-back (1.6 / 1.0) with `SmoothDamp`, FOV boost, smoother rotation, reduced nitro lag (0.43 → 0.85). | **Current version** |

---

## 8. How to test

1. Press **Play**.
2. **Click inside the Game view.** Unity only reads keyboard input, and only runs the game, while the Game view has focus.
3. Drive forward and press **E** to start nitro, then press **E** again to stop it.
4. Watch for: a quick pull-back and wider view, an ease-in after 1 s, and a smooth return when nitro stops.
5. Adjust the Inspector settings in section 5 if needed.

---

## 9. Known issues and open items (not changed)

| Issue | Cause | Proposed fix |
|---|---|---|
| **On-screen Nitro button does nothing** | In `PlayerCarController.fun_ApplyNiro()` the touch-button code is commented out ("Uncomment before using on android"). Only the **E** key turns nitro on. | Two small edits in `fun_ApplyNiro()`, shown below. **Not applied** – it changes the car script and needs approval. |
| **Slight judder at speed** | The car's Rigidbody has **Interpolation = None**, so the car only moves 50 times a second while the screen draws faster. | Set **Interpolate** on the `FixedAltima` Rigidbody. **Not applied** – it is a car setting, not camera. |

Proposed Nitro button fix (in `fun_ApplyNiro()`):

```csharp
// replace:  if( nitoInput = Input.GetKeyDown(KeyCode.E))
bool nitroBtnClicked = (nitroBtn != null && nitroBtn.isPressedToggle != nitroStart);
if( (nitoInput = Input.GetKeyDown(KeyCode.E)) || nitroBtnClicked)

// add at the end of fun_ApplyNiro(), so the button stays in step
// when nitro runs out or is cancelled by drifting
if(nitroBtn != null) nitroBtn.isPressedToggle = nitroStart;
```

---

## 10. How to revert

To restore the original camera behaviour in `NewCameraContoller.cs`:

1. In `FixedUpdate()`, change the target back to `cameraPivot.transform.position`, replace `fun_UpdateRotation()` with `transform.LookAt(cameraPivot.transform.position);`, and remove `fun_UpdateFov();`.
2. In `fun_changeCameraLook()`, set the nitro value back to `0.43f`.
3. Optionally delete the new fields and the three new methods.
4. Reset the Main Camera FOV to 60 if it was left changed.
