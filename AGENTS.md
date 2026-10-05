# AGENTS.md — VTG Pool 3D

## Project
Realistic 3D pool game built with Unity 6.6.x and URP.

## Supported agents
This repository may be edited by:
- Claude Code
- OpenAI Codex
- Cursor
- other coding agents

Every agent must read:
1. MASTER_PROMPT.md
2. CLAUDE.md
3. PROJECT_STATUS.md
4. README.md

before making substantial changes.

## Architecture rules
Keep systems separated:
- Core
- Physics
- Balls
- Cue
- Aiming
- Camera
- Match
- Rules
- AI
- Replay
- Audio
- VFX
- UI
- Input
- Save
- Multiplayer

Do not create a monolithic GameManager.

## Physics rules
- scale is real-world
- ball diameter 0.05715 m
- mass ~0.17 kg
- initial physics Hz ~120
- Rigidbody damping is not the primary cloth model
- sliding, rolling, spin and cushion behaviour must be intentional
- use Continuous Dynamic where appropriate for pool balls
- test high-speed break shots

## Code rules
- C#
- focused classes
- no unnecessary LINQ in hot loops
- no repeated GetComponent in Update
- no per-frame allocation in physics loops
- cache references
- use ScriptableObject configs
- add validation through OnValidate when useful

## Required documentation updates
After significant work update PROJECT_STATUS.md:
- milestone
- completed
- in progress
- changed files
- tuning values
- known issues
- next steps
- test instructions

## Git
Do not modify or commit:
- Library/
- Temp/
- Logs/
- Obj/
- Builds/
- UserSettings/

Source of truth:
- Assets/
- Packages/
- ProjectSettings/
- repository markdown docs
