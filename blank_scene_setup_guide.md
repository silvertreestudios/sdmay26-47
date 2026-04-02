# Level Designer Guide: Blank Scene Setup 
This guide details how to construct a fully functional Pathfinder 2e tactical level entirely from scratch without using any pre-existing Prefabs. Follow the exact hierarchy below to ensure the Core Architecture initializes without `NullReferenceExceptions`.

---

## 1. The Main Camera Setup
The Camera Controller powers both Free-Cam and over-the-shoulder action cameras.
1. Select the `Main Camera`.
2. Add a `CinemachineBrain` component to it.
3. Create an empty GameObject named `[CAMERA_RIG]`.
4. Add the `CameraController (Script)` to it.
5. Add a `CinemachineOrbitalFollow` component to it (required by CameraController).
6. Under `[CAMERA_RIG]`, create an empty child named `VirtualCamera` and add a `CinemachineCamera` component. Link this to the `virtualCamera` slot on the CameraController.

---

## 2. The Core Systems Container
The `ServiceLocator` manages all dependencies. You must create these exact Managers in the scene.
1. Create an empty GameObject named `[CORE_SYSTEMS]`.
2. Attach the `ServiceLocator (Script)` to it.
3. Attach the following System Managers to `[CORE_SYSTEMS]` (or individual child objects, but they must exist in the scene):
   - `GridSystem`: Configures the tile size and X/Z bounds.
   - `PhaseManager`: Controls the state machine (ActionSelection vs FreeMovement).
   - `TurnManager`: Controls the turn order array.
   - `UnitActionSystem`: The central hub for player commands.
   - `TargetLockService` & `TargetingService`: Handles mouse-picking enemies.
   - `ReactionManager`: Stack-based reactor for Attacks of Opportunity.
   - `EnemyAIManager`: The brain for enemy factions.
   - `InputService`: Bootstraps the new Unity Input System.
   - `CompendiumRegistry`: Bootstraps the PF2E item lookups.

---

## 3. The Geometry & Visualizers
1. Create a `[ENVIRONMENT]` empty GameObject to hold your floors and walls. Ensure walls have accurate colliders and crates are layered properly for Line of Sight mathematically.
2. Create an empty GameObject named `[GRID_VISUALIZERS]`.
3. Attach `MoveRangeVisualizer (Script)` and assign the Blue and Red tile Material Prefabs to its slots.
4. Attach `SpellAoEVisualizer (Script)` for spell template drawing.
5. Attach `GridCursor (Script)` to a quad or decal object that will follow the mouse.

---

## 4. The UI Canvas
Create a standard Unity `UI > Canvas` and name it `[MAIN_UI]`.
You must build the following UI elements and attach these controller scripts:

1. **Turn Order Bar:** Create a horizontal layout group at the top of the screen. Attach `TurnSystemUI (Script)`.
2. **Action Bar:** Create a container for buttons at the bottom. Attach `UnitActionSystemUI (Script)`. 
   * *Note: You'll need an `ActionButtonUI` prefab to slot into this, which it will dynamically duplicate based on the selected unit's actions.*
3. **Selected Unit Info:** Create a panel in the bottom-left with TextMeshPro elements for HP and Name. Attach `SelectedUnitUI (Script)`.
4. **Enemy Tooltip:** Create a floating panel in the top-right. Attach `UnitTooltipUI (Script)`.
5. **Phase Status:** Create a text element for Turn notifications. Attach `PhaseUI` (if applicable) or wire it to `UIManager.cs`.

---

## 5. Building a Unit From Scratch
Do not just drag a 3D model into the scene! A Pathfinder Unit requires a strict component stack to participate in the game loop.

**Step A: The Root Object**
1. Create an empty GameObject named `Unit_Fighter`.
2. Assign the Layer to `Unit`.
3. Add a `CharacterController` component (adjust height and radius to fit).
4. Add the following logic components:
   - `Unit` (Set Faction to Player or Enemy)
   - `UnitMovement` (Controls gravity and path following)
   - `UnitConditions` (Mandatory dependency for Health)
   - `UnitHealth` (Set Base Max HP)
   - `UnitEquipment` (Assign Weapon ScriptableObjects to hands)
   - `UnitActionEconomy` (Controls the 3 Action Points)
   - `UnitStealth` (Set base stealth skill modifier)
5. Add specific Action components (e.g., `MeleeAction`, `RangedAction`, `ReactiveStrike`).

**Step B: The Visual Model**
1. Drag your humanoid 3D model FBX so it is a **Child** of `Unit_Fighter`.
2. Ensure the model has an `Animator` component set to Humanoid.
3. Attach the `UnitVisuals (Script)` alongside the Animator.
4. *Critical*: Open the Animation clips and ensure `AnimEvent_StrikeConnects` and `AnimEvent_ActionComplete` are plotted on the attack timelines!
