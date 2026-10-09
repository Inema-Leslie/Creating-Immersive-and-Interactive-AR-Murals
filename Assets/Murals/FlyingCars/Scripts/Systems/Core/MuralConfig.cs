using UnityEngine;

namespace MuralAR
{
    [CreateAssetMenu(menuName = "MuralAR/Mural Config", fileName = "MuralConfig")]
    public sealed class MuralConfig : ScriptableObject
    {
        [Header("Image target (the team framework tracks it; the size comes from FlyingCarsData)")]
        [Tooltip("Name of the mural in the reference image library (team naming rule: the mural name).")]
        public string referenceImageName = "FlyingCars";

        [Tooltip("Measured width in metres of the painted area the tracking image covers (tape, 2026-10-09). " +
                 "The setup copies it into FlyingCarsData.")]
        public float imageWidthMeters = 5.4f;

        [Tooltip("Measured height / width of the painted area (8.2 m / 5.4 m). The photo, taken from below, looks taller (1.62).")]
        public float imageHeightToWidth = 8.2f / 5.4f;

        [Header("Tracking loss (the framework reports it; these set what our content does)")]
        [Tooltip("Loss shorter than this: content holds its last position, nothing changes.")]
        public float lossHoldSeconds = 2f;

        [Tooltip("Loss longer than this: cars return to idle.")]
        public float lossPauseSeconds = 5f;

        [Header("Woman")]
        public float womanHeightMeters = 1.8f;
        public float emergenceSeconds = 10f;

        [Tooltip("Where she lands, in metres in front of the wall (never past the halfway point to the camera).")]
        public float womanStandDistanceFromWall = 2.5f;

        [Tooltip("The painted woman on the tracking image: x, y from the top-left, width, height (normalised). Tap area and emergence start.")]
        public Rect paintedWomanRegion = new(0.30f, 0.37f, 0.42f, 0.63f);

        [Tooltip("Share of the emergence spent dissolving in at the wall; the rest is walking out and shrinking to life size.")]
        [Range(0.1f, 0.9f)] public float emergenceDissolveShare = 0.35f;

        [Header("Portal (Hovl Magic Effects \"Portal yellow\")")]
        [Tooltip("Portal size relative to the painted woman's height.")]
        public float portalSizeToFigure = 0.9f;
        [Tooltip("Height of the portal's glowing ring at scale 1 (measured from a render of the prefab).")]
        public float portalNativeHeight = 1.7f;
        [Tooltip("How far in front of the wall the portal opens, in metres.")]
        public float portalOffsetFromWall = 0.1f;
        [Tooltip("Seconds the portal stays open before she starts dissolving in.")]
        public float portalLeadSeconds = 2f;
        [Tooltip("Seconds for the portal to fade out after she has stepped through.")]
        public float portalCloseSeconds = 2.5f;

        [Header("Woman motion (she is unrigged: procedural instead of Mixamo clips)")]
        [Tooltip("How much she rises and falls with each breath, as a fraction of her height.")]
        public float womanBreathAmount = 0.008f;
        public float womanBreathSeconds = 4f;
        [Tooltip("Slow weight shift from side to side, in degrees.")]
        public float womanSwayDegrees = 1.2f;
        public float womanSwaySeconds = 7f;
        [Tooltip("Up-and-down bob while she moves out of the wall, as a fraction of her height.")]
        public float womanStepBob = 0.015f;
        [Tooltip("Side-to-side roll per step while moving, in degrees.")]
        public float womanStepRollDegrees = 2.5f;
        public float womanStepsPerSecond = 1.6f;

        [Header("Woman in the savanna (she walks while she speaks)")]
        [Tooltip("Points she walks between, in metres around where she landed: x to her right, y towards the viewer. Loops.")]
        public Vector2[] womanWanderPoints =
        {
            new(-1.6f, 1.0f), new(-2.4f, -0.6f), new(-0.8f, -2.0f), new(1.4f, -1.6f), new(2.4f, 0.4f), new(1.0f, 1.6f), new(0f, 0.4f),
        };
        [Tooltip("Seconds into her story before she starts walking (she greets the viewer first).")]
        public float womanWanderStartSeconds = 5f;
        public float womanWalkSpeed = 0.55f;
        [Tooltip("How long she stands and speaks at each point: min, max seconds.")]
        public Vector2 womanPauseSeconds = new(3f, 6f);
        public float womanTurnDegreesPerSecond = 90f;

        [Header("Savanna")]
        [Tooltip("One tap on the painted woman brings her out and starts the savanna as she lands.")]
        public bool savannaStartsWhenSheLands = true;
        public float savannaRevealRadius = 3f;
        public float savannaRevealSeconds = 6f;

        [Tooltip("Length of the gold flash that hides the switch from camera view to savanna.")]
        public float savannaFlashSeconds = 1.2f;

        [Tooltip("No grass or flowers within this radius of where she lands (covers the points she walks between).")]
        public float savannaClearRadiusAroundHer = 5.5f;

        [Tooltip("Half-width of the grass-free strip between her and the viewer.")]
        public float savannaClearPathHalfWidth = 0.9f;

        [Header("Cars")]
        public float carMinCameraDistance = 1.5f;
        [Tooltip("Flight volume in front of the mural: width, height, depth in metres.")]
        public Vector3 carFlightVolume = new(4f, 3f, 3f);
        [Tooltip("Height of the bottom of the flight volume above the foot of the wall.")]
        public float carFlightFloor = 1.2f;
        public float holdToFullRevSeconds = 1.5f;
        [Tooltip("Seconds after the mural wakes before the cars start (design doc 3: stage 5).")]
        public float carsWakeAfterSeconds = 6f;
        [Tooltip("Seconds for a car to peel off the wall and grow to full size.")]
        public float carLiftOffSeconds = 4f;
        [Tooltip("Where a car hovers when idle, in metres in front of its painted spot.")]
        public float carHoverDistanceFromWall = 2.2f;
        public float carHoverBob = 0.06f;
        [Tooltip("Distance from the camera when a car comes over to be inspected.")]
        public float carInspectDistance = 2f;
        [Tooltip("How far a swipe sends a car before it curves back.")]
        public float carLaunchDistance = 3f;
        [Tooltip("Press this long on a car (without moving) to start revving.")]
        public float holdStartSeconds = 0.25f;

        public CarSettings carA = new()
        {
            displayName = "The Cruiser",
            design = "Heavy and calm: a long, low body that glides rather than races.",
            howItHovers = "Its four wheels have folded flat into lift pads; the glow underneath is the air they push down.",
            role = "A shared sky-shuttle for the city: few stops, many passengers, no traffic jams below.",
            paintedCenter = new Vector2(0.515f, 0.1206f),
            paintedSize = new Vector2(0.27f, 0.105f),
            flyingLengthMeters = 3.6f,
            hoverOffset = new Vector2(-1.4f, 0.2f),
            cruiseSpeed = 1.1f,
            loopRadius = 1.6f,
            bankDegrees = 18f,
            idlePitch = 0.75f,
            revPitch = 1.35f,
            lightColor = new Color(1f, 0.78f, 0.35f),
            wakeDelaySeconds = 0f,
        };

        public CarSettings carB = new()
        {
            displayName = "The Swift",
            design = "Small, light and playful: a classic little coupé reborn for the air.",
            howItHovers = "Folded wheels act as hover pads; tight swoops come from shifting its lift side to side.",
            role = "A personal courier that darts between rooftops with parcels and messages.",
            paintedCenter = new Vector2(0.508f, 0.237f),
            paintedSize = new Vector2(0.145f, 0.056f),
            flyingLengthMeters = 2.6f,
            hoverOffset = new Vector2(1.5f, -0.7f),
            cruiseSpeed = 1.9f,
            loopRadius = 0.9f,
            bankDegrees = 32f,
            idlePitch = 1.05f,
            revPitch = 1.9f,
            lightColor = new Color(0.75f, 1f, 0.85f),
            wakeDelaySeconds = 5.5f,
        };

        public CarSettings Car(int index) => index == 0 ? carA : carB;

        [Header("Cars together (they never touch)")]
        [Tooltip("Smallest gap in metres allowed between the two car bodies, anywhere.")]
        public float carMinClearance = 0.4f;
        [Tooltip("While she emerges: both cars circle opposite each other on one loop (x/z radii, metres).")]
        public Vector2 carEmergeOrbitRadii = new(1.8f, 1.2f);
        public float carOrbitSpeed = 1.4f;
        [Tooltip("Through the portal after her: how far to each side of the portal the cars wait their turn.")]
        public float carPortalQueueSide = 3.4f;
        public float carPortalDiveSeconds = 1.3f;
        public float carPortalHiddenSeconds = 0.5f;
        public float carPortalExitSeconds = 2f;
        [Tooltip("Where each car hovers above her after the portal: metres to her side and above the ground.")]
        public float carEscortSide = 2.4f;
        public float carEscortHeight = 3.6f;
        [Tooltip("In the savanna: the swirl centre's height above her feet.")]
        public float carSwirlHeight = 6f;
        [Tooltip("Swirl radius breathes between these (metres); the cars stay opposite, so their gap is twice this.")]
        public Vector2 carSwirlRadius = new(2.8f, 4.2f);
        public float carSwirlSpeed = 2.4f;
        [Tooltip("The cars rise and dip opposite each other by this much (metres).")]
        public float carSwirlLift = 0.7f;
        [Tooltip("How far the swirl centre drifts around her (metres), so the cars roam while she talks.")]
        public float carSwirlDrift = 1.2f;
        [Tooltip("Car engine loudness while she speaks, as a fraction (her voice comes first).")]
        [Range(0f, 1f)] public float carVolumeDuringStory = 0.35f;
        [Tooltip("Seconds before a car stops that its brake sound starts.")]
        public float carBrakeLeadSeconds = 0.7f;

        [Header("Story (her voice and the music bed, both start as the savanna appears)")]
        [Range(0f, 1f)] public float voiceVolume = 1f;
        [Range(0f, 1f)] public float storyMusicVolume = 0.8f;
        [Tooltip("Seconds after her last word before the story counts as finished.")]
        public float storyTailSeconds = 5f;

        [Header("Night (falls during her story)")]
        [Tooltip("Seconds into her story when dusk begins (49.5 s is a natural pause in the Final Voice).")]
        public float nightFallsAtSeconds = 49.5f;
        public float nightTransitionSeconds = 10f;
        public int starCount = 450;

        [Header("Cinematic camera (the view during her story; the phone steers again afterwards)")]
        public bool cinematicCamera = true;
        [Tooltip("Seconds to glide back to the phone's view when the story ends.")]
        public float cinematicReturnSeconds = 2.5f;
        [Tooltip("Shots in time order. Position is yaw (degrees around the subject, 0 = in front of her), distance, height.")]
        public CinematicShot[] shots =
        {
            new() { name = "Opening push-in", startSeconds = 0f, subject = ShotSubject.Woman, from = new(0f, 6f, 1.6f), to = new(10f, 2.8f, 1.6f), lookFrom = new(0f, 1.45f, 0f), lookTo = new(0f, 1.6f, 0f), fovFrom = 55f, fovTo = 42f, blendSeconds = 2.5f },
            new() { name = "Low hero", startSeconds = 9.6f, subject = ShotSubject.Woman, from = new(-35f, 2.6f, 0.55f), to = new(-20f, 2.3f, 0.5f), lookFrom = new(0f, 1.8f, 0f), lookTo = new(0f, 2.6f, 0f), fovFrom = 50f, fovTo = 48f, blendSeconds = 0f },
            new() { name = "Orbit", startSeconds = 16.1f, subject = ShotSubject.Woman, from = new(30f, 4.2f, 1.7f), to = new(150f, 4.2f, 1.9f), lookFrom = new(0f, 1.4f, 0f), lookTo = new(0f, 1.4f, 0f), fovFrom = 45f, fovTo = 45f, blendSeconds = 1.2f },
            new() { name = "High crane", startSeconds = 22.1f, subject = ShotSubject.Woman, from = new(180f, 5f, 2f), to = new(200f, 9f, 8f), lookFrom = new(0f, 1.2f, 0f), lookTo = new(0f, 0.5f, 0f), fovFrom = 50f, fovTo = 55f, blendSeconds = 1.5f },
            new() { name = "Into the baobab", startSeconds = 32.2f, subject = ShotSubject.Tree, treeIndex = 0, from = new(20f, 16f, 2.5f), to = new(10f, 9f, 4.5f), lookFrom = new(0f, 4f, 0f), lookTo = new(0f, 6.5f, 0f), fovFrom = 50f, fovTo = 35f, blendSeconds = 0f },
            new() { name = "The cars above", startSeconds = 42.8f, subject = ShotSubject.Cars, from = new(0f, 12f, -2f), to = new(40f, 11f, -2f), lookFrom = Vector3.zero, lookTo = Vector3.zero, fovFrom = 50f, fovTo = 45f, blendSeconds = 1.5f },
            new() { name = "Night falls", startSeconds = 49.5f, subject = ShotSubject.Woman, from = new(15f, 4f, 1.2f), to = new(25f, 3.5f, 1.1f), lookFrom = new(0f, 5f, -20f), lookTo = new(0f, 7f, -20f), fovFrom = 60f, fovTo = 60f, blendSeconds = 1.5f },
            new() { name = "Fireflies in the acacia", startSeconds = 61.3f, subject = ShotSubject.Tree, treeIndex = 1, from = new(30f, 7f, 1.2f), to = new(15f, 4.5f, 1.5f), lookFrom = new(0f, 1.5f, 0f), lookTo = new(0f, 2f, 0f), fovFrom = 45f, fovTo = 32f, blendSeconds = 0f },
            new() { name = "Fireflies in the jacaranda", startSeconds = 70f, subject = ShotSubject.Tree, treeIndex = 4, from = new(-20f, 8f, 1f), to = new(-5f, 5f, 2f), lookFrom = new(0f, 2f, 0f), lookTo = new(0f, 2f, 0f), fovFrom = 45f, fovTo = 35f, blendSeconds = 1.5f },
            new() { name = "Her, by starlight", startSeconds = 80.9f, subject = ShotSubject.Woman, from = new(-60f, 3.2f, 0.5f), to = new(30f, 3.4f, 1f), lookFrom = new(0f, 1.7f, 0f), lookTo = new(0f, 1.6f, 0f), fovFrom = 48f, fovTo = 48f, blendSeconds = 0f },
            new() { name = "Cars under the stars", startSeconds = 91f, subject = ShotSubject.Cars, from = new(90f, 12f, -1.5f), to = new(150f, 11f, -0.5f), lookFrom = Vector3.zero, lookTo = Vector3.zero, fovFrom = 50f, fovTo = 50f, blendSeconds = 1.5f },
            new() { name = "Closing rise", startSeconds = 96f, subject = ShotSubject.Woman, from = new(0f, 4f, 1.6f), to = new(0f, 14f, 8f), lookFrom = new(0f, 1.5f, 0f), lookTo = new(0f, 1f, 0f), fovFrom = 45f, fovTo = 55f, blendSeconds = 1.5f },
        };

        [Header("Particles")]
        [Tooltip("Fireflies per cluster (one cluster per tree, one around her).")]
        public int fireflyCount = 40;
        public int leafCount = 30;
        public int dustCount = 60;

        [Header("Wind")]
        public Vector2 windStrengthRange = new(0.2f, 1f);

        [Header("Audio")]
        [Range(0f, 100f)] public float ambienceDuckPercent = 40f;

        [Header("Touch")]
        public float swipeThresholdPixels = 80f;
        public float doubleTapWindowSeconds = 0.3f;
    }
}
