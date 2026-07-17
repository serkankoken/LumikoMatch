using System;
using System.Collections.Generic;

namespace CharacterMatch3.Board
{
    public static class BoardShuffler
    {
        public static bool Shuffle(BoardModel model, Random random, int maxAttempts)
        {
            var movablePieces = new List<BoardPiece>();
            var slots = new List<BoardCoordinate>();

            foreach (var coordinate in model.ActiveCoordinates())
            {
                var cell = model.GetCell(coordinate);
                if (cell == null || cell.CrateLayers > 0 || cell.LockLayers > 0 || cell.Piece == null || !cell.Piece.IsMovable)
                {
                    continue;
                }

                movablePieces.Add(cell.Piece);
                slots.Add(coordinate);
            }

            if (movablePieces.Count < 2)
            {
                return false;
            }

            var attempts = Math.Max(1, maxAttempts);
            for (var attempt = 0; attempt < attempts; attempt++)
            {
                for (var i = movablePieces.Count - 1; i > 0; i--)
                {
                    var j = random.Next(i + 1);
                    (movablePieces[i], movablePieces[j]) = (movablePieces[j], movablePieces[i]);
                }

                for (var i = 0; i < slots.Count; i++)
                {
                    model.GetCell(slots[i]).Piece = movablePieces[i];
                }

                if (!MatchFinder.HasAnyMatch(model) && MoveFinder.HasLegalMove(model))
                {
                    return true;
                }
            }

            return MoveFinder.HasLegalMove(model);
        }
    }
}
