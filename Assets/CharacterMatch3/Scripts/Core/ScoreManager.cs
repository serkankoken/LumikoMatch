using System;

namespace CharacterMatch3.Core
{
    public sealed class ScoreManager
    {
        public int Score { get; private set; }
        public event Action<int> ScoreChanged;

        public void Reset()
        {
            Score = 0;
            ScoreChanged?.Invoke(Score);
        }

        public void AddScore(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Score += amount;
            ScoreChanged?.Invoke(Score);
        }
    }
}
