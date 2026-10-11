using System;
using System.Collections.Generic;

namespace DreamyMosaics.Puzzle
{
    public sealed class PlaySession
    {
        readonly LevelData level;
        readonly bool[] solution;
        readonly bool[] givenCats;
        readonly bool[] givenMarks;
        readonly List<Snapshot> history = new List<Snapshot>();

        bool[] cats;
        bool[] wrongCats;
        bool[] marks;
        bool[] wrongMarks;

        public PlaySession(LevelData level, bool[] solution)
        {
            if (level == null)
                throw new ArgumentNullException("level");
            if (solution == null || solution.Length != level.size * level.size)
                throw new ArgumentException("A verified solution is required.");

            this.level = level;
            this.solution = (bool[])solution.Clone();
            int cells = level.size * level.size;
            givenCats = new bool[cells];
            givenMarks = new bool[cells];
            cats = new bool[cells];
            wrongCats = new bool[cells];
            marks = new bool[cells];
            wrongMarks = new bool[cells];

            if (level.givenCats != null)
            {
                for (int i = 0; i < level.givenCats.Length; i++)
                {
                    int cell = level.givenCats[i];
                    givenCats[cell] = true;
                    cats[cell] = true;
                    if (!solution[cell])
                        throw new ArgumentException("A given cat is not in the verified solution.");
                }
            }

            if (level.givenMarks != null)
            {
                for (int i = 0; i < level.givenMarks.Length; i++)
                {
                    int cell = level.givenMarks[i];
                    givenMarks[cell] = true;
                    marks[cell] = true;
                    if (solution[cell])
                        throw new ArgumentException("A given empty mark sits on the solution cat.");
                }
            }

            HeartsEnabled = level.hearts != 0;
            MaxHearts = 3;
            Hearts = MaxHearts;
            HintLimit = level.hintLimit > 0 ? level.hintLimit : (level.tutorial == 1 ? 6 : 3);
        }

        public LevelData Level { get { return level; } }
        public int Size { get { return level.size; } }
        public bool HeartsEnabled { get; private set; }
        public int MaxHearts { get; private set; }
        public int Hearts { get; private set; }
        public int Moves { get; private set; }
        public int Mistakes { get; private set; }
        public int HintsUsed { get; private set; }
        public int HintLimit { get; private set; }
        public bool CanUndo { get { return history.Count > 0; } }
        public bool[] Cats { get { return cats; } }
        public bool[] WrongCats { get { return wrongCats; } }
        public bool[] Marks { get { return marks; } }
        public bool[] WrongMarks { get { return wrongMarks; } }
        public bool[] Solution { get { return solution; } }

        public bool IsSolved()
        {
            return QueensRules.CompletionError(level.size, level.regions, cats, false) == null;
        }

        public string Describe(int cell)
        {
            int n = level.size;
            if (cell < 0 || cell >= n * n)
                return "Select a cell.";

            int row = QueensRules.RowOf(n, cell) + 1;
            int col = QueensRules.ColOf(n, cell) + 1;
            string where = "Row " + row + ", column " + col + ", " + QueensRules.RegionName(level.regions[cell]) + " region.";
            if (cats[cell] && wrongCats[cell])
                return where + " This memory is not part of the solution.";
            if (cats[cell] && givenCats[cell])
                return where + " This memory is given.";
            if (cats[cell])
                return where + " A memory is placed here.";
            if (givenMarks[cell])
                return where + " This cell starts empty.";
            if (marks[cell] && wrongMarks[cell])
                return where + " This X is on the solution memory.";
            if (marks[cell])
                return where + " You marked this cell with an X.";

            string block = QueensRules.PlacementBlockReason(n, level.regions, cats, givenMarks, cell);
            if (block != null)
                return where + " " + block;
            return where + " This cell can still hold a memory.";
        }

        public ActionResult PlaceCat(int cell)
        {
            int n = level.size;
            if (cell < 0 || cell >= n * n)
                return Reject("Select a cell on the board.");
            if (cats[cell])
                return Reject("This cell already has a memory. Undo to take it back.");

            string block = QueensRules.PlacementBlockReason(n, level.regions, cats, givenMarks, cell);
            if (block != null)
                return Reject(block);

            Push();
            bool refunded = ClearWrongMark(cell);
            marks[cell] = false;
            cats[cell] = true;
            Moves++;

            if (!solution[cell])
            {
                wrongCats[cell] = true;
                if (HeartsEnabled)
                {
                    Mistakes++;
                    Hearts--;
                }

                return new ActionResult
                {
                    Applied = true,
                    Mistake = true,
                    Refunded = refunded,
                    Lost = HeartsEnabled && Hearts <= 0,
                    Message = HeartsEnabled
                        ? "That cell keeps one memory in each row, column, and region, and it does not touch another memory, but it is not part of this puzzle's solution. A heart is spent. Undo the memory to take the heart back."
                        : "That cell is legal so far, but it is not where this puzzle's memory belongs. Undo it and look again.",
                    HighlightCell = cell
                };
            }

            wrongCats[cell] = false;
            bool won = IsSolved();
            return new ActionResult
            {
                Applied = true,
                Won = won,
                Refunded = refunded,
                Message = won
                    ? "Every row, column, and region has one memory, and no two memories touch."
                    : "The memory is placed. Its row, column, region, and every touching cell are no longer available.",
                HighlightCell = cell,
                HighlightPlace = true
            };
        }

        public ActionResult ToggleMark(int cell)
        {
            int n = level.size;
            if (cell < 0 || cell >= n * n)
                return Reject("Select a cell on the board.");
            if (givenMarks[cell])
                return Reject("This cell starts empty, so the X stays.");
            if (givenCats[cell] || (cats[cell] && !wrongCats[cell] && givenCats[cell]))
                return Reject("This memory is given.");
            if (cats[cell])
                return Reject("Undo the memory before marking this cell with an X.");

            if (marks[cell])
            {
                Push();
                bool refunded = ClearWrongMark(cell);
                marks[cell] = false;
                return new ActionResult
                {
                    Applied = true,
                    Refunded = refunded,
                    Message = "The X is removed.",
                    HighlightCell = cell
                };
            }

            Push();
            marks[cell] = true;
            if (solution[cell])
            {
                wrongMarks[cell] = true;
                if (HeartsEnabled)
                {
                    Mistakes++;
                    Hearts--;
                }

                return new ActionResult
                {
                    Applied = true,
                    Mistake = true,
                    Lost = HeartsEnabled && Hearts <= 0,
                    Message = HeartsEnabled
                        ? "That X is on the cell that must hold the memory. A heart is spent. Undo, or right-click the X again, to take the heart back."
                        : "That X covers the cell that must hold the memory. Remove the X.",
                    HighlightCell = cell
                };
            }

            return new ActionResult
            {
                Applied = true,
                Message = "The cell is marked with an X. You can remove it later.",
                HighlightCell = cell
            };
        }

        public ActionResult Erase(int cell)
        {
            int n = level.size;
            if (cell < 0 || cell >= n * n)
                return Reject("Select a cell on the board.");
            if (givenCats[cell])
                return Reject("This memory is given and stays on the board.");
            if (givenMarks[cell])
                return Reject("This empty cell is given and stays marked.");
            if (!cats[cell] && !marks[cell])
                return Reject("There is nothing to erase in that cell.");

            Push();
            bool refunded = false;
            if (cats[cell])
            {
                refunded = wrongCats[cell] && Refund();
                cats[cell] = false;
                wrongCats[cell] = false;
                Moves++;
            }

            if (marks[cell] && !givenMarks[cell])
            {
                refunded = ClearWrongMark(cell) || refunded;
                marks[cell] = false;
            }

            return new ActionResult
            {
                Applied = true,
                Refunded = refunded,
                Message = refunded ? "Erased. The heart from that mistake is restored." : "Erased.",
                HighlightCell = cell
            };
        }

        public ActionResult Undo()
        {
            if (history.Count == 0)
                return Reject("There is nothing to undo.");

            Snapshot snapshot = history[history.Count - 1];
            history.RemoveAt(history.Count - 1);
            cats = snapshot.Cats;
            wrongCats = snapshot.WrongCats;
            marks = snapshot.Marks;
            wrongMarks = snapshot.WrongMarks;
            Moves = snapshot.Moves;
            Mistakes = snapshot.Mistakes;
            Hearts = snapshot.Hearts;
            HintsUsed = snapshot.HintsUsed;
            return new ActionResult
            {
                Applied = true,
                Message = "Undone.",
                HighlightCell = snapshot.Cell
            };
        }

        public void Restart()
        {
            int cells = level.size * level.size;
            cats = new bool[cells];
            wrongCats = new bool[cells];
            marks = new bool[cells];
            wrongMarks = new bool[cells];
            for (int i = 0; i < cells; i++)
            {
                if (givenCats[i])
                    cats[i] = true;
                if (givenMarks[i])
                    marks[i] = true;
            }

            history.Clear();
            Moves = 0;
            Mistakes = 0;
            Hearts = MaxHearts;
            HintsUsed = 0;
        }

        public ActionResult Hint()
        {
            if (IsSolved())
                return Reject("The puzzle is already complete.");
            if (HintsUsed >= HintLimit)
                return Reject("No hints left on this puzzle.");

            for (int i = 0; i < cats.Length; i++)
            {
                if (!wrongCats[i])
                    continue;
                Push();
                HintsUsed++;
                return new ActionResult
                {
                    Applied = true,
                    HighlightCell = i,
                    Message = "Hint: the memory in row " + (QueensRules.RowOf(level.size, i) + 1) + ", column "
                        + (QueensRules.ColOf(level.size, i) + 1) + " is not part of the solution. Undo it, and the heart comes back."
                };
            }

            for (int i = 0; i < marks.Length; i++)
            {
                if (!wrongMarks[i])
                    continue;
                Push();
                HintsUsed++;
                return new ActionResult
                {
                    Applied = true,
                    HighlightCell = i,
                    Message = "Hint: the X in row " + (QueensRules.RowOf(level.size, i) + 1) + ", column "
                        + (QueensRules.ColOf(level.size, i) + 1) + " covers the solution memory. Remove the X."
                };
            }

            bool[] solverCats = new bool[cats.Length];
            bool[] removed = new bool[cats.Length];
            for (int i = 0; i < cats.Length; i++)
            {
                if (cats[i] && solution[i])
                    solverCats[i] = true;
                if (givenMarks[i] || (marks[i] && !solution[i]))
                    removed[i] = true;
            }

            Deduction step;
            if (!QueensSearch.TryNextDeduction(level.size, level.regions, solverCats, removed, out step))
            {
                return Reject("No forced move was found from the current memories and notes. The hint was not spent.");
            }

            Push();
            HintsUsed++;
            return new ActionResult
            {
                Applied = true,
                HighlightCell = step.Cell,
                HighlightPlace = step.PlaceCat,
                Message = "Hint: " + step.Explanation
            };
        }

        bool ClearWrongMark(int cell)
        {
            if (!wrongMarks[cell])
                return false;
            wrongMarks[cell] = false;
            return Refund();
        }

        bool Refund()
        {
            if (!HeartsEnabled)
                return false;
            if (Hearts < MaxHearts)
                Hearts++;
            if (Mistakes > 0)
                Mistakes--;
            return true;
        }

        void Push()
        {
            history.Add(new Snapshot
            {
                Cats = (bool[])cats.Clone(),
                WrongCats = (bool[])wrongCats.Clone(),
                Marks = (bool[])marks.Clone(),
                WrongMarks = (bool[])wrongMarks.Clone(),
                Moves = Moves,
                Mistakes = Mistakes,
                Hearts = Hearts,
                HintsUsed = HintsUsed,
                Cell = -1
            });
        }

        static ActionResult Reject(string message)
        {
            return new ActionResult
            {
                Applied = false,
                Message = message,
                HighlightCell = -1
            };
        }

        sealed class Snapshot
        {
            public bool[] Cats;
            public bool[] WrongCats;
            public bool[] Marks;
            public bool[] WrongMarks;
            public int Moves;
            public int Mistakes;
            public int Hearts;
            public int HintsUsed;
            public int Cell;
        }
    }
}
