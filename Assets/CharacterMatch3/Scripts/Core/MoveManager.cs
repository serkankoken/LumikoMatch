using System;
using UnityEngine;

namespace CharacterMatch3.Core
{
    public sealed class MoveManager
    {
        public int MovesRemaining { get; private set; }
        public event Action<int> MovesChanged;

        public void Reset(int moveLimit)
        {
            MovesRemaining = Mathf.Max(0, moveLimit);
            MovesChanged?.Invoke(MovesRemaining);
        }

        public bool TryConsumeMove()
        {
            if (MovesRemaining <= 0)
            {
                return false;
            }

            MovesRemaining--;
            MovesChanged?.Invoke(MovesRemaining);
            return true;
        }

        public int ConsumeAllRemaining()
        {
            var moves = MovesRemaining;
            MovesRemaining = 0;
            MovesChanged?.Invoke(MovesRemaining);
            return moves;
        }
    }
}
