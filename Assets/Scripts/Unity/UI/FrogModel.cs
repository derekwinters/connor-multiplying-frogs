using System;
using System.Collections.Generic;
using Frogs.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frogs.Unity.UI
{
    /// <summary>
    /// The frog piece — the approved Blender frog (tools/art/frog_model.py),
    /// posed by its own Sit and Hop clips and drawn into the board's canvas in
    /// its frog's colour. docs/specs/ui/game-board.md § The frog piece, FP-1
    /// to FP-9 (#461).
    ///
    /// **The model is imported, not built.** One mesh, one albedo texture, a
    /// 14-bone rig and two clips, loaded from
    /// <c>Assets/Art/Frog/Resources/FrogPiece/</c>. Each piece holds its own
    /// copy of the rig; <see cref="SetPose"/> samples the clip Core's
    /// <see cref="FrogPose"/> names at the fraction it gives. No bone is ever
    /// turned by hand here — what the frog looks like at every point of a hop
    /// is the clip Derek approved.
    ///
    /// **It is drawn straight into the board's canvas, as a mesh.** The board
    /// is a uGUI screen-space overlay, and a SkinnedMeshRenderer is not drawn
    /// by one. The usual answer — a second camera rendering into a texture —
    /// needs a camera, a layer, a light, a lit shader and a render texture per
    /// frog, any of which a player build can quietly be missing. Instead the
    /// rig's renderer is switched off and the mesh is skinned here, on the CPU,
    /// from the rig's bones; then it is turned to the board's fixed view,
    /// scaled into the piece's own rectangle, shaded against one fixed light,
    /// and handed to uGUI back to front (the painter's algorithm — the UI
    /// shader writes no depth). Everything is drawn with uGUI's default
    /// material, which is always in a build.
    ///
    /// Two graphics, because the outline must be untextured and drawn behind:
    /// this one draws the <c>PieceEdge</c> silhouette edge (FP-4), and its
    /// child <see cref="Skin"/> draws the frog itself, textured with the
    /// albedo and tinted with this graphic's <see cref="Graphic.color"/>.
    ///
    /// This type knows how a frog is *drawn*. Which clip and how far through
    /// it is Core's (<see cref="FrogPose"/>), and when it changes is whoever
    /// calls <see cref="SetPose"/> — it holds no clock.
    /// </summary>
    public sealed class FrogModel : MaskableGraphic
    {
        // game-board.md § The frog piece — "How it is drawn".

        /// <summary>How far the frog is turned toward the player from a pure side view, in degrees (FP-5).</summary>
        public const float FrogViewYaw = 25f;

        /// <summary>How far it is seen from above, in degrees (FP-5).</summary>
        public const float FrogViewPitch = 25f;

        /// <summary>The darkest shading takes a vertex, as a fraction of the frog colour.</summary>
        public const float FrogShadeFloor = 0.7f;

        /// <summary>
        /// How squarely a surface must meet the light — the cosine of the
        /// angle between them — to be drawn in exactly the frog colour.
        /// Surfaces turned further away shade down toward
        /// <see cref="FrogShadeFloor"/>.
        /// </summary>
        public const float FrogFullyLit = 0.6f;

        /// <summary>The model, for <see cref="Resources.Load(string)"/>: Assets/Art/Frog/Resources/FrogPiece/frog.fbx.</summary>
        public const string ModelResource = "FrogPiece/frog";

        /// <summary>Its albedo: Assets/Art/Frog/Resources/FrogPiece/frog_albedo.png.</summary>
        public const string AlbedoResource = "FrogPiece/frog_albedo";

        // The clips' names as the file's takes give them. The exporter may
        // prefix the armature's name ("FrogRig|Hop"), so either is accepted.
        const string HopTake = "Hop";
        const string SitTake = "Sit";
        const char TakeSeparator = '|';

        // The editor adds hidden preview copies of every clip; they are not the clip.
        const string PreviewPrefix = "__preview__";

        // The one light, in the piece's own space: from the upper left, in
        // front of the screen. (Unity's z runs into the screen, so the viewer
        // is at negative z.)
        static readonly Vector3 s_light = new Vector3(-0.3f, 0.8f, -0.5f).normalized;

        static readonly string[] s_legParts = { "thigh", "shin", "foot" };

        // Read once, shared by every piece: the model asset and what is in it.
        static GameObject s_model;
        static Texture2D s_albedo;

        /// <summary>The model's albedo — what <see cref="FrogSkin"/> is textured with. Null until a frog has loaded the model.</summary>
        internal static Texture2D Albedo
        {
            get { return s_albedo; }
        }

        static AnimationClip s_hop;
        static AnimationClip s_sit;
        static Vector3[] s_vertices;
        static Vector3[] s_normals;
        static Vector2[] s_uv;
        static int[] s_triangles;
        static BoneWeight[] s_weights;
        static Matrix4x4[] s_bindposes;

        // Measured once from the first rig: the turn from the model's own
        // axes to the board's view, and the box the sitting frog fills in it.
        static bool s_measured;
        static Matrix4x4 s_toView;
        static Rect s_sitBounds;

        /// <summary>One of the frog's four legs: a thigh, a shin and a foot, each hung from the one before.</summary>
        public sealed class Leg
        {
            internal Leg(bool isFront, bool isLeft, Transform thigh, Transform shin, Transform foot)
            {
                IsFront = isFront;
                IsLeft = isLeft;
                Thigh = thigh;
                Shin = shin;
                Foot = foot;
            }

            /// <summary>A front leg, or a back one.</summary>
            public bool IsFront { get; }

            /// <summary>The frog's own left — the side away from the player.</summary>
            public bool IsLeft { get; }

            /// <summary>The thigh bone, turned at the hip.</summary>
            public Transform Thigh { get; }

            /// <summary>The shin bone, a child of <see cref="Thigh"/>, turned at the knee.</summary>
            public Transform Shin { get; }

            /// <summary>The foot bone, a child of <see cref="Shin"/>, turned at the ankle.</summary>
            public Transform Foot { get; }
        }

        // One triangle on its way to the mesh: its three corners, and how far
        // its middle is from the viewer.
        struct Face
        {
            public int A;
            public int B;
            public int C;
            public float Depth;
        }

        readonly List<Leg> _legs = new List<Leg>();
        readonly List<Face> _faces = new List<Face>();

        Transform _rig;
        Transform[] _bones;
        Matrix4x4[] _skinning;
        FrogSkin _skin;
        FrogPose _pose = FrogPose.Rest;
        bool _built;

        // The posed mesh, in the board's view and at unit scale; rebuilt only
        // when the pose changes.
        Vector3[] _posed;
        Vector3[] _posedNormals;
        bool _posedIsStale = true;

        Color _outlineColour = BoardColours.PieceEdge;
        float _outlineWidth;

        /// <summary>The four legs — two front, two back, one of each on either side.</summary>
        public IReadOnlyList<Leg> Legs
        {
            get
            {
                EnsureBuilt();
                return _legs;
            }
        }

        /// <summary>This piece's copy of the model: the rig its clips are sampled onto.</summary>
        public Transform Rig
        {
            get
            {
                EnsureBuilt();
                return _rig;
            }
        }

        /// <summary>The graphic that draws the frog itself, over this one's outline.</summary>
        public FrogSkin Skin
        {
            get
            {
                EnsureBuilt();
                return _skin;
            }
        }

        /// <summary>The model's Hop clip — sit, push off, tuck, reach, sit.</summary>
        public AnimationClip HopClip
        {
            get
            {
                EnsureBuilt();
                return s_hop;
            }
        }

        /// <summary>The model's Sit clip — the pose of a frog that is not hopping.</summary>
        public AnimationClip SitClip
        {
            get
            {
                EnsureBuilt();
                return s_sit;
            }
        }

        /// <summary>The pose the frog was last given — <see cref="FrogPose.Rest"/> until told otherwise.</summary>
        public FrogPose Pose
        {
            get { return _pose; }
        }

        /// <summary>The silhouette's edge: <c>PieceEdge</c>, <c>FrogPieceOutline</c> wide, set by the board.</summary>
        public void SetOutline(Color colour, float width)
        {
            _outlineColour = colour;
            _outlineWidth = width;
            SetVerticesDirty();
        }

        /// <summary>
        /// Poses the frog: samples <paramref name="pose"/>'s clip at its
        /// fraction of the way through, onto this piece's rig.
        /// </summary>
        public void SetPose(FrogPose pose)
        {
            EnsureBuilt();

            _pose = pose;
            Sample(pose);
            _posedIsStale = true;

            SetVerticesDirty();
        }

        /// <summary>
        /// Where a point of the rig — a bone, say — is drawn, in the piece's
        /// own rectangle. What a test checks the frog's facing with.
        /// </summary>
        public Vector2 PiecePointOf(Transform point)
        {
            EnsureBuilt();

            var inView = s_toView.MultiplyPoint3x4(_rig.InverseTransformPoint(point.position));
            return ToPiece(inView);
        }

        public override void SetVerticesDirty()
        {
            base.SetVerticesDirty();

            // The tint is this graphic's colour, so a new colour must redraw
            // the skin too.
            if (_skin != null)
            {
                _skin.SetVerticesDirty();
            }
        }

        protected override void Awake()
        {
            base.Awake();
            EnsureBuilt();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            DrawOutline(vh);
        }

        /// <summary>
        /// Writes the outline into <paramref name="vh"/>: the posed frog's
        /// surfaces that face the player, pushed out along their own normals
        /// by the outline's width, in <c>PieceEdge</c>. The skin is drawn over
        /// it, so only the rim beyond the frog's silhouette shows. Public so a
        /// test can read the triangles without a canvas rebuild.
        /// </summary>
        public void DrawOutline(VertexHelper vh)
        {
            EnsureBuilt();
            vh.Clear();

            if (_outlineWidth <= 0f)
            {
                return;
            }

            EnsurePosed();

            for (var i = 0; i < _posed.Length; i++)
            {
                var at = ToPiece(_posed[i]);
                var push = _posedNormals[i] * _outlineWidth;

                vh.AddVert(new Vector3(at.x + push.x, at.y + push.y, 0f), _outlineColour, Vector2.zero);
            }

            // Only the half facing the player: the back half would lie under
            // the front one, and PieceEdge is translucent, so a second layer
            // would draw the edge twice as dark.
            CollectFacingFaces();

            foreach (var face in _faces)
            {
                vh.AddTriangle(face.A, face.B, face.C);
            }
        }

        // What the skin draws: every vertex in the piece's rectangle, shaded
        // and tinted, then the triangles facing the player, furthest first.
        internal void DrawSkin(VertexHelper vh)
        {
            EnsureBuilt();
            EnsurePosed();

            for (var i = 0; i < _posed.Length; i++)
            {
                var at = ToPiece(_posed[i]);

                vh.AddVert(new Vector3(at.x, at.y, 0f), Shaded(_posedNormals[i]), s_uv[i]);
            }

            CollectFacingFaces();

            // Painter's algorithm: the UI shader writes no depth, so whatever
            // is drawn last is in front. Larger z is further from the player.
            _faces.Sort((a, b) => b.Depth.CompareTo(a.Depth));

            foreach (var face in _faces)
            {
                vh.AddTriangle(face.A, face.B, face.C);
            }
        }

        void CollectFacingFaces()
        {
            _faces.Clear();

            for (var i = 0; i < s_triangles.Length; i += 3)
            {
                var a = s_triangles[i];
                var b = s_triangles[i + 1];
                var c = s_triangles[i + 2];

                // The mesh's own smooth normals say which way a triangle faces,
                // whatever its winding came through the importer as.
                var facing = _posedNormals[a] + _posedNormals[b] + _posedNormals[c];

                if (facing.z > 0f)
                {
                    continue;
                }

                _faces.Add(new Face { A = a, B = b, C = c, Depth = _posed[a].z + _posed[b].z + _posed[c].z });
            }
        }

        // Lit surfaces are exactly the frog colour (times the albedo, which is
        // white wherever the frog is its colour), and shading only darkens —
        // game-board.md's invariant, because the separability bar is measured
        // against the frog colours themselves.
        Color Shaded(Vector3 normal)
        {
            var light = Mathf.Clamp01(Vector3.Dot(normal, s_light) / FrogFullyLit);
            var shade = Mathf.Lerp(FrogShadeFloor, 1f, light);
            var tint = color;

            return new Color(tint.r * shade, tint.g * shade, tint.b * shade, tint.a);
        }

        // The one scale and placement (FP-2, #474): the sitting frog, outline
        // included, as big as fits the piece's square, its box centred on the
        // square's centre. The hop is drawn at the same scale and may
        // overshoot the square — it is not fitted.
        FrogPieceFit Fit
        {
            get
            {
                var size = rectTransform.rect.size;

                return FrogPieceFit.Fit(
                    s_sitBounds.xMin,
                    s_sitBounds.yMin,
                    s_sitBounds.xMax,
                    s_sitBounds.yMax,
                    Mathf.Min(size.x, size.y),
                    _outlineWidth);
            }
        }

        // From the board's view at unit scale to the piece's rectangle.
        Vector2 ToPiece(Vector3 inView)
        {
            var fit = Fit;

            return new Vector2(fit.ToPieceX(inView.x), fit.ToPieceY(inView.y)) + rectTransform.rect.center;
        }

        // Skins the mesh at the rig's current pose, into the board's view.
        void EnsurePosed()
        {
            if (!_posedIsStale)
            {
                return;
            }

            SkinMesh(_posed, _posedNormals);
            _posedIsStale = false;
        }

        void SkinMesh(Vector3[] positions, Vector3[] normals)
        {
            // Each bone's skinning matrix, into the rig's own space and then
            // the board's view. A bindpose takes a vertex from the mesh into
            // its bone's space as it was bound; the bone's transform now takes
            // it back out, posed — Unity's documented skinning, Mesh.bindposes.
            var toRig = _rig.worldToLocalMatrix;

            for (var b = 0; b < _bones.Length; b++)
            {
                _skinning[b] = s_toView * toRig * _bones[b].localToWorldMatrix * s_bindposes[b];
            }

            for (var i = 0; i < s_vertices.Length; i++)
            {
                var w = s_weights[i];
                var position = Vector3.zero;
                var normal = Vector3.zero;

                Blend(w.boneIndex0, w.weight0, i, ref position, ref normal);
                Blend(w.boneIndex1, w.weight1, i, ref position, ref normal);
                Blend(w.boneIndex2, w.weight2, i, ref position, ref normal);
                Blend(w.boneIndex3, w.weight3, i, ref position, ref normal);

                positions[i] = position;
                normals[i] = normal.normalized;
            }
        }

        void Blend(int bone, float weight, int vertex, ref Vector3 position, ref Vector3 normal)
        {
            if (weight <= 0f)
            {
                return;
            }

            var m = _skinning[bone];

            position += m.MultiplyPoint3x4(s_vertices[vertex]) * weight;
            normal += m.MultiplyVector(s_normals[vertex]) * weight;
        }

        void Sample(FrogPose pose)
        {
            var clip = pose.Clip == FrogClip.Hop ? s_hop : s_sit;

            clip.SampleAnimation(_rig.gameObject, Mathf.Clamp01(pose.Fraction) * clip.length);
        }

        // Graphic gives no guarantee Awake() has run before a caller reaches
        // this one straight after AddComponent — the same reasoning as every
        // view's EnsureInitialized in this project.
        void EnsureBuilt()
        {
            if (_built)
            {
                return;
            }

            _built = true;
            raycastTarget = false;

            LoadModel();

            _rig = Instantiate(s_model, transform, worldPositionStays: false).transform;
            _rig.name = "Rig";

            // A non-legacy clip is sampled through an Animator outside the
            // editor; it needs no controller. A Generic import gives the model
            // one, but the sampling must not depend on that.
            if (_rig.GetComponent<Animator>() == null)
            {
                _rig.gameObject.AddComponent<Animator>();
            }

            var renderer = _rig.GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true);
            renderer.enabled = false;
            _bones = renderer.bones;
            _skinning = new Matrix4x4[_bones.Length];
            _posed = new Vector3[s_vertices.Length];
            _posedNormals = new Vector3[s_vertices.Length];

            foreach (var which in new[] { "front", "back" })
            {
                foreach (var side in new[] { "L", "R" })
                {
                    _legs.Add(new Leg(
                        which == "front",
                        side == "L",
                        Bone(which, s_legParts[0], side),
                        Bone(which, s_legParts[1], side),
                        Bone(which, s_legParts[2], side)));
                }
            }

            if (!s_measured)
            {
                Measure();
            }

            var skinGO = new GameObject("Skin", typeof(RectTransform));
            var skinRect = (RectTransform)skinGO.transform;
            skinRect.SetParent(transform, worldPositionStays: false);
            skinRect.anchorMin = Vector2.zero;
            skinRect.anchorMax = Vector2.one;
            skinRect.pivot = new Vector2(0.5f, 0.5f);
            skinRect.offsetMin = Vector2.zero;
            skinRect.offsetMax = Vector2.zero;

            _skin = skinGO.AddComponent<FrogSkin>();
            _skin.Owner = this;
            _skin.raycastTarget = false;

            SetPose(_pose);
        }

        Transform Bone(string which, string part, string side)
        {
            var name = which + "_" + part + "." + side;

            foreach (var bone in _bones)
            {
                if (bone.name == name)
                {
                    return bone;
                }
            }

            throw new InvalidOperationException($"The frog model has no bone named {name}.");
        }

        Transform BoneNamed(string name)
        {
            foreach (var bone in _bones)
            {
                if (bone.name == name)
                {
                    return bone;
                }
            }

            throw new InvalidOperationException($"The frog model has no bone named {name}.");
        }

        // Works out, once, how the model's own axes turn into the board's
        // view, and the box the sitting frog fills there — what the scale is
        // fitted to (FP-2).
        //
        // The frog's facing is read off its own bones rather than assumed
        // from the importer's axis conversion: forward runs from the back
        // hips to the front hips, up from the feet to the hips, sitting. That
        // frame is turned so the frog faces +x (the End log), stands up +y,
        // and shows the player its right side; then the view turns it
        // FrogViewYaw toward the player and FrogViewPitch to be seen from
        // above (FP-5).
        void Measure()
        {
            s_toView = Matrix4x4.identity;
            Sample(FrogPose.Rest);

            Vector3 Mid(params string[] names)
            {
                var sum = Vector3.zero;

                foreach (var name in names)
                {
                    sum += _rig.InverseTransformPoint(BoneNamed(name).position);
                }

                return sum / names.Length;
            }

            var backHips = Mid("back_thigh.L", "back_thigh.R");
            var frontHips = Mid("front_thigh.L", "front_thigh.R");
            var feet = Mid("back_foot.L", "back_foot.R", "front_foot.L", "front_foot.R");

            var forward = (frontHips - backHips).normalized;
            var up = Vector3.ProjectOnPlane(((frontHips + backHips) / 2f) - feet, forward).normalized;
            var right = Vector3.Cross(up, forward);

            var frame = Matrix4x4.identity;
            frame.SetRow(0, forward);
            frame.SetRow(1, up);
            frame.SetRow(2, -right);

            var view = Quaternion.AngleAxis(FrogViewPitch, Vector3.right) * Quaternion.AngleAxis(FrogViewYaw, Vector3.up);
            s_toView = Matrix4x4.Rotate(view) * frame;

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);

            // Still sitting, from the top of this method.
            SkinMesh(_posed, _posedNormals);

            foreach (var point in _posed)
            {
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            s_sitBounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            s_measured = true;
        }

        static void LoadModel()
        {
            if (s_model != null)
            {
                return;
            }

            s_model = Resources.Load<GameObject>(ModelResource);
            s_albedo = Resources.Load<Texture2D>(AlbedoResource);

            if (s_model == null || s_albedo == null)
            {
                throw new InvalidOperationException(
                    $"The frog model is missing: expected Resources/{ModelResource} and Resources/{AlbedoResource}.");
            }

            foreach (var clip in Resources.LoadAll<AnimationClip>(ModelResource))
            {
                if (clip.name.StartsWith(PreviewPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                if (IsTake(clip.name, HopTake))
                {
                    s_hop = clip;
                }
                else if (IsTake(clip.name, SitTake))
                {
                    s_sit = clip;
                }
            }

            if (s_hop == null || s_sit == null)
            {
                throw new InvalidOperationException($"The frog model has no {HopTake} or no {SitTake} clip.");
            }

            var mesh = s_model.GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true).sharedMesh;

            s_vertices = mesh.vertices;
            s_normals = mesh.normals;
            s_uv = mesh.uv;
            s_triangles = mesh.triangles;
            s_weights = mesh.boneWeights;
            s_bindposes = mesh.bindposes;
        }

        static bool IsTake(string clipName, string take)
        {
            return clipName == take || clipName.EndsWith(TakeSeparator + take, StringComparison.Ordinal);
        }
    }
}
