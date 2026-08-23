using System;
using System.Collections.Generic;
using FrogColour = Frogs.Core.FrogColour;

namespace Frogs.Unity.Views
{
    /// <summary>
    /// What is on each of <see cref="HowToPlayScreenView"/>'s five pages: the
    /// heading, the words, and what the picture draws —
    /// docs/specs/ui/how-to-play.md's per-page table and its five committed
    /// mockups, transcribed and nothing more.
    ///
    /// It is a separate file from the view for one reason: **the copy is
    /// Connor's, and it is the open question this whole wireframe exists to
    /// ask** (how-to-play.md's open question 2 — he reads it on the tablet,
    /// and if he cannot follow it, it is wrong). Keeping every word he might
    /// change in one small file, away from the layout that draws it, is what
    /// makes "page 3 is confusing" a one-file change.
    ///
    /// **No page here decides a rule.** Every word is a restatement of
    /// docs/intro/how-to-play.md and docs/specs/rules.md for an eight-year-old
    /// on a tablet, and where this and those disagree, those are right and
    /// this is corrected.
    ///
    /// It draws nothing and knows nothing about UnityEngine: it is the
    /// content, and <see cref="HowToPlayScreenView"/> is the layout.
    /// </summary>
    public static class HowToPlayPages
    {
        /// <summary>Which way the roll points at the pile it picked, on page 2.</summary>
        public const string ArrowGlyph = "→";

        /// <summary>The die on page 2 is drawn as a three-by-three grid of pip places.</summary>
        public const int DieGridSize = 3;

        /// <summary>How many columns page 3's grid is drawn at — `12 × 34` needs four.</summary>
        public const int GridColumns = 4;

        /// <summary>What one of page 3's grid cells is for, which is what it is drawn like.</summary>
        public enum CellKind
        {
            /// <summary>A box to write in — an ordinary bordered cell.</summary>
            Written,

            /// <summary>What the card itself says, printed on the paper rather than written into a box.</summary>
            Printed,

            /// <summary>The bottom row — "the only row the game looks at".</summary>
            Answer
        }

        /// <summary>One paragraph of a page's `words`.</summary>
        public struct Paragraph
        {
            public Paragraph(string text, bool keepsNext = false)
            {
                Text = text;
                KeepsNext = keepsNext;
            }

            /// <summary>The words, in Unity's rich text — `&lt;b&gt;` where the mockup bolds a phrase.</summary>
            public string Text { get; }

            /// <summary>
            /// True for page 5's questions, which sit directly on top of their
            /// own answer: the paragraph gap falls between question-and-answer
            /// pairs, not inside one.
            /// </summary>
            public bool KeepsNext { get; }
        }

        /// <summary>One lane in a picture, and the frog standing on it, if any.</summary>
        public struct DrawnLane
        {
            DrawnLane(string caption, bool hasFrog, FrogColour frog, int position)
            {
                Caption = caption;
                HasFrog = hasFrog;
                Frog = frog;
                Position = position;
            }

            /// <summary>What page 4 writes above this example. Null on the pages whose lanes are one board.</summary>
            public string Caption { get; }

            /// <summary>Whether a frog is drawn on this lane at all.</summary>
            public bool HasFrog { get; }

            /// <summary>Which frog, when there is one.</summary>
            public FrogColour Frog { get; }

            /// <summary>Where it stands: 0 is the Start log, 8 the End log, 1 to 7 its own lily pads.</summary>
            public int Position { get; }

            /// <summary>A lane with a frog on it, and no caption.</summary>
            public static DrawnLane With(FrogColour frog, int position)
            {
                return new DrawnLane(null, true, frog, position);
            }

            /// <summary>One of page 4's captioned examples.</summary>
            public static DrawnLane Example(string caption, FrogColour frog, int position)
            {
                return new DrawnLane(caption, true, frog, position);
            }
        }

        /// <summary>One of the three piles drawn on page 2, and whether the roll picked it.</summary>
        public struct Pile
        {
            public Pile(string text, bool isPicked)
            {
                Text = text;
                IsPicked = isPicked;
            }

            /// <summary>What a card from this pile looks like.</summary>
            public string Text { get; }

            /// <summary>The one the roll of 3 picked. The other two are dimmed, not hidden.</summary>
            public bool IsPicked { get; }
        }

        /// <summary>One row of page 2's "you roll / your card looks like" table.</summary>
        public struct RollRow
        {
            public RollRow(string roll, string card)
            {
                Roll = roll;
                Card = card;
            }

            public string Roll { get; }

            public string Card { get; }
        }

        /// <summary>One row of page 3's grid: four cells, each with a kind and what it holds.</summary>
        public struct GridRow
        {
            readonly CellKind[] _kinds;
            readonly string[] _digits;

            public GridRow(CellKind[] kinds, string[] digits)
            {
                _kinds = kinds;
                _digits = digits;
            }

            public CellKind KindAt(int column)
            {
                return _kinds[column];
            }

            public string DigitAt(int column)
            {
                return _digits[column];
            }
        }

        /// <summary>One of page 3's three call-outs beside the grid.</summary>
        public struct Callout
        {
            public Callout(string heading, string detail, bool isAnswerRow = false)
            {
                Heading = heading;
                Detail = detail;
                IsAnswerRow = isAnswerRow;
            }

            public string Heading { get; }

            public string Detail { get; }

            /// <summary>The answer row's call-out, which is the one drawn in the accent colour.</summary>
            public bool IsAnswerRow { get; }
        }

        static readonly string[] s_headings =
        {
            "Your lane",
            "Roll the die",
            "Work it out",
            "Your frog hops",
            "Things people ask"
        };

        // Pages 1, 4 and 5 draw the pond; pages 2 and 3 are a diagram on
        // paper.
        static readonly bool[] s_isPondPage = { true, false, false, true, true };

        static readonly Paragraph[] s_pageOneWords =
        {
            new Paragraph("Every frog gets a lane of its own, and stays in it."),
            new Paragraph("Yours starts on the <b>Start log</b>. Then seven lily pads. Then the <b>End log</b> on the far side."),
            new Paragraph("The first frog to reach its End log wins.")
        };

        static readonly Paragraph[] s_pageTwoWords =
        {
            new Paragraph("On your turn you roll one die."),
            new Paragraph("The roll picks which pile your card comes from. That is the only thing it does — it never moves your frog."),
            new Paragraph("You do not choose your pile, and you cannot swap the card. Getting an easy one is luck.")
        };

        static readonly Paragraph[] s_pageThreeWords =
        {
            new Paragraph("The grid is your paper. Carry boxes along the top, room to work in the middle."),
            new Paragraph("Your answer goes in the <b>bottom row</b>. That is the only row the game looks at."),
            new Paragraph("Nothing else you write is marked. There is no button to press first and no mode to find.")
        };

        static readonly Paragraph[] s_pageFourWords =
        {
            new Paragraph("Got it right? Your frog hops forward one lily pad."),
            new Paragraph("Got it wrong? It hops back one."),
            new Paragraph("On the Start log there is nowhere further back, so a wrong answer there costs you the turn and nothing else."),
            new Paragraph("Every card is worth the same one lily pad — a hard one is not worth more.")
        };

        static readonly Paragraph[] s_pageFiveWords =
        {
            new Paragraph("Can I land on somebody?", keepsNext: true),
            new Paragraph("No. Frogs never share a lane and never pass each other."),
            new Paragraph("Is a hard card worth more?", keepsNext: true),
            new Paragraph("No. Every card is one lily pad. An easy one is just luck."),
            new Paragraph("Somebody won. Is it over?", keepsNext: true),
            new Paragraph("No. Everybody keeps taking turns until every frog is home.")
        };

        // Page 1: a game about to begin — four lanes, every frog on its Start
        // log.
        static readonly DrawnLane[] s_pageOneLanes =
        {
            DrawnLane.With(FrogColour.Green, StartLogPosition),
            DrawnLane.With(FrogColour.Blue, StartLogPosition),
            DrawnLane.With(FrogColour.Orange, StartLogPosition),
            DrawnLane.With(FrogColour.Pink, StartLogPosition)
        };

        // Page 4: three separate examples rather than three lanes of one game.
        static readonly DrawnLane[] s_pageFourExamples =
        {
            DrawnLane.Example("Right — forward one", FrogColour.Green, 4),
            DrawnLane.Example("Wrong — back one", FrogColour.Orange, 2),
            DrawnLane.Example("Wrong on the Start log — stay", FrogColour.Blue, StartLogPosition)
        };

        // Page 5: a four-lane board mid-game, one frog home on its End log.
        static readonly DrawnLane[] s_pageFiveLanes =
        {
            DrawnLane.With(FrogColour.Green, EndLogPosition),
            DrawnLane.With(FrogColour.Blue, 5),
            DrawnLane.With(FrogColour.Orange, 3),
            DrawnLane.With(FrogColour.Pink, 6)
        };

        static readonly Pile[] s_piles =
        {
            new Pile("68 × 5", false),
            new Pile("22 × 41", true),
            new Pile("331 × 41", false)
        };

        static readonly RollRow[] s_rollTable =
        {
            new RollRow("You roll", "Your card looks like"),
            new RollRow("1 or 2", "68 × 5"),
            new RollRow("3 or 4", "22 × 41"),
            new RollRow("5 or 6", "331 × 41")
        };

        // `12 × 34`, worked out. The two rows in the middle are room to work,
        // and the picture marks neither of them — ADR-0002's constraint, which
        // this page does not get to relax because it is the page about the
        // grid.
        static readonly GridRow[] s_gridRows =
        {
            new GridRow(
                new[] { CellKind.Written, CellKind.Printed, CellKind.Printed, CellKind.Written },
                new[] { "", "1", "2", "" }),
            new GridRow(
                new[] { CellKind.Printed, CellKind.Written, CellKind.Printed, CellKind.Printed },
                new[] { "×", "", "3", "4" }),
            new GridRow(
                new[] { CellKind.Written, CellKind.Written, CellKind.Written, CellKind.Written },
                new[] { "", "4", "8", "" }),
            new GridRow(
                new[] { CellKind.Printed, CellKind.Written, CellKind.Written, CellKind.Written },
                new[] { "+", "3", "6", "0" }),
            new GridRow(
                new[] { CellKind.Answer, CellKind.Answer, CellKind.Answer, CellKind.Answer },
                new[] { "", "4", "0", "8" })
        };

        static readonly Callout[] s_callouts =
        {
            new Callout("Carry boxes", "for a digit you carried"),
            new Callout("Room to work", "nobody marks these rows"),
            new Callout("The answer row", "the bottom row, always", isAnswerRow: true)
        };

        // A lane's two ends, named rather than written as 0 and 8 — they are
        // Frogs.Core.Lane's own positions, and this file is content rather
        // than a second opinion about how long a lane is.
        const int StartLogPosition = 0;
        const int EndLogPosition = Frogs.Core.Lane.LaneWinningPosition;

        /// <summary>The heading for one page — "which page this is, in two or three words".</summary>
        public static string HeadingFor(int page)
        {
            return s_headings[IndexOf(page)];
        }

        /// <summary>Whether one page's picture is drawn on the pond or on paper.</summary>
        public static bool IsPondPage(int page)
        {
            return s_isPondPage[IndexOf(page)];
        }

        /// <summary>One page's paragraphs, in the order they are read.</summary>
        public static IReadOnlyList<Paragraph> WordsFor(int page)
        {
            switch (page)
            {
                case 1:
                    return s_pageOneWords;
                case 2:
                    return s_pageTwoWords;
                case 3:
                    return s_pageThreeWords;
                case 4:
                    return s_pageFourWords;
                case 5:
                    return s_pageFiveWords;
                default:
                    throw Unknown(page);
            }
        }

        /// <summary>Page 1's four lanes.</summary>
        public static IReadOnlyList<DrawnLane> PageOneLanes
        {
            get { return s_pageOneLanes; }
        }

        /// <summary>Page 4's three captioned examples.</summary>
        public static IReadOnlyList<DrawnLane> PageFourExamples
        {
            get { return s_pageFourExamples; }
        }

        /// <summary>Page 5's four lanes.</summary>
        public static IReadOnlyList<DrawnLane> PageFiveLanes
        {
            get { return s_pageFiveLanes; }
        }

        /// <summary>Page 2's three piles, top to bottom.</summary>
        public static IReadOnlyList<Pile> Piles
        {
            get { return s_piles; }
        }

        /// <summary>Page 2's table, its header row first.</summary>
        public static IReadOnlyList<RollRow> RollTable
        {
            get { return s_rollTable; }
        }

        /// <summary>Page 3's grid rows, under its carry boxes.</summary>
        public static IReadOnlyList<GridRow> GridRows
        {
            get { return s_gridRows; }
        }

        /// <summary>Page 3's three call-outs, top to bottom.</summary>
        public static IReadOnlyList<Callout> Callouts
        {
            get { return s_callouts; }
        }

        static int IndexOf(int page)
        {
            if (page < 1 || page > s_headings.Length)
            {
                throw Unknown(page);
            }

            return page - 1;
        }

        static ArgumentOutOfRangeException Unknown(int page)
        {
            return new ArgumentOutOfRangeException(
                nameof(page), page, $"how to play is {s_headings.Length} pages, numbered from 1.");
        }
    }
}
