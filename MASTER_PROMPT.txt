# MASTER PROMPT — VTG POOL 3D

## PROJECT
- Engine: Unity 6.6.x
- Template: Universal 3D (URP)
- Language: C#
- Target: Windows, WebGL, Android; architecture-ready for iOS
- Modes: 8-Ball / 15-ball rack, 9-Ball
- Style: Premium realistic 3D billiards simulator
- Priority: Physics > Controls > Rules > Camera > Visuals > Audio > AI > Replay > Multiplayer

You are the Lead Unity Game Engineer, Gameplay Programmer, Physics Engineer,
Technical Artist, UI/UX Designer, AI Engineer and Game Architect for this project.

Your task is to build a complete, maintainable, production-oriented 3D pool game.
Do not create a throwaway prototype.

Visual/gameplay references only:
- 8 Ball Pool: readable UX, fast aiming, simple controls
- Pool Nation FX: premium lighting, materials, presentation
- Virtual Pool 4: simulator camera, realistic aiming and shot feel

Do not copy proprietary assets, source code, textures, UI or branding.

---

# 1. PRODUCT VISION

Create a realistic but accessible pool game that feels like standing over a professional table.

The game must reward:
- shot angle
- power control
- spin
- cue-ball positioning
- safety play
- table knowledge

Avoid arcade-only behaviour and avoid fake physics whenever a physically meaningful solution is practical.

---

# 2. REQUIRED GAME MODES

## 2.1 8-Ball
Use:
- cue ball
- solids 1–7
- 8-ball
- stripes 9–15

Support configurable rules:
- legal break
- open table
- group assignment
- first legal contact
- legal pocket
- scratch
- ball-in-hand
- early 8-ball loss
- legal 8-ball win
- turn switching
- foul resolution

Use a modular RuleEngine.
Do not hard-code 8-ball rules inside PoolBall or UI scripts.

## 2.2 9-Ball
Use:
- cue ball
- balls 1–9
- diamond rack
- ball 1 at front
- ball 9 in center

Rules:
- lowest numbered ball must be contacted first
- combinations are allowed
- scratch handling
- ball-in-hand
- legal 9-ball pocket validation
- break rules
- turn switching
- win detection

---

# 3. PROJECT ARCHITECTURE

Use modular systems:

- Bootstrap
- GameManager
- MatchManager
- TurnManager
- RuleEngine
- PoolPhysicsSystem
- ShotController
- AimSystem
- CueController
- CueBallSpinController
- BallManager
- RackManager
- PocketManager
- CameraController
- AIController
- ReplaySystem
- AudioManager
- VFXManager
- UIManager
- InputManager
- SaveSystem

Use interfaces where they reduce coupling:
- IRuleSet
- IShotInput
- IBallPhysics
- IAIPlayer
- IMatchPlayer

Never create a giant manager that owns every responsibility.

---

# 4. FOLDER STRUCTURE

Assets/
└── _PoolGame/
    ├── Art/
    │   ├── Balls/
    │   ├── Cue/
    │   ├── Environment/
    │   ├── Materials/
    │   ├── Tables/
    │   ├── Textures/
    │   └── VFX/
    ├── Audio/
    │   ├── BallHits/
    │   ├── CushionHits/
    │   ├── Cue/
    │   ├── Pocket/
    │   └── UI/
    ├── Input/
    ├── Materials/
    │   └── Physics/
    ├── Prefabs/
    │   ├── Balls/
    │   ├── Cue/
    │   ├── Pockets/
    │   ├── Tables/
    │   └── UI/
    ├── Scenes/
    ├── Scripts/
    │   ├── AI/
    │   ├── Aiming/
    │   ├── Audio/
    │   ├── Balls/
    │   ├── Camera/
    │   ├── Core/
    │   ├── Cue/
    │   ├── Input/
    │   ├── Match/
    │   ├── Multiplayer/
    │   ├── Physics/
    │   ├── Replay/
    │   ├── Rules/
    │   │   ├── EightBall/
    │   │   └── NineBall/
    │   ├── Save/
    │   ├── UI/
    │   ├── Utilities/
    │   └── VFX/
    ├── ScriptableObjects/
    └── Tests/
        ├── EditMode/
        └── PlayMode/

---

# 5. UNITY PROJECT INITIALIZATION

Use:
- Unity 6.6.x
- Universal 3D template
- URP
- Linear color space
- Input System package
- Cinemachine package

Avoid starting with HDRP.

Initial project name suggestion:
VTG-Pool-3D

Recommended path:
C:\Project\VTG-Pool-3D

---

# 6. INPUT SYSTEM

Use Unity Input System only.

Create:
Assets/_PoolGame/Input/PoolInput.inputactions

Action Map: Gameplay

Actions:
- Aim
- Look
- Zoom
- SetPower
- Shoot
- Spin
- CameraTop
- CameraCue
- CameraTactical
- Cancel
- Pause

Support:
- Mouse
- Touch
- future Gamepad

Gameplay code must not depend directly on a specific input device.

---

# 7. PHYSICS CONFIGURATION

Use:
1 Unity Unit = 1 meter

Pool ball:
- diameter: 0.05715 m
- radius: 0.028575 m
- mass: ~0.17 kg

Initial fixed timestep:
0.008333333 seconds (~120 Hz)

Benchmark later:
- 1/60
- 1/90
- 1/120

Initial Physics solver:
- Solver Iterations: 10
- Solver Velocity Iterations: 4

Gravity:
(0, -9.81, 0)

Use higher precision only when justified by profiling.

---

# 8. PHYSICS LAYERS

Create layers:
- Ball
- CueBall
- Cushion
- Table
- Pocket
- Cue
- AimHelper
- Environment
- UI3D

Configure collision matrix intentionally.

Cue visual animation must NOT directly rely on the cue collider to physically strike the ball.
ShotController should apply a controlled impulse at the correct moment.

---

# 9. BALL RIGIDBODY

Initial Rigidbody settings:
- Mass: 0.17
- Use Gravity: true
- Is Kinematic: false
- Interpolation: Interpolate
- Collision Detection: Continuous Dynamic
- Linear Damping: 0
- Angular Damping: 0

Do not fake cloth resistance using arbitrary high damping.

---

# 10. PHYSICS MATERIALS

Create:
- PM_Ball
- PM_Cloth
- PM_Cushion

These are helper materials only.
Do not rely on PhysicMaterial alone for realistic pool behaviour.

Main billiards-specific physics must be implemented in PoolPhysicsSystem.

---

# 11. BALL PHYSICS SYSTEM

Physics quality is a critical requirement.

Model ball states:
- Stationary
- Sliding
- Rolling
- Spinning
- Pocketed

Implement:
- sliding friction
- sliding-to-rolling transition
- rolling resistance
- spin decay
- ball-ball collision
- cushion interaction
- side-spin influence
- pocket-jaw interaction

Pure rolling relation should be physically meaningful:
v ≈ ω × r

Do not fake rotation only at the visual mesh level.

---

# 12. BALL COMPONENTS

Create:
PoolBall.cs

Data:
- BallId
- BallType
- Rigidbody
- SphereCollider
- CurrentState
- LinearVelocity
- AngularVelocity
- IsPocketed

Separate physics and visual definitions.

Use:
BallDefinition ScriptableObject

---

# 13. BALL PHYSICS PROFILE

Create:
BallPhysicsProfile ScriptableObject

Expose:
- ballMass
- ballRadius
- ballRestitution
- slidingFriction
- rollingResistance
- spinFriction
- cushionRestitution
- cushionTangentialFriction
- minimumLinearVelocity
- minimumAngularVelocity
- sleepThreshold

Most tuning must be editable without recompilation.

---

# 14. SPIN SYSTEM

Represent cue strike offset as Vector2.

Support:
- center
- top
- bottom
- left
- right
- top-left
- top-right
- bottom-left
- bottom-right

Cue strike must generate:
- linear impulse
- angular impulse

Implement real:
- follow
- draw
- stun
- side spin

Do not fake these as post-collision direction changes.

---

# 15. REQUIRED PHYSICS TESTS

Create:
03_PhysicsTest.unity

Presets:
- Straight Shot
- Stop Shot
- Follow Shot
- Draw Shot
- Left English
- Right English
- Rail Bounce
- Ball Collision
- Break Shot
- Pocket Jaw Test

Tests must be repeatable.

---

# 16. CUSHION PHYSICS

Do not use perfect mirror reflection only.

Model:
- normal restitution
- tangential friction
- spin interaction
- impact speed

Side spin must influence cushion exit angle.

Create:
CushionSurface configuration.

---

# 17. POCKET PHYSICS

A pocket requires:
- mouth region
- jaw colliders
- drop region
- trigger
- capture logic

Desired sequence:
ball reaches pocket mouth
→ interacts with jaws
→ enters drop region
→ falls below table
→ sound plays
→ state becomes Pocketed

Do not simply delete a ball when it crosses a circle.

---

# 18. SHOT STATE MACHINE

Use explicit match/shot states:

- Preparing
- Aiming
- PowerSelection
- Shooting
- BallsMoving
- ResolvingShot
- TurnTransition
- GameOver

Player cannot shoot while any active ball is moving.

Flow:
AIM
→ SELECT SPIN
→ SET POWER
→ FINE AIM
→ SHOOT
→ BALLS MOVING
→ WAIT UNTIL REST
→ RULE EVALUATION
→ NEXT TURN

---

# 19. CUE SYSTEM

Visual sequence:
- align
- draw back
- accelerate
- contact moment
- apply physics impulse
- retract

Use:
AnimationCurve PowerToCueVelocity

Do not map input power linearly without testing.

---

# 20. AIMING SYSTEM

Implement:
- cue direction line
- first collision prediction
- ghost ball
- optional object-ball route
- optional cue-ball post-contact route

AimAssistLevel:
- None
- Minimal
- Standard
- Training

Competitive mode may disable advanced guides.

---

# 21. GHOST BALL

Calculate real geometric impact position using ball radius.

Do not simply draw a line through the target ball.

Render:
- translucent ghost cue ball
- impact marker
- short predicted lines

Avoid excessive visual clutter.

---

# 22. CAMERA SYSTEM

Use Cinemachine.

Required camera modes:
- Tactical
- Cue
- Top
- Cinematic / Replay

Transitions must be smooth.

Desktop:
- mouse orbit
- right-drag look
- wheel zoom

Mobile:
- one-finger aim
- two-finger zoom
- touch camera
- UI power control

UI input must not conflict with camera input.

---

# 23. VISUAL TARGET

Use URP first.

Scene direction:
- premium modern billiards lounge
- table is visual focus
- dark neutral environment
- strong readable table lighting
- green or tournament-blue cloth
- realistic wood and metal
- physically convincing pool balls

Avoid:
- toy-like plastic appearance
- excessive bloom
- excessive darkness
- heavy motion blur while aiming

---

# 24. MATERIALS

Ball material:
- PBR
- smooth surface
- readable numbers
- reflection probe support
- subtle clear-coat feel

Cloth:
- base map
- normal map
- micro texture
- realistic roughness

Table:
- wood
- rubber cushions
- metal corners
- high-quality normals where useful

---

# 25. GRAPHICS QUALITY

Profiles:
- Low
- Medium
- High
- Ultra

Initial target:
- PC: 60+ FPS
- High-end PC: aim for 120 FPS where feasible
- Android: stable 60 FPS target
- WebGL: stable playable performance

Adjust:
- shadows
- MSAA
- reflection quality
- post processing
- light quality

Initial anti-aliasing:
MSAA 4x

Color Space:
Linear

---

# 26. AUDIO

Create AudioManager.

Sounds:
- cue strike
- ball-ball
- cushion
- pocket
- rolling
- break
- UI
- foul
- victory

Collision volume depends on impulse.

Use small pitch/sample variation to avoid repetition.

---

# 27. VFX

Keep realistic.

Good:
- subtle chalk dust
- subtle cloth particles
- subtle pocket dust
- restrained win effect

Avoid giant arcade explosions in default mode.

---

# 28. UI

Gameplay HUD:
- player info
- turn indicator
- remaining balls
- current mode
- power
- spin
- camera selector
- pause/menu

Keep table unobstructed.

8-ball HUD:
- solids/stripes
- pocketed balls
- current player

9-ball HUD:
- highlight lowest legal target

---

# 29. RACK SYSTEM

Procedural rack placement.

8-ball:
triangle rack.

9-ball:
diamond rack.

Use ball diameter mathematically.

Avoid overlapping positions.

Use tiny configurable rack gap only if needed.

---

# 30. EVENT SYSTEM

Events:
- ShotStarted
- BallHit
- CushionHit
- BallPocketed
- CueBallPocketed
- BallsStopped
- FoulCommitted
- TurnChanged
- GameWon

Avoid hard coupling between systems.

---

# 31. SHOT RECORD

Record every shot:
- playerId
- cueBallPosition
- aimDirection
- power
- cueOffset
- initialCueVelocity
- initialAngularVelocity
- ballsPocketed
- firstObjectBallHit
- cushionHits
- foul
- result

This will support:
- replay
- statistics
- AI
- multiplayer

---

# 32. REPLAY SYSTEM

Architecture must support:
- replay previous shot
- original camera
- top camera
- cinematic camera
- future slow motion

Do not tightly couple replay to gameplay UI.

---

# 33. AI

AI must not cheat.

Pipeline:
1. determine legal target balls
2. enumerate candidate pockets
3. determine potting lines
4. calculate ghost-ball position
5. test obstruction
6. calculate aim
7. estimate power
8. predict cue-ball result
9. score candidates
10. choose shot

Scoring factors:
- pot probability
- angle
- distance
- obstruction
- cue-ball position
- scratch risk
- next-shot quality
- rail difficulty
- spin requirement
- power requirement

AIProfile ScriptableObject owns weights.

Difficulty:
- Beginner
- Intermediate
- Advanced
- Expert

Difficulty must use human-like aim/power/planning error.
Do not make all AI perfect.

Advanced AI may later include safety play and 2–3 shot planning.

---

# 34. DEBUG TOOLS

Development-only overlay:
- FPS
- physics Hz
- linear velocity
- angular velocity
- ball state
- shot power
- spin
- first contact
- contacts

Debug drawing:
- velocity vectors
- angular velocity
- collision normals
- ghost-ball position
- aim rays
- AI candidates

Disable in release builds.

---

# 35. TESTS

EditMode:
- ghost-ball math
- ray/sphere obstruction
- rack placement
- rules

PlayMode:
- ball eventually rests
- ball pocket detection
- rack initialization
- shot flow
- camera state
- input lock while balls move

Rule tests:
- legal 8-ball
- wrong first ball
- scratch
- early 8-ball
- legal 9-ball contact
- illegal 9-ball contact
- win conditions

---

# 36. PERFORMANCE RULES

Avoid:
- FindObjectOfType in Update
- repeated GetComponent in hot loops
- LINQ in physics loops
- per-frame allocations
- recreating LineRenderer every frame

Use:
- caching
- object pools
- event subscriptions
- preallocated buffers

Profile before optimizing.

---

# 37. SAVE SYSTEM

Store:
- audio
- graphics
- camera preference
- aim assist preference
- player stats

Player stats:
- matches played
- wins
- win rate
- balls pocketed
- break success
- average shots
- fouls
- win streak
- 8-ball wins
- 9-ball wins

---

# 38. MULTIPLAYER-READY DESIGN

Do not implement online multiplayer before local gameplay is stable.

Player types:
- LocalHumanPlayer
- AIPlayer
- RemotePlayer

Future multiplayer strategy:
shot-based authoritative simulation.

Sync:
- table state
- aim
- power
- spin
- cue-ball placement
- authoritative result snapshot if needed

Do not rely on PhysX being perfectly deterministic across devices.

---

# 39. SCENES

Create:
- 00_Bootstrap.unity
- 01_MainMenu.unity
- 02_Match.unity
- 03_PhysicsTest.unity
- 04_AITraining.unity

Initial development focus:
03_PhysicsTest.unity

Suggested hierarchy:

PhysicsTest
├── Lighting
├── Cameras
├── Table
├── Balls
├── Cue
├── Systems
└── Debug

---

# 40. MAIN MENU

Minimal:
- PLAY
- PRACTICE
- SETTINGS
- EXIT

PLAY:
- 8-BALL
- 9-BALL

Then:
- VS AI
- LOCAL 2 PLAYER
- ONLINE later

---

# 41. PRACTICE MODE

Allow:
- move cue ball
- reset table
- custom ball layout
- reset/undo shot where practical
- full trajectory
- spin selection
- camera selection

---

# 42. MILESTONES

## Milestone 1 — Physics Prototype
- folder architecture
- input
- URP validation
- BallPhysicsProfile
- PoolBall
- cue-ball shot prototype
- sliding
- rolling
- stopping
- spin
- 2-ball collision
- cushion
- pocket
- PhysicsTest scene

Complete only when:
- aim
- shoot
- hit ball
- bank
- pocket
- follow
- draw
- side spin
all work credibly.

## Milestone 2 — 8-Ball
- rack
- rules
- turn manager
- foul
- ball-in-hand
- HUD
- win/loss

## Milestone 3 — 9-Ball
- diamond rack
- rules
- HUD
- win/loss

## Milestone 4 — AI
- candidate shots
- potting
- difficulty
- position play

## Milestone 5 — Presentation
- materials
- lighting
- camera polish
- audio
- VFX
- menus
- settings

## Milestone 6 — Replay / Optimization
- replay
- statistics
- practice
- tests
- WebGL
- Android optimization

---

# 43. ACCEPTANCE CRITERIA — PHYSICS

Reject build if:
- balls tunnel through each other
- balls vibrate indefinitely
- balls drift forever
- draw does not work
- follow does not work
- side spin does not influence rail interaction
- pocket jaws are inconsistent
- balls frequently jump unrealistically
- break simulation is unstable
- rack starts overlapping

---

# 44. ACCEPTANCE CRITERIA — GAMEPLAY

Must complete full:
- 8-ball match
- 9-ball match

Correctly evaluate:
- legal target
- first contact
- pocket
- scratch
- foul
- turn
- win
- loss

---

# 45. ACCEPTANCE CRITERIA — UX

A first-time player should immediately understand:
- aiming direction
- shot power
- spin point
- current player
- legal target balls

without needing a manual.

---

# 46. ACCEPTANCE CRITERIA — VISUALS

At High:
- readable ball numbers
- believable specular/reflections
- cloth material detail
- convincing rails
- smooth camera
- good table readability
- looks like a commercial title, not a primitive prototype

---

# 47. CODE QUALITY

Use:
- focused classes
- composition
- interfaces where useful
- XML docs on important public types
- OnValidate for important settings

Avoid:
- 1000-line controllers
- hidden magic constants
- rule logic in UI
- AI logic in TurnManager
- physics logic scattered across UI callbacks

---

# 48. DOCUMENTATION

Maintain:
- CLAUDE.md
- AGENTS.md
- PROJECT_STATUS.md
- README.md

All important architecture must live in the repository.

Do not rely on hidden chat context.

---

# 49. AGENT WORKFLOW

Before each milestone:
1. inspect repository
2. understand current implementation
3. list files to change
4. implement smallest coherent step
5. compile
6. fix compiler errors
7. run tests
8. validate Play Mode
9. update documentation

Do not rewrite working code unnecessarily.

---

# 50. CONTEXT / TOKEN LIMIT HANDOFF

When context is running low:

FIRST update PROJECT_STATUS.md with:
- completed work
- current state
- changed files
- known bugs
- physics tuning values
- next exact step
- commands required to continue

Then stop cleanly.

Another agent must be able to continue by reading:
- CLAUDE.md
- AGENTS.md
- PROJECT_STATUS.md
- README.md

---

# 51. FIRST AGENT TASK

Read:
- CLAUDE.md
- AGENTS.md
- PROJECT_STATUS.md

Then audit the Unity project.

If it is a fresh project:

1. validate Unity 6.6 / URP
2. validate Input System
3. validate Cinemachine
4. validate Linear color space
5. validate fixed timestep
6. create folder architecture
7. create BallPhysicsProfile
8. create PoolBall
9. create PhysicsTest scene structure
10. implement cue-ball shot prototype
11. implement sliding -> rolling -> stationary
12. add debug velocity/angular velocity rendering
13. compile and fix all C# errors
14. update PROJECT_STATUS.md

Do NOT implement yet:
- AI
- multiplayer
- 8-ball rules
- 9-ball rules
- inventory
- progression
- shop
- online services

Physics correctness has priority over visuals.

Do not ask the user for confirmation between routine implementation steps.
