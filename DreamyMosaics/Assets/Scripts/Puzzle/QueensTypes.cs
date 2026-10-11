using System;

namespace DreamyMosaics.Puzzle
{
    public enum DeductionKind
    {
        Single = 1,
        Pointing = 2,
        Contradiction = 3
    }

    [Serializable]
    public sealed class TutorialStep
    {
        public string text;
        public string waitFor;
        public int cell;
    }

    [Serializable]
    public sealed class LevelData
    {
        public string id;
        public string name;
        public int size;
        public int[] regions;
        public int[] givenCats;
        public int[] givenMarks;
        public int tutorial;
        public int hearts;
        public int hintLimit;
        public string hardestTechnique;
        public int contradictionSteps;
        public int pointingSteps;
        public string band;
        public int verifiedSolutionCount;
        public int seed;
        public string intro;
        public TutorialStep[] steps;
    }

    [Serializable]
    public sealed class CampaignFile
    {
        public LevelData[] levels;
    }

    public sealed class Deduction
    {
        public DeductionKind Kind;
        public int Cell;
        public bool PlaceCat;
        public string Explanation;
    }

    public sealed class Analysis
    {
        public int SolutionCount;
        public bool LogicSolved;
        public string HardestTechnique;
        public bool[] Solution;
        public Deduction[] Steps;
        public string Error;
    }

    public struct ActionResult
    {
        public bool Applied;
        public bool Won;
        public bool Lost;
        public bool Mistake;
        public bool Refunded;
        public string Message;
        public int HighlightCell;
        public bool HighlightPlace;
    }
}
