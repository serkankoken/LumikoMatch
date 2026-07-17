using System;
using System.Collections.Generic;
using UnityEngine;

namespace CharacterMatch3.Goals
{
    [Serializable]
    public sealed class ActiveGoal
    {
        public GoalType goalType;
        public CharacterType characterType;
        public int targetAmount;
        public int currentAmount;

        public int Remaining => Mathf.Max(0, targetAmount - currentAmount);
        public bool IsComplete => currentAmount >= targetAmount;

        public string DisplayName
        {
            get
            {
                return goalType switch
                {
                    GoalType.CollectCharacter => characterType.ToString(),
                    GoalType.ClearSoftCover => "Soft Cover",
                    GoalType.BreakCrates => "Crates",
                    GoalType.DropCompanions => "Companions",
                    GoalType.ReachScore => "Score",
                    _ => goalType.ToString()
                };
            }
        }
    }

    public sealed class GoalManager
    {
        private readonly List<ActiveGoal> activeGoals = new List<ActiveGoal>();

        public IReadOnlyList<ActiveGoal> ActiveGoals => activeGoals;
        public event Action GoalsChanged;

        public void Initialize(LevelDefinition level)
        {
            activeGoals.Clear();
            foreach (var data in level.goals)
            {
                var amount = ResolveGoalAmount(level, data);
                activeGoals.Add(new ActiveGoal
                {
                    goalType = data.goalType,
                    characterType = data.characterType,
                    targetAmount = Mathf.Max(1, amount),
                    currentAmount = 0
                });
            }

            GoalsChanged?.Invoke();
        }

        public void RecordPieceCollected(CharacterType characterType)
        {
            var changed = false;
            foreach (var goal in activeGoals)
            {
                if (goal.goalType == GoalType.CollectCharacter && goal.characterType == characterType && !goal.IsComplete)
                {
                    goal.currentAmount++;
                    changed = true;
                }
            }

            NotifyIfChanged(changed);
        }

        public void RecordSoftCoverCleared()
        {
            IncrementGoal(GoalType.ClearSoftCover, 1);
        }

        public void RecordCrateLayerBroken()
        {
            IncrementGoal(GoalType.BreakCrates, 1);
        }

        public void RecordCompanionDelivered()
        {
            IncrementGoal(GoalType.DropCompanions, 1);
        }

        public void RecordScore(int score)
        {
            var changed = false;
            foreach (var goal in activeGoals)
            {
                if (goal.goalType == GoalType.ReachScore)
                {
                    var newValue = Mathf.Min(score, goal.targetAmount);
                    if (goal.currentAmount != newValue)
                    {
                        goal.currentAmount = newValue;
                        changed = true;
                    }
                }
            }

            NotifyIfChanged(changed);
        }

        public bool AreAllGoalsComplete()
        {
            if (activeGoals.Count == 0)
            {
                return true;
            }

            foreach (var goal in activeGoals)
            {
                if (!goal.IsComplete)
                {
                    return false;
                }
            }

            return true;
        }

        private void IncrementGoal(GoalType goalType, int amount)
        {
            var changed = false;
            foreach (var goal in activeGoals)
            {
                if (goal.goalType == goalType && !goal.IsComplete)
                {
                    goal.currentAmount += amount;
                    changed = true;
                }
            }

            NotifyIfChanged(changed);
        }

        private void NotifyIfChanged(bool changed)
        {
            if (changed)
            {
                GoalsChanged?.Invoke();
            }
        }

        private static int ResolveGoalAmount(LevelDefinition level, LevelGoalData data)
        {
            if (data.amount > 0)
            {
                return data.amount;
            }

            return data.goalType switch
            {
                GoalType.ClearSoftCover => SumLayers(level.softCoverPlacements),
                GoalType.BreakCrates => SumLayers(level.cratePlacements),
                GoalType.DropCompanions => Mathf.Max(1, level.companionTokenStartingPositions.Count),
                GoalType.ReachScore => Mathf.Max(1000, level.oneStarScore),
                _ => Mathf.Max(1, data.amount)
            };
        }

        private static int SumLayers(List<CellLayerData> placements)
        {
            var total = 0;
            foreach (var placement in placements)
            {
                total += Mathf.Max(0, placement.layers);
            }

            return Mathf.Max(1, total);
        }
    }
}
