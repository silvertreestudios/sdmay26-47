# Tactical RPG Engine

This project aims to implement a Pathfinder 2e inspired, turn-based tactical RPG engine built in Unity. It adapts the core mechanics of the **Pathfinder 2e** ruleset into a 3D tactical environment, featuring a custom grid, dynamic camera systems, and a character creation suite.

![Header Image](Assets/_Project/Art/UI/UI%20Elements/ingamescreenshot.png)

---

## Features Implemented

### Tactical Combat Engine
- **3-Action Economy:** Full implementation of the action system with waypoint-based movement planning.
- **Combat Logic:** Weapon-derived strikes, Multiple Attack Penalty (MAP) calculation, and reaction priority stacks (Attacks of Opportunity).
- **Rules Integration:** Flanking and Off-Guard logic, proficiency scaling, and comprehensive Resistance/Weakness/Immunity handling.
- **Senses & Conditions:** Detection logic coupled with conditions (Sickened, Unconscious, etc.).

### Magic & Spellcasting System
- **Modular Architecture:** A flexible spell system supporting geometric area solvers (cones, bursts, lines).
- **Visuals & Physics:** Ballistic projectile physics, multi-phase animation VFX, and persistent environmental effects.

### Grid & Environment
- **3D Voxel Grid:** A multi-layered grid system supporting complex 3D line-of-sight and cover mechanics.
- **Dynamic Worlds:** Support for grids of any shape, interactable objects (doors), and stealth-based actions.
- **Auras:** Real-time aura emitters.

### Enemy Artificial Intelligence
- **Tactical Evaluation:** An AI manager that evaluates grid positions to fight the player units.
- **Target Prioritization:** Heuristic-based targeting using available weapons and pathfinding costs.

### Character Creation & Visuals
- **Visual Customization:** A UI suite for visual character appearance and equipment selection.
- **Ruleset Integration:** Implementation of PF2e Backgrounds, Ancestries, Classes, and Attribute Boosts.
- **Per-Unit Data:** Unique weapon, armor, and spell loadouts for every unit.

### Visuals, UI, & Narrative
- **Controller Optimized:** Modern menu systems built with UI Toolkit, fully navigable via gamepad.
- **Narrative Engine:** JSON-scripted cinematic sequences and a real-time tactical HUD.
- **Animations:** Custom animation state machines integrated with the turn-based logic.

### Tools
- **Backend:** A service-oriented architecture with JSON-based data management and a save/load system.
- **Input & Feedback:** Haptic-enabled input system and integrated music playback management.
- **Developer Tools:** Portrait generators, diagnostic debuggers, and a data importer for external Pathfinder JSON sets.

---

**Summary:** By the end of development, the project included a complete turn-based gameplay loop, advanced PF2e mechanics, tactical enemy AI, save functionality, a scripted narrative, and two playable levels.

---

## Technical Stack

- **Engine:** Unity 6000 (URP - Universal Render Pipeline)
- **UI Architecture:** Unity UI Toolkit (USS/UXML).
- **Camera:** Cinemachine.
- **Input:** Unity Input System.
- **Patterns:** Service Locator, Event-Sourced Character Ledgers, and ScriptableObject-driven data architecture.

---

## Getting Started

1. **Clone the Repository:**
   ```bash
   git clone https://github.com/silvertreestudios/sdmay26-47.git
   ```
2. **Open in Unity:** Use Unity Hub to open the project. Ensure you have the required URP and New Input System packages installed.
3. **Primary Scenes:**
   - `Assets/_Project/Scenes/MainMenu.unity`: Start here for the full experience.
   - `Assets/_Project/Scenes/CharacterCreator.unity`: The character creation suite.
   - `Assets/_Project/Scenes/Story Scene.unity`: The introduction story.
   - `Assets/_Project/Scenes/Level 1.unity`: Level 1 tactical combat.
   - `Assets/_Project/Scenes/Story_Victory_1.unity`: The victory story after level 1.
   - `Assets/_Project/Scenes/Level 2.unity`: Level 2 tactical combat.
   - `Assets/_Project/Scenes/Story_Victory_2.unity`: The victory story after level 2.
   - `Assets/_Project/Scenes/Story_Defeat_1.unity`: The defeat story if you lose a level.

---

## Controls:
![Controls](Assets/_Project/Art/UI/Controls.png)


---

## License

This project is released under the **ORC License** (Open RPG Creative License). See the License in-game or the included documentation for full legal details regarding Pathfinder compatibility and open gaming content.

---

