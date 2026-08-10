using System;

namespace Frogs.Core
{
    /// <summary>
    /// The roll that opens a turn: one face of a six-sided die, drawn from the
    /// game's single seeded <see cref="Rng"/> (#200) and from nothing else —
    /// no <see cref="System.Random"/>, no static randomness — and the pile
    /// that face sends the turn to.
    ///
    /// The roll selects the pile and does nothing else. It is not how far a
    /// frog moves, and it never moves a frog — grading an answer and resolving
    /// a move are a later issue's job, not this type's.
    /// docs/specs/rules.md — "the roll selects the pile and does nothing else".
    /// </summary>
    public sealed class Roll
    {
        /// <summary>The lowest face a six-sided die can show.</summary>
        public const int MinimumFace = 1;

        /// <summary>The highest face a six-sided die can show.</summary>
        public const int MaximumFace = 6;

        // The face-to-pile boundaries, read off the pile labels in the board
        // photograph (docs/specs/reference/index.md#the-pile-labels) and
        // repeated in ADR-0002 and docs/specs/ui/roll-and-card.md: 1 or 2 is
        // the Easy pile, 3 or 4 the Medium pile, 5 or 6 the Hard pile.
        const int EasyPileHighestFace = 2;
        const int MediumPileHighestFace = 4;

        Roll(int face)
        {
            Face = face;
            Pile = PileForFace(face);
        }

        /// <summary>The face that came up, <see cref="MinimumFace"/> to <see cref="MaximumFace"/>.</summary>
        public int Face { get; }

        /// <summary>The pile this roll's face sends the turn to.</summary>
        public Pile Pile { get; }

        /// <summary>
        /// A roll of one die, drawn from <paramref name="rng"/> and from
        /// nothing else.
        /// </summary>
        public static Roll Draw(Rng rng)
        {
            return new Roll(rng.NextInt(MinimumFace, MaximumFace));
        }

        /// <summary>
        /// The pile a die face maps to. Total over every face a die can show;
        /// a face outside <see cref="MinimumFace"/>–<see cref="MaximumFace"/>
        /// cannot come from the die, but the mapping still has to say what it
        /// does with one rather than silently returning a pile for it.
        /// </summary>
        public static Pile PileForFace(int face)
        {
            if (face < MinimumFace || face > MaximumFace)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(face),
                    face,
                    $"a die face is {MinimumFace} to {MaximumFace}; {face} is not a face.");
            }

            if (face <= EasyPileHighestFace)
            {
                return Pile.Easy;
            }

            if (face <= MediumPileHighestFace)
            {
                return Pile.Medium;
            }

            return Pile.Hard;
        }
    }
}
