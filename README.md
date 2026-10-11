# Creating Immersive and Interactive AR Murals

An Android AR app for the ALU campus. Point your phone at one of five murals and it comes to life: things grow out of the wall, move, react to your taps and tell a story.

Built with Unity 6000.4.7f1 (URP), AR Foundation 6.6.2 and Google ARCore.

## Links

- Android app (APK): https://drive.google.com/file/d/1_Rtsr21UJPqvGez_m_WDuTbF4kQJVrPk/view?usp=sharing
- Demonstration video: https://youtu.be/g57dfWJXOSE
- Technical walkthrough, codebase: https://drive.google.com/file/d/1LMpMJBYSVF29-1Mb0KqRJ8McVjLA5eVq/view?usp=sharing
- Technical walkthrough, Unity setup: https://drive.google.com/file/d/1wVsVbHOXdRO0gMIPE8fRnMd5ZPn2jCUr/view?usp=sharing

## The murals

| Mural | Made by | Where on campus | What happens in AR | Wall size (m) |
|---|---|---|---|---|
| Futuristic City | Leslie | In front of reception | The tram pulls out of the wall under glowing street lamps, with a narrated story | 1.5 x 1.49 |
| Convention Center | Brian | In front of reception | A glass dome grows tier by tier and lights up blue, yellow and green, the tower and cups rise and a plant grows. About tells the story of the Kigali Convention Centre | 2.5 x 2.7 |
| Leaders | Bruno | Enterprise Commons, opposite Kenya and Burundi | A guide walks you through the five portraits; a river with reeds and fireflies flows below | 4.4 x 3.45 |
| Orbits | Bruno | Below the food court | The rings, bangles, bubbles, orb and cubes lift off the wall and start to orbit | 5.3 x 3.5 |
| Flying Cars | Gama | Learning Commons, opposite Mozambique | Cars fly out of the mural, Amanirenas steps through a golden portal and the scene turns into a savanna at nightfall | 5.4 x 8.2 |

The app talks about the people and places painted on the walls. For what each mural means, it points visitors to ALU guest relations.

## Getting started

1. In Unity Hub, install 6000.4.7f1 with Android Build Support (tick OpenJDK and Android SDK and NDK Tools).
2. Clone the repo:
   ```
   git clone https://github.com/Inema-Leslie/Creating-Immersive-and-Interactive-AR-Murals.git
   ```
3. Add the folder in Unity Hub and open it. The first import takes a few minutes.
4. Go to File > Build Profiles, pick Android and press Switch Platform.
5. Open `Assets/Framework/Scenes/Main.unity`. This is the scene that goes into the app.

To try it on a phone, plug in an ARCore phone with USB debugging on and use Build And Run in Build Profiles.

## How the app works

The app has four screens. The Start screen has the title and a Start button; phones without ARCore get a message there instead. The Scanning screen shows a camera frame and a hint to point at a mural. On the AR screen the mural comes alive, and the top bar shows its name and place on campus with About, Replay and Exit. About opens a story panel that scrolls. The Exit screen says how many murals you explored and offers Scan again or Close app. The phone's back button works on every screen.

What happens behind the screens:

- All five tracking photos are in one reference image library (`Assets/Framework/ReferenceImages/MuralImageLibrary`), each with the real width of the wall.
- When ARCore finds a photo, `MuralSpawner` places that mural's prefab on the wall and scales it to the real size. It keeps the mural standing straight on the wall and smooths out small tracking corrections, so nothing shakes or tips.
- The scene light is turned toward each mural when it is found, so 3D objects are lit from the visitor's side.
- If you point away, the mural pauses and fades. When you point back, it carries on from where it stopped.
- Taps go through `TapInput` to any object with a `MuralTappable` component.
- Only the mural you are looking at can put text on screen, so one mural's story never shows up over another.

## Testing on a PC

You can run the whole app in Unity without a phone. Open the main scene, set the Game view to a portrait size such as 1080x1920, and press Play. Unity's XR Simulation puts you in a virtual room (`Assets/Framework/Simulation/MuralSimulationRoom`) with all five tracking photos on one long wall at their real sizes, from left to right: Futuristic City, Convention Center, Leaders, Orbits and Flying Cars. You start in front of Leaders.

Hold the right mouse button and move the mouse to look around. W, A, S and D walk, Q and E go down and up, and Shift moves faster. Click Start exploring, walk up to a mural, and click on things to tap them.

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

### Leaders
- Guide character and animations: [Adobe Mixamo](https://www.mixamo.com) (Ch12 with Talking, Breathing Idle, Waving and Pointing)
- Reeds: [3D Game Assets - Flora](https://assetstore.unity.com/packages/3d/environments/3d-game-assets-flora-318366), Unity Asset Store
- Firefly textures: [Firefly Flare Effect](https://assetstore.unity.com/packages/vfx/shaders/firefly-flare-effect-288941) by COMICOMI, Unity Asset Store
- Narration recorded by the team
- Portrait stories written by the team from public sources

### Orbits
- Sound effects from [Pixabay](https://pixabay.com/sound-effects/) (Pixabay Content License)

### Convention Center
- Sound effects from [Pixabay](https://pixabay.com/sound-effects/) and [Freesound](https://freesound.org)
- Story panel facts: [New Times Rwanda](https://www.newtimes.co.rw/article/130969/News/tracing-the-story-behind-the-kigali-convention-complex/amp), [Knippers Helbig](https://www.knippershelbig.com/en/projects/kongresszentrum/), [KT Press](https://ktpress.rw/2024/08/central-bank-issues-new-bank-notes-of-rwf-5000-and-rwf-2000) and the [Rwanda Convention Bureau](https://rcb.rw/Kigali-Convention-Center.html)

### Futuristic City
- Tram: [Red low poly Tram with UV](https://sketchfab.com/3d-models/red-low-poly-tram-with-uv-23d43dda5d3143ec8645cf1af90ab6e5) by ddggoorrddgg, Sketchfab (CC BY 4.0)
- Street lamps: [Street Lamps 2](https://assetstore.unity.com/packages/3d/props/exterior/street-lamps-2-260395), Unity Asset Store
- Tram sound: [tram (9)](https://freesound.org/people/audio_master12376/sounds/774538/) by audio_master12376, Freesound (CC0)
- Story narration recorded by the team

### Flying Cars
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
- Amanirenas character: [Bandit](https://assetstore.unity.com/packages/package/185326) by AlexN, Unity Asset Store
- Toyota AE86 by IvOfficial and Acacia by Poly by Google, [Poly Pizza](https://poly.pizza) (CC BY)
- Unity Asset Store: ARCADE - FREE Racing Car, Magic effects pack by Hovl Studio, Grass Flowers FREE by ALP Assets, Unity Particle Pack

### App screens
- Font: [Poppins](https://fonts.google.com/specimen/Poppins) by the Poppins Project Authors (SIL Open Font License)

All mural photos and tracking images were taken by the team.
