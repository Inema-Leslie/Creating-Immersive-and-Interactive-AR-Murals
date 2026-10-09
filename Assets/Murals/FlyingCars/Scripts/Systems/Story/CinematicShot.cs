using System;
using UnityEngine;

namespace MuralAR
{
    public enum ShotSubject
    {
        Woman,
        Tree,
        Cars,
    }

    [Serializable]
    public struct CinematicShot
    {
        public string name;
        public float startSeconds;
        public ShotSubject subject;
        [Tooltip("For Tree shots: which tree (child order under Savanna/Trees).")]
        public int treeIndex;
        public Vector3 from;
        public Vector3 to;
        public Vector3 lookFrom;
        public Vector3 lookTo;
        public float fovFrom;
        public float fovTo;
        [Tooltip("Seconds to glide in from the previous shot; 0 cuts.")]
        public float blendSeconds;
    }
}
