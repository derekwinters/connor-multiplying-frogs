using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Frogs.Core;

namespace Frogs.Core.Tests
{
    /// <summary>
    /// <see cref="TurnResolution"/> is the fact <see cref="Lane.Resolve"/>
    /// hands back once an answer is graded — docs/specs/ui/answer-result.md's
    /// regions read backwards: `mark`/`verdict` need the outcome and the
    /// card's answer, `chip` needs the position before and after. Nothing
    /// else — no formatted strings, no colour, no next player, no button
    /// label; those are the dialog's job (#224) and #208's turn-advancement
    /// job, not this type's.
    /// </summary>
    public sealed class TurnResolutionTests
    {
        [Test]
        public void ANewTurnResolution_CarriesExactlyTheOutcomePositionsAndAnswer()
        {
            var resolution = new TurnResolution(TurnOutcome.Correct, 3, 4, 13571);

            Assert.That(resolution.Outcome, Is.EqualTo(TurnOutcome.Correct));
            Assert.That(resolution.PositionBefore, Is.EqualTo(3));
            Assert.That(resolution.PositionAfter, Is.EqualTo(4));
            Assert.That(resolution.CorrectAnswer, Is.EqualTo(13571));
        }

        // Structural, not behavioural: answer-result.md's regions need exactly
        // these four facts and nothing more — no formatted strings, no
        // colour, no next player, no button label. A fifth public member here
        // would be a fact the dialog was never asked to render.
        [Test]
        public void TurnResolution_ExposesNoPublicMembersBeyondTheFourFacts()
        {
            var propertyNames = typeof(TurnResolution)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name)
                .OrderBy(name => name)
                .ToArray();

            Assert.That(propertyNames, Is.EqualTo(new[]
            {
                "CorrectAnswer",
                "Outcome",
                "PositionAfter",
                "PositionBefore"
            }));
        }
    }
}
