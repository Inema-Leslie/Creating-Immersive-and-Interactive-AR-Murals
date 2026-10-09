# Creating Immersive and Interactive AR Murals

One Android AR app that recognizes five ALU campus murals and plays a different "the mural comes alive" experience for each one.

- Unity 6000.4.7f1 (Unity 6), Universal Render Pipeline
- AR Foundation 6.6.2 and Google ARCore XR Plugin 6.6.2
- Input System package (new input only)
- Due: Oct 14, 11:59 pm

## Team and murals

| # | Mural name | Owner | Transformation | Measured width x height (m) | Branch |
|---|---|---|---|---|---|
| 1 | FuturisticCity | Leslie | Emergence, Storytelling | 1.6 x not measured | `mural-futuristiccity` |
| 2 | ConventionCenter | Brian | Emergence, Storytelling | 2.5 x 2.7 | `mural-convention-center` |
| 3 | Leaders | Bruno | Reconstruction, Storytelling | 4.4 x 3.45 | `main` (tech lead) |
| 4 | Orbits | Bruno | Animation | 5.3 x 3.5 | `mural-orbits` |
| 5 | FlyingCars | Gama | Expansion, Emergence | 5.4 x 8.2 | `iNTARE` |

Bruno is the tech lead (framework, tracking, main scene, builds).

What any mural means comes only from guest relations. Until they reply, write "ask guest relations". Do not guess.

## First time setup

1. Install Unity 6000.4.7f1 through Unity Hub with the **Android Build Support** module (including OpenJDK and Android SDK and NDK Tools).
2. Clone the repo:
   ```
   git clone https://github.com/Inema-Leslie/Creating-Immersive-and-Interactive-AR-Murals.git
   cd Creating-Immersive-and-Interactive-AR-Murals
   ```
3. In Unity Hub: **Add > Add project from disk**, select the cloned folder, open it with 6000.4.7f1.
4. Wait for the first import to finish. It can take several minutes. Packages install automatically from `Packages/manifest.json`.
5. **File > Build Profiles**, select **Android**, click **Switch Platform**.

## Folder structure

```
Assets/
  Framework/              tech lead only
    Scripts/              MuralData, MuralExperience, MuralSpawner, MuralTappable, TapInput, MuralInfoPanel
    Prefabs/
    Scenes/               Main scene
    ReferenceImages/      tracking photos and the reference image library
  UI/                     UI person only
  Murals/
    <MuralName>/          the mural owner only
      Prefabs/            <MuralName>Mural.prefab
      Scripts/            <MuralName>Mural.cs
      Audio/
      Animations/
      Materials/
      Data/               <MuralName>Data.asset
      Sandbox_<MuralName>.unity
```

## Naming rules

One name per mural, in PascalCase with no spaces, used everywhere:

| Thing | Name | Example |
|---|---|---|
| Image name in the reference library | `<MuralName>` | `Orbits` |
| Folder | `Assets/Murals/<MuralName>/` | `Assets/Murals/Orbits/` |
| Prefab | `<MuralName>Mural` | `OrbitsMural` |
| Data asset | `<MuralName>Data` | `OrbitsData` |
| Script and class | `<MuralName>Mural`, inherits `MuralExperience` | `OrbitsMural` |
| Git branch | `mural-<muralname lowercase>` | `mural-orbits` |
| Sandbox scene | `Sandbox_<MuralName>` | `Sandbox_Orbits` |

The branches actually in use are listed in the team table above. Framework work goes straight to `main` (tech lead).

## Ownership rules

- Only the tech lead edits: `Assets/Framework/`, the main scene, the reference image library, `Packages/`, `ProjectSettings/`.
- Only the UI person edits `Assets/UI/`.
- Everyone else edits only their own `Assets/Murals/<MuralName>/` folder and their own sandbox scene.
- Need a change to a shared file? Ask the tech lead in the group chat. Do not edit it yourself.
- Never open or save someone else's sandbox scene.

## Axis rule

The prefab origin is the center of the tracking image. Units are meters.

- **X** points to the right along the image.
- **Z** points up along the image.
- **Y** points out of the wall, toward the viewer.

Anything that emerges from the wall moves along **+Y**. A flat object lying on the mural has Y = 0.

To place something at a pixel in the square tracking photo, where N is the photo size in pixels and W is the real width in meters:

```
x = (px / N - 0.5) * W
z = (0.5 - py / N) * W
```

For a photo that is not square (width Nw pixels, height Nh pixels, real width W, real height H):

```
x = (px / Nw - 0.5) * W
z = (0.5 - py / Nh) * H
```

Check positions by placing a quad textured with the mural photo at the prefab origin, rotated (90, 0, 0) and scaled to (W, H, 1). Delete the quad, or turn it off, before pushing.

## The data asset (MuralData)

Each mural has one `<MuralName>Data` asset in its `Data` folder. Fields:

| Field | Meaning |
|---|---|
| imageName | Must match the reference library name exactly, for example `Orbits` |
| widthMeters, heightMeters | Size the prefab was built for, in meters. The real measured size goes in the reference library, and the spawner scales the prefab to match |
| prefab | The `<MuralName>Mural` prefab |
| displayTitle | Title shown in the UI |
| location | Where it is on campus |
| transformations | Emergence, Animation, Reconstruction, Expansion, Storytelling |
| description | Short text for the info panel (meaning comes from guest relations) |
| captions | Short lines the mural shows during the experience |
| scanHint | Hint on the scanning screen, for example where to stand |

## The base class (MuralExperience)

Every mural script inherits from `MuralExperience` and sits on the root of the mural prefab.

| Method | Who calls it | What it does |
|---|---|---|
| `PlayIntro()` | Framework, once, the first time the mural is tracked | Override this. Start the transition out of the flat mural here. |
| `OnTapped(MuralTappable t)` | Framework, when the user taps an object with a `MuralTappable` | Override this. Handle interactions here. |
| `OnTrackingLost()` | Framework | Already freezes Animators, particles, audio and Timeline, and fades content to 30 percent. Override only to add extra behaviour, and call `base.OnTrackingLost()` first. |
| `OnTrackingFound()` | Framework, when the image returns | Already fades back in over 0.5 s and resumes. Never replays the intro. Override only to add extra behaviour, and call `base.OnTrackingFound()` first. |
| `ResetExperience()` | UI reset button | Destroys this copy and spawns a fresh one, so the intro plays again. |

Helpers you can use inside your mural script. All of them stop automatically while tracking is lost:

| Helper | Use |
|---|---|
| `yield return Wait(1.5f);` | Use instead of `WaitForSeconds` |
| `yield return ScaleTo(t, Vector3.one, 0.8f);` | Smooth scale |
| `yield return MoveTo(t, new Vector3(0, 0.5f, 0), 1f);` | Smooth local move |
| `DeltaTime` | Use instead of `Time.deltaTime` in `Update` |
| `IsPaused` | True while tracking is lost |
| `PlaySound(clip);` | One shot sound from the prefab root |
| `ShowCaption("text");` or `ShowCaption(0);` | Caption in the UI (index reads from the data asset) |

Minimal mural script:

```csharp
using System.Collections;
using UnityEngine;

public class ExampleMural : MuralExperience
{
    public Transform rings;

    private void Awake()
    {
        rings.localScale = Vector3.zero;
    }

    public override void PlayIntro()
    {
        StartCoroutine(IntroSequence());
    }

    private IEnumerator IntroSequence()
    {
        yield return Wait(1f);
        yield return ScaleTo(rings, Vector3.one, 0.8f);
        ShowCaption(0);
    }

    public override void OnTapped(MuralTappable tappable)
    {
        if (tappable.id == "Rings")
        {
            // interaction here
        }
    }
}
```

Rules for mural scripts:

- Set the starting state (hidden objects, zero scales) in `Awake`, not in `PlayIntro`.
- Use `Wait`, `ScaleTo`, `MoveTo` and `DeltaTime` so everything freezes on tracking loss.
- Every tappable object needs a **Collider** and a **MuralTappable** component.
- Materials that should fade on tracking loss use **Surface Type: Transparent**. Opaque materials darken instead, which is fine for solid models.

## Testing your mural alone

Each owner makes `Sandbox_<MuralName>.unity` in their own mural folder, with an XR Origin, an AR Session, an `ARTrackedImageManager` using their own sandbox reference library, a `MuralSpawner` with only their data asset, and a `TapInput`. Full steps are in your pack. The main scene is only edited by the tech lead.

## Git workflow

### Every day before you start

```
git checkout main
git pull origin main
git checkout mural-<name>
git merge main
```

### First time on your branch

```
git checkout main
git pull origin main
git checkout -b mural-<name>
```

### Saving and pushing your work

Save in Unity first (**File > Save** and **File > Save Project**), then:

```
git status
git add Assets/Murals/<MuralName>
git commit -m "Add <name> mural"
git push -u origin mural-<name>
```

After the first push, `git push` is enough.

Then open a pull request on GitHub from `mural-<name>` into `main` and tell the tech lead in the group chat. The tech lead reviews and merges.

### Rules

- Only `git add` your own folder. Never `git add .` or `git add -A`.
- Always commit the `.meta` files next to your assets. They are what keep references working.
- If `git status` shows changes in `ProjectSettings/`, `Packages/`, `Assets/Framework/` or the main scene, do not commit them. Undo them with `git checkout -- <path>`.
- Commit small and often. Push at least once a day.
- If Git reports a conflict in a file you do not own, stop and message the tech lead.

## Daily standup

Post in the group chat every day before 10 am:

```
Name:
Mural or area:
Done since last standup:
Doing today:
Blocked by (person or thing):
Pushed to branch (yes/no):
```

## Deadlines

| Date | What | Who |
|---|---|---|
| Oct 7 | Photos and tape measurements (done) | all |
| done | Framework on `main` | tech lead |
| done | First mural merged (FuturisticCity) | mural owners |
| fill in | All mural pull requests merged | mural owners |
| fill in | UI screens pushed, design document draft | UI person |
| fill in | Android build for recording | tech lead |
| fill in | Demo video and technical walkthrough recorded | all |
| Oct 14, 11:59 pm | Final PDF submitted | UI person |
