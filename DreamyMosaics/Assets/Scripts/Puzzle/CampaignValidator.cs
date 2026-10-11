using System;
using System.Text;

namespace DreamyMosaics.Puzzle
{
    public static class CampaignValidator
    {
        public static string ValidateLevel(LevelData level, int nodeCap)
        {
            string structural = QueensRules.StructuralProblems(level);
            if (structural != null)
                return structural;
            if (level.verifiedSolutionCount != 1)
                return level.id + " does not record a verified unique solution.";

            Analysis analysis = QueensSearch.Analyze(level, nodeCap);
            if (analysis.SolutionCount != 1)
                return level.id + " solution count is " + analysis.SolutionCount + ". " + analysis.Error;
            if (!analysis.LogicSolved)
                return level.id + " is not finished by the documented techniques. " + analysis.Error;
            if (analysis.HardestTechnique != level.hardestTechnique)
                return level.id + " records technique " + level.hardestTechnique + " but the solver found " + analysis.HardestTechnique + ".";
            int contradictions = 0;
            int pointings = 0;
            if (analysis.Steps != null)
            {
                for (int i = 0; i < analysis.Steps.Length; i++)
                {
                    if (analysis.Steps[i].Kind == DeductionKind.Contradiction)
                        contradictions++;
                    else if (analysis.Steps[i].Kind == DeductionKind.Pointing)
                        pointings++;
                }
            }

            if (level.contradictionSteps != contradictions || level.pointingSteps != pointings)
                return level.id + " records " + level.contradictionSteps + " contradiction steps and " + level.pointingSteps + " pointing steps, but the solver found " + contradictions + " and " + pointings + ".";

            string solved = SimulatePerfectSolve(level, analysis.Solution);
            if (solved != null)
                return solved;

            string tutorial = ValidateTutorial(level, analysis);
            if (tutorial != null)
                return tutorial;
            return null;
        }

        public static string SimulatePerfectSolve(LevelData level, bool[] solution)
        {
            PlaySession session;
            try
            {
                session = new PlaySession(level, solution);
            }
            catch (Exception exception)
            {
                return level.id + " could not start: " + exception.Message;
            }

            for (int i = 0; i < solution.Length; i++)
            {
                if (!solution[i] || session.Cats[i])
                    continue;
                ActionResult result = session.PlaceCat(i);
                if (!result.Applied || result.Mistake)
                    return level.id + " rejected a solution cat at " + i + ": " + result.Message;
            }

            if (!session.IsSolved())
                return level.id + " was not solved after placing the verified solution.";
            if (session.Mistakes != 0 || session.Hearts != session.MaxHearts)
                return level.id + " spent a heart on the verified solution.";
            return null;
        }

        public static string ValidateTutorial(LevelData level, Analysis analysis)
        {
            if (level.tutorial != 1)
                return null;
            if (level.steps == null || level.steps.Length < 2)
                return level.id + " is missing tutorial steps.";
            if (level.steps[0].waitFor != "continue")
                return level.id + " should open with a continue step.";
            if (level.steps[level.steps.Length - 1].waitFor != "complete")
                return level.id + " should end by asking the player to finish.";

            for (int i = 0; i < level.steps.Length; i++)
            {
                TutorialStep step = level.steps[i];
                if (string.IsNullOrEmpty(step.text))
                    return level.id + " has an empty tutorial step.";
                if (step.waitFor == "place")
                {
                    if (step.cell < 0 || step.cell >= analysis.Solution.Length || !analysis.Solution[step.cell])
                        return level.id + " asks for a cat outside the solution.";
                }
                else if (step.waitFor == "mark")
                {
                    if (step.cell < 0 || step.cell >= analysis.Solution.Length || analysis.Solution[step.cell])
                        return level.id + " asks for an X on a solution cell.";
                }
                else if (step.waitFor != "continue" && step.waitFor != "complete")
                {
                    return level.id + " has an unknown tutorial wait '" + step.waitFor + "'.";
                }
            }

            return null;
        }

        public static string ValidateCampaign(LevelData[] levels, int nodeCap)
        {
            if (levels == null || levels.Length == 0)
                return "The campaign has no levels.";

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < levels.Length; i++)
            {
                string error = ValidateLevel(levels[i], nodeCap);
                if (error != null)
                {
                    if (builder.Length > 0)
                        builder.Append('\n');
                    builder.Append(error);
                }
            }

            return builder.Length == 0 ? null : builder.ToString();
        }
    }
}
