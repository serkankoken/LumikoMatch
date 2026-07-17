using System.Collections.Generic;

namespace CharacterMatch3.Board
{
    public sealed class BoardPiece
    {
        public PieceKind Kind;
        public CharacterType Character;
        public LineOrientation LineOrientation;
        public int Id;

        public bool IsSpecial => Kind == PieceKind.Line || Kind == PieceKind.Burst || Kind == PieceKind.Rainbow;
        public bool IsMatchable => Kind == PieceKind.Normal || Kind == PieceKind.Line || Kind == PieceKind.Burst;
        public bool IsMovable => Kind != PieceKind.Companion;

        public BoardPiece Clone()
        {
            return new BoardPiece
            {
                Kind = Kind,
                Character = Character,
                LineOrientation = LineOrientation,
                Id = Id
            };
        }

        public static BoardPiece Normal(CharacterType character, int id)
        {
            return new BoardPiece
            {
                Kind = PieceKind.Normal,
                Character = character,
                LineOrientation = LineOrientation.Horizontal,
                Id = id
            };
        }

        public static BoardPiece Special(PieceKind kind, CharacterType character, LineOrientation lineOrientation, int id)
        {
            return new BoardPiece
            {
                Kind = kind,
                Character = character,
                LineOrientation = lineOrientation,
                Id = id
            };
        }

        public static BoardPiece Companion(int id)
        {
            return new BoardPiece
            {
                Kind = PieceKind.Companion,
                Character = CharacterType.Cat,
                LineOrientation = LineOrientation.Horizontal,
                Id = id
            };
        }
    }

    public sealed class BoardCellState
    {
        public bool Active;
        public int SoftCoverLayers;
        public int CrateLayers;
        public int LockLayers;
        public bool IsCompanionExit;
        public BoardPiece Piece;

        public bool CanHoldPiece => Active && CrateLayers <= 0;
        public bool HasBlockerOnly => Active && CrateLayers > 0;

        public BoardCellState Clone()
        {
            return new BoardCellState
            {
                Active = Active,
                SoftCoverLayers = SoftCoverLayers,
                CrateLayers = CrateLayers,
                LockLayers = LockLayers,
                IsCompanionExit = IsCompanionExit,
                Piece = Piece?.Clone()
            };
        }
    }

    public sealed class BoardModel
    {
        private readonly BoardCellState[] cells;

        public int Width { get; }
        public int Height { get; }
        public int NextPieceId { get; private set; } = 1;

        public BoardModel(int width, int height)
        {
            Width = width;
            Height = height;
            cells = new BoardCellState[width * height];
            for (var i = 0; i < cells.Length; i++)
            {
                cells[i] = new BoardCellState();
            }
        }

        public BoardModel CloneForSimulation()
        {
            var clone = new BoardModel(Width, Height)
            {
                NextPieceId = NextPieceId
            };

            for (var i = 0; i < cells.Length; i++)
            {
                clone.cells[i] = cells[i].Clone();
            }

            return clone;
        }

        public int CreatePieceId()
        {
            return NextPieceId++;
        }

        public bool IsInside(int x, int y)
        {
            return x >= 0 && x < Width && y >= 0 && y < Height;
        }

        public int Index(int x, int y)
        {
            return y * Width + x;
        }

        public BoardCellState GetCell(int x, int y)
        {
            return IsInside(x, y) ? cells[Index(x, y)] : null;
        }

        public BoardCellState GetCell(BoardCoordinate coordinate)
        {
            return GetCell(coordinate.x, coordinate.y);
        }

        public IEnumerable<BoardCoordinate> Coordinates()
        {
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    yield return new BoardCoordinate(x, y);
                }
            }
        }

        public IEnumerable<BoardCoordinate> ActiveCoordinates()
        {
            foreach (var coordinate in Coordinates())
            {
                var cell = GetCell(coordinate);
                if (cell is { Active: true })
                {
                    yield return coordinate;
                }
            }
        }

        public bool AreAdjacent(BoardCoordinate a, BoardCoordinate b)
        {
            var dx = a.x > b.x ? a.x - b.x : b.x - a.x;
            var dy = a.y > b.y ? a.y - b.y : b.y - a.y;
            return dx + dy == 1;
        }

        public bool CanSwap(BoardCoordinate coordinate)
        {
            var cell = GetCell(coordinate);
            return cell is { Active: true, CrateLayers: <= 0, LockLayers: <= 0, Piece: not null } &&
                   cell.Piece.IsMovable;
        }

        public bool CanSwap(BoardCoordinate a, BoardCoordinate b)
        {
            return AreAdjacent(a, b) && CanSwap(a) && CanSwap(b);
        }

        public void SwapPieces(BoardCoordinate a, BoardCoordinate b)
        {
            var cellA = GetCell(a);
            var cellB = GetCell(b);
            if (cellA == null || cellB == null)
            {
                return;
            }

            (cellA.Piece, cellB.Piece) = (cellB.Piece, cellA.Piece);
        }

        public bool IsSpecialAt(BoardCoordinate coordinate)
        {
            var piece = GetCell(coordinate)?.Piece;
            return piece != null && piece.IsSpecial;
        }

        public bool HasRemovablePiece(BoardCoordinate coordinate)
        {
            var piece = GetCell(coordinate)?.Piece;
            return piece != null && piece.Kind != PieceKind.Companion;
        }

        public int CountCharacter(CharacterType characterType)
        {
            var count = 0;
            foreach (var coordinate in ActiveCoordinates())
            {
                var piece = GetCell(coordinate).Piece;
                if (piece != null && piece.IsMatchable && piece.Character == characterType)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
