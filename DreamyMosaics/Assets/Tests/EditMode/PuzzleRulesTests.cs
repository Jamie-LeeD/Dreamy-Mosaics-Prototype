using System.IO;
using DreamyMosaics.Puzzle;
using NUnit.Framework;
using UnityEngine;

public class PuzzleRulesTests
{
    [Test]
    public void Conflicts_AreExplained()
    {
        int size = 4;
        int[] regions = new int[16];
        for (int i = 0; i < regions.Length; i++)
            regions[i] = i / size;
        regions[10] = 0;
        bool[] cats = new bool[16];
        cats[0] = true;

        string row = QueensRules.PlacementBlockReason(size, regions, cats, null, 1);
        string column = QueensRules.PlacementBlockReason(size, regions, cats, null, 8);
        string touch = QueensRules.PlacementBlockReason(size, regions, cats, null, 5);
        string region = QueensRules.PlacementBlockReason(size, regions, cats, null, 10);

        Assert.That(row, Does.Contain("Row"));
        Assert.That(column, Does.Contain("Column"));
        Assert.That(touch, Does.Contain("touch"));
        Assert.That(region, Does.Contain("region"));
    }

    [Test]
    public void Campaign_HasOneVerifiedSolutionEach()
    {
        string path = Path.Combine(Application.dataPath, "Resources/Puzzles/campaign.json");
        Assert.That(File.Exists(path), Is.True, "campaign.json is missing.");
        CampaignFile file = JsonUtility.FromJson<CampaignFile>(File.ReadAllText(path));
        Assert.That(file.levels, Is.Not.Null);
        Assert.That(file.levels.Length, Is.EqualTo(15));
        string error = CampaignValidator.ValidateCampaign(file.levels, QueensSearch.DefaultNodeCap);
        Assert.That(error, Is.Null, error);
    }

    [Test]
    public void WrongCat_SpendsHeart_UndoAndEraseRestoreIt()
    {
        LevelData level = LoadLevel(2);
        Analysis analysis = QueensSearch.Analyze(level, QueensSearch.DefaultNodeCap);
        Assert.That(analysis.SolutionCount, Is.EqualTo(1));
        PlaySession session = new PlaySession(level, analysis.Solution);
        int wrong = FirstOpenWrongCell(session);
        int hearts = session.Hearts;

        ActionResult placed = session.PlaceCat(wrong);
        Assert.That(placed.Applied, Is.True);
        Assert.That(placed.Mistake, Is.True);
        Assert.That(session.Hearts, Is.EqualTo(hearts - 1));
        Assert.That(session.IsSolved(), Is.False);

        session.Undo();
        Assert.That(session.Hearts, Is.EqualTo(hearts));
        Assert.That(session.Cats[wrong], Is.False);

        int solutionCell = FirstUnplacedSolutionCell(session);
        ActionResult marked = session.ToggleMark(solutionCell);
        Assert.That(marked.Mistake, Is.True);
        Assert.That(session.Hearts, Is.EqualTo(hearts - 1));
        session.Erase(solutionCell);
        Assert.That(session.Hearts, Is.EqualTo(hearts));
        Assert.That(session.Marks[solutionCell], Is.False);
    }

    [Test]
    public void IllegalPlacement_DoesNotSpendAHeart()
    {
        LevelData level = LoadLevel(2);
        Analysis analysis = QueensSearch.Analyze(level, QueensSearch.DefaultNodeCap);
        PlaySession session = new PlaySession(level, analysis.Solution);
        int first = FirstUnplacedSolutionCell(session);
        ActionResult placed = session.PlaceCat(first);
        Assert.That(placed.Applied, Is.True);
        Assert.That(placed.Mistake, Is.False);
        int hearts = session.Hearts;

        int blocked = -1;
        for (int i = 0; i < session.Cats.Length; i++)
        {
            if (session.Cats[i])
                continue;
            if (QueensRules.PlacementBlockReason(level.size, level.regions, session.Cats, null, i) != null)
            {
                blocked = i;
                break;
            }
        }

        Assert.That(blocked, Is.GreaterThanOrEqualTo(0));
        ActionResult rejected = session.PlaceCat(blocked);
        Assert.That(rejected.Applied, Is.False);
        Assert.That(rejected.Message, Is.Not.Empty);
        Assert.That(session.Hearts, Is.EqualTo(hearts));
        Assert.That(session.Cats[blocked], Is.False);
    }

    [Test]
    public void Restart_ReturnsToTheOpeningBoard()
    {
        LevelData level = LoadLevel(0);
        Analysis analysis = QueensSearch.Analyze(level, QueensSearch.DefaultNodeCap);
        PlaySession session = new PlaySession(level, analysis.Solution);
        int cell = FirstUnplacedSolutionCell(session);
        session.PlaceCat(cell);
        session.Restart();
        Assert.That(session.Cats[cell], Is.False);
        Assert.That(session.Moves, Is.EqualTo(0));
        Assert.That(session.Hearts, Is.EqualTo(session.MaxHearts));
        Assert.That(session.CanUndo, Is.False);
    }

    static LevelData LoadLevel(int index)
    {
        string path = Path.Combine(Application.dataPath, "Resources/Puzzles/campaign.json");
        CampaignFile file = JsonUtility.FromJson<CampaignFile>(File.ReadAllText(path));
        return file.levels[index];
    }

    static int FirstOpenWrongCell(PlaySession session)
    {
        for (int i = 0; i < session.Solution.Length; i++)
        {
            if (session.Solution[i] || session.Cats[i])
                continue;
            if (QueensRules.PlacementBlockReason(session.Size, session.Level.regions, session.Cats, null, i) == null)
                return i;
        }

        Assert.Fail("No locally legal wrong cell.");
        return -1;
    }

    static int FirstUnplacedSolutionCell(PlaySession session)
    {
        for (int i = 0; i < session.Solution.Length; i++)
        {
            if (session.Solution[i] && !session.Cats[i])
                return i;
        }

        Assert.Fail("No unplaced solution cell.");
        return -1;
    }
}
