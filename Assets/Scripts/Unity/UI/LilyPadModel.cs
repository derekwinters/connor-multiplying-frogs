using System;
using System.Collections.Generic;
using Frogs.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frogs.Unity.UI
{
    /// <summary>
    /// One lily pad — the approved Blender pad (tools/art/lilypad_model.py),
    /// drawn into the board's canvas. docs/specs/ui/game-board.md § The lily
    /// pad is notched, veined, and varies per pad, LP-1 to LP-5 (#465).
    ///
    /// **The model is imported, not built.** Four meshes, one per entry in
    /// <c>LilyPadNotchAngles</c> — <c>LilyPad_Notch10</c>, <c>15</c>,
    /// <c>20</c> and <c>25</c> — and one albedo atlas carrying the pad's own
    /// colours, rim and veins, loaded from
    /// <c>Assets/Art/LilyPad/Resources/LilyPad/</c>. A pad is not tinted: its
    /// colours are the albedo's, and this graphic's <see cref="Graphic.color"/>
    /// stays white.
    ///
    /// **It is drawn straight into the board's canvas, as a mesh**, the way
    /// <see cref="FrogModel"/> draws the frog: no camera, light, render
    /// texture or project shader, only uGUI's default material. The pad is
    /// seen from straight above, so each mesh is laid flat once — its up and
    /// its notch read off its own outline by Core's
    /// <see cref="LilyPadOutline"/> rather than assumed from the importer's
    /// axes — scaled so its rim fills this graphic's rectangle, turned so its
    /// notch points along +x, and shared by every pad of that width. The
    /// board turns the rectangle to point the notch where the variation table
    /// says, exactly as it turned the sprite this replaced.
    ///
    /// Unlit: from straight above the top of the leaf is what shows, drawn in
    /// exactly its albedo (LP-4).
    /// </summary>
    public sealed class LilyPadModel : MaskableGraphic
    {
        /// <summary>The model, for <see cref="Resources.Load(string)"/>: Assets/Art/LilyPad/Resources/LilyPad/lilypad.fbx.</summary>
        public const string ModelResource = "LilyPad/lilypad";

        /// <summary>Its albedo atlas: Assets/Art/LilyPad/Resources/LilyPad/lilypad_albedo.png.</summary>
        public const string AlbedoResource = "LilyPad/lilypad_albedo";

        /// <summary>The meshes' names, before their notch width in two digits: <c>LilyPad_Notch10</c>.</summary>
        public const string MeshNamePrefix = "LilyPad_Notch";

        const string NotchWidthFormat = "00";

        // Read once, shared by every pad: the model, its albedo, and each
        // width's mesh laid flat for drawing.
        static GameObject s_model;
        static Texture2D s_albedo;
        static readonly Dictionary<int, FlatPad> s_flatPads = new Dictionary<int, FlatPad>();

        FlatPad _flat;
        float _notchWidth;

        // One width's mesh, laid flat: every vertex in the pad's own plane at
        // unit radius with the notch along +x, its UV, and the triangles
        // that face up, lowest first.
        sealed class FlatPad
        {
            public Mesh Mesh;
            public Vector2[] Points;
            public Vector2[] Uv;
            public int[] Triangles;
        }

        /// <summary>The atlas every pad is textured with. Null until a pad has loaded the model.</summary>
        public override Texture mainTexture
        {
            get { return s_albedo != null ? s_albedo : base.mainTexture; }
        }

        /// <summary>The width of the notch this pad draws, in degrees — one of <c>LilyPadNotchAngles</c>.</summary>
        public float NotchWidth
        {
            get { return _notchWidth; }
        }

        /// <summary>The model's mesh this pad draws: the one for its notch width. Null until a width is set.</summary>
        public Mesh Mesh
        {
            get { return _flat != null ? _flat.Mesh : null; }
        }

        /// <summary>
        /// Draws the model's pad for this notch width — <c>LilyPad_NotchNN</c>.
        /// Which way it points is the rectangle's rotation, not this.
        /// </summary>
        /// <exception cref="InvalidOperationException">The model has no mesh for that width.</exception>
        public void SetNotchWidth(float notchWidth)
        {
            raycastTarget = false;
            _flat = FlatPadFor(notchWidth);
            _notchWidth = notchWidth;

            SetVerticesDirty();
            SetMaterialDirty();
        }

        /// <summary>
        /// Where a point of the pad is drawn in this graphic's own rectangle,
        /// given in the pad's own terms: a fraction of its radius out from its
        /// centre, at an angle in degrees counter-clockwise from its notch.
        /// What a test checks the notch and the fit against.
        /// </summary>
        public Vector2 RectPointOf(float degreesFromNotch, float radiusFraction)
        {
            var radians = degreesFromNotch * Mathf.Deg2Rad;

            return ToRect(new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radiusFraction);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            Draw(vh);
        }

        /// <summary>
        /// What <see cref="OnPopulateMesh"/> writes: the mesh's upward faces,
        /// textured with the albedo, lowest first — the painter's algorithm,
        /// because the UI shader writes no depth. Public so a test can read
        /// the triangles without a canvas rebuild.
        /// </summary>
        public void Draw(VertexHelper vh)
        {
            vh.Clear();

            if (_flat == null)
            {
                return;
            }

            for (var i = 0; i < _flat.Points.Length; i++)
            {
                var at = ToRect(_flat.Points[i]);

                vh.AddVert(new Vector3(at.x, at.y, 0f), color, _flat.Uv[i]);
            }

            for (var i = 0; i < _flat.Triangles.Length; i += 3)
            {
                vh.AddTriangle(_flat.Triangles[i], _flat.Triangles[i + 1], _flat.Triangles[i + 2]);
            }
        }

        // From the pad's own plane at unit radius to this rectangle: the rim
        // touches its edges (LP-2).
        Vector2 ToRect(Vector2 onPad)
        {
            var rect = rectTransform.rect;

            return rect.center + Vector2.Scale(onPad, rect.size / 2f);
        }

        static FlatPad FlatPadFor(float notchWidth)
        {
            var width = Mathf.RoundToInt(notchWidth);

            if (s_flatPads.TryGetValue(width, out var flat))
            {
                return flat;
            }

            LoadModel();

            var name = MeshNamePrefix + width.ToString(NotchWidthFormat, System.Globalization.CultureInfo.InvariantCulture);
            MeshFilter found = null;

            foreach (var filter in s_model.GetComponentsInChildren<MeshFilter>(includeInactive: true))
            {
                if (filter.name == name || (filter.sharedMesh != null && filter.sharedMesh.name == name))
                {
                    found = filter;
                    break;
                }
            }

            if (found == null || found.sharedMesh == null)
            {
                throw new InvalidOperationException($"The lily pad model has no mesh named {name}.");
            }

            flat = LayFlat(found.sharedMesh, ToModel(found.transform, s_model.transform));
            s_flatPads[width] = flat;

            return flat;
        }

        // The mesh's vertices into the model's own space, through every
        // transform between them — plain TRS arithmetic, so it does not
        // matter that the model is an asset rather than something in a scene.
        static Matrix4x4 ToModel(Transform part, Transform model)
        {
            var matrix = Matrix4x4.identity;

            for (var t = part; t != null && t != model; t = t.parent)
            {
                matrix = Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale) * matrix;
            }

            return matrix;
        }

        static FlatPad LayFlat(Mesh mesh, Matrix4x4 toModel)
        {
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var triangles = mesh.triangles;

            var axes = new[] { new List<float>(vertices.Length), new List<float>(vertices.Length), new List<float>(vertices.Length) };
            var upNormals = new float[vertices.Length];

            for (var i = 0; i < vertices.Length; i++)
            {
                var p = toModel.MultiplyPoint3x4(vertices[i]);

                axes[0].Add(p.x);
                axes[1].Add(p.y);
                axes[2].Add(p.z);
            }

            // Up is the leaf's thin axis, the way its rim curls; the plane is
            // the other two. The pad is symmetric about its notch's axis, so
            // which of the two is drawn as x cannot show.
            var up = LilyPadOutline.UpAxis(axes[0], axes[1], axes[2]);
            var upAxis = Math.Abs(up) - 1;
            var upSign = Math.Sign(up);
            var a = axes[(upAxis + 1) % 3];
            var b = axes[(upAxis + 2) % 3];

            var bearing = LilyPadOutline.NotchBearing(a, b) * Mathf.Deg2Rad;
            var radius = LilyPadOutline.Radius(a, b);
            var cos = Mathf.Cos(-bearing);
            var sin = Mathf.Sin(-bearing);

            var points = new Vector2[vertices.Length];
            var heights = new float[vertices.Length];

            for (var i = 0; i < vertices.Length; i++)
            {
                points[i] = new Vector2((a[i] * cos) - (b[i] * sin), (a[i] * sin) + (b[i] * cos)) / radius;
                heights[i] = upSign * axes[upAxis][i];

                var n = toModel.MultiplyVector(normals[i]);
                upNormals[i] = upSign * (upAxis == 0 ? n.x : upAxis == 1 ? n.y : n.z);
            }

            // Only the faces that look up: seen from straight above, the
            // underside is hidden and the cut and rim walls are edge-on. The
            // mesh's own smooth normals say which way a face looks, whatever
            // its winding came through the importer as.
            var faces = new List<(int A, int B, int C, float Height)>();

            for (var i = 0; i < triangles.Length; i += 3)
            {
                var ia = triangles[i];
                var ib = triangles[i + 1];
                var ic = triangles[i + 2];

                if (upNormals[ia] + upNormals[ib] + upNormals[ic] <= 0f)
                {
                    continue;
                }

                faces.Add((ia, ib, ic, heights[ia] + heights[ib] + heights[ic]));
            }

            // Lowest first, so a curled rim lies over the leaf beside it.
            faces.Sort((x, y) => x.Height.CompareTo(y.Height));

            var drawn = new int[faces.Count * 3];

            for (var f = 0; f < faces.Count; f++)
            {
                drawn[(f * 3) + 0] = faces[f].A;
                drawn[(f * 3) + 1] = faces[f].B;
                drawn[(f * 3) + 2] = faces[f].C;
            }

            return new FlatPad { Mesh = mesh, Points = points, Uv = mesh.uv, Triangles = drawn };
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
                    $"The lily pad model is missing: expected Resources/{ModelResource} and Resources/{AlbedoResource}.");
            }
        }
    }
}
