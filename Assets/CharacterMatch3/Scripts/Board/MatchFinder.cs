using System.Collections.Generic;
using System.Linq;

namespace CharacterMatch3.Board
{
    public sealed class MatchRun
    {
        public CharacterType Character;
        public LineOrientation Orientation;
        public readonly List<BoardCoordinate> Cells = new List<BoardCoordinate>();
    }

    public sealed class MatchGroup
    {
        public CharacterType Character;
        public readonly HashSet<BoardCoordinate> Cells = new HashSet<BoardCoordinate>();
        public readonly List<MatchRun> Runs = new List<MatchRun>();
        public bool HasHorizontalRun;
        public bool HasVerticalRun;
        public int LongestHorizontalRun;
        public int LongestVerticalRun;

        public int LongestRun => LongestHorizontalRun > LongestVerticalRun ? LongestHorizontalRun : LongestVerticalRun;
        public bool IsTOrL => HasHorizontalRun && HasVerticalRun && Cells.Count >= 5;

        public PieceKind CreatedSpecialKind
        {
            get
            {
                if (IsTOrL)
                {
                    return PieceKind.Burst;
                }

                if (LongestRun >= 5)
                {
                    return PieceKind.Rainbow;
                }

                if (LongestRun >= 4)
                {
                    return PieceKind.Line;
                }

                return PieceKind.Normal;
            }
        }

        public LineOrientation CreatedLineOrientation
        {
            get
            {
                if (LongestHorizontalRun >= 4 && LongestHorizontalRun >= LongestVerticalRun)
                {
                    return LineOrientation.Vertical;
                }

                return LineOrientation.Horizontal;
            }
        }

        public bool CreatesSpecial => CreatedSpecialKind != PieceKind.Normal;

        public BoardCoordinate GetSpecialCoordinate(BoardCoordinate preferred)
        {
            if (Cells.Contains(preferred))
            {
                return preferred;
            }

            if (IsTOrL)
            {
                var horizontalCells = new HashSet<BoardCoordinate>(
                    Runs.Where(run => run.Orientation == LineOrientation.Horizontal).SelectMany(run => run.Cells));
                foreach (var run in Runs.Where(run => run.Orientation == LineOrientation.Vertical))
                {
                    foreach (var cell in run.Cells)
                    {
                        if (horizontalCells.Contains(cell))
                        {
                            return cell;
                        }
                    }
                }
            }

            var longest = Runs.OrderByDescending(run => run.Cells.Count).FirstOrDefault();
            if (longest != null && longest.Cells.Count > 0)
            {
                return longest.Cells[longest.Cells.Count / 2];
            }

            return Cells.First();
        }

        public void AddRun(MatchRun run)
        {
            if (Runs.Count == 0)
            {
                Character = run.Character;
            }

            Runs.Add(run);
            foreach (var cell in run.Cells)
            {
                Cells.Add(cell);
            }

            if (run.Orientation == LineOrientation.Horizontal)
            {
                HasHorizontalRun = true;
                if (run.Cells.Count > LongestHorizontalRun)
                {
                    LongestHorizontalRun = run.Cells.Count;
                }
            }
            else
            {
                HasVerticalRun = true;
                if (run.Cells.Count > LongestVerticalRun)
                {
                    LongestVerticalRun = run.Cells.Count;
                }
            }
        }

        public void AddCells(CharacterType character, IEnumerable<BoardCoordinate> cells)
        {
            if (Runs.Count == 0 && Cells.Count == 0)
            {
                Character = character;
            }

            foreach (var cell in cells)
            {
                Cells.Add(cell);
            }
        }

        public void Merge(MatchGroup other)
        {
            foreach (var run in other.Runs)
            {
                AddRun(run);
            }

            AddCells(other.Character, other.Cells);
        }
    }

    public static class MatchFinder
    {
        public const int MinimumConnectedMatchSize = 4;

        private static readonly BoardCoordinate[] CardinalDirections =
        {
            new BoardCoordinate(1, 0),
            new BoardCoordinate(-1, 0),
            new BoardCoordinate(0, 1),
            new BoardCoordinate(0, -1)
        };

        public static List<MatchGroup> FindMatches(BoardModel model)
        {
            var runs = new List<MatchRun>();
            FindHorizontalRuns(model, runs);
            FindVerticalRuns(model, runs);
            var groups = MergeRuns(runs);
            FindConnectedGroups(model, groups);
            return groups;
        }

        public static bool HasAnyMatch(BoardModel model)
        {
            return FindMatches(model).Count > 0;
        }

        private static void FindHorizontalRuns(BoardModel model, List<MatchRun> runs)
        {
            for (var y = 0; y < model.Height; y++)
            {
                var run = new MatchRun { Orientation = LineOrientation.Horizontal };
                for (var x = 0; x < model.Width; x++)
                {
                    AddOrFlushRun(model, x, y, run, runs, LineOrientation.Horizontal);
                }

                FlushRun(run, runs);
            }
        }

        private static void FindVerticalRuns(BoardModel model, List<MatchRun> runs)
        {
            for (var x = 0; x < model.Width; x++)
            {
                var run = new MatchRun { Orientation = LineOrientation.Vertical };
                for (var y = 0; y < model.Height; y++)
                {
                    AddOrFlushRun(model, x, y, run, runs, LineOrientation.Vertical);
                }

                FlushRun(run, runs);
            }
        }

        private static void AddOrFlushRun(BoardModel model, int x, int y, MatchRun run, List<MatchRun> runs, LineOrientation orientation)
        {
            var piece = model.GetCell(x, y)?.Piece;
            if (piece != null && piece.IsMatchable)
            {
                if (run.Cells.Count == 0 || run.Character == piece.Character)
                {
                    run.Character = piece.Character;
                    run.Cells.Add(new BoardCoordinate(x, y));
                    return;
                }
            }

            FlushRun(run, runs);
            run.Orientation = orientation;
            run.Cells.Clear();

            if (piece != null && piece.IsMatchable)
            {
                run.Character = piece.Character;
                run.Cells.Add(new BoardCoordinate(x, y));
            }
        }

        private static void FlushRun(MatchRun run, List<MatchRun> runs)
        {
            if (run.Cells.Count >= 3)
            {
                var committed = new MatchRun
                {
                    Character = run.Character,
                    Orientation = run.Orientation
                };
                committed.Cells.AddRange(run.Cells);
                runs.Add(committed);
            }

            run.Cells.Clear();
        }

        private static List<MatchGroup> MergeRuns(List<MatchRun> runs)
        {
            var groups = new List<MatchGroup>();
            foreach (var run in runs)
            {
                var overlappingGroups = groups
                    .Where(group => group.Character == run.Character && run.Cells.Any(cell => group.Cells.Contains(cell)))
                    .ToList();

                if (overlappingGroups.Count == 0)
                {
                    var group = new MatchGroup();
                    group.AddRun(run);
                    groups.Add(group);
                    continue;
                }

                var target = overlappingGroups[0];
                target.AddRun(run);
                for (var i = 1; i < overlappingGroups.Count; i++)
                {
                    target.Merge(overlappingGroups[i]);
                    groups.Remove(overlappingGroups[i]);
                }
            }

            return groups;
        }

        private static void FindConnectedGroups(BoardModel model, List<MatchGroup> groups)
        {
            var visited = new HashSet<BoardCoordinate>();
            foreach (var coordinate in model.ActiveCoordinates())
            {
                if (visited.Contains(coordinate))
                {
                    continue;
                }

                var piece = model.GetCell(coordinate)?.Piece;
                if (piece == null || !piece.IsMatchable)
                {
                    continue;
                }

                var cells = CollectConnectedCells(model, coordinate, piece.Character, visited);
                if (cells.Count >= MinimumConnectedMatchSize)
                {
                    MergeConnectedCells(groups, piece.Character, cells);
                }
            }
        }

        private static List<BoardCoordinate> CollectConnectedCells(
            BoardModel model,
            BoardCoordinate start,
            CharacterType character,
            HashSet<BoardCoordinate> visited)
        {
            var cells = new List<BoardCoordinate>();
            var queue = new Queue<BoardCoordinate>();
            visited.Add(start);
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                cells.Add(current);

                foreach (var direction in CardinalDirections)
                {
                    var next = new BoardCoordinate(current.x + direction.x, current.y + direction.y);
                    if (visited.Contains(next))
                    {
                        continue;
                    }

                    var piece = model.GetCell(next)?.Piece;
                    if (piece == null || !piece.IsMatchable || piece.Character != character)
                    {
                        continue;
                    }

                    visited.Add(next);
                    queue.Enqueue(next);
                }
            }

            return cells;
        }

        private static void MergeConnectedCells(
            List<MatchGroup> groups,
            CharacterType character,
            List<BoardCoordinate> cells)
        {
            var overlappingGroups = groups
                .Where(group => group.Character == character && cells.Any(cell => group.Cells.Contains(cell)))
                .ToList();

            if (overlappingGroups.Count == 0)
            {
                var group = new MatchGroup();
                group.AddCells(character, cells);
                groups.Add(group);
                return;
            }

            var target = overlappingGroups[0];
            target.AddCells(character, cells);
            for (var i = 1; i < overlappingGroups.Count; i++)
            {
                target.Merge(overlappingGroups[i]);
                groups.Remove(overlappingGroups[i]);
            }
        }
    }
}
