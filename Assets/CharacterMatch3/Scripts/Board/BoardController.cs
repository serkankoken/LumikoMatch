using System;
using System.Collections;
using CharacterMatch3.Core;
using UnityEngine;

namespace CharacterMatch3.Board
{
    public sealed class BoardController : MonoBehaviour
    {
        [SerializeField] private BoardView boardView;
        [SerializeField] private CharacterCatalog characterCatalog;
        [SerializeField] private ScoringConfig scoringConfig;

        private LevelDefinition level;
        private BoardModel model;
        private BoardCoordinate? selectedCoordinate;
        private System.Random shuffleRandom;

        public event Action ValidMoveMade;
        public event Action BoardSettled;
        public event Action<CharacterType, PieceKind, BoardCoordinate> PieceRemoved;
        public event Action<BoardCoordinate> SoftCoverLayerRemoved;
        public event Action<BoardCoordinate> CrateLayerRemoved;
        public event Action<BoardCoordinate> LockLayerRemoved;
        public event Action<BoardCoordinate> CompanionDelivered;
        public event Action<int, BoardCoordinate> ScoreAwarded;

        public Func<BoardCoordinate, BoardCoordinate, bool> SwapAllowedByTutorial;
        public bool IsInputLocked { get; private set; }

        public BoardModel Model => model;
        public BoardView View => boardView;

        public void Initialize(LevelDefinition levelDefinition, CharacterCatalog catalog, ScoringConfig scoring, BoardView view = null)
        {
            level = levelDefinition;
            characterCatalog = catalog;
            scoringConfig = scoring;
            if (view != null)
            {
                boardView = view;
            }

            if (boardView == null)
            {
                boardView = GetComponentInChildren<BoardView>();
            }

            if (boardView == null)
            {
                boardView = gameObject.AddComponent<BoardView>();
            }

            shuffleRandom = new System.Random(level.randomSeed + 719);
            RegenerateBoard();
        }

        public void RegenerateBoard()
        {
            if (level == null)
            {
                Debug.LogError("Character Match-3 board cannot generate without a LevelDefinition.");
                return;
            }

            var generator = new BoardGenerator(level);
            model = generator.Generate();
            selectedCoordinate = null;
            IsInputLocked = false;
            boardView.Build(this, model, characterCatalog);
        }

        public void SetInputLocked(bool locked)
        {
            IsInputLocked = locked;
            if (locked)
            {
                selectedCoordinate = null;
                boardView.SetSelected(null);
            }
        }

        public void HandleCellClicked(BoardCoordinate coordinate)
        {
            if (IsInputLocked || model == null)
            {
                return;
            }

            if (!selectedCoordinate.HasValue)
            {
                SelectIfSwappable(coordinate);
                return;
            }

            var selected = selectedCoordinate.Value;
            if (selected == coordinate)
            {
                selectedCoordinate = null;
                boardView.SetSelected(null);
                return;
            }

            if (model.AreAdjacent(selected, coordinate))
            {
                StartCoroutine(TrySwap(selected, coordinate));
                return;
            }

            SelectIfSwappable(coordinate);
        }

        public void HandleCellDragged(BoardCoordinate coordinate, Vector2Int direction)
        {
            if (IsInputLocked || model == null)
            {
                return;
            }

            if (selectedCoordinate.HasValue)
            {
                selectedCoordinate = null;
                boardView.SetSelected(null);
            }

            var target = new BoardCoordinate(coordinate.x + direction.x, coordinate.y + direction.y);
            StartCoroutine(TrySwap(coordinate, target));
        }

        private void SelectIfSwappable(BoardCoordinate coordinate)
        {
            selectedCoordinate = model.CanSwap(coordinate) ? coordinate : null;
            boardView.SetSelected(selectedCoordinate);
        }

        private IEnumerator TrySwap(BoardCoordinate from, BoardCoordinate to)
        {
            if (IsInputLocked || model == null)
            {
                yield break;
            }

            if (SwapAllowedByTutorial != null && !SwapAllowedByTutorial(from, to))
            {
                IsInputLocked = true;
                selectedCoordinate = null;
                boardView.SetSelected(null);
                yield return boardView.AnimateSwap(from, to, false);
                IsInputLocked = false;
                HapticsManager.Medium();
                yield break;
            }

            if (!model.CanSwap(from, to))
            {
                IsInputLocked = true;
                selectedCoordinate = null;
                boardView.SetSelected(null);
                yield return boardView.AnimateSwap(from, to, false);
                IsInputLocked = false;
                AudioManager.Instance?.Play(AudioManager.Instance.invalidSwap);
                HapticsManager.Medium();
                yield break;
            }

            IsInputLocked = true;
            selectedCoordinate = null;
            boardView.SetSelected(null);

            model.SwapPieces(from, to);
            var isSpecialSwap = BoardResolver.IsSpecialSwap(model, from, to);
            var isValid = isSpecialSwap || MatchFinder.HasAnyMatch(model);

            if (!isValid)
            {
                model.SwapPieces(from, to);
                yield return boardView.AnimateSwap(from, to, false);
                IsInputLocked = false;
                AudioManager.Instance?.Play(AudioManager.Instance.invalidSwap);
                HapticsManager.Medium();
                yield break;
            }

            ValidMoveMade?.Invoke();
            AudioManager.Instance?.Play(AudioManager.Instance.swap);
            HapticsManager.Light();
            yield return boardView.AnimateSwap(from, to, true);

            var preferred = to;
            if (isSpecialSwap)
            {
                boardView.CapturePieceLayout();
                var result = BoardResolver.ResolveSpecialSwap(model, from, to, level, scoringConfig, CreateResolutionEvents());
                if (result.Changed)
                {
                    AudioManager.Instance?.Play(AudioManager.Instance.cascade);
                    yield return boardView.AnimateBoardSettled();
                }
            }

            var cascadeIndex = 0;
            while (true)
            {
                boardView.CapturePieceLayout();
                var result = BoardResolver.ResolveCurrentMatches(model, preferred, level, scoringConfig, CreateResolutionEvents(), cascadeIndex);
                if (!result.Changed)
                {
                    break;
                }

                AudioManager.Instance?.Play(cascadeIndex == 0 ? AudioManager.Instance.normalMatch : AudioManager.Instance.cascade);
                yield return boardView.AnimateBoardSettled();
                cascadeIndex++;
                preferred = new BoardCoordinate(-1, -1);
            }

            if (level.reshufflingAllowed && !MoveFinder.HasLegalMove(model))
            {
                boardView.CapturePieceLayout();
                BoardShuffler.Shuffle(model, shuffleRandom, level.maximumAutomaticReshuffleAttempts);
                yield return boardView.AnimateBoardSettled();
            }

            IsInputLocked = false;
            BoardSettled?.Invoke();
        }

        private BoardResolutionEvents CreateResolutionEvents()
        {
            return new BoardResolutionEvents
            {
                PieceRemoved = (character, kind, coordinate) =>
                {
                    boardView.QueuePieceRemoved(character, kind, coordinate);
                    PieceRemoved?.Invoke(character, kind, coordinate);
                },
                SoftCoverLayerRemoved = coordinate =>
                {
                    boardView.QueueSoftCoverRemoved(coordinate);
                    SoftCoverLayerRemoved?.Invoke(coordinate);
                },
                CrateLayerRemoved = coordinate =>
                {
                    boardView.QueueBlockerHit(coordinate);
                    CrateLayerRemoved?.Invoke(coordinate);
                    AudioManager.Instance?.Play(AudioManager.Instance.blockerBreak);
                    HapticsManager.Medium();
                },
                LockLayerRemoved = coordinate =>
                {
                    boardView.QueueBlockerHit(coordinate);
                    LockLayerRemoved?.Invoke(coordinate);
                    HapticsManager.Medium();
                },
                CompanionDelivered = coordinate =>
                {
                    boardView.QueueCompanionDelivered(coordinate);
                    CompanionDelivered?.Invoke(coordinate);
                    AudioManager.Instance?.Play(AudioManager.Instance.tokenDelivered);
                    HapticsManager.Heavy();
                },
                ScoreAwarded = (amount, coordinate) =>
                {
                    boardView.QueueScorePopup(amount, coordinate);
                    ScoreAwarded?.Invoke(amount, coordinate);
                },
                SpecialCreated = (coordinate, kind) =>
                {
                    boardView.QueueSpecialCreated(coordinate, kind);
                    HapticsManager.Medium();
                },
                SpecialActivated = (coordinate, kind) =>
                {
                    boardView.QueueSpecialActivated(coordinate, kind);
                    HapticsManager.Heavy();
                    if (kind == PieceKind.Line)
                    {
                        AudioManager.Instance?.Play(AudioManager.Instance.lineClear);
                    }
                    else if (kind == PieceKind.Burst)
                    {
                        AudioManager.Instance?.Play(AudioManager.Instance.burst);
                    }
                    else if (kind == PieceKind.Rainbow)
                    {
                        AudioManager.Instance?.Play(AudioManager.Instance.rainbowActivation);
                    }
                }
            };
        }
    }
}
