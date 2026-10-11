using System;
using System.Text;

namespace DreamyMosaics.Puzzle
{
    public static class QueensRules
    {
        public static readonly string[] RegionNames =
        {
            "Rose", "Lilac", "Peach", "Sage", "Sky", "Gold", "Coral", "Mint", "Plum"
        };

        public static int CellCount(int size)
        {
            return size * size;
        }

        public static int RowOf(int size, int cell)
        {
            return cell / size;
        }

        public static int ColOf(int size, int cell)
        {
            return cell % size;
        }

        public static int Index(int size, int row, int col)
        {
            return row * size + col;
        }

        public static string RegionName(int region)
        {
            if (region >= 0 && region < RegionNames.Length)
                return RegionNames[region];
            return "Region " + (region + 1);
        }

        public static string StructuralProblems(LevelData level)
        {
            if (level == null)
                return "Level data is missing.";
            int n = level.size;
            if (n < 4 || n > 9)
                return "Board size must be from 4 to 9.";
            if (level.regions == null || level.regions.Length != n * n)
                return "Region map does not cover the board.";

            bool[] seen = new bool[n];
            for (int i = 0; i < level.regions.Length; i++)
            {
                int region = level.regions[i];
                if (region < 0 || region >= n)
                    return "A cell uses a region outside 0.." + (n - 1) + ".";
                seen[region] = true;
            }

            for (int i = 0; i < n; i++)
            {
                if (!seen[i])
                    return "Region " + i + " is missing from the board.";
            }

            bool[] cats = new bool[n * n];
            bool[] marks = new bool[n * n];
            string givenCatError = MarkFlags(level.givenCats, cats, n, "given cat");
            if (givenCatError != null)
                return givenCatError;
            string givenMarkError = MarkFlags(level.givenMarks, marks, n, "given mark");
            if (givenMarkError != null)
                return givenMarkError;

            for (int i = 0; i < cats.Length; i++)
            {
                if (cats[i] && marks[i])
                    return "A cell is both a given cat and a given empty mark.";
            }

            string conflict = CompletionError(n, level.regions, cats, true);
            if (conflict != null && CountTrue(cats) > 0)
            {
                // Givens may be a partial board. Only report conflicts between the givens themselves.
                string partial = PartialConflict(n, level.regions, cats);
                if (partial != null)
                    return partial;
            }

            return null;
        }

        public static string PartialConflict(int n, int[] regions, bool[] cats)
        {
            for (int i = 0; i < cats.Length; i++)
            {
                if (!cats[i])
                    continue;
                for (int j = i + 1; j < cats.Length; j++)
                {
                    if (!cats[j])
                        continue;
                    string pair = PairConflict(n, regions, i, j);
                    if (pair != null)
                        return pair;
                }
            }

            return null;
        }

        public static string PairConflict(int n, int[] regions, int a, int b)
        {
            int ar = RowOf(n, a);
            int ac = ColOf(n, a);
            int br = RowOf(n, b);
            int bc = ColOf(n, b);
            if (ar == br)
                return "Two memories share row " + (ar + 1) + ".";
            if (ac == bc)
                return "Two memories share column " + (ac + 1) + ".";
            if (regions[a] == regions[b])
                return "Two memories share the " + RegionName(regions[a]) + " region.";
            if (Math.Abs(ar - br) <= 1 && Math.Abs(ac - bc) <= 1)
                return "Memories in row " + (ar + 1) + " column " + (ac + 1) + " and row " + (br + 1) + " column " + (bc + 1) + " touch.";
            return null;
        }

        public static string CompletionError(int n, int[] regions, bool[] cats, bool allowPartial)
        {
            int placed = CountTrue(cats);
            if (!allowPartial && placed != n)
                return "The board has " + placed + " cats and needs " + n + ".";

            int[] rowCount = new int[n];
            int[] colCount = new int[n];
            int[] regionCount = new int[n];
            for (int i = 0; i < cats.Length; i++)
            {
                if (!cats[i])
                    continue;
                rowCount[RowOf(n, i)]++;
                colCount[ColOf(n, i)]++;
                regionCount[regions[i]]++;
            }

            for (int i = 0; i < n; i++)
            {
                if (rowCount[i] > 1)
                    return "Row " + (i + 1) + " has more than one memory.";
                if (colCount[i] > 1)
                    return "Column " + (i + 1) + " has more than one memory.";
                if (regionCount[i] > 1)
                    return "The " + RegionName(i) + " region has more than one memory.";
                if (!allowPartial)
                {
                    if (rowCount[i] != 1)
                        return "Row " + (i + 1) + " does not have a memory.";
                    if (colCount[i] != 1)
                        return "Column " + (i + 1) + " does not have a memory.";
                    if (regionCount[i] != 1)
                        return "The " + RegionName(i) + " region does not have a memory.";
                }
            }

            for (int i = 0; i < cats.Length; i++)
            {
                if (!cats[i])
                    continue;
                for (int j = i + 1; j < cats.Length; j++)
                {
                    if (!cats[j])
                        continue;
                    int dr = Math.Abs(RowOf(n, i) - RowOf(n, j));
                    int dc = Math.Abs(ColOf(n, i) - ColOf(n, j));
                    if (dr <= 1 && dc <= 1)
                        return PairConflict(n, regions, i, j);
                }
            }

            return null;
        }

        public static string PlacementBlockReason(int n, int[] regions, bool[] cats, bool[] blocked, int cell)
        {
            if (cell < 0 || cell >= n * n)
                return "That cell is outside the board.";
            if (cats != null && cats[cell])
                return "This cell already has a memory.";
            if (blocked != null && blocked[cell])
                return "This cell is given as empty.";

            int row = RowOf(n, cell);
            int col = ColOf(n, cell);
            if (cats == null)
                return null;

            for (int i = 0; i < cats.Length; i++)
            {
                if (!cats[i] || i == cell)
                    continue;
                int otherRow = RowOf(n, i);
                int otherCol = ColOf(n, i);
                if (otherRow == row)
                    return "Row " + (row + 1) + " already has a memory in column " + (otherCol + 1) + ".";
                if (otherCol == col)
                    return "Column " + (col + 1) + " already has a memory in row " + (otherRow + 1) + ".";
                if (regions[i] == regions[cell])
                    return "The " + RegionName(regions[cell]) + " region already has a memory in row " + (otherRow + 1) + ", column " + (otherCol + 1) + ".";
                if (Math.Abs(otherRow - row) <= 1 && Math.Abs(otherCol - col) <= 1)
                    return "This cell touches the memory in row " + (otherRow + 1) + ", column " + (otherCol + 1) + ". Memories cannot touch, even diagonally.";
            }

            return null;
        }

        public static int CountTrue(bool[] flags)
        {
            int count = 0;
            if (flags == null)
                return 0;
            for (int i = 0; i < flags.Length; i++)
            {
                if (flags[i])
                    count++;
            }

            return count;
        }

        public static string TechniqueName(int rank)
        {
            if (rank >= (int)DeductionKind.Contradiction)
                return "Contradiction";
            if (rank >= (int)DeductionKind.Pointing)
                return "Pointing";
            return "Single";
        }

        public static string DifficultyLabel(LevelData level)
        {
            if (!string.IsNullOrEmpty(level.band))
                return level.band;
            if (level.tutorial == 1)
                return "Tutorial";
            if (level.contradictionSteps <= 0 && level.pointingSteps <= 0)
                return "Easy";
            if (level.contradictionSteps <= 2)
                return "Medium";
            if (level.size >= 8 && level.contradictionSteps >= 6)
                return "Expert";
            return "Hard";
        }

        public static string FormatClock(float seconds)
        {
            if (seconds < 0f)
                seconds = 0f;
            int total = (int)seconds;
            int minutes = total / 60;
            int secs = total % 60;
            StringBuilder builder = new StringBuilder();
            if (minutes < 10)
                builder.Append('0');
            builder.Append(minutes);
            builder.Append(':');
            if (secs < 10)
                builder.Append('0');
            builder.Append(secs);
            return builder.ToString();
        }

        static string MarkFlags(int[] cells, bool[] flags, int n, string label)
        {
            if (cells == null)
                return null;
            for (int i = 0; i < cells.Length; i++)
            {
                int cell = cells[i];
                if (cell < 0 || cell >= n * n)
                    return "A " + label + " is outside the board.";
                if (flags[cell])
                    return "A " + label + " is listed twice.";
                flags[cell] = true;
            }

            return null;
        }
    }
}
