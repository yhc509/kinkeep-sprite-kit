# KinKeep SpriteKit

A frame-based sprite animation system for Unity, built for 2D games that need directional movement (RPGs, action games, etc.).

Instead of Unity's Animator Controller state machine, SpriteKit gives you a lightweight, code-driven approach: define clips per direction in a single ScriptableObject, wire up frame events, and control everything from script.

## Features

- **Per-frame control** — Each frame has its own duration, flip, and event list. No uniform frame rates.
- **Direction system** — Built-in directional clip resolution. Play `"Attack"` and the system picks the right clip for the current direction.
- **Flip mirroring** — Define sprites for one side only. Left/Right (or any pair) auto-mirrors via FlipEntry, saving memory and sprite work.
- **Frame events** — Hit, Skill, Sound events per frame. Attack/Skill clips get hit events auto-placed at the center frame.
- **Auto Generator** — Point at a `SpriteLibraryAsset`, map actions to directions, and generate a complete `UnitSpriteAnimation` asset with one click.
- **Seamless direction switching** — Change direction mid-animation while preserving the current frame index and timing.
- **Fully configurable directions** — Not limited to 4 or 8 directions. Add custom directions, suffixes, and flip mappings per asset.

## Requirements

- Unity 6000.0+
- [Unity 2D Animation](https://docs.unity3d.com/Packages/com.unity.2d.animation@10.0/manual/index.html) 10.0.0+

## Installation

### Git URL (recommended)

1. Open **Window > Package Manager**
2. Click **+** > **Add package from git URL...**
3. Enter:

```
https://github.com/yhc509/kinkeep-sprite-kit.git
```

### manifest.json

Add to your `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.kinkeep.sprite-kit": "https://github.com/yhc509/kinkeep-sprite-kit.git"
  }
}
```

Pin a specific version with a tag:

```
https://github.com/yhc509/kinkeep-sprite-kit.git#v0.1.0
```

### Local (for development)

```bash
git clone https://github.com/yhc509/kinkeep-sprite-kit.git
```

Then **Window > Package Manager > + > Add package from disk...** and select `package.json`.

## Usage

### 1. Create an animation asset

Open **KinKeep > Sprite Animation Editor**.

Use **Auto Generate** to build clips from a `SpriteLibraryAsset`:
- Assign your sprite library as the source
- Map animation types (Idle, Move, Attack, ...) to source actions
- Click **Generate** — clips are created for each direction automatically

Or create a `UnitSpriteAnimation` manually and add clips in the inspector.

### 2. Set up your GameObject

Add a `SpriteAnimator` component (automatically adds `SpriteRenderer`). Assign your `UnitSpriteAnimation` asset.

### 3. Control from script

```csharp
using KinKeep.SpriteKit;

var animator = GetComponent<SpriteAnimator>();

// Play an animation — direction is resolved automatically
animator.Play("Idle");

// Change direction (0=None, 1=Up, 2=Down, 3=Left, 4=Right by default)
animator.SetDirection(3);

// Play attack and wait for the hit frame
animator.Play("Attack");
animator.OnHit += OnAttackHit;

// Or use the coroutine helper
yield return AnimationEventUtil.WaitHit(animator);
ApplyDamage();

// Wait for a non-looping animation to finish
animator.Play("Die");
yield return AnimationEventUtil.WaitAnimationComplete(animator);
```

### Direction configuration

Each `UnitSpriteAnimation` asset has three configurable direction tables:

| Table | Purpose | Default |
|-------|---------|---------|
| **DirectionEntry** | Maps direction index to a name | 0=None, 1=Up, 2=Down, 3=Left, 4=Right |
| **FlipEntry** | Mirrors one direction to another | Left(3) ↔ Right(4) |
| **GeneratorDirectionEntry** | Maps sprite library category suffixes to directions | B→Up, F→Down, S→Right |

These are fully customizable per asset — you can define 8 directions, diagonal-only setups, or any scheme your game needs.

## License

See [LICENSE](LICENSE) for details.
