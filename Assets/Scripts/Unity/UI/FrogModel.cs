using System.Collections.Generic;
using Frogs.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frogs.Unity.UI
{
    /// <summary>
    /// The frog piece — a small 3D frog built from simple shapes, whose legs
    /// take whatever <see cref="FrogLegPose"/> it is handed.
    /// docs/specs/ui/game-board.md § The frog piece, FP-1 to FP-8 (#461).
    ///
    /// **It is built, not imported.** A body, a head, two eyes with a pupil
    /// each, and four legs; every one of those is an ellipsoid on its own
    /// transform, and each leg is a thigh hung from a <see cref="Leg.Hip"/>
    /// with a shin hung from a <see cref="Leg.Knee"/> on the end of it. Turning
    /// a joint's transform moves everything below it, so posing the legs is
    /// four pairs of rotations and nothing else.
    ///
    /// **It is drawn straight into the board's canvas, as a mesh.** The board
    /// is a uGUI screen-space overlay, and a MeshRenderer is not drawn by one.
    /// The usual answer — a second camera rendering into a texture — would
    /// need a camera, a layer, a light, a lit shader and a render texture per
    /// frog, any of which a player build can quietly be missing. Instead this
    /// is an ordinary <see cref="MaskableGraphic"/>: it takes each shape's
    /// triangles into the piece's own rectangle, drops those facing away,
    /// shades the rest by hand against one fixed light, and hands them to
    /// uGUI back to front (the painter's algorithm — the UI shader writes no
    /// depth). It therefore moves, scales and layers with the board exactly
    /// as the flat disc it replaced did, with the default UI material.
    ///
    /// The silhouette gets a <c>FrogPieceOutline</c>-wide edge in
    /// <c>PieceEdge</c> (FP-4): every shape is drawn once more, pushed out
    /// along its own surface by that width, *underneath* the frog — so only
    /// the rim beyond the frog's own outline shows.
    ///
    /// This type knows how a frog *looks*. Where its legs are is Core's
    /// (<see cref="FrogLegPose"/>), and when they move is whoever calls
    /// <see cref="SetLegPose"/> — it holds no clock.
    /// </summary>
    public sealed class FrogModel : MaskableGraphic
    {
        // game-board.md § The frog piece — "How it is drawn".

        /// <summary>How far the frog is turned toward the player from a pure side view, in degrees (FP-5).</summary>
        public const float FrogViewYaw = 25f;

        /// <summary>How far it is seen from above, in degrees (FP-5).</summary>
        public const float FrogViewPitch = 25f;

        /// <summary>The darkest a shaded face gets, as a fraction of the frog colour.</summary>
        public const float FrogShadeFloor = 0.7f;

        /// <summary>
        /// How squarely a face must meet the light — the cosine of the angle
        /// between them — to be drawn in exactly the frog colour. Faces
        /// turned further away shade down toward <see cref="FrogShadeFloor"/>.
        /// </summary>
        public const float FrogFullyLit = 0.6f;

        // The frog's proportions, in pixels before FrogModelScale. Placeholders
        // for Connor to judge on the tablet — that it has these parts is the
        // spec; their sizes are not. x runs toward the head, y up, z across.
        const float FrogModelScale = 0.9f;

        static readonly Vector3 s_bodyCentre = Vector3.zero;
        static readonly Vector3 s_bodyRadii = new Vector3(20f, 13f, 15f);
        static readonly Vector3 s_headCentre = new Vector3(19f, 6f, 0f);
        static readonly Vector3 s_headRadii = new Vector3(12f, 9f, 12f);
        static readonly Vector3 s_eyeCentre = new Vector3(21f, 14f, 7f);
        const float EyeRadius = 5f;

        // The pupil sits on the front of the eye, looking forward, up a
        // little and out to its own side.
        static readonly Vector3 s_pupilLooks = new Vector3(1f, 0.4f, 0.9f);
        const float PupilOut = 3.5f;
        const float PupilRadius = 2.4f;

        static readonly Vector3 s_backHip = new Vector3(-13f, -5f, 11f);
        static readonly Vector3 s_frontHip = new Vector3(12f, -8f, 9f);
        const float BackThighLength = 14f;
        const float BackThighRadius = 5f;
        const float BackShinLength = 14f;
        const float BackShinRadius = 4f;
        const float FrontUpperLength = 9f;
        const float FrontUpperRadius = 3.5f;
        const float FrontLowerLength = 9f;
        const float FrontLowerRadius = 3f;

        // How far a leg segment runs on past each of its joints, as a fraction
        // of its radius — so thigh and shin overlap at the knee instead of
        // meeting at two points.
        const float SegmentOverlap = 0.8f;

        // The one light, in the piece's own space: from the upper left, in
        // front of the screen. (Unity's z runs into the screen, so the viewer
        // is at negative z.)
        static readonly Vector3 s_light = new Vector3(-0.3f, 0.8f, -0.5f).normalized;

        // The unit sphere every part is drawn from: Longitude x Latitude
        // quads, less the degenerate halves at the poles.
        const int Longitude = 12;
        const int Latitude = 8;
        static Vector3[] s_sphere;

        /// <summary>One of the frog's four legs: a thigh on a hip, a shin on a knee.</summary>
        public sealed class Leg
        {
            internal Leg(bool isFront, bool isLeft, Transform hip, Transform knee)
            {
                IsFront = isFront;
                IsLeft = isLeft;
                Hip = hip;
                Knee = knee;
            }

            /// <summary>A front leg, or a back one.</summary>
            public bool IsFront { get; }

            /// <summary>The frog's own left — the side away from the player.</summary>
            public bool IsLeft { get; }

            /// <summary>Where the leg hangs from the body. Turned by the pose's hip angle about the frog's side-to-side axis.</summary>
            public Transform Hip { get; }

            /// <summary>The joint between thigh and shin, a child of <see cref="Hip"/>. Turned back by the pose's knee angle.</summary>
            public Transform Knee { get; }
        }

        // A shape to draw: its transform, and whether it is a pupil (drawn
        // flat in BoardInk) rather than skin (drawn shaded in the frog colour).
        struct Part
        {
            public Transform Transform;
            public bool IsPupil;
        }

        // One triangle on its way to the mesh, in the piece's own space.
        struct Face
        {
            public Vector3 A;
            public Vector3 B;
            public Vector3 C;
            public Color Colour;
            public float Depth;
        }

        readonly List<Part> _parts = new List<Part>();
        readonly List<Leg> _legs = new List<Leg>();
        readonly List<Face> _faces = new List<Face>();
        readonly List<Face> _outline = new List<Face>();

        Transform _model;
        FrogLegPose _pose = FrogLegPose.Rest;
        bool _built;

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

        /// <summary>The pose the legs were last given — <see cref="FrogLegPose.Rest"/> until told otherwise.</summary>
        public FrogLegPose LegPose
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
        /// Turns every leg's joints to <paramref name="pose"/>: both front legs
        /// to its <see cref="FrogLegPose.Front"/>, both back legs to its
        /// <see cref="FrogLegPose.Back"/>.
        /// </summary>
        public void SetLegPose(FrogLegPose pose)
        {
            EnsureBuilt();

            _pose = pose;

            foreach (var leg in _legs)
            {
                var joints = leg.IsFront ? pose.Front : pose.Back;

                // A positive hip angle swings the thigh forward, toward the
                // head: about z, +x is turned toward +y, so straight down is
                // turned toward +x.
                leg.Hip.localRotation = Quaternion.AngleAxis(joints.Hip, Vector3.forward);

                // The knee folds the shin back, the other way about.
                leg.Knee.localRotation = Quaternion.AngleAxis(-joints.Knee, Vector3.forward);
            }

            SetVerticesDirty();
        }

        protected override void Awake()
        {
            base.Awake();
            EnsureBuilt();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            Draw(vh);
        }

        /// <summary>
        /// Writes the frog into <paramref name="vh"/>: the outline first, then
        /// every face turned toward the player, furthest first. What
        /// <see cref="OnPopulateMesh"/> does, public so a test can read the
        /// triangles without a canvas rebuild.
        /// </summary>
        public void Draw(VertexHelper vh)
        {
            EnsureBuilt();

            vh.Clear();
            _faces.Clear();
            _outline.Clear();

            var toPiece = transform.worldToLocalMatrix;
            var centre = (Vector3)rectTransform.rect.center;

            foreach (var part in _parts)
            {
                Collect(toPiece * part.Transform.localToWorldMatrix, centre, part.IsPupil);
            }

            // Painter's algorithm: the UI shader writes no depth, so whatever
            // is drawn last is in front. Larger z is further from the player.
            _faces.Sort((a, b) => b.Depth.CompareTo(a.Depth));

            foreach (var face in _outline)
            {
                AddFace(vh, face);
            }

            foreach (var face in _faces)
            {
                AddFace(vh, face);
            }
        }

        void Collect(Matrix4x4 shape, Vector3 centre, bool isPupil)
        {
            var sphere = UnitSphere;
            var middle = shape.MultiplyPoint3x4(Vector3.zero) + centre;

            // The normal of a stretched sphere is the inverse transpose's.
            var normals = shape.inverse.transpose;

            for (var i = 0; i < sphere.Length; i += 3)
            {
                var a = shape.MultiplyPoint3x4(sphere[i]) + centre;
                var b = shape.MultiplyPoint3x4(sphere[i + 1]) + centre;
                var c = shape.MultiplyPoint3x4(sphere[i + 2]) + centre;

                var normal = OutwardNormal(a, b, c, middle);

                if (normal.z <= 0f)
                {
                    _faces.Add(new Face
                    {
                        A = a,
                        B = b,
                        C = c,
                        Colour = isPupil ? BoardColours.BoardInk : Shaded(normal),
                        Depth = Centroid(a, b, c).z
                    });
                }

                if (_outlineWidth <= 0f)
                {
                    continue;
                }

                var oa = a + (normals.MultiplyVector(sphere[i]).normalized * _outlineWidth);
                var ob = b + (normals.MultiplyVector(sphere[i + 1]).normalized * _outlineWidth);
                var oc = c + (normals.MultiplyVector(sphere[i + 2]).normalized * _outlineWidth);

                // Only the half facing the player: the back half would lie
                // under the front one, and PieceEdge is translucent, so a
                // second layer would draw the edge twice as dark.
                if (OutwardNormal(oa, ob, oc, middle).z <= 0f)
                {
                    _outline.Add(new Face { A = oa, B = ob, C = oc, Colour = _outlineColour });
                }
            }
        }

        static Vector3 OutwardNormal(Vector3 a, Vector3 b, Vector3 c, Vector3 middle)
        {
            var normal = Vector3.Cross(b - a, c - a).normalized;

            // The sphere's winding says nothing about which way is out once a
            // shape is stretched and turned; its own centre does.
            return Vector3.Dot(normal, Centroid(a, b, c) - middle) < 0f ? -normal : normal;
        }

        static Vector3 Centroid(Vector3 a, Vector3 b, Vector3 c)
        {
            const float corners = 3f;

            return (a + b + c) / corners;
        }

        // Lit faces are exactly the frog colour, and shading only darkens —
        // game-board.md's invariant, because the separability bar is measured
        // against the frog colours themselves.
        Color Shaded(Vector3 normal)
        {
            var light = Mathf.Clamp01(Vector3.Dot(normal, s_light) / FrogFullyLit);
            var shade = Mathf.Lerp(FrogShadeFloor, 1f, light);
            var tint = color;

            return new Color(tint.r * shade, tint.g * shade, tint.b * shade, tint.a);
        }

        static void AddFace(VertexHelper vh, Face face)
        {
            var first = vh.currentVertCount;

            vh.AddVert(face.A, face.Colour, Vector2.zero);
            vh.AddVert(face.B, face.Colour, Vector2.zero);
            vh.AddVert(face.C, face.Colour, Vector2.zero);
            vh.AddTriangle(first, first + 1, first + 2);
        }

        // Every three entries are one triangle of the unit sphere.
        static Vector3[] UnitSphere
        {
            get
            {
                if (s_sphere != null)
                {
                    return s_sphere;
                }

                var triangles = new List<Vector3>();

                for (var around = 0; around < Longitude; around++)
                {
                    for (var down = 0; down < Latitude; down++)
                    {
                        var a = SpherePoint(around, down);
                        var b = SpherePoint(around + 1, down);
                        var c = SpherePoint(around + 1, down + 1);
                        var d = SpherePoint(around, down + 1);

                        if (down != 0)
                        {
                            triangles.Add(a);
                            triangles.Add(b);
                            triangles.Add(c);
                        }

                        if (down != Latitude - 1)
                        {
                            triangles.Add(a);
                            triangles.Add(c);
                            triangles.Add(d);
                        }
                    }
                }

                s_sphere = triangles.ToArray();
                return s_sphere;
            }
        }

        static Vector3 SpherePoint(int around, int down)
        {
            var polar = Mathf.PI * down / Latitude;
            var azimuth = 2f * Mathf.PI * around / Longitude;

            return new Vector3(
                Mathf.Sin(polar) * Mathf.Cos(azimuth),
                Mathf.Cos(polar),
                Mathf.Sin(polar) * Mathf.Sin(azimuth));
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

            _model = new GameObject("Frog").transform;
            _model.SetParent(transform, worldPositionStays: false);
            _model.localRotation = Quaternion.AngleAxis(FrogViewPitch, Vector3.right)
                * Quaternion.AngleAxis(FrogViewYaw, Vector3.up);
            _model.localScale = Vector3.one * FrogModelScale;

            AddPart("Body", _model, s_bodyCentre, s_bodyRadii, isPupil: false);
            AddPart("Head", _model, s_headCentre, s_headRadii, isPupil: false);

            foreach (var isLeft in new[] { true, false })
            {
                // The frog's left is +z: away from the player, who sees its
                // right side turned toward them.
                var side = isLeft ? 1f : -1f;
                var sideName = isLeft ? "Left" : "Right";

                var eye = Across(s_eyeCentre, side);
                AddPart(sideName + "Eye", _model, eye, Vector3.one * EyeRadius, isPupil: false);
                AddPart(
                    sideName + "Pupil",
                    _model,
                    eye + (Across(s_pupilLooks, side).normalized * PupilOut),
                    Vector3.one * PupilRadius,
                    isPupil: true);

                AddLeg(sideName + "Front", isFront: true, isLeft, Across(s_frontHip, side), FrontUpperLength, FrontUpperRadius, FrontLowerLength, FrontLowerRadius);
                AddLeg(sideName + "Back", isFront: false, isLeft, Across(s_backHip, side), BackThighLength, BackThighRadius, BackShinLength, BackShinRadius);
            }

            SetLegPose(_pose);
        }

        void AddLeg(string label, bool isFront, bool isLeft, Vector3 hipAt, float upperLength, float upperRadius, float lowerLength, float lowerRadius)
        {
            var hip = new GameObject(label + "Hip").transform;
            hip.SetParent(_model, worldPositionStays: false);
            hip.localPosition = hipAt;

            AddSegment(label + "Thigh", hip, upperLength, upperRadius);

            var knee = new GameObject(label + "Knee").transform;
            knee.SetParent(hip, worldPositionStays: false);
            knee.localPosition = Vector3.down * upperLength;

            AddSegment(label + "Shin", knee, lowerLength, lowerRadius);

            _legs.Add(new Leg(isFront, isLeft, hip, knee));
        }

        // A leg segment hangs straight down from its joint, so turning the
        // joint swings it.
        void AddSegment(string label, Transform joint, float length, float radius)
        {
            AddPart(
                label,
                joint,
                Vector3.down * (length / 2f),
                new Vector3(radius, (length / 2f) + (radius * SegmentOverlap), radius),
                isPupil: false);
        }

        void AddPart(string label, Transform parent, Vector3 centre, Vector3 radii, bool isPupil)
        {
            var part = new GameObject(label).transform;
            part.SetParent(parent, worldPositionStays: false);
            part.localPosition = centre;
            part.localScale = radii;

            _parts.Add(new Part { Transform = part, IsPupil = isPupil });
        }

        static Vector3 Across(Vector3 point, float side)
        {
            return new Vector3(point.x, point.y, point.z * side);
        }
    }
}
