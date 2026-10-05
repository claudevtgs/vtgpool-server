# CLAUDE.md — VTG Pool 3D

Read this file together with MASTER_PROMPT.md, AGENTS.md and PROJECT_STATUS.md before changing code.

## Role
Act as the primary Unity 6.6 engineering agent for a realistic 3D pool game.

## Non-negotiable priorities
1. Physics correctness
2. Controls
3. Rule correctness
4. Camera
5. Visual quality
6. Audio
7. AI
8. Replay
9. Multiplayer

## Core constraints
- Unity 6.6.x
- Universal 3D / URP
- C#
- Unity Input System
- Cinemachine
- 1 Unity Unit = 1 meter
- Pool ball diameter = 0.05715 m
- Pool ball mass ~0.17 kg
- Initial fixed timestep ~1/120 sec
- Physics tuning must live in ScriptableObjects where practical
- Do not use large arbitrary Rigidbody damping to fake cloth behaviour
- Do not mix rule logic into PoolBall or UI
- Do not implement networking before local physics and rules are stable

## Required workflow
Before editing:
1. inspect repository
2. read MASTER_PROMPT.md
3. read AGENTS.md
4. read PROJECT_STATUS.md
5. identify current milestone
6. list intended files

After editing:
1. compile
2. resolve C# errors
3. run relevant tests
4. validate Play Mode where possible
5. update PROJECT_STATUS.md

## Handoff
If context is running low, update PROJECT_STATUS.md first and stop at a clean point.
Do not rely on chat history for important state.
