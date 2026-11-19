using UnityEngine;
using UnityEngine.UI;

namespace Frogs.Unity.UI
{
    /// <summary>
    /// The frog itself — the skinned mesh of the approved frog model, textured
    /// with its albedo and tinted with its <see cref="FrogModel"/>'s colour.
    /// docs/specs/ui/game-board.md § The frog piece.
    ///
    /// A child of the piece, so uGUI draws it over the <see cref="FrogModel"/>
    /// that owns it, which draws the untextured outline. It works nothing out
    /// for itself: the triangles are its owner's.
    /// </summary>
    public sealed class FrogSkin : MaskableGraphic
    {
        internal FrogModel Owner { get; set; }

        public override Texture mainTexture
        {
            get { return FrogModel.Albedo != null ? FrogModel.Albedo : base.mainTexture; }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            Draw(vh);
        }

        /// <summary>
        /// What <see cref="OnPopulateMesh"/> writes — public so a test can read
        /// the triangles without a canvas rebuild.
        /// </summary>
        public void Draw(VertexHelper vh)
        {
            vh.Clear();

            if (Owner != null)
            {
                Owner.DrawSkin(vh);
            }
        }
    }
}
