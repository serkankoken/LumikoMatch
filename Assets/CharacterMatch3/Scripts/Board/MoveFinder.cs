using System.Collections.Generic;

namespace CharacterMatch3.Board
{
    public readonly struct LegalMove
    {
        public readonly BoardCoordinate From;
        public readonly BoardCoordinate To;

        public LegalMove(BoardCoordinate from, BoardCoordinate to)
        {
            From = from;
            To = to;
        }
    }

    public static class MoveFinder
    {
        public static List<LegalMove> FindLegalMoves(BoardModel model)
        {
            var moves = new List<LegalMove>();
            foreach (var coordinate in model.ActiveCoordinates())
            {
                TryAddMove(model, coordinate, new BoardCoordinate(coordinate.x + 1, coordinate.y), moves);
                TryAddMove(model, coordinate, new BoardCoordinate(coordinate.x, coordinate.y + 1), moves);
            }

            return moves;
        }

        public static bool HasLegalMove(BoardModel model)
        {
            foreach (var coordinate in model.ActiveCoordinates())
            {
                if (IsLegalMove(model, coordinate, new BoardCoordinate(coordinate.x + 1, coordinate.y)) ||
                    IsLegalMove(model, coordinate, new BoardCoordinate(coordinate.x, coordinate.y + 1)))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsLegalMove(BoardModel model, BoardCoordinate from, BoardCoordinate to)
        {
            if (!model.CanSwap(from, to))
            {
                return false;
            }

            var fromPiece = model.GetCell(from)?.Piece;
            var toPiece = model.GetCell(to)?.Piece;
            if (fromPiece == null || toPiece == null)
            {
                return false;
            }

            if (fromPiece.IsSpecial || toPiece.IsSpecial)
            {
                return true;
            }

            var clone = model.CloneForSimulation();
            clone.SwapPieces(from, to);
            return MatchFinder.HasAnyMatch(clone);
        }

        private static void TryAddMove(BoardModel model, BoardCoordinate from, BoardCoordinate to, List<LegalMove> moves)
        {
            if (IsLegalMove(model, from, to))
            {
                moves.Add(new LegalMove(from, to));
            }
        }
    }
}
