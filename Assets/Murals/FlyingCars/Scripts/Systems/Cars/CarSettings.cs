using System;
using UnityEngine;

namespace MuralAR
{
    [Serializable]
    public struct CarSettings
    {
        public string displayName;
        [TextArea(2, 4)] public string design;
        [TextArea(2, 4)] public string howItHovers;
        [TextArea(2, 4)] public string role;

        [Tooltip("The painted car on the tracking image: centre and size, normalised, from the top-left.")]
        public Vector2 paintedCenter;
        public Vector2 paintedSize;

        [Tooltip("Length once it has grown to full size and flies.")]
        public float flyingLengthMeters;
        [Tooltip("Shift of its idle hover spot from straight in front of its painting (metres, across and up), so the two cars don't overlap.")]
        public Vector2 hoverOffset;
        [Tooltip("Flight speed in metres per second.")]
        public float cruiseSpeed;
        [Tooltip("Radius of its loops: wide and slow for the heavy car, tight for the light one.")]
        public float loopRadius;
        public float bankDegrees;

        [Tooltip("Engine pitch at idle and at full rev.")]
        public float idlePitch;
        public float revPitch;
        public Color lightColor;

        [Tooltip("Extra delay before this car wakes, so they don't start in unison.")]
        public float wakeDelaySeconds;
    }
}
