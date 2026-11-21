using System;
using System.Collections.Generic;
using Frogs.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frogs.Unity.UI
{
    /// <summary>
    /// One shared log — the approved Blender log (tools/art/log_model.py),
    /// assembled to the log's length and drawn into the board's canvas.
    /// docs/specs/ui/game-board.md § The log model, LG-1 to LG-5 (#468).
    ///
    /// **The model is imported, then assembled.** Three meshes —
    /// <c>LogBody_A</c>, <c>LogBody_B</c> and <c>LogCap</c> — and one albedo
    /// carrying the log's own colours, loaded from
    /// <c>Assets/Art/Log/Resources/Log/</c>. Core's <see cref="LogAssembly"/>
    /// says which pieces a log of this graphic's height needs and where each
    /// goes; this draws them. A log is not tinted: its colours are the
    /// albedo's, and this graphic's <see cref="Graphic.color"/> stays white.
    ///
    /// **It is drawn straight into the board's canvas, as a mesh**, the way
    /// <see cref="LilyPadModel"/> draws a pad: no camera, light, render
    /// texture or project shader, only uGUI's default material. The log is
    /// seen from straight above, so each piece is laid flat once — which way
    /// it runs and which way is up read off <c>LogBody_B</c> by
    /// <see cref="LogAssembly.MeasureFrame"/> rather than assumed from the
    /// importer's axes — and shared by every log.
    ///
    /// In this graphic's rectangle the log runs from the bottom edge to the
    /// top, centred across it, at <see cref="UnitSize"/> pixels to one frog
    /// unit both along and across: stretched along by the assembly rule only,
    /// never across (LG-3).
    ///
    /// Unlit: from straight above the top of the bark is what shows, drawn in
    /// exactly its albedo (LG-4).
    /// </summary>
    public sealed class LogModel : MaskableGraphic
    {
        /// <summary>The model, for <see cref="Resources.Load(string)"/>: Assets/Art/Log/Resources/Log/log.fbx.</summary>
        public const string ModelResource = "Log/log";

        /// <summary>Its albedo: Assets/Art/Log/Resources/Log/log_albedo.png.</summary>
        public const string AlbedoResource = "Log/log_albedo";

        /// <summary>The plain middle section's node name in the model.</summary>
        public const string BodyAName = "LogBody_A";

        /// <summary>The middle section with the branch stub — and the piece the frame is measured on.</summary>
        public const string BodyBName = "LogBody_B";

        /// <summary>The sawn end's node name in the model.</summary>
        public const string CapName = "LogCap";

        // Read once, shared by every log: the model, its albedo, and each
        // piece laid flat.
        static GameObject s_model;
        static Texture2D s_albedo;
        static Dictionary<LogPieceKind, FlatPiece> s_flatPieces;

        // A face is drawn when its corners' normals, summed, point up by more
        // than this: the sawn ends are flat faces exactly side-on, and an
        // importer's rounding must not tip them into view as a sliver.
        const float FacingUp = 0.01f;

        float _unitSize;

        // One piece, laid flat: every vertex as (across, along) in frog units
        // from the piece's own origin, its height above the waterline, its
        // UV, and the triangles that face up.
        sealed class FlatPiece
        {
            public Mesh Mesh;
            public Vector2[] Points;
            public float[] Heights;
            public Vector2[] Uv;
            public int[] Triangles;
        }

        /// <summary>The albedo every log is textured with. Null until a log has loaded the model.</summary>
        public override Texture mainTexture
        {
            get { return s_albedo != null ? s_albedo : base.mainTexture; }
        }

        /// <summary>How many pixels one frog unit is drawn: a lily pad's width. Zero until <see cref="SetUnitSize"/>.</summary>
        public float UnitSize
        {
            get { return _unitSize; }
        }

        /// <summary>How long this log is, in frog units: its rectangle's height over <see cref="UnitSize"/>.</summary>
        public float LengthInUnits
        {
            get { return _unitSize > 0f ? rectTransform.rect.height / _unitSize : 0f; }
        }

        /// <summary>The pieces this log draws, from its bottom end — Core's assembly for <see cref="LengthInUnits"/>.</summary>
        public IReadOnlyList<LogPiece> Pieces
        {
            get { return LogAssembly.For(LengthInUnits); }
        }

        /// <summary>The model's mesh for a piece. Loads the model if no log has yet.</summary>
        public static Mesh MeshFor(LogPieceKind kind)
        {
            return FlatPieces()[kind].Mesh;
        }

        /// <summary>
        /// Draws the model at this many pixels to one frog unit — a lily pad's
        /// width, since one frog unit is one lily pad. How long the log is
        /// follows from its rectangle's height.
        /// </summary>
        /// <exception cref="InvalidOperationException">The model is missing, or has no piece of one of the three names.</exception>
        public void SetUnitSize(float pixels)
        {
            raycastTarget = false;
            FlatPieces();
            _unitSize = pixels;

            SetVerticesDirty();
            SetMaterialDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            Draw(vh);
        }

        /// <summary>
        /// What <see cref="OnPopulateMesh"/> writes: every piece of the
        /// assembly's upward faces, textured with the albedo, lowest first
        /// across the whole log — the painter's algorithm, because the UI
        /// shader writes no depth. Public so a test can read the triangles
        /// without a canvas rebuild.
        /// </summary>
        public void Draw(VertexHelper vh)
        {
            vh.Clear();

            // Nothing until it has a scale, and nothing while its rectangle
            // is shorter than any log the rule assembles — a rectangle not
            // laid out yet is zero high, and a canvas rebuild is no place to
            // throw.
            if (_unitSize <= 0f || LengthInUnits < LogAssembly.ShortestLength)
            {
                return;
            }

            var flatPieces = FlatPieces();
            var rect = rectTransform.rect;
            var faces = new List<(int A, int B, int C, float Height)>();

            foreach (var piece in Pieces)
            {
                var flat = flatPieces[piece.Kind];
                var first = vh.currentVertCount;
                var turn = piece.Reversed ? -1f : 1f;

                for (var i = 0; i < flat.Points.Length; i++)
                {
                    var across = turn * flat.Points[i].x;
                    var along = piece.Origin + (turn * flat.Points[i].y * piece.Stretch);
                    var at = new Vector2(rect.center.x + (across * _unitSize), rect.yMin + (along * _unitSize));

                    vh.AddVert(new Vector3(at.x, at.y, 0f), color, flat.Uv[i]);
                }

                for (var t = 0; t < flat.Triangles.Length; t += 3)
                {
                    var a = flat.Triangles[t];
                    var b = flat.Triangles[t + 1];
                    var c = flat.Triangles[t + 2];

                    faces.Add((first + a, first + b, first + c, flat.Heights[a] + flat.Heights[b] + flat.Heights[c]));
                }
            }

            // Lowest first, across every piece, so the top of the bark lies
            // over the sides of the log and the stub over the water.
            faces.Sort((x, y) => x.Height.CompareTo(y.Height));

            foreach (var face in faces)
            {
                vh.AddTriangle(face.A, face.B, face.C);
            }
        }

        static Dictionary<LogPieceKind, FlatPiece> FlatPieces()
        {
            if (s_flatPieces != null)
            {
                return s_flatPieces;
            }

            LoadModel();

            var bodyA = FindPiece(BodyAName);
            var bodyB = FindPiece(BodyBName);
            var cap = FindPiece(CapName);

            // The frame is measured on B, the one piece whose stub says which
            // side is which.
            var bPoints = PiecePoints(bodyB);
            var frame = LogAssembly.MeasureFrame(bPoints[0], bPoints[1], bPoints[2]);

            s_flatPieces = new Dictionary<LogPieceKind, FlatPiece>
            {
                { LogPieceKind.BodyA, LayFlat(bodyA, frame) },
                { LogPieceKind.BodyB, LayFlat(bodyB, frame) },
                { LogPieceKind.Cap, LayFlat(cap, frame) },
            };

            return s_flatPieces;
        }

        // A piece by its own node's name — never by its mesh's, which the
        // file's two prebuilt assemblies share, each child with its own
        // offset, turn and stretch.
        static MeshFilter FindPiece(string name)
        {
            foreach (var filter in s_model.GetComponentsInChildren<MeshFilter>(includeInactive: true))
            {
                if (filter.name == name && filter.sharedMesh != null)
                {
                    return filter;
                }
            }

            throw new InvalidOperationException($"The log model has no piece named {name}.");
        }

        // The piece's vertices into the model's own space, through every
        // transform between them, relative to the piece's own origin — its
        // joining face, on the waterline. Plain TRS arithmetic, so it does
        // not matter that the model is an asset rather than something in a
        // scene.
        static List<float>[] PiecePoints(MeshFilter piece)
        {
            var toModel = ToModel(piece.transform, s_model.transform);
            var origin = toModel.MultiplyPoint3x4(Vector3.zero);
            var vertices = piece.sharedMesh.vertices;
            var axes = new[] { new List<float>(vertices.Length), new List<float>(vertices.Length), new List<float>(vertices.Length) };

            foreach (var vertex in vertices)
            {
                var p = toModel.MultiplyPoint3x4(vertex) - origin;

                axes[0].Add(p.x);
                axes[1].Add(p.y);
                axes[2].Add(p.z);
            }

            return axes;
        }

        static Matrix4x4 ToModel(Transform part, Transform model)
        {
            var matrix = Matrix4x4.identity;

            for (var t = part; t != null && t != model; t = t.parent)
            {
                matrix = Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale) * matrix;
            }

            return matrix;
        }

        static FlatPiece LayFlat(MeshFilter piece, LogFrame frame)
        {
            var mesh = piece.sharedMesh;
            var toModel = ToModel(piece.transform, s_model.transform);
            var axes = PiecePoints(piece);
            var normals = mesh.normals;
            var triangles = mesh.triangles;

            var points = new Vector2[axes[0].Count];
            var heights = new float[axes[0].Count];
            var upNormals = new float[axes[0].Count];

            for (var i = 0; i < points.Length; i++)
            {
                points[i] = new Vector2(
                    Component(axes, i, frame.Across) / frame.UnitLength,
                    Component(axes, i, frame.Along) / frame.UnitLength);
                heights[i] = Component(axes, i, frame.Up) / frame.UnitLength;

                var n = toModel.MultiplyVector(normals[i]);
                upNormals[i] = Component(n, frame.Up);
            }

            // Only the faces that look up: seen from straight above, the
            // underside and the cut below the waterline are hidden, and the
            // sawn ends and the open joins are edge-on. The model's own
            // normals say which way a face looks, whatever its winding came
            // through the importer as.
            var drawn = new List<int>(triangles.Length);

            for (var i = 0; i < triangles.Length; i += 3)
            {
                var a = triangles[i];
                var b = triangles[i + 1];
                var c = triangles[i + 2];

                if (upNormals[a] + upNormals[b] + upNormals[c] > FacingUp)
                {
                    drawn.Add(a);
                    drawn.Add(b);
                    drawn.Add(c);
                }
            }

            return new FlatPiece { Mesh = mesh, Points = points, Heights = heights, Uv = mesh.uv, Triangles = drawn.ToArray() };
        }

        // One coordinate of a point along a signed axis: ±1 x, ±2 y, ±3 z.
        static float Component(List<float>[] axes, int i, int signedAxis)
        {
            return Math.Sign(signedAxis) * axes[Math.Abs(signedAxis) - 1][i];
        }

        static float Component(Vector3 v, int signedAxis)
        {
            return Math.Sign(signedAxis) * v[Math.Abs(signedAxis) - 1];
        }

        static void LoadModel()
        {
            if (s_model != null && s_albedo != null)
            {
                return;
            }

            s_model = Resources.Load<GameObject>(ModelResource);
            s_albedo = Resources.Load<Texture2D>(AlbedoResource);

            if (s_model == null || s_albedo == null)
            {
                throw new InvalidOperationException(
                    $"The log model is missing: expected Resources/{ModelResource} and Resources/{AlbedoResource}.");
            }
        }
    }
}
