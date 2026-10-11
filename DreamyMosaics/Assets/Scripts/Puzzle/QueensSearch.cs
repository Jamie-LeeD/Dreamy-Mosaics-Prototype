using System;
using System.Collections.Generic;

namespace DreamyMosaics.Puzzle
{
    public static class QueensSearch
    {
        public const int DefaultNodeCap = 750000;

        public static Analysis Analyze(LevelData level, int nodeCap)
        {
            Analysis analysis = new Analysis();
            string structural = QueensRules.StructuralProblems(level);
            if (structural != null)
            {
                analysis.Error = structural;
                analysis.SolutionCount = 0;
                analysis.HardestTechnique = "None";
                return analysis;
            }

            int n = level.size;
            int cells = n * n;
            bool[] givenCats = new bool[cells];
            bool[] givenMarks = new bool[cells];
            CopyCells(level.givenCats, givenCats);
            CopyCells(level.givenMarks, givenMarks);

            int nodes = 0;
            int count = 0;
            bool[] solution = null;
            bool capped = false;
            bool[] colUsed = new bool[n];
            bool[] regionUsed = new bool[n];
            bool[] placed = new bool[cells];

            for (int i = 0; i < cells; i++)
            {
                if (!givenCats[i])
                    continue;
                if (!CanPlace(n, level.regions, givenMarks, colUsed, regionUsed, placed, QueensRules.RowOf(n, i), QueensRules.ColOf(n, i)))
                {
                    analysis.SolutionCount = 0;
                    analysis.Error = "The given cats already break a rule.";
                    analysis.HardestTechnique = "None";
                    return analysis;
                }

                Place(n, level.regions, colUsed, regionUsed, placed, QueensRules.RowOf(n, i), QueensRules.ColOf(n, i));
            }

            Search(n, level.regions, givenCats, givenMarks, colUsed, regionUsed, placed, 0, 2, nodeCap, ref nodes, ref count, ref solution, ref capped);

            if (capped && count < 2)
            {
                analysis.SolutionCount = -1;
                analysis.Error = "The solution search stopped at the node cap, so uniqueness was not verified.";
                analysis.HardestTechnique = "None";
                return analysis;
            }

            analysis.SolutionCount = count;
            analysis.Solution = solution;
            if (count != 1)
            {
                analysis.HardestTechnique = "None";
                analysis.Error = "Solution count is " + count + ".";
                return analysis;
            }

            List<Deduction> steps = new List<Deduction>();
            int rank;
            bool solved = TryLogicSolve(n, level.regions, givenCats, givenMarks, solution, steps, out rank);
            analysis.LogicSolved = solved;
            analysis.HardestTechnique = QueensRules.TechniqueName(rank);
            analysis.Steps = steps.ToArray();
            if (!solved)
                analysis.Error = "The documented techniques did not finish this puzzle.";
            return analysis;
        }

        public static bool TryNextDeduction(int n, int[] regions, bool[] cats, bool[] removed, out Deduction step)
        {
            step = null;
            bool[] open = BuildOpen(n, regions, cats, removed, out string conflict);
            if (conflict != null || open == null)
                return false;

            if (UnitIsDead(n, regions, cats, open))
                return false;

            int singleCell;
            string singleReason;
            if (FindSingle(n, regions, cats, open, out singleCell, out singleReason))
            {
                step = new Deduction
                {
                    Kind = DeductionKind.Single,
                    Cell = singleCell,
                    PlaceCat = true,
                    Explanation = singleReason
                };
                return true;
            }

            int pointedCell;
            string pointedReason;
            if (FindPointing(n, regions, cats, open, out pointedCell, out pointedReason))
            {
                step = new Deduction
                {
                    Kind = DeductionKind.Pointing,
                    Cell = pointedCell,
                    PlaceCat = false,
                    Explanation = pointedReason
                };
                return true;
            }

            int contradictedCell;
            string contradictedReason;
            if (FindContradiction(n, regions, cats, removed, open, out contradictedCell, out contradictedReason))
            {
                step = new Deduction
                {
                    Kind = DeductionKind.Contradiction,
                    Cell = contradictedCell,
                    PlaceCat = false,
                    Explanation = contradictedReason
                };
                return true;
            }

            return false;
        }

        static bool TryLogicSolve(int n, int[] regions, bool[] givenCats, bool[] givenMarks, bool[] expected, List<Deduction> steps, out int rank)
        {
            rank = 0;
            int cells = n * n;
            bool[] cats = (bool[])givenCats.Clone();
            bool[] removed = (bool[])givenMarks.Clone();
            int guard = cells + 2;
            while (QueensRules.CountTrue(cats) < n && guard-- > 0)
            {
                Deduction step;
                if (!TryNextDeduction(n, regions, cats, removed, out step))
                    return false;
                steps.Add(step);
                if ((int)step.Kind > rank)
                    rank = (int)step.Kind;
                if (step.PlaceCat)
                {
                    if (cats[step.Cell] || removed[step.Cell])
                        return false;
                    cats[step.Cell] = true;
                }
                else
                {
                    if (cats[step.Cell])
                        return false;
                    removed[step.Cell] = true;
                }
            }

            if (QueensRules.CountTrue(cats) != n)
                return false;
            if (QueensRules.CompletionError(n, regions, cats, false) != null)
                return false;
            for (int i = 0; i < cells; i++)
            {
                if (cats[i] != expected[i])
                    return false;
            }

            if (rank == 0)
                rank = (int)DeductionKind.Single;
            return true;
        }

        static void Search(
            int n,
            int[] regions,
            bool[] givenCats,
            bool[] givenMarks,
            bool[] colUsed,
            bool[] regionUsed,
            bool[] placed,
            int row,
            int limit,
            int nodeCap,
            ref int nodes,
            ref int count,
            ref bool[] solution,
            ref bool capped)
        {
            if (count >= limit || capped)
                return;
            nodes++;
            if (nodes > nodeCap)
            {
                capped = true;
                return;
            }

            if (row == n)
            {
                count++;
                if (count == 1)
                    solution = (bool[])placed.Clone();
                return;
            }

            int givenCol = -1;
            for (int col = 0; col < n; col++)
            {
                if (givenCats[QueensRules.Index(n, row, col)])
                    givenCol = col;
            }

            if (givenCol >= 0)
            {
                Search(n, regions, givenCats, givenMarks, colUsed, regionUsed, placed, row + 1, limit, nodeCap, ref nodes, ref count, ref solution, ref capped);
                return;
            }

            for (int col = 0; col < n; col++)
            {
                if (!CanPlace(n, regions, givenMarks, colUsed, regionUsed, placed, row, col))
                    continue;
                Place(n, regions, colUsed, regionUsed, placed, row, col);
                Search(n, regions, givenCats, givenMarks, colUsed, regionUsed, placed, row + 1, limit, nodeCap, ref nodes, ref count, ref solution, ref capped);
                Unplace(n, regions, colUsed, regionUsed, placed, row, col);
                if (count >= limit || capped)
                    return;
            }
        }

        static bool CanPlace(int n, int[] regions, bool[] givenMarks, bool[] colUsed, bool[] regionUsed, bool[] placed, int row, int col)
        {
            int cell = QueensRules.Index(n, row, col);
            if (givenMarks[cell] || colUsed[col] || regionUsed[regions[cell]])
                return false;

            for (int i = 0; i < placed.Length; i++)
            {
                if (!placed[i])
                    continue;
                int dr = Math.Abs(QueensRules.RowOf(n, i) - row);
                int dc = Math.Abs(QueensRules.ColOf(n, i) - col);
                if (dr <= 1 && dc <= 1)
                    return false;
            }

            return true;
        }

        static void Place(int n, int[] regions, bool[] colUsed, bool[] regionUsed, bool[] placed, int row, int col)
        {
            int cell = QueensRules.Index(n, row, col);
            placed[cell] = true;
            colUsed[col] = true;
            regionUsed[regions[cell]] = true;
        }

        static void Unplace(int n, int[] regions, bool[] colUsed, bool[] regionUsed, bool[] placed, int row, int col)
        {
            int cell = QueensRules.Index(n, row, col);
            placed[cell] = false;
            colUsed[col] = false;
            regionUsed[regions[cell]] = false;
        }

        static bool[] BuildOpen(int n, int[] regions, bool[] cats, bool[] removed, out string conflict)
        {
            conflict = null;
            int cells = n * n;
            bool[] open = new bool[cells];
            for (int i = 0; i < cells; i++)
                open[i] = (removed == null || !removed[i]) && (cats == null || !cats[i]);

            if (cats == null)
                return open;

            string partial = QueensRules.PartialConflict(n, regions, cats);
            if (partial != null)
            {
                conflict = partial;
                return null;
            }

            for (int i = 0; i < cells; i++)
            {
                if (!cats[i])
                    continue;
                if (removed != null && removed[i])
                {
                    conflict = "A cat sits on a cell that was removed.";
                    return null;
                }

                int row = QueensRules.RowOf(n, i);
                int col = QueensRules.ColOf(n, i);
                for (int j = 0; j < cells; j++)
                {
                    if (j == i)
                        continue;
                    int otherRow = QueensRules.RowOf(n, j);
                    int otherCol = QueensRules.ColOf(n, j);
                    bool hits = otherRow == row || otherCol == col || regions[j] == regions[i]
                        || (Math.Abs(otherRow - row) <= 1 && Math.Abs(otherCol - col) <= 1);
                    if (hits)
                        open[j] = false;
                }
            }

            return open;
        }

        static bool UnitIsDead(int n, int[] regions, bool[] cats, bool[] open)
        {
            return FirstEmptyUnit(n, regions, cats, open) != null;
        }

        static string FirstEmptyUnit(int n, int[] regions, bool[] cats, bool[] open)
        {
            for (int row = 0; row < n; row++)
            {
                if (UnitHasCat(n, cats, row, -1, -1) )
                    continue;
                if (CountOpenInRow(n, open, row) == 0)
                    return "row " + (row + 1);
            }

            for (int col = 0; col < n; col++)
            {
                if (UnitHasCat(n, cats, -1, col, -1))
                    continue;
                if (CountOpenInCol(n, open, col) == 0)
                    return "column " + (col + 1);
            }

            for (int region = 0; region < n; region++)
            {
                if (UnitHasCat(n, cats, -1, -1, region, regions))
                    continue;
                if (CountOpenInRegion(n, regions, open, region) == 0)
                    return "the " + QueensRules.RegionName(region) + " region";
            }

            return null;
        }

        static bool FindSingle(int n, int[] regions, bool[] cats, bool[] open, out int cell, out string reason)
        {
            cell = -1;
            reason = null;
            for (int row = 0; row < n; row++)
            {
                if (UnitHasCat(n, cats, row, -1, -1))
                    continue;
                int found = OnlyOpen(n, open, row, -1, -1, regions);
                if (found >= 0)
                {
                    cell = found;
                    reason = "Row " + (row + 1) + " has only one cell that can still hold a memory: column " + (QueensRules.ColOf(n, found) + 1) + ".";
                    return true;
                }
            }

            for (int col = 0; col < n; col++)
            {
                if (UnitHasCat(n, cats, -1, col, -1))
                    continue;
                int found = OnlyOpen(n, open, -1, col, -1, regions);
                if (found >= 0)
                {
                    cell = found;
                    reason = "Column " + (col + 1) + " has only one cell that can still hold a memory: row " + (QueensRules.RowOf(n, found) + 1) + ".";
                    return true;
                }
            }

            for (int region = 0; region < n; region++)
            {
                if (UnitHasCat(n, cats, -1, -1, region, regions))
                    continue;
                int found = OnlyOpen(n, open, -1, -1, region, regions);
                if (found >= 0)
                {
                    cell = found;
                    reason = "The " + QueensRules.RegionName(region) + " region has only one cell that can still hold a memory: row "
                        + (QueensRules.RowOf(n, found) + 1) + ", column " + (QueensRules.ColOf(n, found) + 1) + ".";
                    return true;
                }
            }

            return false;
        }

        static bool FindPointing(int n, int[] regions, bool[] cats, bool[] open, out int cell, out string reason)
        {
            cell = -1;
            reason = null;
            for (int region = 0; region < n; region++)
            {
                if (UnitHasCat(n, cats, -1, -1, region, regions))
                    continue;
                int row = SharedLine(n, regions, open, region, true);
                if (row >= 0)
                {
                    for (int col = 0; col < n; col++)
                    {
                        int candidate = QueensRules.Index(n, row, col);
                        if (open[candidate] && regions[candidate] != region)
                        {
                            cell = candidate;
                            reason = "Every open cell in the " + QueensRules.RegionName(region) + " region is in row " + (row + 1)
                                + ", so row " + (row + 1) + " column " + (col + 1) + " cannot hold a memory. Mark it with an X.";
                            return true;
                        }
                    }
                }

                int colLine = SharedLine(n, regions, open, region, false);
                if (colLine >= 0)
                {
                    for (int rowIndex = 0; rowIndex < n; rowIndex++)
                    {
                        int candidate = QueensRules.Index(n, rowIndex, colLine);
                        if (open[candidate] && regions[candidate] != region)
                        {
                            cell = candidate;
                            reason = "Every open cell in the " + QueensRules.RegionName(region) + " region is in column " + (colLine + 1)
                                + ", so row " + (rowIndex + 1) + " column " + (colLine + 1) + " cannot hold a memory. Mark it with an X.";
                            return true;
                        }
                    }
                }
            }

            for (int row = 0; row < n; row++)
            {
                if (UnitHasCat(n, cats, row, -1, -1))
                    continue;
                int region = SharedRegionInRow(n, regions, open, row);
                if (region < 0)
                    continue;
                for (int i = 0; i < n * n; i++)
                {
                    if (open[i] && regions[i] == region && QueensRules.RowOf(n, i) != row)
                    {
                        cell = i;
                        reason = "Every open cell in row " + (row + 1) + " is in the " + QueensRules.RegionName(region)
                            + " region, so row " + (QueensRules.RowOf(n, i) + 1) + " column " + (QueensRules.ColOf(n, i) + 1)
                            + " cannot hold that region's memory. Mark it with an X.";
                        return true;
                    }
                }
            }

            for (int col = 0; col < n; col++)
            {
                if (UnitHasCat(n, cats, -1, col, -1))
                    continue;
                int region = SharedRegionInCol(n, regions, open, col);
                if (region < 0)
                    continue;
                for (int i = 0; i < n * n; i++)
                {
                    if (open[i] && regions[i] == region && QueensRules.ColOf(n, i) != col)
                    {
                        cell = i;
                        reason = "Every open cell in column " + (col + 1) + " is in the " + QueensRules.RegionName(region)
                            + " region, so row " + (QueensRules.RowOf(n, i) + 1) + " column " + (QueensRules.ColOf(n, i) + 1)
                            + " cannot hold that region's memory. Mark it with an X.";
                        return true;
                    }
                }
            }

            return false;
        }

        static bool FindContradiction(int n, int[] regions, bool[] cats, bool[] removed, bool[] open, out int cell, out string reason)
        {
            cell = -1;
            reason = null;
            int cells = n * n;
            for (int i = 0; i < cells; i++)
            {
                if (!open[i])
                    continue;
                bool[] trialCats = (bool[])cats.Clone();
                bool[] trialRemoved = removed == null ? new bool[cells] : (bool[])removed.Clone();
                trialCats[i] = true;
                string failure;
                if (!ForcedStable(n, regions, trialCats, trialRemoved, out failure))
                {
                    cell = i;
                    reason = "If row " + (QueensRules.RowOf(n, i) + 1) + ", column " + (QueensRules.ColOf(n, i) + 1)
                        + " held a memory, " + failure + " That cell can be marked with an X.";
                    return true;
                }
            }

            return false;
        }

        static bool ForcedStable(int n, int[] regions, bool[] cats, bool[] removed, out string failure)
        {
            failure = null;
            int guard = n * n;
            while (guard-- > 0)
            {
                bool[] open = BuildOpen(n, regions, cats, removed, out failure);
                if (open == null)
                    return false;
                string dead = FirstEmptyUnit(n, regions, cats, open);
                if (dead != null)
                {
                    failure = dead + " would have nowhere left for a memory.";
                    return false;
                }

                bool placedOne = false;
                int cell;
                string ignored;
                while (FindSingle(n, regions, cats, open, out cell, out ignored))
                {
                    cats[cell] = true;
                    placedOne = true;
                    open = BuildOpen(n, regions, cats, removed, out failure);
                    if (open == null)
                        return false;
                    dead = FirstEmptyUnit(n, regions, cats, open);
                    if (dead != null)
                    {
                        failure = dead + " would have nowhere left for a memory.";
                        return false;
                    }
                }

                if (!placedOne)
                    return true;
            }

            failure = "the forced placements did not settle.";
            return false;
        }

        static int OnlyOpen(int n, bool[] open, int row, int col, int region, int[] regions)
        {
            int found = -1;
            int count = 0;
            for (int i = 0; i < open.Length; i++)
            {
                if (!open[i] || !InUnit(n, regions, i, row, col, region))
                    continue;
                count++;
                found = i;
                if (count > 1)
                    return -1;
            }

            return count == 1 ? found : -1;
        }

        static int SharedLine(int n, int[] regions, bool[] open, int region, bool byRow)
        {
            int line = -1;
            bool any = false;
            for (int i = 0; i < open.Length; i++)
            {
                if (!open[i] || regions[i] != region)
                    continue;
                int value = byRow ? QueensRules.RowOf(n, i) : QueensRules.ColOf(n, i);
                if (!any)
                {
                    line = value;
                    any = true;
                }
                else if (line != value)
                {
                    return -1;
                }
            }

            return any ? line : -1;
        }

        static int SharedRegionInRow(int n, int[] regions, bool[] open, int row)
        {
            int region = -1;
            bool any = false;
            for (int col = 0; col < n; col++)
            {
                int cell = QueensRules.Index(n, row, col);
                if (!open[cell])
                    continue;
                if (!any)
                {
                    region = regions[cell];
                    any = true;
                }
                else if (region != regions[cell])
                {
                    return -1;
                }
            }

            return any ? region : -1;
        }

        static int SharedRegionInCol(int n, int[] regions, bool[] open, int col)
        {
            int region = -1;
            bool any = false;
            for (int row = 0; row < n; row++)
            {
                int cell = QueensRules.Index(n, row, col);
                if (!open[cell])
                    continue;
                if (!any)
                {
                    region = regions[cell];
                    any = true;
                }
                else if (region != regions[cell])
                {
                    return -1;
                }
            }

            return any ? region : -1;
        }

        static bool InUnit(int n, int[] regions, int cell, int row, int col, int region)
        {
            if (row >= 0 && QueensRules.RowOf(n, cell) != row)
                return false;
            if (col >= 0 && QueensRules.ColOf(n, cell) != col)
                return false;
            if (region >= 0 && regions[cell] != region)
                return false;
            return true;
        }

        static bool UnitHasCat(int n, bool[] cats, int row, int col, int region, int[] regions = null)
        {
            if (cats == null)
                return false;
            for (int i = 0; i < cats.Length; i++)
            {
                if (!cats[i])
                    continue;
                if (row >= 0 && QueensRules.RowOf(n, i) != row)
                    continue;
                if (col >= 0 && QueensRules.ColOf(n, i) != col)
                    continue;
                if (region >= 0 && (regions == null || regions[i] != region))
                    continue;
                if (row < 0 && col < 0 && region < 0)
                    continue;
                return true;
            }

            return false;
        }

        static int CountOpenInRow(int n, bool[] open, int row)
        {
            int count = 0;
            for (int col = 0; col < n; col++)
            {
                if (open[QueensRules.Index(n, row, col)])
                    count++;
            }

            return count;
        }

        static int CountOpenInCol(int n, bool[] open, int col)
        {
            int count = 0;
            for (int row = 0; row < n; row++)
            {
                if (open[QueensRules.Index(n, row, col)])
                    count++;
            }

            return count;
        }

        static int CountOpenInRegion(int n, int[] regions, bool[] open, int region)
        {
            int count = 0;
            for (int i = 0; i < open.Length; i++)
            {
                if (open[i] && regions[i] == region)
                    count++;
            }

            return count;
        }

        static void CopyCells(int[] source, bool[] target)
        {
            if (source == null)
                return;
            for (int i = 0; i < source.Length; i++)
                target[source[i]] = true;
        }
    }
}
