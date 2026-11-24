# Arcade Drift System – Change Log

Project: **Inferno** (Unity 6, URP)
Branch: `feature/arcade-drift` (based on `unity-project` @ `69ceb84`)
Date: 24 Nov 2026
Scope: Asphalt-style arcade drift for the player car, plus the vehicle-physics changes it depends on. Camera code is **not** changed.

---

## 1. Summary

| Item | Status |
|---|---|
| New scripts | `ArcadeDriftController.cs` (drift system), `DriftTelemetry.cs`, `DriftAutopilot.cs`, `DriftTestPad.cs` (debug/test tools) |
| Changed scripts | `PlayerCarController.cs` only |
| Prefab changes | `FixedAltima.prefab`: drift components added, collision ContinuousDynamic, `maxSpeed` 220. `GTR.prefab` inherits all of it (prefab variant). |
| Scene changes | `TestingMao.unity`: removed the `maxSpeed = 140` override so the prefab's 220 applies |
| Camera | **Untouched.** `NewCameraContoller.cs` is the `unity-project` version (nitro camera + spawned-car lookup). |
| Tested | 14 scripted test runs plus re-tests in `TestingMao` (local project, before the merge with `unity-project`), see section 7 |
| Needs a check in Unity | Open the project on this branch, let it compile, then drive `TestingMao` and `MainGame`. The merge with the garage/tail-light work was done as a text merge and has **not** been compiled in Unity yet. |

What the player gets:

- **Hold drift (Q / drift button) and steer** above 40 km/h: the car rotates into a controlled slide in about 0.4 s and holds about 33° (18–35° depending on steering), losing about 10% speed.
- **Steer the other way while still holding drift** to flick straight into the opposite drift (chain).
- **Release drift**: the car straightens and catches its line in about 0.35 s, without snapping.
- **Spins are caught automatically.**
- **Normal (non-drift) driving is unchanged below 100 km/h.** Above that, a stability assist keeps the car planted up to 220 km/h.
- **Smoke, tyre trails, tail lights and nitro refill** follow the real drift instead of the Q key.

---

## 2. How it fits together

```
FixedAltima (and GTR, which is a prefab variant)
 ├─ PlayerCarController   execution order 0    existing controller: engine, gears, steering, nitro, friction tables
 ├─ ArcadeDriftController execution order 50   NEW: drift state machine; sets grip/drive multipliers on the
 │                                              controller, adds yaw torque, front-wheel counter-steer, speed
 │                                              control, recovery, stability assist, effects
 └─ DriftTelemetry        execution order 100  NEW (debug): read-only overlay + CSV recorder
```

`ArcadeDriftController` never replaces `PlayerCarController`. It runs after it in every physics step and works through a small set of hooks the controller exposes (section 4). With every `apply…` toggle off, the car drives exactly like the old Q-drift.

State machine (one value, `driftBlend` 0→1, scales every physics change):

```
Normal ──(drift held + steer ≥ 0.3 + ≥ 40 km/h + grounded)──► Initiation ──(angle ≥ 70% target or timeout)──► Active
   ▲                                                               │                                         │
   └──────────── Recovery ◄──(drift released / too slow / straight / full counter-steer / airborne)──────────┘
                   ▲     (steer firmly the other way while holding drift = chain → Initiation, opposite direction)
SpinRecovery ──────┘     (entered from any drift state if angle > 60° or yaw rate > 117°/s)
```

---

## 3. New files

| File | Purpose |
|---|---|
| `Assets/Scripts/Controller/ArcadeDriftController.cs` (+ `.meta`) | The drift system. It detects drift states (Normal, Initiation, Active, Recovery, SpinRecovery), measures drift angle and yaw rate every physics step, and drives these subsystems, each with its own Inspector toggle:<br>• **Rear grip loss:** rear side grip goes down to ×0.5 at full drift.<br>• **Drive scale:** rear torque ×0.7 while drifting (replaces the old −200 Nm brake).<br>• **Yaw assist:** a cascaded angle → yaw-rate → yaw-acceleration controller, using torque in `ForceMode.Acceleration`.<br>• **Front-wheel counter-steer:** the wheels point along the direction of travel, plus player trim.<br>• **Speed control:** about 10% loss over 2 s at any speed, plus sideways damping.<br>• **Velocity alignment** during Recovery.<br>• **Spin catch.**<br>• **Chain flick.**<br>• **Speed curves** for 130–220 km/h.<br>• **High-speed stability** for normal driving: a turn-rate cap of 1.5 g / speed, a slip catch above 4°, and rear grip ×1.15 above 100 km/h.<br>• **Drift effects:** smoke, trails, tail lights, nitro refill and nitro cut.<br>It also has an on-screen debug overlay (`showOverlay`). |
| `Assets/Scripts/DebugTools.meta` | Folder asset for the debug tools. |
| `Assets/Scripts/DebugTools/DriftTelemetry.cs` (+ `.meta`) | **Read-only** telemetry. It never writes to the car.<br>• **Keys:** F1 shows or hides the overlay, F9 starts or stops recording, F10 resets peaks.<br>• **CSV contents:** one row per physics step with speed, forward/sideways velocity, drift angle, yaw rate, steer angle, torque, rear grip, per-wheel grounded/slip, drift state/blend/angle/target, yaw assist, travel turn rate, per-wheel smoke flags, nitro level and refill flag.<br>• **File location:** `<Project>/Telemetry/drift_<label>_<time>.csv` in the Editor, `persistentDataPath/Telemetry` in builds. |
| `Assets/Scripts/DebugTools/DriftAutopilot.cs` (+ `.meta`) | **Test helper (Editor only in practice).** Drives the car by pressing the on-screen buttons, through scenarios: Straight, Corner, DriftQ, DriftRelease, CounterSteerExit, SpinKick, Chain. It starts and stops `DriftTelemetry` recording.<br>Its `#if UNITY_EDITOR` bootstrap reads `<Project>/Telemetry/autopilot_request.txt` at Play start (`scenario;triggerSpeed;label[;hold[;spinKick[;maxSpeed]]]`) and does nothing if the file is missing or empty. Used for every test in section 7. Safe to delete. |
| `Assets/Scripts/DebugTools/DriftTestPad.cs` (+ `.meta`) | **Test helper (Editor only, compiled out of builds).** If `<Project>/Telemetry/testpad_request.txt` contains a height, it moves `FixedAltima` to that height at Play start, without saving the scene, so tests can run on the flat pad at y = 100. Empty or missing file = no effect. Safe to delete. |
| `ARCADE_DRIFT_CHANGES.md` | This document. |

---

## 4. Modified files

### `Assets/Scripts/Controller/PlayerCarController.cs`

Every drift edit is tagged `// [DRIFT STEP n]` in the code.

| Step | Change | Effect with the drift system off |
|---|---|---|
| 2 | Friction is now applied once per physics step in `FixedUpdate` (it used to be applied in `Update` via `fun_ApplyDrift`). The front and rear axles are written separately, with stiffness = Inspector base × multiplier. Adds `SetAxleGripMultipliers(frontSide, rearSide, frontFwd, rearFwd)` and `ResetAxleGripMultipliers()`. | Identical (all multipliers = 1) |
| 3 | Read-only input properties: `RawSteerInput` (−1…1, buttons or keyboard) and `DriftInputHeld`. | None |
| 4 | `[HideInInspector] useNormalFrictionInDrift`: when true, the old all-wheel drift friction set is skipped and the drift system lowers rear grip itself. | Identical (false) |
| 5 | `[HideInInspector] useDriftDriveScale` and `driveTorqueScale`: when true, holding drift no longer applies −200 Nm; positive rear torque is scaled instead (`fun_ApplyDriftDriveScale()`). | Identical (false) |
| 13 | `useExternalDriftEffects`, `externalDriftActive`, `SetDriftEffects(fl, fr, rl, rr, brakeLight)`, `AddNitro(amount)`. When the drift system drives effects, `fun_ApplyDrift()` only sets `driftMode`; smoke, trails and tail lights come from `SetDriftEffects`, and the nitro cut uses `externalDriftActive`. `AddNitro` never goes above `nitroMaxCapacity` (the old refill could). | Identical (false) |
| 14 | `steerReturnRate` (Inspector, default 6/s): when no steering key is held, steering returns to centre at this rate instead of snapping to 0 in one physics step (the snap was a 36–42° wheel jump). 0 = old snap. | Steering release is smooth |

**Merge with `unity-project`.** The drift work was done in a local copy of the project that also contained an **Input System migration** that is not on GitHub. The drift code reads input through it, so it is part of this branch:

- **Keyboard input** is built in code (`InputAction`): A/D or the arrow keys steer, Q drifts, E fires nitro. It replaces `Input.GetAxis("Horizontal")` / `Input.GetKey(KeyCode.Q)` / `Input.GetKeyDown(KeyCode.E)`. The project already has `com.unity.inputsystem` 1.20.1 and Active Input Handling = Both.
- **`motorPowerDelay`** (Inspector, set to 1 s on the prefab): engine power starts this long after the scene loads.
- **The old `DriftController` steering override** in `fun_ApplySteering()` (`currentYawAngle > 42`) is commented out, and `driftController` is no longer looked up. The `ArcadeDriftController` counter-steer replaces it.

From `unity-project`, kept as is:

- **Tail lights:** the `TailLights` component (`tailLights`, found in `Start()`), with `SetDrift(true/false)` in `fun_ApplyDrift()`. The old `brakeMat` / `brakeColor` / `brakeColorIntensity` fields stay removed. The Step 13 effects now switch the tail lights through `tailLights.SetDrift()` as well, so they glow together with the drift smoke.

### `Assets/PreFab/FixedAltima.prefab`

Built from the `unity-project` version (which keeps the garage `TailLights` child and the `DriftController` component), with only these additions:

| Change | Why |
|---|---|
| Added `ArcadeDriftController` component, with all values as tuned and tested (`maxSpeedCorrection` 7, `rearGripMultiplier` 0.5, `driftDriveScale` 0.7, `maxLateralG` 1.5, `highSpeedRearGrip` 1.15, …) | The drift system |
| Added `DriftTelemetry` component (`showOverlay` off) | Debug overlay and recorder. Disable or remove it for release if not wanted. |
| Rigidbody `m_CollisionDetection` Discrete → **ContinuousDynamic** | Stops the car tunnelling through thin colliders at 200+ km/h |
| `PlayerCarController.maxSpeed` 180 → **220** | Top-speed target for the final prototype |
| `PlayerCarController.motorPowerDelay` = 1, `steerReturnRate` = 6 | Values from the tested local setup |

`Assets/PreFab/GTR.prefab` is a prefab variant of `FixedAltima.prefab`, so the GT-R gets all of the above automatically. Its own overrides (body, wheels, colliders) are unchanged.

### `Assets/Scenes/TestingMao.unity`

| Change | Why |
|---|---|
| Removed the `FixedAltima` override `maxSpeed = 140` | So the scene uses the prefab's 220, as in the tested setup. Nothing else in the scene changed. |

---

## 5. Inspector reference (`ArcadeDriftController`)

| Group | Key settings (default) |
|---|---|
| Entry | `minDriftSpeed` 40 km/h, `initiationSteerThreshold` 0.3, `entryDuration` 0.25 s |
| Drift angle targets | `neutralDriftAngle` 18°, `maxDriftAngle` 35°, `counterSteerAngle` 5°, `targetAngleRate` 50°/s |
| Exit | `exitSpeed` 30 km/h, `exitAngle` 5°, `recoveryDuration` 0.5 s, `allowChainFlick` on, `chainSteerThreshold` 0.6, `chainDelay` 0.15 s, `driftSteerResponse` 6/s |
| Spin detection | `spinAngle` 60°, `yawRateLimit` 90°/s, `spinMaxYawAccel` 900°/s², `spinGripBlend` 0.25, `spinReentryCooldown` 0.5 s |
| Speed curves | `yawRateBySpeed` / `yawAccelBySpeed`: 1 up to 130 km/h, 0.75 at 220; `rearGripLossBySpeed`: flat 1 |
| Rear grip | `applyRearGrip`, `rearGripMultiplier` 0.5 |
| Drive during drift | `applyDriveScale`, `driftDriveScale` 0.7 |
| Yaw assistance | `applyYawAssist`, `angleGain` 4, `initiationGainBoost` 1.5, `maxRelativeYawRate` 90°/s, `rateGain` 8, `maxYawAccel` 460°/s², `recoveryAngleGain` 2, `spinRateGain` 12 |
| Front-wheel counter-steer | `applyFrontCounterSteer`, `counterSteerFactor` 0.9, `playerSteerTrim` 12°, `maxWheelAngle` 45° |
| Speed control | `applySpeedControl`, `targetSpeedLossPercent` 10, `speedLossWindow` 2 s, `speedTrackGain` 6, `maxSpeedCorrection` 7 m/s², `lateralDamping` 0.5 |
| Recovery | `applyVelocityAlignment`, `velocityAlignRate` 75°/s |
| High-speed stability | `applyStability`, `stabilityStartSpeed` 100, `stabilityFullSpeed` 140 km/h, `maxLateralG` 1.5, `stabilitySlipDeadband` 4°, `stabilitySlipGain` 4, `stabilityRateGain` 8, `stabilityMaxYawAccel` 300°/s², `highSpeedRearGrip` 1.15, `highSpeedRearGripFade` 20 km/h |
| Drift effects | `applyDriftEffects`, `effectsMinAngle` 8°, `effectsMinBlend` 0.3, `frontEffectsSlip` 0.45, `driftNitroRefillRate` 5/s, `nitroRefillMinAngle` 10° |
| Debug | `showOverlay` |

Suggested tuning order:

1. `rearGripMultiplier`
2. `maxDriftAngle` / `neutralDriftAngle`
3. `angleGain` / `maxYawAccel`
4. `targetSpeedLossPercent`
5. `velocityAlignRate`
6. `maxLateralG`

---

## 6. Development steps (for reference)

| Step | What was added |
|---|---|
| 1 | `DriftTelemetry` (measurement only) and baseline recordings |
| 2 | Per-axle friction application and grip multipliers (no behaviour change) |
| 3 | `ArcadeDriftController` state machine, detection only |
| 4 | Rear-only grip loss driven by `driftBlend` |
| 5 | Drive scale during drift instead of the −200 Nm brake |
| 6 | Yaw assist (cascaded, acceleration-limited) |
| 7 | Front-wheel counter-steer |
| 8 | Speed control (~10% loss at any speed) and sideways damping |
| 9 | Recovery with velocity alignment |
| 10 | Spin detection and SpinRecovery |
| 11 | Speed curves, chain flick, faster drift steering response |
| 12 | 220 km/h scaling: continuous collision, stronger speed correction; high-speed stability assist and rear grip bias (12c) |
| 13 | Smoke, trails, tail lights and nitro refill follow the drift state (camera untouched) |
| 14 | Full test pass; smooth steering return on release |

---

## 7. Test results (local project, Step 14)

All tests used `DriftAutopilot` on the flat test pad (y = 100) in `TestingMao`. No collisions or crashes occurred.

| Test | Result |
|---|---|
| Straight line | 0–100 km/h in 10.1 s, 0–200 km/h in 14.8 s, top speed 220.5 km/h, slip < 0.1° |
| Normal corners at 40 / 80 / 150 / 210 km/h | Max slip 1.4° / 3.0° / 1.7° / 2.1°; no drift states, no smoke |
| Drifts at 64 / 94 / 158 / 215 km/h | 30° reached in 0.72–0.82 s, held at 32–34° (target 33°), peak 37–39°, speed lost while held 10.0–11.0%, back under 5° in 0.32–0.40 s after release, no spins |
| Counter-steer exit, 90 km/h | Target angle drops to ~25°, clean exit, 0.7° overshoot |
| Spin catch (300°/s kick), 90 km/h | Peak 89°, caught below 30° in 1.08 s |
| Chain flick, 60 and 90 km/h | Left drift into right drift, held at ~36°, clean exit |
| Q held at 30–40 km/h | Stays Normal, no smoke, no nitro refill |
| Steering release (after the Step 14 fix) | Largest single-step wheel change 5.3° at 80 km/h and 4.6° at 210 km/h (was 40.7° and 35.9°); corner grip unchanged |

---

## 8. Known issues and follow-ups

1. **Compile and play-test this branch in Unity first.** The merge with the garage/tail-light work was done as a text merge and has not been compiled in Unity.
2. **`MainGame.unity` overrides `maxSpeed` = 150** on its `FixedAltima`. Change or remove that override if the race map should reach 220 km/h. (Left unchanged on purpose: it is per-map tuning.)
3. **The `DriftController` component on `FixedAltima.prefab`** (from `unity-project`) has an empty `targetObject` in the prefab; `CarSpawner` assigns it at runtime. `PlayerCarController` no longer reads it, because the Input System migration commented that out. If it causes errors or rotates the car in a scene, disable it: `ArcadeDriftController` replaces its job.
4. **Rear wheelspin below 100 km/h:** rear forward slip is about 0.96 under power (existing friction tables). Not changed; needs a decision.
5. **On-screen steering buttons take ~1.5 s to reach full input** (`ButtonSetUp` sensitivity 2). Out of scope for this branch.
6. **`nitroMaxCapacity` is 100 in the scene**, so the drift refill has no visible effect while the tank is full.
7. **The test helpers** (`DriftAutopilot`, `DriftTestPad`) and the `Telemetry/` output folder are for development only. `Telemetry/` and `Backups/` are local folders and are not part of this branch.
8. **Not included** (local-only, unrelated to drift):
   - one extra line in `Assets/Scripts/ReferneceScript/CarController.cs` (`playerRB.centerOfMass = …`);
   - local edits to `CanvasNew.prefab` and `EventSystem.prefab`;
   - the local `NewCameraContoller.cs`, which is **older** than `unity-project`'s and would remove the nitro camera.
