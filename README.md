# VTG Pool 3D

A realistic 3D pool game project for Unity 6.6.x using URP.

## Target Modes
- 8-Ball / 15-ball rack
- 9-Ball

## Platforms
- Windows
- WebGL
- Android
- iOS-ready architecture

## Play locally or against AI
Open `Assets/_PoolGame/Scenes/02_Match.unity` and enter Play mode.
Press **P** to open the pause menu. Choose **8-ball vs AI** or **9-ball vs AI**;
click **New AI: ...** first to cycle the difficulty for the new match.
Player 1 is human, Player 2 is the computer. **Restart match** retains that choice
and alternates the breaker. The local two-player buttons return both seats to human control.

AI currently handles direct pots, breaks and ball in hand. Advanced position play,
banks and safety planning are still in development; see `PROJECT_STATUS.md`.

## Unity Setup
Recommended:
- Unity 6.6.x
- Template: Universal 3D
- Color Space: Linear
- Input System
- Cinemachine

## Initial Physics
- 1 Unity Unit = 1 meter
- Ball diameter = 0.05715 m
- Ball mass ≈ 0.17 kg
- Fixed timestep ≈ 1/120 sec

## Documents
Read in this order:
1. MASTER_PROMPT.md
2. CLAUDE.md
3. AGENTS.md
4. PROJECT_STATUS.md

## First Agent Command
Ask your coding agent:

> Read MASTER_PROMPT.md, CLAUDE.md, AGENTS.md and PROJECT_STATUS.md completely. Audit the Unity project and start Milestone 1 only. Do not implement AI, multiplayer, rules or menus until the physics prototype is stable.

## Development Priority
Physics > Controls > Rules > Camera > Visuals > Audio > AI > Replay > Multiplayer
