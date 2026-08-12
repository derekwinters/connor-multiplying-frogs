using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Frogs.Core;
using Frogs.Unity.Views;
// UnityEngine.UI also declares a Button type — the same collision
// RollAndCardDialogViewTests.cs works around — so the shared components are
// pulled in by explicit alias, and a bare `Button` in this file always means
// the shared component's.
using Button = Frogs.Unity.UI.Button;
using ButtonKind = Frogs.Unity.UI.ButtonKind;
using DialogPanel = Frogs.Unity.UI.DialogPanel;
using FrogColours = Frogs.Unity.UI.FrogColours;
using PlayerChip = Frogs.Unity.UI.PlayerChip;
using PlayerChipState = Frogs.Unity.UI.PlayerChipState;
// Frogs.Core declares a Screen too — the router's, one of the game's screens —
// and the raycast harness below wants the engine's, in pixels. Same treatment
// as Button: a bare `Screen` in this file always means UnityEngine's.
using Screen = UnityEngine.Screen;

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// The working-out grid — issue #223, built to
    /// docs/specs/ui/working-out-grid.md and its three committed 1:1 mockups.
    /// Written before <see cref="WorkingOutGridView"/> exists, per
    /// docs/engineering/testing.md's sanctioned flow: pushed unexecuted, with
    /// CI turning these red before green — there is no editor here to watch
    /// them fail.
    ///
    /// **Nothing in this file knows a correct answer**, except the one test
    /// that hands a submitted answer to <see cref="Lane.Resolve"/> to prove
    /// the digits arrive intact. The grid itself is never asked whether
    /// anything in it is right, because it has no way to answer that.
    ///
    /// The two card shapes are the spec's own worked examples: `68 × 5` from
    /// the easy pile (three digit columns) and `331 × 41` from the hard pile
    /// (five). They are drawn from a seeded <see cref="Rng"/> rather than
    /// constructed, because <see cref="Card"/> only builds through
    /// <see cref="Card.Draw"/> — and the *shape* is all these tests need, which
    /// is exactly what the pile fixes.
    /// </summary>
    public sealed class WorkingOutGridViewTests
    {
        const ulong Seed = 20260810UL;

        // docs/specs/ui/working-out-grid.md: "`331 × 41` needs five digit
        // columns (`13571`); `68 × 5` needs three (`340`)" — plus the operator
        // column, in both.
        const int EasyColumnCount = 4;
        const int HardColumnCount = 6;

        [Test]
        public void Grid_ForTheEasyShape_DrawsCoresOwnRowSequence_WithARuleUnderTheMultiplierAndUnderTheSection()
        {
            // #223's checklist was written before the addition section became
            // growable (#234) and asks for "four rows for a 1-digit
            // multiplier". The spec page and #204's merged model now give
            // every card the same seven rows at the starting count, and the
            // easy card is a *narrower* grid rather than a shorter one. This
            // asserts what Core actually reports, which is the boundary the
            // issue is really about.
            var view = CreateView(Turn(Pile.Easy));

            try
            {
                Assert.That(view.RowKinds, Is.EqualTo(new[]
                {
                    GridRowKind.CarryStrip,
                    GridRowKind.Multiplicand,
                    GridRowKind.Multiplier,
                    GridRowKind.AdditionRow,
                    GridRowKind.AdditionRow,
                    GridRowKind.CarryStrip,
                    GridRowKind.AnswerRow
                }));

                foreach (var row in view.Cells)
                {
                    Assert.That(row.Count, Is.EqualTo(EasyColumnCount), "one operator column plus three digit columns");
                }

                // Two rules, and they are not rows: one under the multiplier,
                // one under the bottom of the addition section.
                Assert.That(view.RuleRects.Count, Is.EqualTo(2));

                var multiplier = view.RowRects[RowIndexOf(view, GridRowKind.Multiplier)];
                var firstAddition = view.RowRects[RowIndexOf(view, GridRowKind.AdditionRow)];
                var lastAddition = view.RowRects[RowIndexOf(view, GridRowKind.AdditionRow, 1)];
                var secondStrip = view.RowRects[RowIndexOf(view, GridRowKind.CarryStrip, 1)];

                AssertRuleBetween(view.RuleRects[0], multiplier, firstAddition);
                AssertRuleBetween(view.RuleRects[1], lastAddition, secondStrip);

                foreach (var rule in view.RuleRects)
                {
                    Assert.That(rule.rect.height, Is.EqualTo(WorkingOutGridView.GridRuleThickness).Within(0.001f));
                    Assert.That(rule.rect.width, Is.EqualTo(view.GridRect.rect.width).Within(0.001f));
                }

                // Sized with the named constants, from the column count Core
                // reported — not from a count this view worked out.
                var expectedWidth = (EasyColumnCount * WorkingOutGridView.GridCellSize)
                    + ((EasyColumnCount - 1) * WorkingOutGridView.GridCellGap);

                Assert.That(view.GridRect.rect.width, Is.EqualTo(expectedWidth).Within(0.001f));
                Assert.That(view.GridRect.rect.height, Is.EqualTo(DealtGridHeight()).Within(0.001f));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void Grid_ForTheHardShape_IsWiderThanTheEasyOne_AndOnlyBecauseCoresModelSaysSo()
        {
            var easy = CreateView(Turn(Pile.Easy));
            var hard = CreateView(Turn(Pile.Hard));

            try
            {
                // The same view code, the same row kinds, a different model.
                Assert.That(hard.RowKinds, Is.EqualTo(easy.RowKinds));
                Assert.That(hard.RuleRects.Count, Is.EqualTo(easy.RuleRects.Count));

                foreach (var row in hard.Cells)
                {
                    Assert.That(row.Count, Is.EqualTo(HardColumnCount));
                }

                Assert.That(hard.GridRect.rect.width, Is.GreaterThan(easy.GridRect.rect.width));

                // Same rows, so the same height — "the grid is sized to the
                // card in its columns", and no longer in its rows.
                Assert.That(hard.GridRect.rect.height, Is.EqualTo(easy.GridRect.rect.height).Within(0.001f));

                // The keypad and `Check it` do not move between the two.
                Assert.That(
                    hard.KeypadRect.anchoredPosition,
                    Is.EqualTo(easy.KeypadRect.anchoredPosition));
                Assert.That(
                    hard.CheckItButton.RectTransform.rect.size,
                    Is.EqualTo(easy.CheckItButton.RectTransform.rect.size));
            }
            finally
            {
                Destroy(easy);
                Destroy(hard);
            }
        }

        [Test]
        public void OperatorColumn_ReadsMultiplyPlusAndEquals_FromTheRowKindAndNothingElse()
        {
            foreach (var pile in new[] { Pile.Easy, Pile.Medium, Pile.Hard })
            {
                var view = CreateView(Turn(pile));

                try
                {
                    var glyphs = view.Cells.Select(row => row[0].Content).ToArray();

                    Assert.That(glyphs, Is.EqualTo(new[]
                    {
                        string.Empty, // carry strip
                        string.Empty, // multiplicand
                        "×",          // multiplier
                        string.Empty, // first addition row
                        "+",          // the bottom row of the section
                        string.Empty, // second carry strip
                        "="           // answer row
                    }), "the operator column, top to bottom, for " + pile);

                    // The operator column is never fillable, whatever is drawn
                    // in it.
                    foreach (var row in view.Cells)
                    {
                        Assert.That(row[0].IsEditable, Is.False);
                    }
                }
                finally
                {
                    Destroy(view);
                }
            }
        }

        [Test]
        public void OperatorColumn_KeepsThePlusOnTheBottomRow_AsTheSectionGrows()
        {
            var view = CreateView(Turn(Pile.Hard));

            try
            {
                GrowSectionBy(view, 1);

                var glyphs = view.Cells.Select(row => row[0].Content).ToArray();

                Assert.That(glyphs, Is.EqualTo(new[]
                {
                    string.Empty, string.Empty, "×",
                    string.Empty, string.Empty, "+",
                    string.Empty, "="
                }), "growing the section moves the `+` down with it rather than stamping one on every row");
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void PrintedRows_AreTheCardsOwnDigitsRightAligned_AndNothingTypedTouchesThem()
        {
            var turn = Turn(Pile.Hard);
            var view = CreateView(turn);

            try
            {
                var multiplicand = view.Cells[RowIndexOf(view, GridRowKind.Multiplicand)];
                var multiplier = view.Cells[RowIndexOf(view, GridRowKind.Multiplier)];

                Assert.That(
                    string.Concat(multiplicand.Select(cell => cell.Content)),
                    Is.EqualTo(turn.Card.Multiplicand.ToString()));
                Assert.That(
                    string.Concat(multiplier.Skip(1).Select(cell => cell.Content)),
                    Is.EqualTo(turn.Card.Multiplier.ToString()),
                    "the multiplier's digits, right-aligned, past the `×`");

                foreach (var cell in multiplicand.Concat(multiplier))
                {
                    Assert.That(cell.IsEditable, Is.False, "these two rows *are* the card");
                }
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void CheckIt_IsDisabledUntilTheAnswerRowHasADigit()
        {
            var view = CreateView(Turn(Pile.Easy));

            try
            {
                Assert.That(view.CheckItButton.Kind, Is.EqualTo(ButtonKind.Primary));
                Assert.That(view.CheckItButton.Label.text, Is.EqualTo("Check it"));
                Assert.That(view.CheckItButton.IsDisabled, Is.True, "an empty answer is not a wrong answer");
                Assert.That(
                    view.CheckItButton.CanvasGroup.alpha,
                    Is.EqualTo(Button.ButtonDisabledOpacity).Within(0.001f),
                    "the shared Button's own Disabled state, not a bespoke grey");

                // A digit in an addition row is not an answer.
                Tap(CellAt(view, GridRowKind.AdditionRow, 0, 1));
                Type(view, 7);
                Assert.That(view.CheckItButton.IsDisabled, Is.True);

                // A digit in a carry box is not an answer either.
                Tap(CellAt(view, GridRowKind.CarryStrip, 0, 1));
                Type(view, 1);
                Assert.That(view.CheckItButton.IsDisabled, Is.True);

                Tap(CellAt(view, GridRowKind.AnswerRow, 0, EasyColumnCount - 1));
                Type(view, 4);
                Assert.That(view.CheckItButton.IsDisabled, Is.False);
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void Caret_OpensOnTheRightmostAnswerCell_AndEachDigitFillsTheNextCellToItsLeft()
        {
            var view = CreateView(Turn(Pile.Hard));

            try
            {
                var answerRow = RowIndexOf(view, GridRowKind.AnswerRow);

                Assert.That(
                    view.CaretCell,
                    Is.EqualTo(view.Cells[answerRow][HardColumnCount - 1]),
                    "the first empty answer cell, filling right to left");

                Type(view, 1);
                Assert.That(view.Cells[answerRow][HardColumnCount - 1].Content, Is.EqualTo("1"));
                Assert.That(view.CaretCell, Is.EqualTo(view.Cells[answerRow][HardColumnCount - 2]));

                Type(view, 7);
                Assert.That(view.Cells[answerRow][HardColumnCount - 2].Content, Is.EqualTo("7"));

                Type(view, 5);
                Type(view, 3);
                Type(view, 1);

                // Typed units-first, which is the direction the digits are
                // worked out in — and reads left to right as the answer.
                Assert.That(view.AnswerText, Is.EqualTo("13571"));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void TappingAnyEditableCell_MovesTheCaretThere_AndTheNextDigitLandsThere()
        {
            var view = CreateView(Turn(Pile.Hard));

            try
            {
                // An addition row, out of order: the player who does the
                // partial products first is not fighting the caret.
                var additionCell = CellAt(view, GridRowKind.AdditionRow, 0, 3);
                Tap(additionCell);
                Assert.That(view.CaretCell, Is.EqualTo(additionCell));

                Type(view, 9);
                Assert.That(additionCell.Content, Is.EqualTo("9"));

                // A carry box on the top strip.
                var carryCell = CellAt(view, GridRowKind.CarryStrip, 0, 2);
                Tap(carryCell);
                Assert.That(view.CaretCell, Is.EqualTo(carryCell));

                Type(view, 2);
                Assert.That(carryCell.Content, Is.EqualTo("2"));

                // And back to the answer row, mid-row.
                var answerCell = CellAt(view, GridRowKind.AnswerRow, 0, 2);
                Tap(answerCell);
                Type(view, 6);
                Assert.That(answerCell.Content, Is.EqualTo("6"));

                // Tapping a printed digit or the operator column moves
                // nothing: they are not cells anything can be typed into.
                var caretBefore = view.CaretCell;
                Tap(view.Cells[RowIndexOf(view, GridRowKind.Multiplicand)][HardColumnCount - 1]);
                Tap(view.Cells[RowIndexOf(view, GridRowKind.AnswerRow)][0]);
                Assert.That(view.CaretCell, Is.EqualTo(caretBefore));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void Backspace_TakesTheCaretsDigit_OrTheLastEnteredInItsBlock_AndNothingOutsideIt()
        {
            var view = CreateView(Turn(Pile.Hard));

            try
            {
                var additionCell = CellAt(view, GridRowKind.AdditionRow, 0, 2);
                Tap(additionCell);
                Type(view, 4);

                // The answer row: two digits, then backspace with the caret on
                // an empty cell — it takes the last one entered in this row,
                // and leaves the addition row alone.
                Tap(CellAt(view, GridRowKind.AnswerRow, 0, HardColumnCount - 1));
                Type(view, 2);
                Type(view, 8);

                Assert.That(view.AnswerText, Is.EqualTo("82"));

                Tap(CellAt(view, GridRowKind.AnswerRow, 0, 1));
                Backspace(view);

                Assert.That(view.AnswerText, Is.EqualTo("2"), "the last digit entered in this block, and only it");
                Assert.That(additionCell.Content, Is.EqualTo("4"), "nothing outside the caret's block moved");

                // Now with the caret's own cell filled: that digit goes.
                Tap(CellAt(view, GridRowKind.AnswerRow, 0, HardColumnCount - 1));
                Backspace(view);

                Assert.That(view.AnswerText, Is.Empty);
                Assert.That(additionCell.Content, Is.EqualTo("4"));

                // Backspace on an empty block does nothing at all.
                Backspace(view);
                Assert.That(view.AnswerText, Is.Empty);
                Assert.That(additionCell.Content, Is.EqualTo("4"));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void Clear_EmptiesTheCaretsBlockOnly_AndThereIsNoUndoAnywhere()
        {
            var view = CreateView(Turn(Pile.Hard));

            try
            {
                var carryCell = CellAt(view, GridRowKind.CarryStrip, 0, 1);
                Tap(carryCell);
                Type(view, 3);

                var additionCell = CellAt(view, GridRowKind.AdditionRow, 0, 4);
                Tap(additionCell);
                Type(view, 6);

                Tap(CellAt(view, GridRowKind.AnswerRow, 0, HardColumnCount - 1));
                Type(view, 1);
                Type(view, 2);
                Type(view, 3);

                Assert.That(view.AnswerText, Is.EqualTo("321"));

                Clear(view);

                Assert.That(view.AnswerText, Is.Empty, "the block the caret is in");
                Assert.That(carryCell.Content, Is.EqualTo("3"), "and nothing outside it");
                Assert.That(additionCell.Content, Is.EqualTo("6"));

                // "There is no `undo`, only backspace and `clear`."
                Assert.That(
                    view.Keys.Select(key => key.Kind).Distinct().OrderBy(kind => kind),
                    Is.EqualTo(new[] { KeypadKeyKind.Digit, KeypadKeyKind.Backspace, KeypadKeyKind.Clear }.OrderBy(kind => kind)));
                Assert.That(
                    view.Keys.Count(key => key.Kind == KeypadKeyKind.Digit),
                    Is.EqualTo(10),
                    "`1`–`9` and `0`, and no decimal point or minus");
                Assert.That(
                    DeclaredMembers(typeof(WorkingOutGridView))
                        .Where(name => name.IndexOf("Undo", StringComparison.OrdinalIgnoreCase) >= 0),
                    Is.Empty);
                Assert.That(
                    view.KeypadRect.GetComponentsInChildren<Text>(true).Select(text => text.text),
                    Has.No.Member("undo"));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void CheckIt_ReadsTheAnswerLeftToRight_HandsItToTurnResolution_AndMarksNothing()
        {
            var turn = Turn(Pile.Easy);
            var router = new ScreenRouter();
            router.OpenDialog(Frogs.Core.Dialog.WorkingOutGrid);

            var view = CreateView(turn, router);
            var submitted = new List<int>();
            view.AnswerSubmitted += answer => submitted.Add(answer);

            try
            {
                // A deliberately wrong answer, so the test cannot pass by the
                // grid quietly grading anything: `340` is the largest product
                // the easy shape can hold and is not this card's.
                Tap(CellAt(view, GridRowKind.AnswerRow, 0, EasyColumnCount - 1));
                Type(view, 0);
                Type(view, 4);
                Type(view, 3);

                Assert.That(view.AnswerText, Is.EqualTo("340"));

                var before = CellColours(view);

                Tap(view.CheckItButton);

                Assert.That(turn.Submitted, Is.EqualTo(new[] { 340 }), "read left to right, handed over once");
                Assert.That(submitted, Is.EqualTo(new[] { 340 }));
                Assert.That(router.CurrentDialog, Is.EqualTo(Frogs.Core.Dialog.AnswerResult), "the way out");

                // Not one cell changed colour on the way out: the verdict is
                // #224's to draw, on its own dialog.
                Assert.That(CellColours(view), Is.EqualTo(before));

                // The digits that left are the digits Core grades — proved by
                // grading them here, which is the one place in this file that
                // knows a right answer from a wrong one.
                var lane = new Lane();
                var resolution = lane.Resolve(turn.Submitted[0], turn.Card);
                Assert.That(resolution.CorrectAnswer, Is.EqualTo(turn.Card.Product));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void HardwareBack_LeavesTheGridOpenAndUnchanged_AndNothingHereDismissesTheDialog()
        {
            var turn = Turn(Pile.Hard);
            var router = new ScreenRouter();
            router.OpenDialog(Frogs.Core.Dialog.WorkingOutGrid);

            var view = CreateView(turn, router);

            try
            {
                Tap(CellAt(view, GridRowKind.AnswerRow, 0, HardColumnCount - 1));
                Type(view, 5);

                var before = CellTexts(view);

                router.HandleBack();

                Assert.That(router.CurrentDialog, Is.EqualTo(Frogs.Core.Dialog.WorkingOutGrid), "back does not dismiss");
                Assert.That(view.Dialog.IsOpen, Is.True);
                Assert.That(CellTexts(view), Is.EqualTo(before), "and changes no cell");
                Assert.That(turn.Submitted, Is.Empty, "back does not press `Check it` either");

                // The shared Dialog routes back to whichever button an
                // instance nominates as least destructive. This dialog
                // nominates none, which is what makes back inert rather than a
                // `Check it` in disguise.
                Assert.That(view.Dialog.LeastDestructiveButton, Is.Null);

                // The router owns hardware back (#213); this view must not add
                // a second handler that could disagree with it.
                Assert.That(
                    DeclaredMembers(typeof(WorkingOutGridView))
                        .Where(name => name.IndexOf("Back", StringComparison.OrdinalIgnoreCase) >= 0
                            && name.IndexOf("Backspace", StringComparison.OrdinalIgnoreCase) < 0),
                    Is.Empty,
                    "hardware back is the router's, not this view's");

                // No tap-outside, no close cross: the only affordances on this
                // dialog are the keys, the cells and `Check it`.
                Assert.That(
                    view.Dialog.Scrim.GetComponents<MonoBehaviour>()
                        .OfType<IPointerClickHandler>()
                        .ToArray(),
                    Is.Empty,
                    "no tap-outside-to-dismiss");
                Assert.That(
                    DeclaredMembers(typeof(WorkingOutGridView))
                        .Where(name => name.IndexOf("Dismiss", StringComparison.OrdinalIgnoreCase) >= 0
                            || name.IndexOf("Close", StringComparison.OrdinalIgnoreCase) >= 0
                            || name.IndexOf("Cancel", StringComparison.OrdinalIgnoreCase) >= 0),
                    Is.Empty);
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void AdditionSection_GrowsADigitAtATime_StopsAtTheCap_AndGivesTheRowBackWhenItIsBackspacedEmpty()
        {
            var view = CreateView(Turn(Pile.Hard));

            try
            {
                Assert.That(view.AdditionRowCount, Is.EqualTo(WorkingOutGrid.GridAdditionRowsAtStart));

                // A digit in the section's *bottom* row appends another row
                // beneath it. A digit in the row above does not.
                Tap(CellAt(view, GridRowKind.AdditionRow, 0, 1));
                Type(view, 1);
                Assert.That(view.AdditionRowCount, Is.EqualTo(WorkingOutGrid.GridAdditionRowsAtStart));

                GrowSectionBy(view, 1);
                Assert.That(view.AdditionRowCount, Is.EqualTo(WorkingOutGrid.GridAdditionRowsAtStart + 1));
                Assert.That(
                    view.RowKinds.Count(kind => kind == GridRowKind.AdditionRow),
                    Is.EqualTo(view.AdditionRowCount),
                    "the drawn rows are the model's rows");

                // The new row is appended to the bottom of the section, still
                // above the answer row.
                Assert.That(
                    view.RowKinds.Last(),
                    Is.EqualTo(GridRowKind.AnswerRow));

                // And it stops at the cap, however much is typed into it.
                GrowSectionBy(view, WorkingOutGrid.GridAdditionRowsMax);
                Assert.That(view.AdditionRowCount, Is.EqualTo(WorkingOutGrid.GridAdditionRowsMax));

                // Backspacing the last digit out of a grown row gives the row
                // back — Derek's call on #204's open question 5.
                var bottom = view.AdditionRowCount - 1;
                Tap(CellAt(view, GridRowKind.AdditionRow, bottom, 1));
                Backspace(view);

                Assert.That(view.AdditionRowCount, Is.EqualTo(WorkingOutGrid.GridAdditionRowsMax - 1));

                // The floor is what every card is dealt: emptying the rows the
                // card came with never takes one away.
                while (view.AdditionRowCount > WorkingOutGrid.GridAdditionRowsAtStart)
                {
                    Tap(CellAt(view, GridRowKind.AdditionRow, view.AdditionRowCount - 1, 1));
                    Backspace(view);
                }

                Tap(CellAt(view, GridRowKind.AdditionRow, WorkingOutGrid.GridAdditionRowsAtStart - 1, 1));
                Backspace(view);
                Backspace(view);

                Assert.That(view.AdditionRowCount, Is.EqualTo(WorkingOutGrid.GridAdditionRowsAtStart));

                // A grown row is indistinguishable from a dealt one: same
                // cells, same size, same colours, no badge.
                GrowSectionBy(view, 1);
                var rows = Enumerable.Range(0, view.AdditionRowCount)
                    .Select(ordinal => view.Cells[RowIndexOf(view, GridRowKind.AdditionRow, ordinal)])
                    .ToArray();

                foreach (var row in rows)
                {
                    Assert.That(row.Count, Is.EqualTo(rows[0].Count));
                    Assert.That(
                        row[1].RectTransform.rect.size,
                        Is.EqualTo(rows[0][1].RectTransform.rect.size));
                    Assert.That(row[1].Fill.color, Is.EqualTo(rows[0][1].Fill.color));
                }
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void GrownAdditionRows_TakeTheirOwnSmallerHeight_SoTheWholeGridStillFitsAtTheCap()
        {
            // Derek's call on #223, open question 3: "smaller cells for
            // addition rows only." Everything else — the multiplicand and
            // multiplier rows, the rules, both carry strips and the answer row
            // — keeps its size at every count.
            var view = CreateView(Turn(Pile.Hard));

            try
            {
                var panel = view.Dialog.PanelRect;

                // As dealt, at today's numbers: a 732 px grid, unchanged from
                // the committed mockups.
                Assert.That(view.GridRect.rect.height, Is.EqualTo(732f).Within(0.001f));
                Assert.That(view.GridRect.rect.height, Is.EqualTo(DealtGridHeight()).Within(0.001f));
                Assert.That(
                    view.RowRects[RowIndexOf(view, GridRowKind.AdditionRow)].rect.height,
                    Is.EqualTo(WorkingOutGridView.GridCellSize).Within(0.001f),
                    "the section is full size until the player grows it");

                GrowSectionBy(view, WorkingOutGrid.GridAdditionRowsMax);
                Assert.That(view.AdditionRowCount, Is.EqualTo(WorkingOutGrid.GridAdditionRowsMax));

                // Six grown rows at GridAdditionRowHeight: 892 px, in the
                // 908 px the panel has between the header and DialogPadding.
                Assert.That(view.GridRect.rect.height, Is.EqualTo(892f).Within(0.001f));
                Assert.That(view.GridRect.rect.height, Is.EqualTo(GrownGridHeight()).Within(0.001f));
                Assert.That(GridBand(panel), Is.EqualTo(908f).Within(0.001f));
                Assert.That(view.GridRect.rect.height, Is.LessThanOrEqualTo(GridBand(panel)));

                foreach (var kind in new[] { GridRowKind.CarryStrip, GridRowKind.Multiplicand, GridRowKind.Multiplier, GridRowKind.AnswerRow })
                {
                    Assert.That(
                        view.RowRects[RowIndexOf(view, kind)].rect.height,
                        Is.EqualTo(view.RowHeightFor(kind)).Within(0.001f),
                        kind + " keeps its size once the section shrinks");
                }

                Assert.That(
                    view.RowRects[RowIndexOf(view, GridRowKind.AdditionRow)].rect.height,
                    Is.EqualTo(WorkingOutGridView.GridAdditionRowHeight).Within(0.001f));

                foreach (var rule in view.RuleRects)
                {
                    Assert.That(rule.rect.height, Is.EqualTo(WorkingOutGridView.GridRuleThickness).Within(0.001f));
                }

                // Nothing is off the bottom of the tablet, and nothing is
                // under the header — including the answer row, which is what
                // the third mockup drew falling off the screen.
                Assert.That(
                    TopEdge(panel) - TopEdge(view.GridRect),
                    Is.GreaterThanOrEqualTo(WorkingOutGridView.GridHeaderHeight - 0.001f));
                Assert.That(
                    BottomEdge(view.GridRect) - BottomEdge(panel),
                    Is.GreaterThanOrEqualTo(DialogPanel.DialogPadding - 0.001f));

                var answerRow = view.RowRects[RowIndexOf(view, GridRowKind.AnswerRow)];
                Assert.That(
                    BottomEdge(answerRow) - BottomEdge(panel),
                    Is.GreaterThanOrEqualTo(DialogPanel.DialogPadding - 0.001f),
                    "the answer row is on the screen at the cap");
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void Panel_LaysOutHeaderGridAndKeypad_AtTheMockupsOwnGeometry()
        {
            var view = CreateView(Turn(Pile.Hard));

            try
            {
                var panel = view.Dialog.PanelRect;

                // Full-bleed: the canvas inset by SafeMargin on every side,
                // which is what the shared Dialog's maxima are.
                Assert.That(
                    panel.rect.size,
                    Is.EqualTo(new Vector2(DialogPanel.DialogMaxWidth, DialogPanel.DialogMaxHeight)));

                // header — the chip, `Work it out`, and the card readout, one
                // GridHeaderGap apart, DialogPadding in from the left.
                Assert.That(view.WhoseChip.State, Is.EqualTo(PlayerChipState.Active));
                Assert.That(view.PromptText.text, Is.EqualTo("Work it out"));
                Assert.That(view.PromptText.fontSize, Is.EqualTo((int)WorkingOutGridView.GridPromptSize));
                Assert.That(view.CardReadoutText.fontSize, Is.EqualTo((int)WorkingOutGridView.GridCardReadoutLabelSize));
                Assert.That(view.CardReadoutRect.rect.height, Is.EqualTo(WorkingOutGridView.GridCardReadoutHeight).Within(0.001f));
                Assert.That(
                    view.CardReadoutFill.rectTransform.offsetMin,
                    Is.EqualTo(new Vector2(WorkingOutGridView.GridCardReadoutBorderWidth, WorkingOutGridView.GridCardReadoutBorderWidth)),
                    "the readout is outlined, not flat");

                Assert.That(
                    LeftEdge(view.WhoseChip.RectTransform) - LeftEdge(panel),
                    Is.EqualTo(DialogPanel.DialogPadding).Within(0.001f));
                Assert.That(
                    TopEdge(panel) - TopEdge(view.WhoseChip.RectTransform),
                    Is.EqualTo(WorkingOutGridView.GridHeaderTop).Within(0.001f));
                Assert.That(
                    LeftEdge(view.PromptText.rectTransform) - RightEdge(view.WhoseChip.RectTransform),
                    Is.EqualTo(WorkingOutGridView.GridHeaderGap).Within(0.001f));
                Assert.That(
                    LeftEdge(view.CardReadoutRect) - RightEdge(view.PromptText.rectTransform),
                    Is.EqualTo(WorkingOutGridView.GridHeaderGap).Within(0.001f));

                // The header band the grid is measured against: the top of the
                // panel down to the bottom of the chip.
                Assert.That(
                    WorkingOutGridView.GridHeaderTop + PlayerChip.PlayerChipHeight,
                    Is.EqualTo(WorkingOutGridView.GridHeaderHeight).Within(0.001f));

                // keypad — pinned right, DialogPadding from the panel edge,
                // KeypadTop down, and the same place on every problem.
                Assert.That(
                    RightEdge(panel) - RightEdge(view.KeypadRect),
                    Is.EqualTo(DialogPanel.DialogPadding).Within(0.001f));
                Assert.That(
                    TopEdge(panel) - TopEdge(view.KeypadRect),
                    Is.EqualTo(WorkingOutGridView.KeypadTop).Within(0.001f));
                Assert.That(view.KeypadRect.rect.width, Is.EqualTo(WorkingOutGridView.KeypadWidth).Within(0.001f));

                Assert.That(view.Keys.Count, Is.EqualTo(12));
                Assert.That(
                    view.Keys.Select(key => key.Label.text),
                    Is.EqualTo(new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "⌫", "0", "clear" }));

                foreach (var key in view.Keys)
                {
                    Assert.That(
                        key.RectTransform.rect.size,
                        Is.EqualTo(new Vector2(WorkingOutGridView.KeypadKeySize, WorkingOutGridView.KeypadKeySize)));
                    Assert.That(
                        key.Fill.rectTransform.offsetMin,
                        Is.EqualTo(new Vector2(WorkingOutGridView.KeypadKeyBorderWidth, WorkingOutGridView.KeypadKeyBorderWidth)),
                        "a key is outlined, not flat");
                }

                Assert.That(
                    LeftEdge(view.Keys[1].RectTransform) - RightEdge(view.Keys[0].RectTransform),
                    Is.EqualTo(WorkingOutGridView.KeypadKeyGap).Within(0.001f));
                Assert.That(
                    BottomEdge(view.Keys[0].RectTransform) - TopEdge(view.Keys[3].RectTransform),
                    Is.EqualTo(WorkingOutGridView.KeypadKeyGap).Within(0.001f));

                // submit — `Check it`, full keypad width, KeypadSubmitGap
                // under the bottom row of keys.
                Assert.That(
                    view.CheckItButton.RectTransform.rect.size,
                    Is.EqualTo(new Vector2(WorkingOutGridView.KeypadWidth, WorkingOutGridView.CheckButtonHeight)));
                Assert.That(
                    BottomEdge(view.Keys[9].RectTransform) - TopEdge(view.CheckItButton.RectTransform),
                    Is.EqualTo(WorkingOutGridView.KeypadSubmitGap).Within(0.001f));
                Assert.That(
                    LeftEdge(view.CheckItButton.RectTransform),
                    Is.EqualTo(LeftEdge(view.KeypadRect)).Within(0.001f));

                // grid — centred in what is left: inside the padding, right of
                // nothing, left of the keypad column, below the header band.
                var regionLeft = LeftEdge(panel) + DialogPanel.DialogPadding;
                var regionRight = RightEdge(panel) - DialogPanel.DialogPadding - WorkingOutGridView.KeypadWidth;
                var regionTop = TopEdge(panel) - WorkingOutGridView.GridHeaderHeight;
                var regionBottom = BottomEdge(panel) + DialogPanel.DialogPadding;

                Assert.That(
                    LeftEdge(view.GridRect) - regionLeft,
                    Is.EqualTo(regionRight - RightEdge(view.GridRect)).Within(0.001f),
                    "the grid is centred in the space the keypad leaves");
                Assert.That(
                    regionTop - TopEdge(view.GridRect),
                    Is.EqualTo(BottomEdge(view.GridRect) - regionBottom).Within(0.001f),
                    "and centred in the space under the header");

                // Cells: a square GridCellSize digit cell, GridCellGap apart,
                // outlined; the answer row taller and heavier.
                var additionRow = view.Cells[RowIndexOf(view, GridRowKind.AdditionRow)];
                Assert.That(
                    additionRow[1].RectTransform.rect.size,
                    Is.EqualTo(new Vector2(WorkingOutGridView.GridCellSize, WorkingOutGridView.GridCellSize)));
                Assert.That(
                    LeftEdge(additionRow[2].RectTransform) - RightEdge(additionRow[1].RectTransform),
                    Is.EqualTo(WorkingOutGridView.GridCellGap).Within(0.001f));
                Assert.That(
                    additionRow[1].Fill.rectTransform.offsetMin,
                    Is.EqualTo(new Vector2(WorkingOutGridView.GridCellBorderWidth, WorkingOutGridView.GridCellBorderWidth)));
                Assert.That(additionRow[1].Label.fontSize, Is.EqualTo((int)WorkingOutGridView.GridDigitSize));

                var answerRow = view.Cells[RowIndexOf(view, GridRowKind.AnswerRow)];
                Assert.That(
                    answerRow[1].RectTransform.rect.size,
                    Is.EqualTo(new Vector2(WorkingOutGridView.GridCellSize, WorkingOutGridView.GridAnswerRowHeight)));
                Assert.That(
                    answerRow[1].Fill.rectTransform.offsetMin,
                    Is.EqualTo(new Vector2(WorkingOutGridView.GridAnswerBorderWidth, WorkingOutGridView.GridAnswerBorderWidth)),
                    "the answer row's border is the heavier one");

                // Carry strip: a GridCarryRowHeight row of GridCarryBoxSize
                // boxes, one per digit column and none in the operator column.
                var carryRow = view.Cells[RowIndexOf(view, GridRowKind.CarryStrip)];
                Assert.That(
                    view.RowRects[RowIndexOf(view, GridRowKind.CarryStrip)].rect.height,
                    Is.EqualTo(WorkingOutGridView.GridCarryRowHeight).Within(0.001f));
                Assert.That(
                    carryRow[1].Border.rectTransform.rect.size,
                    Is.EqualTo(WorkingOutGridView.GridCarryBoxSize));
                Assert.That(carryRow[0].Border, Is.Null, "no carry box over the operator column");
                Assert.That(
                    carryRow.Skip(1).Count(cell => cell.Kind == GridCellKind.CarryBox),
                    Is.EqualTo(HardColumnCount - 1));

                // Both strips are always on screen, on every card.
                Assert.That(
                    view.RowKinds.Count(kind => kind == GridRowKind.CarryStrip),
                    Is.EqualTo(WorkingOutGrid.CarryStripCount));

                // This dialog has no title — the header does that job.
                Assert.That(view.Dialog.TitleText.text, Is.Empty);
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void Header_NamesTheFrogAndTheCard_AndTakesThePileFromWhatCoreReported()
        {
            var turn = Turn(Pile.Easy, FrogColour.Pink);
            var view = CreateView(turn);

            try
            {
                Assert.That(view.WhoseChip.Label.text, Is.EqualTo("Pink"));
                Assert.That(view.WhoseChip.Swatch.color, Is.EqualTo(FrogColours.For(FrogColour.Pink)));
                Assert.That(
                    view.CardReadoutText.text,
                    Is.EqualTo(turn.Card.Multiplicand + " × " + turn.Card.Multiplier + " · easy pile"));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void EveryGeometryValue_IsANamedConstantFromWorkingOutGridsTable()
        {
            // docs/specs/ui/working-out-grid.md#named-constants, in full: the
            // thirteen pixel values the page always had, plus the twenty this
            // issue added to the table for values the mockups draw and the
            // page had never named, plus GridAdditionRowHeight and
            // GridSmallDigitSize, which are Derek's answer to open question 3.
            var expected = new Dictionary<string, float>
            {
                { "GridCellSize", 104f },
                { "GridCellGap", 8f },
                { "GridCellBorderWidth", 3f },
                { "GridCellRadius", 10f },
                { "GridCarryRowHeight", 56f },
                { "GridCarryBoxBorderWidth", 3f },
                { "GridCarryBoxRadius", 8f },
                { "GridRuleThickness", 6f },
                { "GridAdditionRowHeight", 56f },
                { "GridAnswerRowHeight", 128f },
                { "GridAnswerBorderWidth", 6f },
                { "GridDigitSize", 56f },
                { "GridSmallDigitSize", 28f },
                { "GridEqualsSize", 28f },
                { "GridHeaderHeight", 140f },
                { "GridHeaderTop", 44f },
                { "GridHeaderGap", 32f },
                { "GridPromptSize", 44f },
                { "GridCardReadoutHeight", 96f },
                { "GridCardReadoutPaddingX", 32f },
                { "GridCardReadoutRadius", 20f },
                { "GridCardReadoutBorderWidth", 3f },
                { "GridCardReadoutLabelSize", 40f },
                { "KeypadTop", 216f },
                { "KeypadKeySize", 140f },
                { "KeypadKeyGap", 16f },
                { "KeypadKeyRadius", 20f },
                { "KeypadKeyBorderWidth", 3f },
                { "KeypadKeyLabelSize", 56f },
                { "KeypadBackspaceLabelSize", 40f },
                { "KeypadClearLabelSize", 32f },
                { "KeypadWidth", 452f },
                { "KeypadSubmitGap", 24f },
                { "CheckButtonHeight", 128f }
            };

            var constants = typeof(WorkingOutGridView)
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(field => field.IsLiteral && !field.IsInitOnly)
                .ToArray();

            Assert.That(
                constants.Select(field => field.Name).OrderBy(name => name),
                Is.EqualTo(expected.Keys.OrderBy(name => name)),
                "WorkingOutGridView's public constants are exactly working-out-grid.md's own, under the identical names");

            foreach (var field in constants)
            {
                Assert.That(
                    Convert.ToSingle(field.GetValue(null)),
                    Is.EqualTo(expected[field.Name]).Within(0.001f),
                    "WorkingOutGridView." + field.Name);
            }

            // The one two-number row on the table stays one value here.
            Assert.That(WorkingOutGridView.GridCarryBoxSize, Is.EqualTo(new Vector2(56f, 52f)));

            // The keypad's width is its keys and gaps, not a fourth number
            // that could disagree with them.
            Assert.That(
                (3f * WorkingOutGridView.KeypadKeySize) + (2f * WorkingOutGridView.KeypadKeyGap),
                Is.EqualTo(WorkingOutGridView.KeypadWidth).Within(0.001f));

            // The counts on the same table are Core's, and are referenced
            // rather than copied — a second `2` in the shell and a `2` in Core
            // is exactly the drift the page warns about.
            foreach (var name in new[] { "GridAdditionRowsAtStart", "GridAdditionRowsMax", "CarryStripCount" })
            {
                Assert.That(
                    typeof(WorkingOutGridView).GetField(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly),
                    Is.Null,
                    "WorkingOutGridView must reference WorkingOutGrid." + name + ", not redeclare it");
            }

            // The shared Dialog's and Button's own constants are referenced,
            // never redeclared here.
            foreach (var name in new[] { "DialogPadding", "DialogMaxWidth", "DialogMaxHeight", "DialogRadius", "SafeMargin", "ButtonHeight", "ButtonLabelSize" })
            {
                Assert.That(
                    typeof(WorkingOutGridView).GetField(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly),
                    Is.Null,
                    "WorkingOutGridView must not redeclare " + name);
            }
        }

        [Test]
        public void Grid_WiresItsBuiltInSpritesAndFonts_RatherThanLeavingThemMissing()
        {
            var view = CreateView(Turn(Pile.Hard));

            try
            {
                Assert.That(view.PromptText.font, Is.Not.Null);
                Assert.That(view.CardReadoutText.font, Is.Not.Null);
                Assert.That(view.CardReadoutBorder.sprite, Is.Not.Null);
                Assert.That(view.Keys[0].Border.sprite, Is.Not.Null);
                Assert.That(view.Keys[0].Label.font, Is.Not.Null);

                var answerCell = CellAt(view, GridRowKind.AnswerRow, 0, 1);
                Assert.That(answerCell.Border.sprite, Is.Not.Null);
                Assert.That(answerCell.Fill.sprite, Is.Not.Null);
                Assert.That(answerCell.Label.font, Is.Not.Null);

                var carryCell = CellAt(view, GridRowKind.CarryStrip, 0, 1);
                Assert.That(carryCell.Border.sprite, Is.Not.Null);
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void Grid_MarksNothing_AndHasNoWayToKnowWhatWouldBeCorrect()
        {
            var view = CreateView(Turn(Pile.Hard));

            try
            {
                // Every editable cell is drawn the same way whatever is typed
                // into it — the grid looks identical whether every digit so
                // far is right or wrong.
                var before = CellColours(view);

                Tap(CellAt(view, GridRowKind.AnswerRow, 0, HardColumnCount - 1));
                Type(view, 9);
                Type(view, 9);
                Type(view, 9);

                Tap(CellAt(view, GridRowKind.CarryStrip, 1, 1));
                Type(view, 8);

                var after = CellColours(view);

                // Only the caret's own cell differs, and it differs because it
                // is the caret's, not because of what is in it.
                var differences = before.Zip(after, (first, second) => first == second).Count(same => !same);
                Assert.That(differences, Is.LessThanOrEqualTo(2), "nothing is marked; only the caret moves");

                var forbidden = new[] { "Correct", "Wrong", "Grade", "Verdict", "Score", "Marked", "Valid", "Product" };

                foreach (var type in new[] { typeof(WorkingOutGridView), typeof(WorkingOutGridCell), typeof(WorkingOutKeypadKey) })
                {
                    foreach (var member in DeclaredMembers(type))
                    {
                        foreach (var word in forbidden)
                        {
                            Assert.That(
                                member.IndexOf(word, StringComparison.OrdinalIgnoreCase),
                                Is.LessThan(0),
                                type.Name + "." + member + " looks like " + word + " — nothing on this screen is graded");
                        }
                    }

                    var coroutines = type
                        .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                        .Where(method => typeof(IEnumerator).IsAssignableFrom(method.ReturnType))
                        .ToArray();

                    Assert.That(coroutines, Is.Empty, type.Name + " starts no coroutine");
                }

                // No timer, ever.
                Assert.That(
                    DeclaredMembers(typeof(WorkingOutGridView))
                        .Where(name => name.IndexOf("Timer", StringComparison.OrdinalIgnoreCase) >= 0
                            || name.IndexOf("Countdown", StringComparison.OrdinalIgnoreCase) >= 0
                            || name.IndexOf("Deadline", StringComparison.OrdinalIgnoreCase) >= 0),
                    Is.Empty);
            }
            finally
            {
                Destroy(view);
            }
        }

        // ---------------------------------------------------------------
        // Whether a tap arrives at all (#288).
        //
        // Every other test in this file taps by calling `OnPointerClick`, and
        // that tests what the view does *with* a tap. It cannot test whether
        // one ever gets there, because it bypasses the raycast — which is the
        // gap the keypad and the grid were dead in for a whole release. These
        // five go through the raycast instead, on a canvas, by screen
        // position; see TappableScreen for what that can and cannot say.
        // ---------------------------------------------------------------

        [Test]
        public void ARaycastAtCheckIt_ReachesTheButton_WhichIsHowTheHarnessProvesItself()
        {
            // The control, and it has already earned its place: a shared
            // Button has a raycast target today, so this passes with the
            // keypad and the grid still dead — and when it did *not* pass, it
            // said the harness was wrong rather than the keypad, which is
            // exactly what it is here to say.
            var screen = new TappableScreen(Turn(Pile.Easy));

            try
            {
                var button = screen.View.CheckItButton;
                var point = CentreOf(button.RectTransform);

                Assert.That(
                    screen.TargetAt<IPointerDownHandler>(point),
                    Is.SameAs(button.gameObject),
                    screen.Describe(point));
            }
            finally
            {
                screen.Destroy();
            }
        }

        [Test]
        public void ARaycastAtEveryKeypadKey_ReachesThatKey()
        {
            var screen = new TappableScreen(Turn(Pile.Easy));

            try
            {
                // All twelve, one at a time: `1`–`9`, backspace, `0`, `clear`.
                // Twelve keys resolving to twelve different keys is also the
                // answer to "the tap area is the key, not the gap between
                // them" — a hit area spilling into its neighbour would land
                // two of these on the same key.
                foreach (var key in screen.View.Keys)
                {
                    var point = CentreOf(key.RectTransform);

                    Assert.That(
                        screen.TargetAt<IPointerClickHandler>(point),
                        Is.SameAs(key.gameObject),
                        key.name + " cannot be tapped." + Environment.NewLine + screen.Describe(point));
                }
            }
            finally
            {
                screen.Destroy();
            }
        }

        [Test]
        public void ARaycastAtAnEditableCellOrACarryBox_ReachesThatCell_AndTwoAdjacentOnesReachTwoDifferentCells()
        {
            var screen = new TappableScreen(Turn(Pile.Easy));

            try
            {
                var view = screen.View;

                var reachable = new[]
                {
                    CellAt(view, GridRowKind.AnswerRow, 0, EasyColumnCount - 1),
                    CellAt(view, GridRowKind.AnswerRow, 0, EasyColumnCount - 2),
                    CellAt(view, GridRowKind.AdditionRow, 0, EasyColumnCount - 1),
                    // The carry boxes are editable too — scratch paper you can
                    // write in is scratch paper you can tap.
                    CellAt(view, GridRowKind.CarryStrip, 0, 1),
                    CellAt(view, GridRowKind.CarryStrip, 1, 1)
                };

                foreach (var cell in reachable)
                {
                    var point = CentreOf(cell.RectTransform);

                    Assert.That(cell.IsEditable, Is.True, "the fixture picked a cell nobody can type in");
                    Assert.That(
                        screen.TargetAt<IPointerClickHandler>(point),
                        Is.SameAs(cell.gameObject),
                        cell.RowKind + " cell " + cell.Column + " cannot be tapped."
                            + Environment.NewLine + screen.Describe(point));
                }

                // And the two next to each other are two, not one: the tap
                // area is the cell's own box.
                Assert.That(
                    screen.TargetAt<IPointerClickHandler>(CentreOf(reachable[0].RectTransform)),
                    Is.Not.SameAs(screen.TargetAt<IPointerClickHandler>(CentreOf(reachable[1].RectTransform))));
            }
            finally
            {
                screen.Destroy();
            }
        }

        [Test]
        public void ARaycastAtAPrintedDigitOrTheOperatorColumn_ReachesNothing()
        {
            var screen = new TappableScreen(Turn(Pile.Easy));

            try
            {
                var view = screen.View;

                // The card's own digits and the `×` are not cells the caret
                // can go to, and HandleCellTapped's early return says so. That
                // return stays a real guard only while these are unhittable —
                // a hit that is then ignored is a different design.
                var unreachable = new[]
                {
                    CellAt(view, GridRowKind.Multiplicand, 0, EasyColumnCount - 1),
                    CellAt(view, GridRowKind.Multiplier, 0, EasyColumnCount - 1),
                    CellAt(view, GridRowKind.Multiplier, 0, 0)
                };

                foreach (var cell in unreachable)
                {
                    var point = CentreOf(cell.RectTransform);

                    Assert.That(cell.IsEditable, Is.False, "the fixture picked a cell the player can type in");
                    Assert.That(
                        screen.TargetAt<IPointerClickHandler>(point),
                        Is.Null,
                        cell.RowKind + " cell " + cell.Column + " takes a tap and should not."
                            + Environment.NewLine + screen.Describe(point));
                }
            }
            finally
            {
                screen.Destroy();
            }
        }

        [Test]
        public void AWholeTurn_TappedThroughTheRaycaster_FillsTheAnswer_EnablesCheckIt_AndLandsOnTheAnswerResult()
        {
            // The test that would have caught #288: a turn played the way the
            // tablet plays one, from the first tap on the grid to the dialog
            // that comes next. Nothing here calls a handler by hand.
            var turn = Turn(Pile.Easy);
            var router = new ScreenRouter();
            router.OpenDialog(Frogs.Core.Dialog.WorkingOutGrid);

            var screen = new TappableScreen(turn, router);

            try
            {
                var view = screen.View;

                Assert.That(view.CheckItButton.IsDisabled, Is.True, "an empty answer is not a wrong answer");

                screen.TapAt(CentreOf(CellAt(view, GridRowKind.AnswerRow, 0, EasyColumnCount - 1).RectTransform));

                // `340` again: the largest product the easy shape holds, and
                // deliberately not this card's, so nothing can pass by the
                // grid quietly grading it.
                screen.TapAt(CentreOf(KeyFor(view, 0).RectTransform));
                screen.TapAt(CentreOf(KeyFor(view, 4).RectTransform));
                screen.TapAt(CentreOf(KeyFor(view, 3).RectTransform));

                Assert.That(view.AnswerText, Is.EqualTo("340"), "three taps on the keypad, three digits in the answer row");
                Assert.That(view.CheckItButton.IsDisabled, Is.False, "a digit is in, so the turn can be finished");

                screen.TapAt(CentreOf(view.CheckItButton.RectTransform));

                Assert.That(turn.Submitted, Is.EqualTo(new[] { 340 }));
                Assert.That(
                    router.CurrentDialog,
                    Is.EqualTo(Frogs.Core.Dialog.AnswerResult),
                    "the way out of the dialog that cannot be dismissed");
            }
            finally
            {
                screen.Destroy();
            }
        }

        static WorkingOutKeypadKey KeyFor(WorkingOutGridView view, int digit)
        {
            return view.Keys.Single(key => key.Kind == KeypadKeyKind.Digit && key.Digit == digit);
        }

        static Vector2 CentreOf(RectTransform rect)
        {
            var corners = Corners(rect);
            return (Vector2)(corners[0] + corners[2]) / 2f;
        }

        /// <summary>
        /// A canvas with the grid under it, and a tap delivered the way the
        /// running app delivers one: find the <see cref="Graphic"/> under a
        /// screen position, then walk *up* to the first ancestor that handles
        /// the event. A component with no raycast target anywhere beneath it
        /// is unreachable that way however good its handler is, which is all
        /// #288 ever was.
        ///
        /// The canvas is <c>AppRoot</c>'s in shape — screen-space overlay, a
        /// design of 1920 × 1200 scaled to fit the screen the way its
        /// CanvasScaler's Expand mode scales it. It is placed by hand rather
        /// than left to Unity, because an edit-mode session ticks no frame for
        /// Unity to place it on and everything below works in screen pixels.
        ///
        /// **Why this does not call <c>GraphicRaycaster.Raycast</c>.** It was
        /// written that way first and the answer came back from CI: the
        /// raycaster's first filter is `graphic.depth == -1` — "hasn't been
        /// processed by the canvas, which means it isn't actually drawn" — and
        /// a headless editor never draws a canvas, so every graphic in this
        /// fixture reports -1, `Check it`'s border included. It returns nothing
        /// for every point on the screen, which is a harness that can only ever
        /// say no.
        ///
        /// So the raycast is driven one level down, through the pieces the
        /// raycaster itself is made of: the canvas's own
        /// <see cref="GraphicRegistry"/>, the same rectangle-contains-point
        /// test, and <see cref="Graphic.Raycast"/> — which is what honours a
        /// <c>CanvasGroup</c> that is not blocking, or an <c>Image</c> with a
        /// hit-test threshold. What goes with the depth filter is draw order:
        /// this cannot say which of two overlapping targets wins. What it can
        /// say is whether there is anything under the point to send a tap to,
        /// which is the whole of #288.
        /// </summary>
        sealed class TappableScreen
        {
            const float DesignWidth = 1920f;
            const float DesignHeight = 1200f;

            readonly GameObject _canvasGO;
            readonly Canvas _canvas;

            internal TappableScreen(IWorkingOutTurn turn, ScreenRouter router = null)
            {
                Assert.That(Screen.width, Is.GreaterThan(0), "this editor session reports no screen to raycast against");
                Assert.That(Screen.height, Is.GreaterThan(0), "this editor session reports no screen to raycast against");

                _canvasGO = new GameObject("RaycastCanvas", typeof(RectTransform), typeof(Canvas));

                _canvas = _canvasGO.GetComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                // What Unity does to an overlay canvas on a frame that never
                // comes here: the rect is the screen, in screen pixels, and a
                // world position is therefore a screen position.
                var canvasRect = (RectTransform)_canvasGO.transform;
                canvasRect.sizeDelta = new Vector2(Screen.width, Screen.height);
                canvasRect.localScale = Vector3.one;
                canvasRect.position = new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);

                var host = new GameObject(nameof(WorkingOutGridViewTests), typeof(RectTransform));
                var hostRect = (RectTransform)host.transform;
                hostRect.SetParent(canvasRect, worldPositionStays: false);

                // CanvasScaler's Expand: the design never gets cropped, so it
                // shrinks to whichever of the two axes is tighter.
                var scale = Mathf.Min(Screen.width / DesignWidth, Screen.height / DesignHeight);
                hostRect.localScale = new Vector3(scale, scale, 1f);

                View = host.AddComponent<WorkingOutGridView>();
                View.Initialize(turn, router);

                Canvas.ForceUpdateCanvases();
            }

            internal WorkingOutGridView View { get; }

            /// <summary>
            /// The first thing under <paramref name="screenPoint"/> that could
            /// receive <typeparamref name="THandler"/>, or null. The scrim and
            /// the panel are raycast targets of their own and neither handles
            /// anything, so they are stepped over rather than counted as a hit.
            /// </summary>
            internal GameObject TargetAt<THandler>(Vector2 screenPoint) where THandler : IEventSystemHandler
            {
                foreach (var graphic in GraphicsUnder(screenPoint))
                {
                    var handler = ExecuteEvents.GetEventHandler<THandler>(graphic.gameObject);

                    if (handler != null)
                    {
                        return handler;
                    }
                }

                return null;
            }

            /// <summary>
            /// A tap, in StandaloneInputModule's own order: pointer down and
            /// pointer up to whatever handles them, then the click.
            /// </summary>
            internal void TapAt(Vector2 screenPoint)
            {
                var eventData = new PointerEventData(null)
                {
                    position = screenPoint,
                    pressPosition = screenPoint
                };

                var pressed = TargetAt<IPointerDownHandler>(screenPoint);
                var clicked = TargetAt<IPointerClickHandler>(screenPoint);

                if (pressed == null && clicked == null)
                {
                    Assert.Fail("nothing under " + screenPoint + " can take a tap."
                        + Environment.NewLine + Describe(screenPoint));
                }

                if (pressed != null)
                {
                    ExecuteEvents.Execute(pressed, eventData, ExecuteEvents.pointerDownHandler);
                    ExecuteEvents.Execute(pressed, eventData, ExecuteEvents.pointerUpHandler);
                }

                if (clicked != null)
                {
                    ExecuteEvents.Execute(clicked, eventData, ExecuteEvents.pointerClickHandler);
                }
            }

            /// <summary>
            /// Every graphic registered against this canvas that accepts a
            /// raycast and whose rectangle contains the point — the set the
            /// GraphicRaycaster would pick its winner from.
            /// </summary>
            internal List<Graphic> GraphicsUnder(Vector2 screenPoint)
            {
                var found = new List<Graphic>();
                var registered = GraphicRegistry.GetGraphicsForCanvas(_canvas);

                for (var index = 0; index < registered.Count; index++)
                {
                    var graphic = registered[index];

                    if (graphic == null || !graphic.raycastTarget || !graphic.isActiveAndEnabled)
                    {
                        continue;
                    }

                    if (!RectTransformUtility.RectangleContainsScreenPoint(graphic.rectTransform, screenPoint, null))
                    {
                        continue;
                    }

                    // The graphic's own say: a CanvasGroup up the tree that is
                    // not blocking raycasts, or an Image with a hit-test
                    // threshold, refuses the tap here exactly as it would in
                    // the running app.
                    if (!graphic.Raycast(screenPoint, null))
                    {
                        continue;
                    }

                    found.Add(graphic);
                }

                return found;
            }

            /// <summary>
            /// What was under the point, for a failure message. "Nothing was
            /// hit" is worth two different fixes depending on why, so the
            /// failure says which: nothing raycastable under the point, or
            /// nothing raycastable in the whole view.
            /// </summary>
            internal string Describe(Vector2 screenPoint)
            {
                var report = new StringBuilder();
                var canvasRect = (RectTransform)_canvasGO.transform;

                report.AppendLine("point " + screenPoint
                    + " on a " + Screen.width + " x " + Screen.height + " screen");
                report.AppendLine("canvas rect " + canvasRect.rect
                    + " at " + canvasRect.position
                    + ", view scaled " + View.transform.localScale);

                foreach (var graphic in GraphicsUnder(screenPoint))
                {
                    report.AppendLine("  under the point: " + PathOf(graphic.gameObject));
                }

                foreach (var graphic in View.GetComponentsInChildren<Graphic>(true))
                {
                    if (!graphic.raycastTarget)
                    {
                        continue;
                    }

                    report.AppendLine("  raycast target " + PathOf(graphic.gameObject)
                        + " (canvas " + (graphic.canvas == null ? "none" : graphic.canvas.name) + ")");
                }

                return report.ToString();
            }

            internal void Destroy()
            {
                if (_canvasGO != null)
                {
                    UnityEngine.Object.DestroyImmediate(_canvasGO);
                }
            }

            static string PathOf(GameObject target)
            {
                var path = target.name;

                for (var parent = target.transform.parent; parent != null; parent = parent.parent)
                {
                    path = parent.name + "/" + path;
                }

                return path;
            }
        }

        // docs/specs/ui/working-out-grid.md#mockup's own arithmetic, rebuilt
        // from the constants rather than quoted: the grid as every card deals
        // it, and the grid with the section grown to the cap.
        static float DealtGridHeight()
        {
            return FixedRowsHeight()
                + (WorkingOutGrid.GridAdditionRowsAtStart * WorkingOutGridView.GridCellSize)
                + ((6 + WorkingOutGrid.GridAdditionRowsAtStart) * WorkingOutGridView.GridCellGap);
        }

        static float GrownGridHeight()
        {
            return FixedRowsHeight()
                + (WorkingOutGrid.GridAdditionRowsMax * WorkingOutGridView.GridAdditionRowHeight)
                + ((6 + WorkingOutGrid.GridAdditionRowsMax) * WorkingOutGridView.GridCellGap);
        }

        // Everything that is the same height at every addition-row count: two
        // carry strips, the multiplicand, the multiplier, two rules and the
        // answer row.
        static float FixedRowsHeight()
        {
            return (2f * WorkingOutGridView.GridCarryRowHeight)
                + (2f * WorkingOutGridView.GridCellSize)
                + (2f * WorkingOutGridView.GridRuleThickness)
                + WorkingOutGridView.GridAnswerRowHeight;
        }

        static float GridBand(RectTransform panel)
        {
            return panel.rect.height - WorkingOutGridView.GridHeaderHeight - DialogPanel.DialogPadding;
        }

        static void AssertRuleBetween(RectTransform rule, RectTransform above, RectTransform below)
        {
            Assert.That(
                BottomEdge(above) - TopEdge(rule),
                Is.EqualTo(WorkingOutGridView.GridCellGap).Within(0.001f));
            Assert.That(
                BottomEdge(rule) - TopEdge(below),
                Is.EqualTo(WorkingOutGridView.GridCellGap).Within(0.001f));
        }

        static int RowIndexOf(WorkingOutGridView view, GridRowKind kind, int ordinal = 0)
        {
            var seen = 0;

            for (var index = 0; index < view.RowKinds.Count; index++)
            {
                if (view.RowKinds[index] != kind)
                {
                    continue;
                }

                if (seen == ordinal)
                {
                    return index;
                }

                seen++;
            }

            throw new AssertionException("no row of kind " + kind + " at ordinal " + ordinal);
        }

        static WorkingOutGridCell CellAt(WorkingOutGridView view, GridRowKind kind, int ordinal, int column)
        {
            return view.Cells[RowIndexOf(view, kind, ordinal)][column];
        }

        static string[] CellTexts(WorkingOutGridView view)
        {
            return view.Cells.SelectMany(row => row).Select(cell => cell.Content).ToArray();
        }

        static Color[] CellColours(WorkingOutGridView view)
        {
            return view.Cells
                .SelectMany(row => row)
                .Where(cell => cell.Border != null)
                .SelectMany(cell => new[] { cell.Border.color, cell.Fill.color, cell.Label.color })
                .ToArray();
        }

        // Grows the addition section by typing one digit into whatever its
        // bottom row currently is, `times` times — which is the only thing
        // that grows it.
        static void GrowSectionBy(WorkingOutGridView view, int times)
        {
            for (var step = 0; step < times; step++)
            {
                Tap(CellAt(view, GridRowKind.AdditionRow, view.AdditionRowCount - 1, 1));
                Type(view, 1);
            }
        }

        static void Type(WorkingOutGridView view, int digit)
        {
            Tap(view.Keys.Single(key => key.Kind == KeypadKeyKind.Digit && key.Digit == digit));
        }

        static void Backspace(WorkingOutGridView view)
        {
            Tap(view.Keys.Single(key => key.Kind == KeypadKeyKind.Backspace));
        }

        static void Clear(WorkingOutGridView view)
        {
            Tap(view.Keys.Single(key => key.Kind == KeypadKeyKind.Clear));
        }

        static void Tap(WorkingOutKeypadKey key)
        {
            key.OnPointerClick(new PointerEventData(null));
        }

        static void Tap(WorkingOutGridCell cell)
        {
            cell.OnPointerClick(new PointerEventData(null));
        }

        static void Tap(Button button)
        {
            var corners = Corners(button.RectTransform);
            var eventData = new PointerEventData(null)
            {
                position = (Vector2)(corners[0] + corners[2]) / 2f
            };

            button.OnPointerDown(eventData);
            button.OnPointerUp(eventData);
        }

        static StubTurn Turn(Pile pile, FrogColour frog = FrogColour.Green)
        {
            return new StubTurn
            {
                Frog = frog,
                Pile = pile,
                Card = Card.Draw(pile, Rng.FromSeed(Seed))
            };
        }

        /// <summary>
        /// A fixed card, and somewhere for the answer to go. There is no
        /// grading in here: <see cref="Submitted"/> records what the grid
        /// handed over, and nothing tells the grid what came of it.
        /// </summary>
        sealed class StubTurn : IWorkingOutTurn
        {
            public FrogColour Frog { get; set; }

            public Pile Pile { get; set; }

            public Card Card { get; set; }

            public List<int> Submitted { get; } = new List<int>();

            public void SubmitAnswer(int answer)
            {
                Submitted.Add(answer);
            }
        }

        static IEnumerable<string> DeclaredMembers(Type type)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            return type.GetMembers(flags).Select(member => member.Name);
        }

        static Vector3[] Corners(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return corners;
        }

        static float LeftEdge(RectTransform rect) => Corners(rect)[0].x;

        static float RightEdge(RectTransform rect) => Corners(rect)[2].x;

        static float TopEdge(RectTransform rect) => Corners(rect)[2].y;

        static float BottomEdge(RectTransform rect) => Corners(rect)[0].y;

        static WorkingOutGridView CreateView(IWorkingOutTurn turn, ScreenRouter router = null)
        {
            var host = new GameObject(nameof(WorkingOutGridViewTests), typeof(RectTransform));
            var view = host.AddComponent<WorkingOutGridView>();
            view.Initialize(turn, router);
            return view;
        }

        static void Destroy(WorkingOutGridView view)
        {
            if (view != null)
            {
                UnityEngine.Object.DestroyImmediate(view.gameObject);
            }
        }
    }
}
