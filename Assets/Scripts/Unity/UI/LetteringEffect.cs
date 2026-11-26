using UnityEngine;
using UnityEngine.UI;

namespace Frogs.Unity.UI
{
    /// <summary>RED STUB (#520): letter spacing and an outside outline. Implemented in the next commit.</summary>
    public sealed class LetteringEffect : BaseMeshEffect
    {
        public float Tracking { get; private set; }
        public float OutlineWidth { get; private set; }
        public Color OutlineColor { get; private set; }

        public void Configure(float tracking, float outlineWidth, Color outlineColor) { }

        public override void ModifyMesh(VertexHelper vh) { }
    }
}
