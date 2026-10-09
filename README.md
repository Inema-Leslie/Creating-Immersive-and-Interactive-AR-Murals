# Creating Immersive and Interactive AR Murals

An Android AR app for the ALU campus. Point your phone at one of five murals and it comes to life: things grow out of the wall, move, react to your taps and tell a story.

Built with Unity 6000.4.7f1 (URP), AR Foundation 6.6.2 and Google ARCore.

## The murals

| Mural | Made by | What happens in AR | Wall size (m) |
|---|---|---|---|
| Futuristic City | Leslie | The tram pulls out of the wall under glowing street lamps, with a narrated story | 1.5 x 1.49 |
| Convention Center | Brian | The dome lights up, the tower and cups rise and a plant grows | 2.5 x 2.7 |
| Leaders | Bruno | A guide walks you through the five portraits; a river with reeds and fireflies flows below | 4.4 x 3.45 |
| Orbits | Bruno | The rings, bangles, bubbles, orb and cubes lift off the wall and start to orbit | 5.3 x 3.5 |
| Flying Cars | Gama | Cars fly out of the mural, Amanirenas steps through a golden portal and the scene turns into a savanna at nightfall | 5.4 x 8.2 |

The stories behind the murals come from ALU guest relations.

## Getting started

1. In Unity Hub, install **6000.4.7f1** with **Android Build Support** (tick OpenJDK and Android SDK and NDK Tools).
2. Clone the repo:
   ```
   git clone https://github.com/Inema-Leslie/Creating-Immersive-and-Interactive-AR-Murals.git
   ```
3. Add the folder in Unity Hub and open it. The first import takes a few minutes.
4. Go to **File > Build Profiles**, pick **Android** and press **Switch Platform**.
5. Open `Assets/Framework/Scenes/Main.unity`. This is the scene that goes into the app.

To try it on a phone, plug in an ARCore phone with USB debugging on and use **Build And Run** in Build Profiles.

## How the app works

The app has four screens:

- **Start**: title and a Start button. Phones without ARCore get a message instead.
- **Scanning**: a camera frame and a hint to point at a mural.
- **AR**: the mural comes alive. The top bar shows its name with About, Replay and Exit buttons.
- **Exit**: shows how many murals you explored, with Scan again and Close app.

Behind the screens:

- All five tracking photos are in one reference image library (`Assets/Framework/ReferenceImages/MuralImageLibrary`), each with the real width of the wall.
- When ARCore finds a photo, `MuralSpawner` places that mural's prefab on the wall and scales it to the real size.
- If you point away, the mural pauses and fades. When you point back, it carries on from where it stopped.
- Taps go through `TapInput` to any object with a `MuralTappable` component.

## Project layout

```
Assets/
  Framework/          shared code, main scene, tracking images
  Murals/
    Leaders/
    Orbits/
    ConventionCenter/
    FuturisticCity/
    FlyingCars/
```

Each mural folder has the same parts:

- a prefab (`LeadersMural.prefab`)
- a script that inherits from `MuralExperience` (`LeadersMural.cs`)
- a data asset (`LeadersData.asset`) with the image name, title and text
- its own audio, materials and a preview scene for testing on its own

## Building a mural

The mural script overrides a few methods from `MuralExperience`:

- `PlayIntro()` runs once, the first time the mural is found. This is where the transition out of the wall starts.
- `OnTapped(MuralTappable t)` runs when the visitor taps something.
- `OnTrackingLost()` and `OnTrackingFound()` already pause and resume animations, particles, audio and Timeline. Only override them to add something extra, and call the base version first.

Use `Wait()`, `ScaleTo()`, `MoveTo()` and `DeltaTime` instead of the normal Unity versions, so everything freezes properly when tracking is lost.

```csharp
public class ExampleMural : MuralExperience
{
    public Transform rings;

    private void Awake()
    {
        rings.localScale = Vector3.zero;
    }

    public override void PlayIntro()
    {
        StartCoroutine(Intro());
    }

    private IEnumerator Intro()
    {
        yield return Wait(1f);
        yield return ScaleTo(rings, Vector3.one, 0.8f);
        ShowCaption(0);
    }
}
```

The prefab's origin is the center of the tracking photo. X goes right, Z goes up the wall and Y comes out of the wall toward the viewer, all in meters. To place something at a pixel (px, py) of a photo that is Nw by Nh pixels on a wall W by H meters:

```
x = (px / Nw - 0.5) * W
z = (0.5 - py / Nh) * H
```

## Working together

Everyone works on their own branch and only changes their own mural folder. When a mural is ready, open a pull request into `main`. Always commit the `.meta` files with your assets, or Unity loses the links between them.

| Branch | Mural |
|---|---|
| `mural-futuristiccity` | Futuristic City |
| `mural-convention-center` | Convention Center |
| `mural-orbits` | Orbits |
| `iNTARE` | Flying Cars |
| `main` | Framework, main scene and Leaders |

## Credits

**Leaders**
- Guide character and animations: [Adobe Mixamo](https://www.mixamo.com) (Ch12 with Talking, Breathing Idle, Waving and Pointing)
- Reeds: [3D Game Assets - Flora](https://assetstore.unity.com/packages/3d/environments/3d-game-assets-flora-318366), Unity Asset Store
- Firefly textures: [Firefly Flare Effect](https://assetstore.unity.com/packages/vfx/shaders/firefly-flare-effect-288941) by COMICOMI, Unity Asset Store
- Narration recorded by the team

**Orbits**
- Sound effects from [Pixabay](https://pixabay.com/sound-effects/) (Pixabay Content License)

**Convention Center**
- Sound effects from [Pixabay](https://pixabay.com/sound-effects/) and [Freesound](https://freesound.org)

**Futuristic City**
- Tram: [Red low poly Tram with UV](https://sketchfab.com/3d-models/red-low-poly-tram-with-uv-23d43dda5d3143ec8645cf1af90ab6e5) by ddggoorrddgg, Sketchfab (CC BY 4.0)
- Street lamps: [Street Lamps 2](https://assetstore.unity.com/packages/3d/props/exterior/street-lamps-2-260395), Unity Asset Store
- Tram sound: [tram (9)](https://freesound.org/people/audio_master12376/sounds/774538/) by audio_master12376, Freesound (CC0)
- Story narration recorded by the team

**Flying Cars**
- Story narration recorded by Amanda
- Car sounds from Pixabay:
  [engine starting](https://pixabay.com/sound-effects/city-car-engine-starting-43705/),
  [flying vehicle](https://pixabay.com/sound-effects/film-special-effects-flying-vehicle-sound-98568/),
  [supercar revs](https://pixabay.com/sound-effects/film-special-effects-aggressive-supercar-engine-throttle-revs-537640),
  [car passing with birds](https://pixabay.com/sound-effects/city-car-passing-and-birds-chirping-in-the-background-26051/),
  [car brake](https://pixabay.com/sound-effects/film-special-effects-car-brake-324939/),
  [car brake 3](https://pixabay.com/sound-effects/film-special-effects-car-brake3-325523/),
  [hand brake](https://pixabay.com/sound-effects/city-car-hand-brake-while-engine-is-running-85726/),
  [car honk](https://pixabay.com/sound-effects/film-special-effects-car-honk-386166/)
- Story music, mixed from Pixabay tracks:
  [African drums](https://pixabay.com/music/solo-instruments-african-drums-209632/),
  [African background music](https://pixabay.com/music/supernatural-african-african-background-music-348249/),
  [tribal drums](https://pixabay.com/sound-effects/musical-tribal-drums-526712/),
  [Harambee Africa](https://pixabay.com/music/drum-n-bass-harambee-africa-swahili-drum-and-bass-594062/)
- Toyota AE86 by IvOfficial and Acacia by Poly by Google, [Poly Pizza](https://poly.pizza) (CC BY)
- Unity Asset Store: ARCADE - FREE Racing Car, Magic effects pack by Hovl Studio, Grass Flowers FREE by ALP Assets, Unity Particle Pack

All mural photos and tracking images were taken by the team.
