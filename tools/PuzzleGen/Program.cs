using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DreamyMosaics.Puzzle;

namespace PuzzleGen
{
    static class Program
    {
        const int NodeCap = QueensSearch.DefaultNodeCap;

        static readonly string[] Names =
        {
            "First Nap",
            "Sunny Windowsill",
            "Yarn Basket",
            "Quiet Corner",
            "Paw Prints",
            "Garden Wall",
            "Moonlight",
            "Knit Blanket",
            "Teacup",
            "Library Nook",
            "Rainy Roof",
            "Velvet Cushion",
            "Starlight",
            "Hidden Den",
            "Dreaming Crown"
        };

        static int Main()
        {
            string ruleError = RunRuleSelfCheck();
            if (ruleError != null)
            {
                Console.WriteLine("SELF-CHECK FAILED: " + ruleError);
                return 1;
            }

            Console.WriteLine("Self-check passed. Searching for puzzles...");
            List<LevelData> levels = BuildCampaign();
            if (levels == null || levels.Count != 15)
            {
                Console.WriteLine("Could not fill 15 verified levels. Found " + (levels == null ? 0 : levels.Count) + ".");
                return 1;
            }

            string campaignError = CampaignValidator.ValidateCampaign(levels.ToArray(), NodeCap);
            if (campaignError != null)
            {
                Console.WriteLine(campaignError);
                return 1;
            }

            string mistakeError = RunMistakeSelfCheck(levels[2]);
            if (mistakeError != null)
            {
                Console.WriteLine("MISTAKE CHECK FAILED: " + mistakeError);
                return 1;
            }

            string directory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "DreamyMosaics", "Assets", "Resources", "Puzzles"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "campaign.json");
            File.WriteAllText(path, WriteCampaign(levels), new UTF8Encoding(false));
            Console.WriteLine("Wrote " + path);
            for (int i = 0; i < levels.Count; i++)
            {
                LevelData level = levels[i];
                Console.WriteLine(
                    (i + 1).ToString("00") + " " + level.name + " " + level.size + "x" + level.size
                    + " " + QueensRules.DifficultyLabel(level) + " " + level.hardestTechnique
                    + (level.tutorial == 1 ? " tutorial" : ""));
            }

            return 0;
        }

        static string RunRuleSelfCheck()
        {
            int n = 4;
            int[] regions = new int[16];
            for (int i = 0; i < 16; i++)
                regions[i] = i / n;
            bool[] cats = new bool[16];
            cats[0] = true;
            cats[1] = true;
            string row = QueensRules.PairConflict(n, regions, 0, 1);
            if (row == null)
                return "Same-row cats were accepted.";
            string adjacent = QueensRules.PlacementBlockReason(n, regions, cats, null, 5);
            if (adjacent == null || adjacent.IndexOf("touch", StringComparison.Ordinal) < 0)
                return "Diagonal touch was accepted.";
            string column = QueensRules.PlacementBlockReason(n, regions, cats, null, 8);
            if (column == null || column.IndexOf("column", StringComparison.OrdinalIgnoreCase) < 0 && column.IndexOf("Column", StringComparison.Ordinal) < 0)
                return "Same-column placement was accepted. Message: " + column;
            return null;
        }

        static string RunMistakeSelfCheck(LevelData level)
        {
            Analysis analysis = QueensSearch.Analyze(level, NodeCap);
            PlaySession session = new PlaySession(level, analysis.Solution);
            int wrong = -1;
            for (int i = 0; i < analysis.Solution.Length; i++)
            {
                if (analysis.Solution[i])
                    continue;
                if (QueensRules.PlacementBlockReason(level.size, level.regions, session.Cats, null, i) == null)
                {
                    wrong = i;
                    break;
                }
            }

            if (wrong < 0)
                return "No locally legal wrong cell was available.";
            int hearts = session.Hearts;
            ActionResult placed = session.PlaceCat(wrong);
            if (!placed.Applied || !placed.Mistake || session.Hearts != hearts - 1)
                return "A wrong cat did not spend a heart.";
            ActionResult illegal = session.PlaceCat(wrong);
            if (illegal.Applied)
                return "A second cat was placed on the same cell.";
            session.Undo();
            if (session.Hearts != hearts || session.Cats[wrong])
                return "Undo did not restore the heart.";

            int solutionCell = -1;
            for (int i = 0; i < analysis.Solution.Length; i++)
            {
                if (analysis.Solution[i] && !session.Cats[i])
                {
                    solutionCell = i;
                    break;
                }
            }

            ActionResult marked = session.ToggleMark(solutionCell);
            if (!marked.Mistake || session.Hearts != hearts - 1)
                return "An X on the solution cat did not spend a heart.";
            session.Erase(solutionCell);
            if (session.Hearts != hearts || session.Marks[solutionCell])
                return "Erasing the wrong X did not restore the heart.";
            return null;
        }

        static List<LevelData> BuildCampaign()
        {
            Random rng = new Random(20261011);
            List<LevelData> easy = new List<LevelData>();
            List<LevelData> medium = new List<LevelData>();
            List<LevelData> hard = new List<LevelData>();
            List<LevelData> expert = new List<LevelData>();
            HashSet<string> seen = new HashSet<string>();
            int attempts = 0;
            int accepted = 0;
            int[] histogram = new int[4];
            int[] contraHist = new int[24];

            while (attempts < 80000 && (easy.Count < 4 || medium.Count < 4 || hard.Count < 4 || expert.Count < 3))
            {
                attempts++;
                int size = ChooseSize(rng, easy.Count, medium.Count, hard.Count, expert.Count);
                int[] columns = new int[size];
                if (!TryPlaceCats(size, rng, columns, new bool[size], 0))
                    continue;

                int style = rng.Next(3);
                int minimum = 2;
                int[] regions = GrowRegions(size, columns, rng, style, minimum);
                if (regions == null)
                    continue;
                string key = size + ":" + string.Join(",", regions);
                if (!seen.Add(key))
                    continue;

                LevelData probe = new LevelData();
                probe.id = "probe";
                probe.name = "probe";
                probe.size = size;
                probe.regions = regions;
                probe.givenCats = new int[0];
                probe.givenMarks = new int[0];
                probe.hearts = 1;
                probe.hintLimit = 3;
                probe.verifiedSolutionCount = 1;
                probe.hardestTechnique = "Single";
                probe.steps = new TutorialStep[0];

                Analysis analysis = QueensSearch.Analyze(probe, NodeCap);
                if (analysis.SolutionCount != 1 || !analysis.LogicSolved)
                    continue;

                accepted++;
                CountSteps(analysis, out probe.contradictionSteps, out probe.pointingSteps);
                int rank = RankOf(analysis.HardestTechnique);
                histogram[rank]++;
                int bucket = probe.contradictionSteps;
                if (bucket > 23)
                    bucket = 23;
                contraHist[bucket]++;
                if (accepted % 25 == 0)
                {
                    Console.WriteLine(
                        "attempts " + attempts + " kept " + accepted
                        + " E" + easy.Count + " M" + medium.Count
                        + " H" + hard.Count + " X" + expert.Count
                        + " hist S" + histogram[1] + " P" + histogram[2] + " C" + histogram[3]);
                }

                probe.hardestTechnique = analysis.HardestTechnique;
                probe.seed = attempts;
                if (analysis.Steps == null || analysis.Steps.Length == 0 || analysis.Steps[0].Kind == DeductionKind.Single)
                    continue;
                if (MinRegionSize(size, regions) < 2)
                    continue;

                if (easy.Count < 4 && (size == 5 || size == 6) && probe.contradictionSteps <= 2 && probe.pointingSteps >= 1)
                {
                    FinishLevel(probe, false, "Easy");
                    easy.Add(probe);
                    continue;
                }

                if (medium.Count < 4 && (size == 6 || size == 7) && probe.contradictionSteps >= 2 && probe.contradictionSteps <= 5)
                {
                    FinishLevel(probe, false, "Medium");
                    medium.Add(probe);
                    continue;
                }

                if (hard.Count < 4 && (size == 7 || size == 8) && probe.contradictionSteps >= 4)
                {
                    FinishLevel(probe, false, "Hard");
                    hard.Add(probe);
                    continue;
                }

                if (expert.Count < 3 && (size == 8 || size == 9) && probe.contradictionSteps >= 6)
                {
                    FinishLevel(probe, false, "Expert");
                    expert.Add(probe);
                }
            }

            Console.WriteLine("Search finished after " + attempts + " attempts. Unique logic puzzles seen: " + accepted + ".");
            Console.WriteLine("Histogram Single " + histogram[1] + " Pointing " + histogram[2] + " Contradiction " + histogram[3]);
            Console.Write("Contradiction steps:");
            for (int i = 0; i < contraHist.Length; i++)
            {
                if (contraHist[i] > 0)
                    Console.Write(" " + i + "=" + contraHist[i]);
            }

            Console.WriteLine();
            if (easy.Count < 4 || medium.Count < 4 || hard.Count < 4 || expert.Count < 3)
            {
                Console.WriteLine("Shortfall E" + easy.Count + " M" + medium.Count + " H" + hard.Count + " X" + expert.Count);
                return easy;
            }

            List<LevelData> all = new List<LevelData>();
            all.AddRange(easy);
            all.AddRange(medium);
            all.AddRange(hard);
            all.AddRange(expert);
            for (int i = 0; i < all.Count; i++)
            {
                all[i].id = "level-" + (i + 1).ToString("00");
                all[i].name = Names[i];
            }

            return all;
        }

        static int ChooseSize(Random rng, int easy, int medium, int hard, int expert)
        {
            if (easy < 4)
                return rng.Next(2) == 0 ? 5 : 6;
            if (medium < 4)
                return rng.Next(2) == 0 ? 6 : 7;
            if (hard < 4)
                return rng.Next(2) == 0 ? 7 : 8;
            return rng.Next(2) == 0 ? 8 : 9;
        }

        static void CountSteps(Analysis analysis, out int contradictions, out int pointings)
        {
            contradictions = 0;
            pointings = 0;
            if (analysis.Steps == null)
                return;
            for (int i = 0; i < analysis.Steps.Length; i++)
            {
                if (analysis.Steps[i].Kind == DeductionKind.Contradiction)
                    contradictions++;
                else if (analysis.Steps[i].Kind == DeductionKind.Pointing)
                    pointings++;
            }
        }

        static bool TryMakeTutorial(LevelData level, Analysis analysis, int index)
        {
            List<TutorialStep> steps = new List<TutorialStep>();
            steps.Add(new TutorialStep
            {
                text = index == 0
                    ? "Place one cat in every row, every column, and every coloured region. Cats cannot touch, even diagonally. A dimmed cell is no longer legal. Click Continue."
                    : "You can mark a cell with an X when you know it cannot hold a cat. An X is a note, and you can remove it. Click Continue.",
                waitFor = "continue",
                cell = -1
            });

            int guided = 0;
            for (int i = 0; i < analysis.Steps.Length && guided < 2; i++)
            {
                Deduction step = analysis.Steps[i];
                if (step.PlaceCat && guided == 0)
                {
                    steps.Add(new TutorialStep
                    {
                        text = step.Explanation + " Place the cat on the highlighted cell.",
                        waitFor = "place",
                        cell = step.Cell
                    });
                    guided++;
                }
                else if (!step.PlaceCat && guided == 1)
                {
                    steps.Add(new TutorialStep
                    {
                        text = step.Explanation + " The highlighted cell is the one to mark.",
                        waitFor = "mark",
                        cell = step.Cell
                    });
                    guided++;
                }
            }

            if (guided < 1)
                return false;

            steps.Add(new TutorialStep
            {
                text = "Finish the board with the same rules. Right-click marks an X. The buttons on the right do that too.",
                waitFor = "complete",
                cell = -1
            });

            level.steps = steps.ToArray();
            FinishLevel(level, true, "Tutorial");
            string error = CampaignValidator.ValidateLevel(level, NodeCap);
            return error == null;
        }

        static void FinishLevel(LevelData level, bool tutorial, string band)
        {
            level.tutorial = tutorial ? 1 : 0;
            level.hearts = tutorial ? 0 : 1;
            level.hintLimit = tutorial ? 6 : 3;
            level.band = band;
            if (!tutorial)
                level.steps = new TutorialStep[0];
            level.intro = tutorial
                ? "A short guided puzzle. Hearts are not spent here."
                : "Place one memory in every row, column, and region. Memories cannot touch.";
            level.verifiedSolutionCount = 1;
        }

        static int CountOfSize(List<LevelData> levels, int size)
        {
            int count = 0;
            for (int i = 0; i < levels.Count; i++)
            {
                if (levels[i].size == size)
                    count++;
            }

            return count;
        }

        static int MinRegionSize(int n, int[] regions)
        {
            int[] counts = new int[n];
            for (int i = 0; i < regions.Length; i++)
                counts[regions[i]]++;
            int min = n * n;
            for (int i = 0; i < n; i++)
            {
                if (counts[i] < min)
                    min = counts[i];
            }

            return min;
        }

        static int RankOf(string technique)
        {
            if (technique == "Contradiction")
                return 3;
            if (technique == "Pointing")
                return 2;
            if (technique == "Single")
                return 1;
            return 0;
        }

        static bool TryPlaceCats(int n, Random rng, int[] columns, bool[] used, int row)
        {
            if (row == n)
                return true;
            int[] order = Shuffled(n, rng);
            for (int i = 0; i < order.Length; i++)
            {
                int col = order[i];
                if (used[col])
                    continue;
                if (row > 0 && Math.Abs(columns[row - 1] - col) <= 1)
                    continue;
                used[col] = true;
                columns[row] = col;
                if (TryPlaceCats(n, rng, columns, used, row + 1))
                    return true;
                used[col] = false;
            }

            return false;
        }

        static int[] GrowRegions(int n, int[] columns, Random rng, int style, int minimumSize)
        {
            int[] regions = new int[n * n];
            for (int i = 0; i < regions.Length; i++)
                regions[i] = -1;
            for (int row = 0; row < n; row++)
                regions[row * n + columns[row]] = row;

            int filled = n;
            if (minimumSize >= 2)
            {
                for (int region = 0; region < n; region++)
                {
                    List<int> options = RegionOptions(n, regions, region);
                    if (options.Count == 0)
                        return null;
                    regions[ChooseGrowth(n, regions, columns, options, rng, style, region)] = region;
                    filled++;
                }
            }

            int guard = n * n * n;
            while (filled < n * n && guard-- > 0)
            {
                bool grew = false;
                for (int region = 0; region < n && filled < n * n; region++)
                {
                    List<int> options = RegionOptions(n, regions, region);
                    if (options.Count == 0)
                        continue;
                    int pick = ChooseGrowth(n, regions, columns, options, rng, style, region);
                    regions[pick] = region;
                    filled++;
                    grew = true;
                    if (style == 2)
                        break;
                }

                if (grew)
                    continue;

                List<int> any = new List<int>();
                List<int> owners = new List<int>();
                for (int region = 0; region < n; region++)
                {
                    List<int> options = RegionOptions(n, regions, region);
                    for (int i = 0; i < options.Count; i++)
                    {
                        any.Add(options[i]);
                        owners.Add(region);
                    }
                }

                if (any.Count == 0)
                    return null;
                int index = rng.Next(any.Count);
                regions[any[index]] = owners[index];
                filled++;
            }

            if (filled != n * n)
                return null;
            for (int i = 0; i < regions.Length; i++)
            {
                if (regions[i] < 0)
                    return null;
            }

            if (MinRegionSize(n, regions) < minimumSize)
                return null;
            return regions;
        }

        static List<int> RegionOptions(int n, int[] regions, int region)
        {
            List<int> options = new List<int>();
            for (int cell = 0; cell < regions.Length; cell++)
            {
                if (regions[cell] != region)
                    continue;
                int row = cell / n;
                int col = cell % n;
                TryAdd(options, regions, n, row - 1, col);
                TryAdd(options, regions, n, row + 1, col);
                TryAdd(options, regions, n, row, col - 1);
                TryAdd(options, regions, n, row, col + 1);
            }

            return options;
        }

        static void TryAdd(List<int> options, int[] regions, int n, int row, int col)
        {
            if (row < 0 || col < 0 || row >= n || col >= n)
                return;
            int cell = row * n + col;
            if (regions[cell] != -1)
                return;
            if (!options.Contains(cell))
                options.Add(cell);
        }

        static int ChooseGrowth(int n, int[] regions, int[] columns, List<int> options, Random rng, int style, int region)
        {
            if (style == 0)
            {
                int best = options[0];
                int bestDistance = int.MaxValue;
                int seed = region * n + columns[region];
                int seedRow = seed / n;
                int seedCol = seed % n;
                for (int i = 0; i < options.Count; i++)
                {
                    int cell = options[i];
                    int distance = Math.Abs(cell / n - seedRow) + Math.Abs(cell % n - seedCol);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = cell;
                    }
                }

                return best;
            }

            return options[rng.Next(options.Count)];
        }

        static int[] Shuffled(int n, Random rng)
        {
            int[] values = new int[n];
            for (int i = 0; i < n; i++)
                values[i] = i;
            for (int i = n - 1; i > 0; i--)
            {
                int swap = rng.Next(i + 1);
                int temp = values[i];
                values[i] = values[swap];
                values[swap] = temp;
            }

            return values;
        }

        static string WriteCampaign(List<LevelData> levels)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("{\"levels\":[");
            for (int i = 0; i < levels.Count; i++)
            {
                if (i > 0)
                    builder.Append(',');
                WriteLevel(builder, levels[i]);
            }

            builder.Append("]}");
            return builder.ToString();
        }

        static void WriteLevel(StringBuilder builder, LevelData level)
        {
            builder.Append("{\"id\":\"").Append(Esc(level.id));
            builder.Append("\",\"name\":\"").Append(Esc(level.name));
            builder.Append("\",\"size\":").Append(level.size);
            builder.Append(",\"regions\":").Append(ArrayText(level.regions));
            builder.Append(",\"givenCats\":").Append(ArrayText(level.givenCats));
            builder.Append(",\"givenMarks\":").Append(ArrayText(level.givenMarks));
            builder.Append(",\"tutorial\":").Append(level.tutorial);
            builder.Append(",\"hearts\":").Append(level.hearts);
            builder.Append(",\"hintLimit\":").Append(level.hintLimit);
            builder.Append(",\"hardestTechnique\":\"").Append(Esc(level.hardestTechnique));
            builder.Append("\",\"contradictionSteps\":").Append(level.contradictionSteps);
            builder.Append(",\"pointingSteps\":").Append(level.pointingSteps);
            builder.Append(",\"band\":\"").Append(Esc(level.band));
            builder.Append("\",\"verifiedSolutionCount\":").Append(level.verifiedSolutionCount);
            builder.Append(",\"seed\":").Append(level.seed);
            builder.Append(",\"intro\":\"").Append(Esc(level.intro));
            builder.Append("\",\"steps\":[");
            if (level.steps != null)
            {
                for (int i = 0; i < level.steps.Length; i++)
                {
                    if (i > 0)
                        builder.Append(',');
                    TutorialStep step = level.steps[i];
                    builder.Append("{\"text\":\"").Append(Esc(step.text));
                    builder.Append("\",\"waitFor\":\"").Append(Esc(step.waitFor));
                    builder.Append("\",\"cell\":").Append(step.cell).Append('}');
                }
            }

            builder.Append("]}");
        }

        static string ArrayText(int[] values)
        {
            if (values == null || values.Length == 0)
                return "[]";
            return "[" + string.Join(",", values) + "]";
        }

        static string Esc(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
        }
    }
}
