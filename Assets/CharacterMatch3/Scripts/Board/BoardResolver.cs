using System;
using System.Collections.Generic;
using UnityEngine;

namespace CharacterMatch3.Board
{
    public sealed class BoardResolutionEvents
    {
        public Action<CharacterType, PieceKind, BoardCoordinate> PieceRemoved;
        public Action<BoardCoordinate> SoftCoverLayerRemoved;
        public Action<BoardCoordinate> CrateLayerRemoved;
        public Action<BoardCoordinate> LockLayerRemoved;
        public Action<BoardCoordinate> CompanionDelivered;
        public Action<int, BoardCoordinate> ScoreAwarded;
        public Action<BoardCoordinate, PieceKind> SpecialCreated;
        public Action<BoardCoordinate, PieceKind> SpecialActivated;
    }

    public sealed class BoardResolveResult
    {
        public bool Changed;
        public int RemovedPieces;
        public int DeliveredCompanions;
        public int SpecialsCreated;
    }

    public static class BoardResolver
    {
        private static readonly BoardCoordinate[] CardinalDirections =
        {
            new BoardCoordinate(1, 0),
            new BoardCoordinate(-1, 0),
            new BoardCoordinate(0, 1),
            new BoardCoordinate(0, -1)
        };

        public static bool IsSpecialSwap(BoardModel model, BoardCoordinate a, BoardCoordinate b)
        {
            var pieceA = model.GetCell(a)?.Piece;
            var pieceB = model.GetCell(b)?.Piece;
            return pieceA != null && pieceB != null && (pieceA.IsSpecial || pieceB.IsSpecial);
        }

        public static BoardResolveResult ResolveSpecialSwap(
            BoardModel model,
            BoardCoordinate a,
            BoardCoordinate b,
            LevelDefinition level,
            ScoringConfig scoring,
            BoardResolutionEvents events)
        {
            var result = new BoardResolveResult();
            var pieceA = model.GetCell(a)?.Piece?.Clone();
            var pieceB = model.GetCell(b)?.Piece?.Clone();
            if (pieceA == null || pieceB == null)
            {
                return result;
            }

            var activated = new HashSet<BoardCoordinate>();
            var affected = new HashSet<BoardCoordinate>();
            ResolveCombination(model, a, pieceA, b, pieceB, affected, activated, level);
            InvokeSpecialSwapActivations(a, pieceA, b, pieceB, events);

            if (pieceA.Kind != PieceKind.Rainbow || pieceB.Kind != PieceKind.Rainbow)
            {
                affected.Add(a);
                affected.Add(b);
            }

            ApplyHits(model, affected, new HashSet<BoardCoordinate>(), activated, true, scoring, events, result);
            result.Changed |= ApplyGravityAndRefill(model, level, scoring, events, result);
            return result;
        }

        private static void InvokeSpecialSwapActivations(
            BoardCoordinate a,
            BoardPiece pieceA,
            BoardCoordinate b,
            BoardPiece pieceB,
            BoardResolutionEvents events)
        {
            if (pieceA.IsSpecial)
            {
                events?.SpecialActivated?.Invoke(a, pieceA.Kind);
            }

            if (pieceB.IsSpecial)
            {
                events?.SpecialActivated?.Invoke(b, pieceB.Kind);
            }
        }

        public static BoardResolveResult ResolveCurrentMatches(
            BoardModel model,
            BoardCoordinate preferredSpecialCoordinate,
            LevelDefinition level,
            ScoringConfig scoring,
            BoardResolutionEvents events,
            int cascadeIndex)
        {
            var result = new BoardResolveResult();
            var matches = MatchFinder.FindMatches(model);
            if (matches.Count == 0)
            {
                return result;
            }

            var allMatchedCells = new HashSet<BoardCoordinate>();
            var specialSpawnCells = new Dictionary<BoardCoordinate, BoardPiece>();
            var activated = new HashSet<BoardCoordinate>();

            foreach (var group in matches)
            {
                foreach (var cell in group.Cells)
                {
                    allMatchedCells.Add(cell);
                }

                if (!group.CreatesSpecial)
                {
                    continue;
                }

                if (!TryGetSpecialSpawnCoordinate(model, group, preferredSpecialCoordinate, out var spawnCoordinate))
                {
                    continue;
                }

                var kind = group.CreatedSpecialKind;
                var orientation = kind == PieceKind.Line ? group.CreatedLineOrientation : LineOrientation.Horizontal;
                specialSpawnCells[spawnCoordinate] = BoardPiece.Special(kind, group.Character, orientation, model.CreatePieceId());
                result.SpecialsCreated++;
                events?.SpecialCreated?.Invoke(spawnCoordinate, kind);

                if (kind == PieceKind.Rainbow)
                {
                    Award(scoring?.fiveMatchCreationBonus ?? 300, cascadeIndex, spawnCoordinate, scoring, events);
                }
                else if (kind == PieceKind.Burst)
                {
                    Award(scoring?.tOrLMatchBonus ?? 240, cascadeIndex, spawnCoordinate, scoring, events);
                }
                else
                {
                    Award(scoring?.fourMatchCreationBonus ?? 120, cascadeIndex, spawnCoordinate, scoring, events);
                }
            }

            ApplyHits(model, allMatchedCells, specialSpawnCells.Keys, activated, false, scoring, events, result, cascadeIndex);
            DamageAdjacentCrates(model, allMatchedCells, scoring, events, result, cascadeIndex);

            foreach (var pair in specialSpawnCells)
            {
                var cell = model.GetCell(pair.Key);
                if (cell is { Active: true, CrateLayers: <= 0 })
                {
                    cell.Piece = pair.Value;
                }
            }

            result.Changed = true;
            result.Changed |= ApplyGravityAndRefill(model, level, scoring, events, result);
            return result;
        }

        public static bool ApplyGravityAndRefill(
            BoardModel model,
            LevelDefinition level,
            ScoringConfig scoring,
            BoardResolutionEvents events,
            BoardResolveResult result = null)
        {
            var changed = false;
            var random = new System.Random(level.randomSeed + model.NextPieceId * 17);

            for (var x = 0; x < model.Width; x++)
            {
                var y = 0;
                while (y < model.Height)
                {
                    while (y < model.Height && !CanOccupy(model, x, y))
                    {
                        y++;
                    }

                    if (y >= model.Height)
                    {
                        break;
                    }

                    var segment = new List<int>();
                    while (y < model.Height && CanOccupy(model, x, y))
                    {
                        segment.Add(y);
                        y++;
                    }

                    var pieces = new List<BoardPiece>();
                    foreach (var segmentY in segment)
                    {
                        var cell = model.GetCell(x, segmentY);
                        if (cell.Piece != null)
                        {
                            pieces.Add(cell.Piece);
                            cell.Piece = null;
                        }
                    }

                    for (var i = 0; i < pieces.Count; i++)
                    {
                        var targetCell = model.GetCell(x, segment[i]);
                        if (targetCell.Piece != pieces[i])
                        {
                            changed = true;
                        }

                        targetCell.Piece = pieces[i];
                    }

                    for (var i = pieces.Count; i < segment.Count; i++)
                    {
                        model.GetCell(x, segment[i]).Piece = BoardPiece.Normal(PickRandomCharacter(level, random), model.CreatePieceId());
                        changed = true;
                    }
                }
            }

            changed |= DeliverCompanions(model, scoring, events, result);
            return changed;
        }

        public static CharacterType ChooseFallbackRainbowCharacter(BoardModel model)
        {
            var best = CharacterType.Cat;
            var bestCount = -1;
            foreach (var character in CharacterMatch3Constants.AllCharacters)
            {
                var count = model.CountCharacter(character);
                if (count > bestCount)
                {
                    best = character;
                    bestCount = count;
                }
            }

            return best;
        }

        private static void ResolveCombination(
            BoardModel model,
            BoardCoordinate a,
            BoardPiece pieceA,
            BoardCoordinate b,
            BoardPiece pieceB,
            HashSet<BoardCoordinate> affected,
            HashSet<BoardCoordinate> activated,
            LevelDefinition level)
        {
            var center = b;

            if (pieceA.Kind == PieceKind.Rainbow && pieceB.Kind == PieceKind.Rainbow)
            {
                foreach (var coordinate in model.ActiveCoordinates())
                {
                    if (model.HasRemovablePiece(coordinate))
                    {
                        affected.Add(coordinate);
                    }
                }

                MarkActivated(a, pieceA, activated);
                MarkActivated(b, pieceB, activated);
                return;
            }

            if (pieceA.Kind == PieceKind.Rainbow || pieceB.Kind == PieceKind.Rainbow)
            {
                var rainbowCoordinate = pieceA.Kind == PieceKind.Rainbow ? a : b;
                var otherPiece = pieceA.Kind == PieceKind.Rainbow ? pieceB : pieceA;
                var target = otherPiece.Kind == PieceKind.Companion ? ChooseFallbackRainbowCharacter(model) : otherPiece.Character;
                MarkActivated(rainbowCoordinate, pieceA.Kind == PieceKind.Rainbow ? pieceA : pieceB, activated);

                if (otherPiece.Kind == PieceKind.Line || otherPiece.Kind == PieceKind.Burst)
                {
                    var conversionKind = otherPiece.Kind;
                    foreach (var coordinate in model.ActiveCoordinates())
                    {
                        var cell = model.GetCell(coordinate);
                        var piece = cell?.Piece;
                        if (piece == null || !piece.IsMatchable || piece.Character != target)
                        {
                            continue;
                        }

                        piece.Kind = conversionKind;
                        if (conversionKind == PieceKind.Line)
                        {
                            piece.LineOrientation = UnityEngine.Random.value > 0.5f
                                ? LineOrientation.Horizontal
                                : LineOrientation.Vertical;
                        }

                        CollectSpecialEffect(model, coordinate, piece.Clone(), affected, activated, level);
                    }
                }
                else
                {
                    foreach (var coordinate in model.ActiveCoordinates())
                    {
                        var piece = model.GetCell(coordinate)?.Piece;
                        if (piece != null && piece.IsMatchable && piece.Character == target)
                        {
                            affected.Add(coordinate);
                        }
                    }
                }

                affected.Add(rainbowCoordinate);
                return;
            }

            if (pieceA.Kind == PieceKind.Line && pieceB.Kind == PieceKind.Line)
            {
                AddRow(model, center.y, affected);
                AddColumn(model, center.x, affected);
                MarkActivated(a, pieceA, activated);
                MarkActivated(b, pieceB, activated);
                return;
            }

            if ((pieceA.Kind == PieceKind.Line && pieceB.Kind == PieceKind.Burst) ||
                (pieceA.Kind == PieceKind.Burst && pieceB.Kind == PieceKind.Line))
            {
                for (var offset = -1; offset <= 1; offset++)
                {
                    AddRow(model, center.y + offset, affected);
                    AddColumn(model, center.x + offset, affected);
                }

                MarkActivated(a, pieceA, activated);
                MarkActivated(b, pieceB, activated);
                return;
            }

            if (pieceA.Kind == PieceKind.Burst && pieceB.Kind == PieceKind.Burst)
            {
                AddRadius(model, center, 2, affected);
                MarkActivated(a, pieceA, activated);
                MarkActivated(b, pieceB, activated);
                return;
            }

            CollectSpecialEffect(model, a, pieceA, affected, activated, level);
            CollectSpecialEffect(model, b, pieceB, affected, activated, level);
        }

        private static void CollectSpecialEffect(
            BoardModel model,
            BoardCoordinate coordinate,
            BoardPiece piece,
            HashSet<BoardCoordinate> affected,
            HashSet<BoardCoordinate> activated,
            LevelDefinition level,
            bool alreadyActivated = false)
        {
            if (piece == null || (!alreadyActivated && !MarkActivated(coordinate, piece, activated)))
            {
                return;
            }

            switch (piece.Kind)
            {
                case PieceKind.Line:
                    if (piece.LineOrientation == LineOrientation.Horizontal)
                    {
                        AddRow(model, coordinate.y, affected);
                    }
                    else
                    {
                        AddColumn(model, coordinate.x, affected);
                    }

                    break;
                case PieceKind.Burst:
                    AddRadius(model, coordinate, 1, affected);
                    break;
                case PieceKind.Rainbow:
                    var target = ChooseFallbackRainbowCharacter(model);
                    foreach (var active in model.ActiveCoordinates())
                    {
                        var other = model.GetCell(active)?.Piece;
                        if (other != null && other.IsMatchable && other.Character == target)
                        {
                            affected.Add(active);
                        }
                    }

                    affected.Add(coordinate);
                    break;
            }
        }

        private static bool MarkActivated(BoardCoordinate coordinate, BoardPiece piece, HashSet<BoardCoordinate> activated)
        {
            if (piece == null || !piece.IsSpecial)
            {
                return false;
            }

            return activated.Add(coordinate);
        }

        private static void ApplyHits(
            BoardModel model,
            HashSet<BoardCoordinate> affected,
            ICollection<BoardCoordinate> preservedPieces,
            HashSet<BoardCoordinate> activated,
            bool specialHit,
            ScoringConfig scoring,
            BoardResolutionEvents events,
            BoardResolveResult result,
            int cascadeIndex = 0)
        {
            var queue = new Queue<(BoardCoordinate Coordinate, BoardPiece Piece)>();
            foreach (var coordinate in affected)
            {
                var piece = model.GetCell(coordinate)?.Piece;
                if (piece != null && piece.IsSpecial && !preservedPieces.Contains(coordinate) && activated.Add(coordinate))
                {
                    queue.Enqueue((coordinate, piece.Clone()));
                    events?.SpecialActivated?.Invoke(coordinate, piece.Kind);
                }
            }

            foreach (var coordinate in affected)
            {
                HitCell(model, coordinate, preservedPieces.Contains(coordinate), scoring, events, result, cascadeIndex);
            }

            while (queue.Count > 0)
            {
                var activation = queue.Dequeue();
                var chainedAffected = new HashSet<BoardCoordinate>();
                CollectSpecialEffect(model, activation.Coordinate, activation.Piece, chainedAffected, activated, null, true);
                foreach (var coordinate in chainedAffected)
                {
                    var piece = model.GetCell(coordinate)?.Piece;
                    if (piece != null && piece.IsSpecial && activated.Add(coordinate))
                    {
                        queue.Enqueue((coordinate, piece.Clone()));
                        events?.SpecialActivated?.Invoke(coordinate, piece.Kind);
                    }
                }

                foreach (var coordinate in chainedAffected)
                {
                    HitCell(model, coordinate, false, scoring, events, result, cascadeIndex);
                }
            }
        }

        private static bool TryGetSpecialSpawnCoordinate(
            BoardModel model,
            MatchGroup group,
            BoardCoordinate preferred,
            out BoardCoordinate spawnCoordinate)
        {
            var primary = group.GetSpecialCoordinate(preferred);
            if (CanCreateSpecialAt(model, primary))
            {
                spawnCoordinate = primary;
                return true;
            }

            var bestDistance = int.MaxValue;
            var found = false;
            spawnCoordinate = default;
            foreach (var coordinate in group.Cells)
            {
                if (!CanCreateSpecialAt(model, coordinate))
                {
                    continue;
                }

                var dx = coordinate.x - primary.x;
                var dy = coordinate.y - primary.y;
                var distance = dx * dx + dy * dy;
                if (found && distance >= bestDistance)
                {
                    continue;
                }

                found = true;
                bestDistance = distance;
                spawnCoordinate = coordinate;
            }

            return found;
        }

        private static bool CanCreateSpecialAt(BoardModel model, BoardCoordinate coordinate)
        {
            var piece = model.GetCell(coordinate)?.Piece;
            return piece != null && piece.Kind == PieceKind.Normal;
        }

        private static void HitCell(
            BoardModel model,
            BoardCoordinate coordinate,
            bool preservePiece,
            ScoringConfig scoring,
            BoardResolutionEvents events,
            BoardResolveResult result,
            int cascadeIndex)
        {
            var cell = model.GetCell(coordinate);
            if (cell == null || !cell.Active)
            {
                return;
            }

            if (cell.SoftCoverLayers > 0)
            {
                cell.SoftCoverLayers--;
                events?.SoftCoverLayerRemoved?.Invoke(coordinate);
                Award(scoring?.softCoverLayerRemoved ?? 80, cascadeIndex, coordinate, scoring, events);
                result.Changed = true;
            }

            if (cell.CrateLayers > 0)
            {
                cell.CrateLayers--;
                events?.CrateLayerRemoved?.Invoke(coordinate);
                Award(scoring?.crateLayerRemoved ?? 100, cascadeIndex, coordinate, scoring, events);
                result.Changed = true;
                return;
            }

            if (cell.LockLayers > 0)
            {
                cell.LockLayers--;
                events?.LockLayerRemoved?.Invoke(coordinate);
                Award(scoring?.characterLockRemoved ?? 100, cascadeIndex, coordinate, scoring, events);
                result.Changed = true;
                if (cell.LockLayers > 0)
                {
                    return;
                }
            }

            if (preservePiece || cell.Piece == null || cell.Piece.Kind == PieceKind.Companion)
            {
                return;
            }

            var removed = cell.Piece;
            cell.Piece = null;
            result.RemovedPieces++;
            result.Changed = true;
            events?.PieceRemoved?.Invoke(removed.Character, removed.Kind, coordinate);
            Award(scoring?.normalPieceRemoved ?? 60, cascadeIndex, coordinate, scoring, events);
        }

        private static void DamageAdjacentCrates(
            BoardModel model,
            HashSet<BoardCoordinate> sourceCells,
            ScoringConfig scoring,
            BoardResolutionEvents events,
            BoardResolveResult result,
            int cascadeIndex)
        {
            var damaged = new HashSet<BoardCoordinate>();
            foreach (var source in sourceCells)
            {
                foreach (var direction in CardinalDirections)
                {
                    var target = new BoardCoordinate(source.x + direction.x, source.y + direction.y);
                    var cell = model.GetCell(target);
                    if (cell == null || cell.CrateLayers <= 0 || !damaged.Add(target))
                    {
                        continue;
                    }

                    cell.CrateLayers--;
                    events?.CrateLayerRemoved?.Invoke(target);
                    Award(scoring?.crateLayerRemoved ?? 100, cascadeIndex, target, scoring, events);
                    result.Changed = true;
                }
            }
        }

        private static bool DeliverCompanions(
            BoardModel model,
            ScoringConfig scoring,
            BoardResolutionEvents events,
            BoardResolveResult result)
        {
            var changed = false;
            foreach (var coordinate in model.ActiveCoordinates())
            {
                var cell = model.GetCell(coordinate);
                if (cell is not { IsCompanionExit: true } || cell.Piece == null || cell.Piece.Kind != PieceKind.Companion)
                {
                    continue;
                }

                cell.Piece = null;
                result ??= new BoardResolveResult();
                result.DeliveredCompanions++;
                result.Changed = true;
                changed = true;
                events?.CompanionDelivered?.Invoke(coordinate);
                events?.ScoreAwarded?.Invoke(scoring?.companionTokenDelivered ?? 1000, coordinate);
            }

            return changed;
        }

        private static bool CanOccupy(BoardModel model, int x, int y)
        {
            var cell = model.GetCell(x, y);
            return cell is { Active: true, CrateLayers: <= 0 };
        }

        private static CharacterType PickRandomCharacter(LevelDefinition level, System.Random random)
        {
            var available = level.availableCharacterTypes;
            if (available == null || available.Count == 0)
            {
                return CharacterType.Cat;
            }

            return available[random.Next(available.Count)];
        }

        private static void AddRow(BoardModel model, int y, HashSet<BoardCoordinate> cells)
        {
            if (y < 0 || y >= model.Height)
            {
                return;
            }

            for (var x = 0; x < model.Width; x++)
            {
                if (model.GetCell(x, y) is { Active: true })
                {
                    cells.Add(new BoardCoordinate(x, y));
                }
            }
        }

        private static void AddColumn(BoardModel model, int x, HashSet<BoardCoordinate> cells)
        {
            if (x < 0 || x >= model.Width)
            {
                return;
            }

            for (var y = 0; y < model.Height; y++)
            {
                if (model.GetCell(x, y) is { Active: true })
                {
                    cells.Add(new BoardCoordinate(x, y));
                }
            }
        }

        private static void AddRadius(BoardModel model, BoardCoordinate center, int radius, HashSet<BoardCoordinate> cells)
        {
            for (var y = center.y - radius; y <= center.y + radius; y++)
            {
                for (var x = center.x - radius; x <= center.x + radius; x++)
                {
                    if (model.GetCell(x, y) is { Active: true })
                    {
                        cells.Add(new BoardCoordinate(x, y));
                    }
                }
            }
        }

        private static void Award(
            int baseScore,
            int cascadeIndex,
            BoardCoordinate coordinate,
            ScoringConfig scoring,
            BoardResolutionEvents events)
        {
            var amount = scoring != null ? scoring.ApplyCascadeMultiplier(baseScore, cascadeIndex) : baseScore;
            events?.ScoreAwarded?.Invoke(amount, coordinate);
        }
    }
}
