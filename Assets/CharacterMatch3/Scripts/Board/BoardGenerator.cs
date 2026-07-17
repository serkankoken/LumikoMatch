using System;
using System.Collections.Generic;
using UnityEngine;

namespace CharacterMatch3.Board
{
    public sealed class BoardGenerator
    {
        private readonly LevelDefinition level;
        private readonly System.Random random;

        public BoardGenerator(LevelDefinition level)
        {
            this.level = level;
            level.EnsureDefaults();
            random = new System.Random(level.randomSeed);
        }

        public BoardModel Generate()
        {
            var attempts = Mathf.Max(10, level.maximumAutomaticReshuffleAttempts + 3);
            BoardModel last = null;
            for (var attempt = 0; attempt < attempts; attempt++)
            {
                var model = CreateEmptyModel();
                ApplyBoardElements(model);
                ApplyPrePlacedPieces(model);
                FillEmptyCells(model);

                if (!MatchFinder.HasAnyMatch(model) && MoveFinder.HasLegalMove(model))
                {
                    return model;
                }

                last = model;
            }

            if (last != null && !MoveFinder.HasLegalMove(last))
            {
                BoardShuffler.Shuffle(last, random, level.maximumAutomaticReshuffleAttempts);
            }

            return last ?? CreateEmptyModel();
        }

        private BoardModel CreateEmptyModel()
        {
            var model = new BoardModel(level.boardWidth, level.boardHeight);
            foreach (var coordinate in model.Coordinates())
            {
                var cell = model.GetCell(coordinate);
                cell.Active = level.IsActive(coordinate.x, coordinate.y);
            }

            return model;
        }

        private void ApplyBoardElements(BoardModel model)
        {
            foreach (var placement in level.softCoverPlacements)
            {
                var cell = model.GetCell(placement.coordinate);
                if (cell is { Active: true })
                {
                    cell.SoftCoverLayers = Mathf.Max(cell.SoftCoverLayers, placement.layers);
                }
            }

            foreach (var placement in level.cratePlacements)
            {
                var cell = model.GetCell(placement.coordinate);
                if (cell is { Active: true })
                {
                    cell.CrateLayers = Mathf.Max(cell.CrateLayers, placement.layers);
                    cell.Piece = null;
                }
            }

            foreach (var placement in level.lockPlacements)
            {
                var cell = model.GetCell(placement.coordinate);
                if (cell is { Active: true, CrateLayers: <= 0 })
                {
                    cell.LockLayers = Mathf.Max(cell.LockLayers, placement.layers);
                }
            }

            foreach (var exit in level.companionExitCells)
            {
                var cell = model.GetCell(exit);
                if (cell is { Active: true })
                {
                    cell.IsCompanionExit = true;
                }
            }

            foreach (var token in level.companionTokenStartingPositions)
            {
                var cell = model.GetCell(token);
                if (cell is { Active: true, CrateLayers: <= 0 })
                {
                    cell.Piece = BoardPiece.Companion(model.CreatePieceId());
                }
            }
        }

        private void ApplyPrePlacedPieces(BoardModel model)
        {
            foreach (var placement in level.prePlacedNormalPieces)
            {
                var cell = model.GetCell(placement.coordinate);
                if (cell is { Active: true, CrateLayers: <= 0 } && cell.Piece == null)
                {
                    cell.Piece = BoardPiece.Normal(placement.character, model.CreatePieceId());
                }
            }

            foreach (var placement in level.prePlacedSpecialPieces)
            {
                var cell = model.GetCell(placement.coordinate);
                if (cell is { Active: true, CrateLayers: <= 0 } && cell.Piece == null)
                {
                    var kind = placement.kind == PieceKind.Normal ? PieceKind.Line : placement.kind;
                    cell.Piece = BoardPiece.Special(kind, placement.character, placement.lineOrientation, model.CreatePieceId());
                }
            }
        }

        private void FillEmptyCells(BoardModel model)
        {
            foreach (var coordinate in model.ActiveCoordinates())
            {
                var cell = model.GetCell(coordinate);
                if (!cell.CanHoldPiece || cell.Piece != null)
                {
                    continue;
                }

                cell.Piece = BoardPiece.Normal(PickCharacterAvoidingImmediateMatch(model, coordinate), model.CreatePieceId());
            }
        }

        private CharacterType PickCharacterAvoidingImmediateMatch(BoardModel model, BoardCoordinate coordinate)
        {
            var available = level.availableCharacterTypes;
            if (available.Count == 0)
            {
                return CharacterType.Cat;
            }

            for (var attempt = 0; attempt < 30; attempt++)
            {
                var character = available[random.Next(available.Count)];
                if (!WouldCreateImmediateMatch(model, coordinate, character))
                {
                    return character;
                }
            }

            return available[random.Next(available.Count)];
        }

        private static bool WouldCreateImmediateMatch(BoardModel model, BoardCoordinate coordinate, CharacterType character)
        {
            return CountSame(model, coordinate, character, -1, 0) + CountSame(model, coordinate, character, 1, 0) >= 2 ||
                   CountSame(model, coordinate, character, 0, -1) + CountSame(model, coordinate, character, 0, 1) >= 2;
        }

        private static int CountSame(BoardModel model, BoardCoordinate coordinate, CharacterType character, int dx, int dy)
        {
            var count = 0;
            var x = coordinate.x + dx;
            var y = coordinate.y + dy;
            while (model.IsInside(x, y))
            {
                var piece = model.GetCell(x, y)?.Piece;
                if (piece == null || !piece.IsMatchable || piece.Character != character)
                {
                    break;
                }

                count++;
                x += dx;
                y += dy;
            }

            return count;
        }
    }
}
