using System.Collections.Generic;
using CharacterMatch3.Board;
using UnityEditor;
using UnityEngine;

namespace CharacterMatch3.Editor
{
    public sealed class LevelValidationReport
    {
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public float DifficultyEstimate;

        public bool IsValid => Errors.Count == 0;

        public void Log(LevelDefinition level)
        {
            var prefix = level != null ? $"Level {level.levelNumber:000}" : "Level";
            foreach (var error in Errors)
            {
                Debug.LogError($"{prefix}: {error}", level);
            }

            foreach (var warning in Warnings)
            {
                Debug.LogWarning($"{prefix}: {warning}", level);
            }

            if (IsValid)
            {
                Debug.Log($"{prefix}: validation passed. Difficulty estimate {DifficultyEstimate:0.0}.", level);
            }
        }
    }

    public static class LevelValidator
    {
        public static LevelValidationReport Validate(LevelDefinition level)
        {
            var report = new LevelValidationReport();
            if (level == null)
            {
                report.Errors.Add("Missing LevelDefinition.");
                return report;
            }

            level.EnsureDefaults();
            if (level.moveLimit <= 0)
            {
                report.Errors.Add("Move limit must be greater than zero.");
            }

            if (level.goals.Count > 3)
            {
                report.Errors.Add("Levels should contain at most three simultaneous goals.");
            }

            ValidateCoordinates(level, level.softCoverPlacements, "Soft Cover", report);
            ValidateCoordinates(level, level.cratePlacements, "Crate", report);
            ValidateCoordinates(level, level.lockPlacements, "Character Lock", report);
            ValidateCoordinates(level, level.companionTokenStartingPositions, "Companion Token", report);
            ValidateCoordinates(level, level.companionExitCells, "Companion Exit", report);
            ValidatePlacements(level, report);
            ValidateGoals(level, report);
            ValidateCompanionPaths(level, report);

            try
            {
                var model = new BoardGenerator(level).Generate();
                if (MatchFinder.HasAnyMatch(model))
                {
                    report.Errors.Add("Generated board has accidental initial matches.");
                }

                if (!MoveFinder.HasLegalMove(model))
                {
                    report.Errors.Add("Generated board has no legal moves at start.");
                }
            }
            catch (System.Exception exception)
            {
                report.Errors.Add($"Board generation failed: {exception.Message}");
            }

            report.DifficultyEstimate = EstimateDifficulty(level);
            if (report.DifficultyEstimate > 92f)
            {
                report.Warnings.Add("Difficulty estimate is very high; inspect for excessive randomness or low move count.");
            }
            else if (report.DifficultyEstimate < 18f && level.levelNumber > 10)
            {
                report.Warnings.Add("Difficulty estimate is low for this part of the chapter.");
            }

            return report;
        }

        public static float EstimateDifficulty(LevelDefinition level)
        {
            var active = Mathf.Max(1, level.ActiveCellCount());
            var blockerLayers = SumLayers(level.softCoverPlacements) + SumLayers(level.cratePlacements) * 1.4f + SumLayers(level.lockPlacements) * 1.1f;
            var characterPressure = Mathf.Max(0, level.availableCharacterTypes.Count - 3) * 5f;
            var movePressure = Mathf.Clamp01((active * 0.55f + blockerLayers - level.moveLimit) / Mathf.Max(1f, active)) * 35f;
            var goalPressure = 0f;
            foreach (var goal in level.goals)
            {
                goalPressure += goal.goalType switch
                {
                    GoalType.CollectCharacter => goal.amount * 0.25f,
                    GoalType.ClearSoftCover => goal.amount * 0.35f,
                    GoalType.BreakCrates => goal.amount * 0.55f,
                    GoalType.DropCompanions => goal.amount * 8f,
                    GoalType.ReachScore => goal.amount / 1200f,
                    _ => 2f
                };
            }

            var fragmentation = EstimateFragmentation(level) * 6f;
            var companionPressure = level.companionTokenStartingPositions.Count * 6f + AverageCompanionPathLength(level) * 0.4f;
            var specialOpportunityRelief = (level.prePlacedSpecialPieces.Count + level.prePlacedNormalPieces.Count / 5f) * 2.5f;
            return Mathf.Clamp(18f + blockerLayers * 0.9f + characterPressure + movePressure + goalPressure + fragmentation + companionPressure - specialOpportunityRelief, 1f, 100f);
        }

        private static void ValidateCoordinates(LevelDefinition level, List<CellLayerData> placements, string label, LevelValidationReport report)
        {
            var seen = new HashSet<BoardCoordinate>();
            foreach (var placement in placements)
            {
                ValidateCoordinate(level, placement.coordinate, label, report);
                if (!seen.Add(placement.coordinate))
                {
                    report.Errors.Add($"{label} has duplicate cell {placement.coordinate}.");
                }

                if (placement.layers <= 0)
                {
                    report.Errors.Add($"{label} at {placement.coordinate} has non-positive layer count.");
                }
            }
        }

        private static void ValidateCoordinates(LevelDefinition level, List<BoardCoordinate> coordinates, string label, LevelValidationReport report)
        {
            var seen = new HashSet<BoardCoordinate>();
            foreach (var coordinate in coordinates)
            {
                ValidateCoordinate(level, coordinate, label, report);
                if (!seen.Add(coordinate))
                {
                    report.Errors.Add($"{label} has duplicate cell {coordinate}.");
                }
            }
        }

        private static void ValidateCoordinate(LevelDefinition level, BoardCoordinate coordinate, string label, LevelValidationReport report)
        {
            if (!level.IsInside(coordinate.x, coordinate.y))
            {
                report.Errors.Add($"{label} coordinate {coordinate} is outside the board.");
            }
            else if (!level.IsActive(coordinate.x, coordinate.y))
            {
                report.Errors.Add($"{label} coordinate {coordinate} is on an inactive cell.");
            }
        }

        private static void ValidatePlacements(LevelDefinition level, LevelValidationReport report)
        {
            var crates = new HashSet<BoardCoordinate>();
            foreach (var crate in level.cratePlacements)
            {
                crates.Add(crate.coordinate);
            }

            foreach (var placement in level.lockPlacements)
            {
                if (crates.Contains(placement.coordinate))
                {
                    report.Errors.Add($"Character Lock at {placement.coordinate} overlaps a Crate.");
                }
            }

            foreach (var placement in level.prePlacedNormalPieces)
            {
                ValidateCoordinate(level, placement.coordinate, "Pre-placed normal", report);
                if (crates.Contains(placement.coordinate))
                {
                    report.Errors.Add($"Pre-placed normal at {placement.coordinate} overlaps a Crate.");
                }
            }

            foreach (var placement in level.prePlacedSpecialPieces)
            {
                ValidateCoordinate(level, placement.coordinate, "Pre-placed special", report);
                if (placement.kind == PieceKind.Companion || placement.kind == PieceKind.Normal)
                {
                    report.Errors.Add($"Invalid special-piece placement at {placement.coordinate}: {placement.kind}.");
                }

                if (crates.Contains(placement.coordinate))
                {
                    report.Errors.Add($"Pre-placed special at {placement.coordinate} overlaps a Crate.");
                }
            }

            foreach (var companion in level.companionTokenStartingPositions)
            {
                if (crates.Contains(companion))
                {
                    report.Errors.Add($"Companion Token at {companion} overlaps a Crate.");
                }
            }
        }

        private static void ValidateGoals(LevelDefinition level, LevelValidationReport report)
        {
            var softLayers = SumLayers(level.softCoverPlacements);
            var crateLayers = SumLayers(level.cratePlacements);

            foreach (var goal in level.goals)
            {
                if (goal.goalType == GoalType.CollectCharacter && !level.availableCharacterTypes.Contains(goal.characterType))
                {
                    report.Errors.Add($"Goal requires unavailable character type {goal.characterType}.");
                }

                if (goal.goalType == GoalType.ClearSoftCover && goal.amount > softLayers)
                {
                    report.Errors.Add("Soft Cover goal requires more layers than exist on the board.");
                }

                if (goal.goalType == GoalType.BreakCrates && goal.amount > crateLayers)
                {
                    report.Errors.Add("Crate goal requires more layers than exist on the board.");
                }

                if (goal.goalType == GoalType.DropCompanions && goal.amount > level.companionTokenStartingPositions.Count)
                {
                    report.Errors.Add("DropCompanions goal requires more tokens than are placed.");
                }
            }
        }

        private static void ValidateCompanionPaths(LevelDefinition level, LevelValidationReport report)
        {
            foreach (var start in level.companionTokenStartingPositions)
            {
                if (!CanReachAnyExit(level, start))
                {
                    report.Errors.Add($"Companion Token at {start} has no valid downward path to an exit.");
                }
            }
        }

        private static bool CanReachAnyExit(LevelDefinition level, BoardCoordinate start)
        {
            var exits = new HashSet<BoardCoordinate>(level.companionExitCells);
            if (exits.Count == 0)
            {
                return false;
            }

            var crates = new HashSet<BoardCoordinate>();
            foreach (var crate in level.cratePlacements)
            {
                crates.Add(crate.coordinate);
            }

            var queue = new Queue<BoardCoordinate>();
            var visited = new HashSet<BoardCoordinate>();
            queue.Enqueue(start);
            visited.Add(start);

            var directions = new[]
            {
                new BoardCoordinate(0, -1),
                new BoardCoordinate(-1, 0),
                new BoardCoordinate(1, 0)
            };

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (exits.Contains(current))
                {
                    return true;
                }

                foreach (var direction in directions)
                {
                    var next = new BoardCoordinate(current.x + direction.x, current.y + direction.y);
                    if (!level.IsActive(next.x, next.y) || crates.Contains(next) || !visited.Add(next))
                    {
                        continue;
                    }

                    queue.Enqueue(next);
                }
            }

            return false;
        }

        private static float EstimateFragmentation(LevelDefinition level)
        {
            var inactive = 0;
            for (var i = 0; i < level.activeCells.Length; i++)
            {
                if (!level.activeCells[i])
                {
                    inactive++;
                }
            }

            return inactive / Mathf.Max(1f, level.activeCells.Length);
        }

        private static float AverageCompanionPathLength(LevelDefinition level)
        {
            if (level.companionTokenStartingPositions.Count == 0)
            {
                return 0f;
            }

            var total = 0f;
            foreach (var start in level.companionTokenStartingPositions)
            {
                total += start.y;
            }

            return total / level.companionTokenStartingPositions.Count;
        }

        private static int SumLayers(List<CellLayerData> placements)
        {
            var total = 0;
            foreach (var placement in placements)
            {
                total += Mathf.Max(0, placement.layers);
            }

            return total;
        }
    }
}
