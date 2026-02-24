# Napoleonic Wars RTS

A real-time strategy game set in the Napoleonic era, inspired by Total War and Cossacks.

## Features (Planned)
- **Massive battles** — 1000-5000 units on the battlefield in real-time
- **Napoleonic formations** — Line, column, square formations with morale system
- **Campaign map** — Manage cities, armies, diplomacy across Europe
- **Multiplayer** — Online battles and campaigns

## Tech Stack
- **Engine**: Unity 2022 LTS+ (URP)
- **Language**: C#
- **Performance**: Unity DOTS/ECS + GPU Instancing (Phase 3)
- **Networking**: Unity Netcode for GameObjects (Phase 5)

## Project Structure
```
Assets/
├── Scripts/
│   ├── Core/           # GameManager, InputManager, CameraController
│   ├── Units/          # UnitBase, Regiment, Formation, Combat
│   ├── AI/             # EnemyAI, Pathfinding
│   ├── Campaign/       # CampaignMap, City, Army, Diplomacy
│   ├── Economy/        # ResourceManager, Building, TechTree
│   ├── Multiplayer/    # NetworkManager, LobbyManager
│   └── UI/             # BattleHUD, CityPanel, CampaignUI
├── Prefabs/
├── Scenes/
├── Art/
├── Audio/
└── Data/               # ScriptableObjects
```

## Getting Started
1. Open the project in Unity 2022.3 LTS or later
2. Open `Assets/Scenes/Battle.unity`
3. Press Play

## Current Phase: Phase 1 — Prototype
- [x] Project setup
- [x] RTS Camera
- [x] Unit movement
- [x] Unit selection (click + box select)
- [x] Formations
- [x] Basic combat
