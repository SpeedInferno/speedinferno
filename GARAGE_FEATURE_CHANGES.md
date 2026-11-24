# Garage (Car + Map Selection) and Tail Lights – Change Log

Project: **Inferno** (Unity 6, URP)
Branch: `feature/garage-car-selection` (based on `unity-project`)
Date: 24 Nov 2026

---

## 1. What the feature does

1. The player presses **Play** on the Title screen, and the new **Garage** screen opens.
2. The player selects one of **2 cars** (Altima, GT-R) and one of **2 maps** (Map 1, Map 2).
3. The player presses **RACE**. The Loading screen loads the selected map, and the selected car is spawned in it.
4. Tail lights are now a **separate object** on each car. They switch on while the drift button or **Q** is held. **Brake is not added**; it can be added later without changing how the lamps work.

New scene flow:

```
Title --(Play)--> Garage --(Race)--> Loading1 --> MainGame (Map 1) or Map2 (Map 2)
                     \--(Back)--> Title
```

Index order used everywhere: cars `0 = Altima`, `1 = GT-R`; maps `0 = Map 1 (MainGame)`, `1 = Map 2 (Map2)`.

---

## 2. New files

| File | Purpose |
|---|---|
| `Assets/Scripts/Manager/GameSelection.cs` | Static class that holds the selected car and map. It is saved in `PlayerPrefs` (`Garage_SelectedCar`, `Garage_SelectedMap`), so the last choice is remembered. Also holds the car names, map names and map scene names (`MainGame`, `Map2`). |
| `Assets/Scripts/Manager/GarageController.cs` | Garage screen logic: car and map buttons, orange highlight on the selected buttons, shows the matching preview model on the turntable, rotates the turntable, label "Car \| Map", **RACE** (loads `Loading1`) and **Back** (loads `Title`). |
| `Assets/Scripts/Manager/CarSpawner.cs` | Placed in each race scene. Reads the Garage selection in `Awake`. If the Altima is selected, the scene's existing Altima is kept untouched, including its per-scene tuning. If another car is selected, it hides the placed Altima, spawns the selected car prefab at the same position and rotation, connects the scene's HUD and buttons (`rightBtn`, `leftBtn`, `driftBtn`, `nitroBtn`, `speedText`, `gearNoText`, `hudElement`, `tireTrail`), points the `DriftController` target correctly, then removes the Altima. |
| `Assets/Scripts/Helper/TailLights.cs` | Separate tail-light component. `SetDrift(bool)` switches it on and off. It can drive: **emissive renderers** (lamp mesh glows through `_EmissionColor` using a MaterialPropertyBlock, so the material asset is never changed), **show-when-on renderers** (lamp pieces only visible when on) and **lights** (red glow on the road). Settings: `glowColor`, `glowIntensity`. |
| `Assets/Scenes/Garage.unity` | New Garage scene: camera, lights, floor, turntable with the two preview models (`Preview_Altima`, `Preview_GTR`), UI canvas (1920×1080 scaling) with GARAGE title, BACK, SELECT CAR (ALTIMA / GT-R), SELECT MAP (MAP 1 / MAP 2), RACE, and `GarageController`. |
| `Assets/PreFab/GTR.prefab` | GT-R car: a **prefab variant of `FixedAltima.prefab`**, so it inherits the driving, nitro, drift, sounds, camera anchors and tag `Player`. Altima body mesh hidden; GT-R body (`GTR_Body`) added; the four wheel meshes replaced with the GT-R's; wheel colliders moved to the GT-R's wheel positions; nitro flames moved to the GT-R exhausts; body collider resized; tail lights use the GT-R's own `Taillight` mesh. |
| `Assets/Material/TailLightMat.mat` | URP Lit, dark red, emission enabled (black when off). Used by both cars' tail lamps. |
| `Assets/Material/GarageFloorMat.mat` | Garage floor material. |
| `Assets/Material/GarageTurntableMat.mat` | Garage turntable material. |
| `Assets/Cars/GTR/GTR.fbx` (+ `Assets/Cars.meta`, `Assets/Cars/GTR.meta`) | GT-R model, **moved** from `Assets/Delete/GTR.fbx`. Same asset ID, so existing references (e.g. `TestCamera` scene) still work. |
| `GARAGE_FEATURE_CHANGES.md` | This document. |
| `NITRO_CAMERA_CHANGES.md` | Change log for the earlier nitro camera work (same file as on the `docs/nitro-camera-readme` branch). |

---

## 3. Modified files

### Scripts

| File | Change |
|---|---|
| `Assets/Scripts/Controller/PlayerCarController.cs` | Removed `brakeMat`, `brakeColor`, `brakeColorIntensity` (never worked: `brakeMat` was empty in both maps). Added private `TailLights tailLights`, found in `Start()` with `GetComponentInChildren<TailLights>(true)`. In `fun_ApplyDrift()`: drift on → `tailLights.SetDrift(true)`, drift off → `tailLights.SetDrift(false)`. `driftController` now comes from `GetComponent<DriftController>()` instead of `GameObject.Find("FixedAltima")`. |
| `Assets/Scripts/Controller/DriftController.cs` | `playerCar = GetComponent<PlayerCarController>()` instead of `GameObject.Find("FixedAltima")`, so it works on any car. |
| `Assets/Scripts/Controller/NewCameraContoller.cs` | `Start()` now finds the active car with `FindAnyObjectByType<PlayerCarController>()` (not by name), and takes `camPivot` and `camDefAnchor` from **that** car. This way the camera follows the spawned car. The nitro camera behaviour is unchanged. |
| `Assets/Scripts/Manager/SceneManagerCustom.cs` | `fun_LoadMainGame()` loads `GameSelection.SelectedMapScene` instead of always `"MainGame"`. Added `fun_LoadGarage()` (loads `"Garage"`). `fun_loadMapSelection()` kept but no longer used. |

### Prefabs

| File | Change |
|---|---|
| `Assets/PreFab/FixedAltima.prefab` | Added child **`TailLights`** with a `TailLights` component: `LampL` / `LampR` lamp pieces at the Altima's rear lamps (±0.635, 0.835, −2.40), hidden until on, using `TailLightMat`. `TailGlow` red point light (intensity 1.6, range 3.5), off until on. |

### Scenes

| File | Change |
|---|---|
| `Assets/Scenes/Title.unity` | `btn_StartGame` (**Play**) now calls `SceneManagerCustom.fun_LoadGarage` (was `fun_loadMapSelection`). |
| `Assets/Scenes/MainGame.unity` | Added `CarSpawner` object: `sceneCar = FixedAltima`, `carPrefabs = [FixedAltima, GTR]`, `sceneCarIndex = 0`. |
| `Assets/Scenes/Map2.unity` | Same `CarSpawner` setup as MainGame. (Large diff because Unity re-saved the scene file in its current format.) |

### Project settings / packages

| File | Change |
|---|---|
| `ProjectSettings/EditorBuildSettings.asset` | `Assets/Scenes/Garage.unity` added to the build, right after `Title`. |
| `ProjectSettings/VersionControlSettings.asset` | Mode `Unity Version Control` → `Hidden Meta Files` (Unity changed this locally, not part of the feature). |
| `Packages/packages-lock.json` | `com.unity.collab-proxy` (Unity Version Control package) removed from the lock file (local change, not part of the feature). |

### Removed

| File | Change |
|---|---|
| `Assets/Delete/GTR.fbx` (+ `.meta`) | Moved to `Assets/Cars/GTR/GTR.fbx`. |

---

## 4. Inspector settings

**`TailLights` (child of each car)**
- `emissiveRenderers`: GT-R → `GTR_Body/Taillight`; Altima → none
- `showWhenOnRenderers`: Altima → `LampL`, `LampR`; GT-R → none
- `lights`: `TailGlow` (GT-R intensity 0.7, range 3)
- `glowColor` (red), `glowIntensity` (6)

**`CarSpawner` (MainGame, Map2)**
- `sceneCar`, `carPrefabs` (Garage order), `sceneCarIndex`

**`GarageController` (Garage)**
- Buttons, previews, `selectionText`, `turntable`, `turntableSpeed` (20), `normalColor` / `selectedColor`, `loadingScene` (`Loading1`), `titleScene` (`Title`)

**GT-R handling**
Edit `Assets/PreFab/GTR.prefab`. It currently inherits the Altima prefab values, e.g. max speed 180. In MainGame the placed Altima has its own override (150), which is kept when the Altima is selected.

---

## 5. Tested (Unity editor, stepping frames in Play mode)

| Test | Result |
|---|---|
| GT-R selected, MainGame | GT-R spawned at the Altima's spot, HUD and buttons connected, camera follows the GT-R |
| Drift held / released on GT-R | Tail lamp glow + red light on, then off |
| Garage: select GT-R / Altima / Map 2 | Correct preview, orange highlight, label "Altima \| Map 2" |
| Garage → RACE → Loading1 → Map2 (GT-R) | Map2 loaded, one car (GT-R), camera following, no errors |
| Altima, MainGame | Altima kept (max speed 150 override intact); lamps on during drift, off after |

---

## 6. Notes

- **Last selection is used everywhere:** pressing Play directly in `MainGame` or `Map2` in the editor uses the last Garage selection.
- **Old map selection screen:** `MapSelection.unity` is still in the project and build list, but nothing opens it any more.
- **Not committed:** `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset` changes. That is TextMesh Pro's dynamic font atlas, regenerated by Unity automatically.
- **Version control mode:** for git, Unity recommends **Visible Meta Files** (Edit → Project Settings → Version Control). It is currently set to Hidden Meta Files.
