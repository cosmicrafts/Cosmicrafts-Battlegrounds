# Cosmicrafts Battlegrounds - Unit Spawning and Energy System

## Energy System

### Overview

The energy system serves as the primary resource mechanic in Cosmicrafts Battlegrounds. It's a regenerating resource that players spend to deploy units and spells onto the battlefield.

### Key Components

1. **Energy Management (Player.cs)**
   - `CurrentEnergy`: Tracks the player's current energy amount (0-99 range)
   - `MaxEnergy`: Maximum energy capacity (10 by default)
   - `SpeedEnergy`: Rate of energy regeneration per second (1 by default)

2. **Energy Generation**
   - Automatic regeneration occurs in the `Update()` method of `Player.cs`
   - Energy increases by `SpeedEnergy * Time.deltaTime` each frame
   - Generation can be toggled on/off via the `SetCanGenEnergy(bool)` method

3. **Energy Consumption**
   - Energy is spent when deploying units or spells via `DeplyUnit(NFTsCard)`
   - Each card has an `EnergyCost` property that determines how much energy is required
   - Deployment fails if current energy is less than the cost of the card

4. **Energy UI**
   - The energy value is displayed on the game UI (managed by `UIGameMng`)
   - The preview of units changes color based on whether the player has enough energy

## Unit Spawning System

### Overview

The unit spawning system allows players to deploy units from their deck onto the battlefield by dragging cards to valid spawn areas.

### Key Components

1. **Card Management (Player.cs)**
   - Players have a deck of up to 8 cards (stored in `PlayerDeck`)
   - Cards can be ships (units) or spells
   - Each card is linked to an NFT data object that defines its properties
   - Cards can be selected by clicking or by using number keys (1-8)

2. **Deployment Process**

   a. **Card Selection**
      - Player selects a card via `SelectCard(int)` or by dragging via `DragDeckUnit(int)`
      - Selection prepares the unit for deployment via `PrepareDeploy(GameObject, float)`

   b. **Preview Visualization**
      - `DragUnitCtrl` handles the preview of the unit to be deployed
      - Shows a 3D model of the unit at the cursor position
      - Preview changes color based on placement validity and energy availability

   c. **Placement Validation**
      - Valid spawn areas are tagged with "Spawnarea" in the game world
      - `DragUnitCtrl` tracks collision with spawn areas
      - Deployment is only valid when the preview overlaps a spawn area (`IsValid()`)

   d. **Deployment Execution**
      - When player drops a card in a valid location, `DropDeckUnit()` calls `DeplyUnit(NFTsCard)`
      - Energy cost is deducted from player's current energy
      - The actual unit is created via `GameMng.CreateUnit()`

3. **Spawn Areas**
   - Spawn areas are designated zones where units can be deployed
   - They're attached to the player's units via the `SA` GameObject reference in `Unit.cs`
   - Their size is determined by the unit's `SpawnAreaSize` property
   - Only visible for the player's team units

### Integration with GameMng

1. **Unit Creation (GameMng.cs)**
   - `CreateUnit(GameObject, Vector3, Team, string, int)` instantiates the unit
   - Assigns team, player ID, and a unique unit ID
   - Links the unit to its NFT data via `SetNfts()`
   - Adds the unit to the global list for tracking

2. **Unit Tracking**
   - All active units are tracked in a list within `GameMng`
   - Units register themselves via `AddUnit(Unit)` when created
   - Units can be removed via `DeleteUnit(Unit)` or `KillUnit(Unit)`

## NFT Integration

1. **NFT Data Management**
   - Units are linked to NFT data objects (`NFTsUnit`)
   - NFT data defines core stats: HitPoints, Shield, Damage, Speed
   - The system tracks NFTs via a dictionary in `GameMng`: `allPlayersNfts`

2. **Loading Unit Data**
   - When a unit is spawned, it loads its NFT data via `SetNfts(NFTsUnit)`
   - This applies the NFT's stats to the unit
   - Ship movement speed, damage, health, and shields come from NFT data

## Technical Implementation Details

1. **Drag and Drop Mechanics**
   - `DragUnitCtrl` handles the drag-and-drop interface
   - Uses raycasting to determine mouse position in 3D space
   - Checks for collisions with spawn areas to validate placement

2. **Resource Management**
   - Energy is a client-side resource that regenerates over time
   - Energy costs are defined in the NFT data for each unit/spell
   - The system prevents deployment if energy is insufficient

3. **Deployment Flow**
   - Player clicks/drags a card (UI event)
   - Preview model appears and follows cursor (`DragUnitCtrl`)
   - Player drops card on valid spawn area
   - System checks energy cost vs. current energy
   - Unit is instantiated at the selected position
   - Energy is deducted from player's resource

## Limitations and Design Considerations

1. **Spawn Area Restrictions**
   - Units can only be deployed in valid spawn areas
   - Spawn areas are typically attached to existing friendly units
   - The base station has a spawn area by default

2. **Energy Balance**
   - Energy regeneration creates a time-based pacing mechanism
   - Higher cost units require saving energy or waiting longer
   - Energy management becomes a key strategic element

3. **Team-Based Spawning**
   - Units deployed automatically belong to the player's team
   - Units set their visual appearance based on team affiliation
   - Enemy units cannot deploy in the player's spawn areas 