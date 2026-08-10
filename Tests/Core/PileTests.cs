using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Frogs.Core;

namespace Frogs.Core.Tests
{
    /// <summary>
    /// <see cref="Pile"/> is which of the three sources a card is drawn from —
    /// decided by the roll, never chosen by the player. docs/specs/rules.md —
    /// "the roll's mapping to piles is fixed".
    /// </summary>
    public sealed class PileTests
    {
        // CONTEXT.md's avoid-list for "pile": the words a different board game
        // would reach for, and the reason this type is named `Pile` rather than
        // `Tier`, `Difficulty`, `Deck` or `Stack` despite ADR-0002's prose using
        // "difficulty tiers".
        static readonly string[] AvoidedWords = { "Tier", "Difficulty", "Deck", "Stack" };

        // The Frogs.Core types this issue adds — deliberately not "every type in
        // the assembly", so this test does not start failing on some unrelated
        // future type that happens to need one of these words for its own
        // reasons.
        static readonly Type[] TypesAddedByThisIssue = { typeof(Pile), typeof(Roll) };

        [Test]
        public void Pile_HasExactlyTheThreeNamedMembers()
        {
            var members = Enum.GetNames(typeof(Pile));

            Assert.That(members, Is.EquivalentTo(new[] { "Easy", "Medium", "Hard" }));
        }

        // A reflection sweep over every type, member and parameter name this
        // issue adds, against CONTEXT.md's avoid-list for "pile". ADR-0002's own
        // prose calls the piles "difficulty tiers", which is exactly the kind of
        // unpinned name this test exists to keep out of Core.
        [Test]
        public void TypesAddedByThisIssue_NameNoneOfTheAvoidedWords()
        {
            var offenders = TypesAddedByThisIssue
                .SelectMany(NamesToCheck)
                .Where(named => AvoidedWords.Any(avoided =>
                    named.Contains(avoided, StringComparison.OrdinalIgnoreCase)))
                .ToArray();

            Assert.That(offenders, Is.Empty,
                "found a name containing one of: " + string.Join(", ", AvoidedWords));
        }

        static string[] NamesToCheck(Type type)
        {
            var typeNames = new[] { type.Name };

            var memberNames = type
                .GetMembers(BindingFlags.Public | BindingFlags.NonPublic
                    | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(member => member.Name);

            var parameterNames = type
                .GetMembers(BindingFlags.Public | BindingFlags.NonPublic
                    | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .OfType<MethodBase>()
                .SelectMany(method => method.GetParameters())
                .Select(parameter => parameter.Name);

            return typeNames.Concat(memberNames).Concat(parameterNames)
                .Where(name => name != null)
                .ToArray()!;
        }
    }
}
