using System.Collections;
using System.Collections.Generic;
using CharacterMatch3.Input;
using CharacterMatch3.UI;
using UnityEngine;
using UnityEngine.UI;

namespace CharacterMatch3.Board
{
    public sealed class BoardView : MonoBehaviour
    {
        private const float CandyTextDuration = 0.86f;
        private const float CandyTextRise = 106f;
        private const float CandyTextSideDrift = 18f;

        [SerializeField] private RectTransform boardRoot;
        [Header("Input")]
        [SerializeField] private Match3InputSettings inputSettings;
        [SerializeField, Min(1f)] private float minimumSwipeDistance = 48f;
        [SerializeField, Min(1f)] private float maximumTapMovementTolerance = 24f;
        [SerializeField, Min(0.01f)] private float maximumTapDuration = 0.3f;
        [SerializeField, Min(0.1f)] private float inputSensitivity = 1f;
        [SerializeField] private bool enableTapToSelect = true;

        private readonly Dictionary<BoardCoordinate, BoardCellView> cellViews = new Dictionary<BoardCoordinate, BoardCellView>();
        private readonly List<BoardVisualEffect> pendingPreRefreshEffects = new List<BoardVisualEffect>();
        private readonly List<BoardVisualEffect> pendingPostRefreshEffects = new List<BoardVisualEffect>();
        private readonly Dictionary<BoardCoordinate, int> pendingScorePopups = new Dictionary<BoardCoordinate, int>();
        private readonly Dictionary<int, BoardCoordinate> capturedPieceCoordinates = new Dictionary<int, BoardCoordinate>();
        private BoardController controller;
        private BoardModel model;
        private CharacterCatalog catalog;
        private BoardCoordinate? selectedCoordinate;
        private RectTransform effectsRoot;

        private enum BoardVisualEffectType
        {
            PieceRemoved,
            SoftCoverBroken,
            CrateDamaged,
            BlockerHit,
            SpecialCreated,
            SpecialActivated,
            CompanionDelivered
        }

        private readonly struct BoardVisualEffect
        {
            public readonly BoardVisualEffectType Type;
            public readonly BoardCoordinate Coordinate;
            public readonly Color Color;
            public readonly PieceKind PieceKind;
            public readonly LineOrientation LineOrientation;

            public BoardVisualEffect(
                BoardVisualEffectType type,
                BoardCoordinate coordinate,
                Color color,
                PieceKind pieceKind = PieceKind.Normal,
                LineOrientation lineOrientation = LineOrientation.Horizontal)
            {
                Type = type;
                Coordinate = coordinate;
                Color = color;
                PieceKind = pieceKind;
                LineOrientation = lineOrientation;
            }
        }

        private readonly struct PieceSettleAnimation
        {
            public readonly BoardCellView View;
            public readonly Vector2 FromOffset;
            public readonly float Delay;
            public readonly float Duration;
            public readonly bool FadeIn;

            public PieceSettleAnimation(BoardCellView view, Vector2 fromOffset, float delay, float duration, bool fadeIn)
            {
                View = view;
                FromOffset = fromOffset;
                Delay = delay;
                Duration = duration;
                FadeIn = fadeIn;
            }
        }

        public RectTransform BoardRoot => boardRoot;
        public float MinimumSwipeDistance => inputSettings != null ? inputSettings.MinimumSwipeDistance : Mathf.Max(1f, minimumSwipeDistance);
        public float MaximumTapMovementTolerance => inputSettings != null ? inputSettings.MaximumTapMovementTolerance : Mathf.Max(1f, maximumTapMovementTolerance);
        public float MaximumTapDuration => inputSettings != null ? inputSettings.MaximumTapDuration : Mathf.Max(0.01f, maximumTapDuration);
        public float InputSensitivity => inputSettings != null ? inputSettings.InputSensitivity : Mathf.Max(0.1f, inputSensitivity);
        public bool EnableTapToSelect => inputSettings != null ? inputSettings.EnableTapToSelect : enableTapToSelect;

        public void SetInputSettings(Match3InputSettings settings)
        {
            inputSettings = settings;
        }

        public void Build(BoardController owner, BoardModel boardModel, CharacterCatalog characterCatalog)
        {
            controller = owner;
            model = boardModel;
            catalog = characterCatalog;
            EnsureRoot();
            EnsureEffectsRoot();

            for (var i = boardRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(boardRoot.GetChild(i).gameObject);
            }

            ClearEffectsRoot();
            capturedPieceCoordinates.Clear();
            cellViews.Clear();
            var grid = boardRoot.GetComponent<GridLayoutGroup>();
            if (grid == null)
            {
                grid = boardRoot.gameObject.AddComponent<GridLayoutGroup>();
            }

            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = model.Width;
            grid.spacing = new Vector2(8, 8);
            grid.childAlignment = TextAnchor.MiddleCenter;
            var side = Mathf.Min(900f / model.Width, 900f / model.Height);
            grid.cellSize = new Vector2(side, side);

            for (var y = model.Height - 1; y >= 0; y--)
            {
                for (var x = 0; x < model.Width; x++)
                {
                    var cellObject = new GameObject($"Cell_{x}_{y}", typeof(RectTransform), typeof(Image), typeof(BoardCellView));
                    cellObject.transform.SetParent(boardRoot, false);
                    var view = cellObject.GetComponent<BoardCellView>();
                    var coordinate = new BoardCoordinate(x, y);
                    view.Initialize(this, coordinate);
                    cellViews[coordinate] = view;
                }
            }

            RefreshAll();
        }

        public void RefreshAll()
        {
            if (model == null)
            {
                return;
            }

            foreach (var pair in cellViews)
            {
                pair.Value.Refresh(model.GetCell(pair.Key), catalog, selectedCoordinate.HasValue && selectedCoordinate.Value == pair.Key);
            }
        }

        public void SetSelected(BoardCoordinate? coordinate)
        {
            selectedCoordinate = coordinate;
            RefreshAll();
        }

        public void CapturePieceLayout()
        {
            capturedPieceCoordinates.Clear();
            if (model == null)
            {
                return;
            }

            foreach (var coordinate in model.ActiveCoordinates())
            {
                var piece = model.GetCell(coordinate)?.Piece;
                if (piece != null)
                {
                    capturedPieceCoordinates[piece.Id] = coordinate;
                }
            }
        }

        public void QueuePieceRemoved(CharacterType character, PieceKind kind, BoardCoordinate coordinate)
        {
            pendingPreRefreshEffects.Add(new BoardVisualEffect(BoardVisualEffectType.PieceRemoved, coordinate, GetCharacterColor(character), kind));
        }

        public void QueueBlockerHit(BoardCoordinate coordinate)
        {
            pendingPreRefreshEffects.Add(new BoardVisualEffect(BoardVisualEffectType.BlockerHit, coordinate, new Color(1f, 0.82f, 0.34f)));
        }

        public void QueueCrateDamaged(BoardCoordinate coordinate)
        {
            pendingPreRefreshEffects.Add(new BoardVisualEffect(BoardVisualEffectType.CrateDamaged, coordinate, new Color(1f, 0.64f, 0.22f)));
        }

        public void QueueSoftCoverRemoved(BoardCoordinate coordinate)
        {
            pendingPreRefreshEffects.Add(new BoardVisualEffect(BoardVisualEffectType.SoftCoverBroken, coordinate, new Color(0.65f, 0.93f, 1f)));
        }

        public void QueueCompanionDelivered(BoardCoordinate coordinate)
        {
            pendingPreRefreshEffects.Add(new BoardVisualEffect(BoardVisualEffectType.CompanionDelivered, coordinate, new Color(1f, 0.92f, 0.42f), PieceKind.Companion));
        }

        public void QueueSpecialActivated(BoardCoordinate coordinate, PieceKind kind, LineOrientation lineOrientation)
        {
            pendingPreRefreshEffects.Add(new BoardVisualEffect(BoardVisualEffectType.SpecialActivated, coordinate, GetSpecialColor(kind), kind, lineOrientation));
        }

        public void QueueSpecialCreated(BoardCoordinate coordinate, PieceKind kind, LineOrientation lineOrientation)
        {
            pendingPostRefreshEffects.Add(new BoardVisualEffect(BoardVisualEffectType.SpecialCreated, coordinate, GetSpecialColor(kind), kind, lineOrientation));
        }

        public void QueueScorePopup(int amount, BoardCoordinate coordinate)
        {
            if (amount <= 0)
            {
                return;
            }

            if (pendingScorePopups.ContainsKey(coordinate))
            {
                pendingScorePopups[coordinate] += amount;
            }
            else
            {
                pendingScorePopups[coordinate] = amount;
            }
        }

        public IEnumerator AnimateSwap(BoardCoordinate a, BoardCoordinate b, bool valid)
        {
            var fromView = GetCellView(a);
            var toView = GetCellView(b);
            var offset = GetCellOffset(a, b);

            if (fromView == null || offset == Vector2.zero)
            {
                yield return new WaitForSeconds(valid ? 0.12f : 0.2f);
                RefreshAll();
                yield break;
            }

            if (valid && toView != null)
            {
                yield return AnimateTwoPieceSwap(fromView, toView, offset, 0.18f);
            }
            else if (toView != null)
            {
                yield return AnimateRejectedPairSwap(fromView, toView, offset * 0.34f, 0.2f);
            }
            else
            {
                yield return AnimateRejectedSwap(fromView, offset * 0.34f, 0.2f);
            }

            RefreshAll();
        }

        public IEnumerator AnimateBoardSettled()
        {
            var preRefreshEffects = new List<BoardVisualEffect>(pendingPreRefreshEffects);
            var postRefreshEffects = new List<BoardVisualEffect>(pendingPostRefreshEffects);
            var scorePopups = new Dictionary<BoardCoordinate, int>(pendingScorePopups);

            pendingPreRefreshEffects.Clear();
            pendingPostRefreshEffects.Clear();
            pendingScorePopups.Clear();

            if (preRefreshEffects.Count > 0 || scorePopups.Count > 0)
            {
                yield return PlayPreRefreshEffects(preRefreshEffects, scorePopups);
            }
            else
            {
                yield return WaitUnscaled(0.05f);
            }

            RefreshAll();

            var specialCreatedCoordinates = new HashSet<BoardCoordinate>();
            foreach (var effect in postRefreshEffects)
            {
                if (effect.Type == BoardVisualEffectType.SpecialCreated)
                {
                    specialCreatedCoordinates.Add(effect.Coordinate);
                }
            }

            yield return PlayPieceSettleAnimations(specialCreatedCoordinates);

            if (postRefreshEffects.Count > 0)
            {
                yield return PlayPostRefreshEffects(postRefreshEffects);
            }
            else
            {
                yield return WaitUnscaled(0.04f);
            }
        }

        public void CellClicked(BoardCoordinate coordinate)
        {
            controller?.HandleCellClicked(coordinate);
        }

        public void CellSwiped(BoardCoordinate coordinate, Vector2Int direction)
        {
            controller?.HandleCellDragged(coordinate, direction);
        }

        public void CellDragged(BoardCoordinate coordinate, Vector2Int direction)
        {
            CellSwiped(coordinate, direction);
        }

        private void EnsureRoot()
        {
            if (boardRoot != null)
            {
                EnsureEffectsRoot();
                return;
            }

            var root = new GameObject("BoardRoot", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(transform, false);
            boardRoot = root.GetComponent<RectTransform>();
            boardRoot.sizeDelta = new Vector2(940, 940);
            root.GetComponent<Image>().color = new Color(0.15f, 0.28f, 0.38f, 0.32f);
            EnsureEffectsRoot();
        }

        private void EnsureEffectsRoot()
        {
            if (boardRoot == null)
            {
                return;
            }

            if (effectsRoot == null)
            {
                var effectsObject = new GameObject("BoardEffects", typeof(RectTransform), typeof(CanvasGroup));
                effectsObject.transform.SetParent(boardRoot.parent, false);
                effectsRoot = effectsObject.GetComponent<RectTransform>();
                var group = effectsObject.GetComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;
            }

            effectsRoot.anchorMin = boardRoot.anchorMin;
            effectsRoot.anchorMax = boardRoot.anchorMax;
            effectsRoot.pivot = boardRoot.pivot;
            effectsRoot.anchoredPosition = boardRoot.anchoredPosition;
            effectsRoot.sizeDelta = boardRoot.sizeDelta;
            effectsRoot.SetAsLastSibling();
        }

        private void ClearEffectsRoot()
        {
            if (effectsRoot == null)
            {
                return;
            }

            for (var i = effectsRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(effectsRoot.GetChild(i).gameObject);
            }
        }

        private IEnumerator PlayPreRefreshEffects(List<BoardVisualEffect> effects, Dictionary<BoardCoordinate, int> scorePopups)
        {
            EnsureEffectsRoot();
            if (effects.Count >= 4)
            {
                StartCoroutine(AnimateBoardResonance(effects));
            }

            foreach (var effect in effects)
            {
                StartCoroutine(PlayVisualEffect(effect, false));
            }

            foreach (var pair in scorePopups)
            {
                StartCoroutine(AnimateScorePopup(pair.Key, pair.Value));
            }

            yield return WaitUnscaled(0.34f);
        }

        private IEnumerator PlayPostRefreshEffects(List<BoardVisualEffect> effects)
        {
            EnsureEffectsRoot();
            foreach (var effect in effects)
            {
                StartCoroutine(PlayVisualEffect(effect, true));
            }

            yield return WaitUnscaled(0.26f);
        }

        private IEnumerator PlayPieceSettleAnimations(HashSet<BoardCoordinate> skipCoordinates)
        {
            if (model == null || capturedPieceCoordinates.Count == 0)
            {
                yield break;
            }

            var animations = new List<PieceSettleAnimation>();
            var cellSize = GetEffectCellSize();
            var spawnTopY = effectsRoot != null
                ? effectsRoot.rect.yMax + cellSize.y * 0.75f
                : cellSize.y * 2f;

            foreach (var coordinate in model.ActiveCoordinates())
            {
                if (skipCoordinates.Contains(coordinate))
                {
                    continue;
                }

                var cell = model.GetCell(coordinate);
                var piece = cell?.Piece;
                var view = GetCellView(coordinate);
                if (piece == null || view == null)
                {
                    continue;
                }

                var currentPosition = GetEffectPosition(coordinate);
                Vector2 fromOffset;
                float duration;
                float delay;
                var fadeIn = false;

                if (capturedPieceCoordinates.TryGetValue(piece.Id, out var previousCoordinate))
                {
                    if (previousCoordinate.Equals(coordinate))
                    {
                        continue;
                    }

                    var previousPosition = GetEffectPosition(previousCoordinate);
                    fromOffset = previousPosition - currentPosition;
                    duration = Mathf.Clamp(0.2f + fromOffset.magnitude / 1350f, 0.22f, 0.38f);
                    delay = Mathf.Clamp((model.Height - coordinate.y) * 0.012f, 0f, 0.09f);
                }
                else
                {
                    var offsetY = Mathf.Max(cellSize.y * 0.9f, spawnTopY - currentPosition.y);
                    fromOffset = new Vector2(0f, offsetY);
                    duration = Mathf.Clamp(0.24f + offsetY / 1950f, 0.26f, 0.42f);
                    delay = Mathf.Clamp((model.Height - 1 - coordinate.y) * 0.018f, 0f, 0.14f);
                    fadeIn = true;
                }

                animations.Add(new PieceSettleAnimation(view, fromOffset, delay, duration, fadeIn));
            }

            capturedPieceCoordinates.Clear();
            if (animations.Count == 0)
            {
                yield break;
            }

            var wait = 0f;
            foreach (var animation in animations)
            {
                animation.View.SetPieceOffset(animation.FromOffset);
                animation.View.SetPieceScale(animation.FadeIn ? 0.96f : 1f);
                animation.View.SetPieceAlpha(animation.FadeIn ? 0.35f : 1f);
                wait = Mathf.Max(wait, animation.Delay + animation.Duration);
                StartCoroutine(AnimatePieceSettle(animation));
            }

            yield return WaitUnscaled(wait + 0.02f);
        }

        private IEnumerator AnimatePieceSettle(PieceSettleAnimation animation)
        {
            if (animation.Delay > 0f)
            {
                yield return WaitUnscaled(animation.Delay);
            }

            for (var elapsed = 0f; elapsed < animation.Duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / animation.Duration);
                var eased = EaseOutCubic(t);
                var offset = Vector2.Lerp(animation.FromOffset, Vector2.zero, eased);
                if (t > 0.72f)
                {
                    var landingT = (t - 0.72f) / 0.28f;
                    offset.y -= Mathf.Sin(landingT * Mathf.PI) * 5f;
                }

                var landingPulse = t > 0.68f ? Mathf.Sin((t - 0.68f) / 0.32f * Mathf.PI) * 0.035f : 0f;
                animation.View.SetPieceOffset(offset);
                animation.View.SetPieceScale(Mathf.Lerp(animation.FadeIn ? 0.96f : 1f, 1f, EaseOutCubic(t)) + landingPulse);
                animation.View.SetPieceAlpha(animation.FadeIn ? Mathf.Lerp(0.35f, 1f, EaseOutCubic(t)) : 1f);
                yield return null;
            }

            animation.View.ResetPieceVisualState();
        }

        private IEnumerator PlayVisualEffect(BoardVisualEffect effect, bool afterRefresh)
        {
            var view = GetCellView(effect.Coordinate);
            switch (effect.Type)
            {
                case BoardVisualEffectType.PieceRemoved:
                    SpawnBurst(effect.Coordinate, effect.Color, effect.PieceKind == PieceKind.Rainbow ? 24 : 14, effect.PieceKind == PieceKind.Normal ? 98f : 118f, effect.PieceKind, 0.42f);
                    StartCoroutine(AnimateMatchSnap(effect.Coordinate, effect.Color, effect.PieceKind, effect.PieceKind != PieceKind.Normal));
                    StartCoroutine(AnimateExplosionRound(effect.Coordinate, effect.Color, effect.PieceKind, effect.PieceKind == PieceKind.Normal ? 0.74f : 0.98f, 0.28f));
                    StartCoroutine(AnimateMiniShockwave(effect.Coordinate, effect.Color, effect.PieceKind == PieceKind.Normal ? 0.72f : 1.02f));
                    StartCoroutine(AnimateToonRing(effect.Coordinate, effect.Color, 0.3f, 0.38f, effect.PieceKind == PieceKind.Normal ? 1.18f : 1.42f, 0.2f, false));
                    StartCoroutine(AnimatePopImpact(effect.Coordinate, effect.Color, effect.PieceKind, false));
                    if (view != null)
                    {
                        yield return AnimatePiecePop(view, 0.32f);
                    }

                    break;
                case BoardVisualEffectType.SoftCoverBroken:
                    SpawnBurst(effect.Coordinate, effect.Color, 10, 62f, effect.PieceKind, 0.38f);
                    yield return AnimateSoftCoverBreak(effect.Coordinate, effect.Color, 0.34f);
                    break;
                case BoardVisualEffectType.CrateDamaged:
                    SpawnBurst(effect.Coordinate, effect.Color, 16, 86f, effect.PieceKind, 0.42f);
                    StartCoroutine(AnimatePopImpact(effect.Coordinate, effect.Color, PieceKind.Normal, false));
                    StartCoroutine(AnimateCrateBreak(effect.Coordinate, effect.Color, 0.58f));
                    if (view != null)
                    {
                        yield return AnimatePieceShake(view, 0.22f, 18f);
                    }

                    break;
                case BoardVisualEffectType.BlockerHit:
                    SpawnBurst(effect.Coordinate, effect.Color, 8, 52f, effect.PieceKind, 0.32f);
                    StartCoroutine(AnimatePopImpact(effect.Coordinate, effect.Color, PieceKind.Normal, false));
                    if (view != null)
                    {
                        yield return AnimatePieceShake(view, 0.16f, 12f);
                    }

                    break;
                case BoardVisualEffectType.CompanionDelivered:
                    SpawnBurst(effect.Coordinate, effect.Color, 20, 112f, effect.PieceKind, 0.42f);
                    StartCoroutine(AnimateSparkleHalo(effect.Coordinate, effect.Color, 12, 82f, 0.46f, PieceKind.Companion, false));
                    StartCoroutine(AnimatePopImpact(effect.Coordinate, effect.Color, PieceKind.Companion, true));
                    StartCoroutine(AnimateCandyTextPopup(effect.Coordinate, "SAVED!", effect.Color, 0.03f));
                    if (view != null)
                    {
                        yield return AnimatePiecePop(view, 0.32f);
                    }

                    break;
                case BoardVisualEffectType.SpecialActivated:
                    SpawnBurst(effect.Coordinate, effect.Color, effect.PieceKind == PieceKind.Rainbow ? 34 : effect.PieceKind == PieceKind.Burst ? 28 : 22, effect.PieceKind == PieceKind.Rainbow ? 172f : effect.PieceKind == PieceKind.Burst ? 150f : 132f, effect.PieceKind, 0.5f);
                    StartCoroutine(AnimateToonRing(effect.Coordinate, effect.Color, 0.42f, 0.62f, effect.PieceKind == PieceKind.Rainbow ? 2.55f : effect.PieceKind == PieceKind.Burst ? 2.05f : 1.72f, 0.34f, false));
                    StartCoroutine(AnimateSparkleHalo(effect.Coordinate, effect.Color, effect.PieceKind == PieceKind.Rainbow ? 22 : 14, effect.PieceKind == PieceKind.Burst ? 118f : 100f, 0.52f, effect.PieceKind, false));
                    StartCoroutine(AnimateSpecialSignature(effect.Coordinate, effect.Color, effect.PieceKind, effect.LineOrientation));
                    StartCoroutine(AnimateExplosionRound(effect.Coordinate, effect.Color, effect.PieceKind, effect.PieceKind == PieceKind.Burst ? 1.64f : 1.12f, effect.PieceKind == PieceKind.Burst ? 0.42f : 0.34f));
                    StartCoroutine(AnimatePopImpact(effect.Coordinate, effect.Color, effect.PieceKind, true));
                    StartCoroutine(AnimateSpecialFlash(effect.Coordinate, effect.Color, effect.PieceKind, effect.LineOrientation));
                    StartCoroutine(AnimateCandyTextPopup(effect.Coordinate, GetSpecialActivatedText(effect.PieceKind), GetCandyTextColor(effect.PieceKind), 0.02f));
                    if (view != null)
                    {
                        yield return AnimatePiecePulse(view, 0.28f, effect.PieceKind == PieceKind.Rainbow ? 1.36f : effect.PieceKind == PieceKind.Burst ? 1.3f : 1.24f);
                    }

                    break;
                case BoardVisualEffectType.SpecialCreated:
                    if (afterRefresh)
                    {
                        SpawnBurst(effect.Coordinate, effect.Color, 18, 82f, effect.PieceKind, 0.46f);
                        StartCoroutine(AnimateSpecialCreatedGleam(effect.Coordinate, effect.Color, effect.PieceKind, effect.LineOrientation));
                        if (effect.PieceKind == PieceKind.Burst)
                        {
                            StartCoroutine(AnimateExplosionRound(effect.Coordinate, effect.Color, effect.PieceKind, 1.18f, 0.34f));
                        }

                        StartCoroutine(AnimateToonRing(effect.Coordinate, effect.Color, 0.42f, 1.45f, 0.72f, 0.28f, true));
                        StartCoroutine(AnimateSparkleHalo(effect.Coordinate, effect.Color, 10, 76f, 0.46f, effect.PieceKind, true));
                        StartCoroutine(AnimatePopImpact(effect.Coordinate, effect.Color, effect.PieceKind, false));
                        StartCoroutine(AnimateCellFlash(effect.Coordinate, effect.Color, 0.3f, 0.55f, 1.18f));
                        StartCoroutine(AnimateCandyTextPopup(effect.Coordinate, GetSpecialCreatedText(effect.PieceKind), GetCandyTextColor(effect.PieceKind), 0.04f));
                        if (view != null)
                        {
                            yield return AnimatePieceSpawn(view, 0.3f);
                        }
                    }

                    break;
            }
        }

        private IEnumerator AnimatePiecePop(BoardCellView view, float duration)
        {
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                var scale = t < 0.36f
                    ? Mathf.Lerp(1f, 1.2f, EaseOutCubic(t / 0.36f))
                    : Mathf.Lerp(1.2f, 0.12f, EaseInCubic((t - 0.36f) / 0.64f));
                view.SetPieceScale(scale);
                view.SetPieceAlpha(1f - EaseInCubic(t));
                yield return null;
            }

            view.SetPieceScale(0.12f);
            view.SetPieceAlpha(0f);
        }

        private IEnumerator AnimatePiecePulse(BoardCellView view, float duration, float peakScale)
        {
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                var scale = t < 0.5f
                    ? Mathf.Lerp(1f, peakScale, EaseOutCubic(t * 2f))
                    : Mathf.Lerp(peakScale, 1f, EaseInOut((t - 0.5f) * 2f));
                view.SetPieceScale(scale);
                yield return null;
            }

            view.SetPieceScale(1f);
        }

        private IEnumerator AnimatePieceSpawn(BoardCellView view, float duration)
        {
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                var scale = t < 0.65f
                    ? Mathf.Lerp(0.6f, 1.18f, EaseOutBack(t / 0.65f))
                    : Mathf.Lerp(1.18f, 1f, EaseInOut((t - 0.65f) / 0.35f));
                view.SetPieceScale(scale);
                view.SetPieceAlpha(EaseOutCubic(t));
                yield return null;
            }

            view.ResetPieceVisualState();
        }

        private IEnumerator AnimatePieceShake(BoardCellView view, float duration, float amplitude)
        {
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                var damping = 1f - t;
                var offset = new Vector2(Mathf.Sin(t * Mathf.PI * 6f) * amplitude * damping, 0f);
                view.SetPieceOffset(offset);
                view.SetPieceScale(Mathf.Lerp(1.08f, 1f, t));
                yield return null;
            }

            view.ResetPieceVisualState();
        }

        private BoardCellView GetCellView(BoardCoordinate coordinate)
        {
            return cellViews.TryGetValue(coordinate, out var view) ? view : null;
        }

        private Vector2 GetCellOffset(BoardCoordinate from, BoardCoordinate to)
        {
            var fromView = GetCellView(from);
            var toView = GetCellView(to);
            if (fromView != null && toView != null)
            {
                var fromRect = (RectTransform)fromView.transform;
                var toRect = (RectTransform)toView.transform;
                return toRect.anchoredPosition - fromRect.anchoredPosition;
            }

            var grid = boardRoot != null ? boardRoot.GetComponent<GridLayoutGroup>() : null;
            var step = grid != null ? grid.cellSize + grid.spacing : new Vector2(120f, 120f);
            return new Vector2((to.x - from.x) * step.x, (to.y - from.y) * step.y);
        }

        private Vector2 GetEffectPosition(BoardCoordinate coordinate)
        {
            var view = GetCellView(coordinate);
            if (view == null || effectsRoot == null)
            {
                return Vector2.zero;
            }

            var cellRect = (RectTransform)view.transform;
            var worldCenter = cellRect.TransformPoint(cellRect.rect.center);
            var localCenter = effectsRoot.InverseTransformPoint(worldCenter);
            return new Vector2(localCenter.x, localCenter.y);
        }

        private Vector2 GetEffectCellSize()
        {
            var grid = boardRoot != null ? boardRoot.GetComponent<GridLayoutGroup>() : null;
            return grid != null ? grid.cellSize : new Vector2(112f, 112f);
        }

        private Color GetCharacterColor(CharacterType character)
        {
            return catalog != null ? catalog.GetFallbackColor(character) : Color.white;
        }

        private static Color GetSpecialColor(PieceKind kind)
        {
            return kind switch
            {
                PieceKind.Line => new Color(0.38f, 0.88f, 1f),
                PieceKind.Burst => new Color(1f, 0.48f, 0.22f),
                PieceKind.Rainbow => new Color(1f, 0.9f, 0.18f),
                PieceKind.Companion => new Color(1f, 0.84f, 0.32f),
                _ => new Color(1f, 0.95f, 0.65f)
            };
        }

        private void SpawnBurst(BoardCoordinate coordinate, Color color, int particleCount, float radius, PieceKind kind = PieceKind.Normal, float duration = 0.38f)
        {
            if (effectsRoot == null)
            {
                return;
            }

            StartCoroutine(AnimateBurst(coordinate, color, particleCount, radius, duration, kind));
        }

        private IEnumerator AnimateBurst(BoardCoordinate coordinate, Color color, int particleCount, float radius, float duration, PieceKind kind)
        {
            var root = new GameObject("Burst", typeof(RectTransform));
            root.transform.SetParent(effectsRoot, false);
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = GetEffectPosition(coordinate);
            rootRect.sizeDelta = Vector2.zero;

            var particles = new RectTransform[particleCount];
            var graphics = new Graphic[particleCount];
            var particleWeights = new float[particleCount];
            for (var i = 0; i < particleCount; i++)
            {
                var texture = GetBurstTexture(kind, i);
                var graphic = CreateEffectGraphic($"Particle_{i}", rootRect, texture, ShiftColor(color, i));
                var rect = graphic.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                var size = Mathf.Lerp(16f, kind == PieceKind.Rainbow ? 34f : 26f, (i % 4) / 3f);
                var rectangular = texture != null && (texture == catalog?.ToonConfettiTexture || texture == catalog?.ToonLineTexture);
                rect.sizeDelta = rectangular ? new Vector2(size * 0.55f, size * 1.55f) : Vector2.one * size;
                rect.localRotation = Quaternion.Euler(0f, 0f, i * 37f);

                particles[i] = rect;
                graphics[i] = graphic;
                particleWeights[i] = Mathf.Lerp(0.52f, 1.08f, ((i * 17) % 11) / 10f);
            }

            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                var moveT = EaseOutCubic(t);
                var fade = 1f - EaseInCubic(t);
                for (var i = 0; i < particles.Length; i++)
                {
                    var angle = (i / (float)particles.Length) * Mathf.PI * 2f + 0.35f;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    var tangent = new Vector2(-direction.y, direction.x);
                    var drift = direction * radius * particleWeights[i];
                    var swirl = tangent * radius * 0.12f * Mathf.Sin(t * Mathf.PI) * (i % 2 == 0 ? 1f : -1f);
                    particles[i].anchoredPosition = drift * moveT + swirl;
                    particles[i].localScale = Vector3.one * Mathf.Lerp(1.15f, 0.16f, EaseInCubic(t));
                    particles[i].localRotation = Quaternion.Euler(0f, 0f, i * 37f + Mathf.Lerp(0f, 260f, EaseOutCubic(t)));
                    var particleColor = graphics[i].color;
                    particleColor.a = fade;
                    graphics[i].color = particleColor;
                }

                yield return null;
            }

            Destroy(root);
        }

        private IEnumerator AnimateSparkleHalo(BoardCoordinate coordinate, Color color, int particleCount, float radius, float duration, PieceKind kind, bool inward)
        {
            if (effectsRoot == null)
            {
                yield break;
            }

            var root = new GameObject(inward ? "SparkleInhale" : "SparkleHalo", typeof(RectTransform));
            root.transform.SetParent(effectsRoot, false);
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = GetEffectPosition(coordinate);
            rootRect.sizeDelta = Vector2.zero;

            var particles = new RectTransform[particleCount];
            var graphics = new Graphic[particleCount];
            for (var i = 0; i < particleCount; i++)
            {
                var texture = GetSparkleTexture(kind, i);
                var graphic = CreateEffectGraphic($"Sparkle_{i}", rootRect, texture, ShiftColor(color, i + 5));
                var rect = graphic.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = Vector2.one * Mathf.Lerp(18f, kind == PieceKind.Rainbow ? 38f : 28f, (i % 3) / 2f);
                particles[i] = rect;
                graphics[i] = graphic;
            }

            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = inward ? EaseInOut(t) : EaseOutCubic(t);
                var fadeIn = Mathf.Clamp01(t / 0.18f);
                var fadeOut = 1f - EaseInCubic(Mathf.Clamp01((t - 0.18f) / 0.82f));
                var alpha = Mathf.Min(fadeIn, fadeOut);
                for (var i = 0; i < particles.Length; i++)
                {
                    var phase = (i / (float)particles.Length) * Mathf.PI * 2f;
                    var spin = phase + (inward ? -1f : 1f) * t * Mathf.PI * (kind == PieceKind.Rainbow ? 0.72f : 0.42f);
                    var currentRadius = inward
                        ? Mathf.Lerp(radius, 10f, eased)
                        : Mathf.Lerp(radius * 0.24f, radius, eased);
                    var direction = new Vector2(Mathf.Cos(spin), Mathf.Sin(spin));
                    particles[i].anchoredPosition = direction * currentRadius;
                    particles[i].localScale = Vector3.one * Mathf.Lerp(inward ? 1.05f : 0.55f, inward ? 0.24f : 1.1f, eased);
                    particles[i].localRotation = Quaternion.Euler(0f, 0f, Mathf.Rad2Deg * spin + 180f * t);
                    var graphicColor = graphics[i].color;
                    graphicColor.a = alpha * (inward ? 0.82f : 0.9f);
                    graphics[i].color = graphicColor;
                }

                yield return null;
            }

            Destroy(root);
        }

        private IEnumerator AnimateToonRing(BoardCoordinate coordinate, Color color, float duration, float startScale, float endScale, float peakAlpha, bool inward)
        {
            if (effectsRoot == null)
            {
                yield break;
            }

            var texture = GetFirstAvailableTexture(catalog?.ToonRingTexture, catalog?.ToonGlowTexture, catalog?.ToonAuraTexture);
            var graphic = CreateEffectGraphic(inward ? "ToonInhaleRing" : "ToonRing", effectsRoot, texture, new Color(color.r, color.g, color.b, peakAlpha));
            var rect = graphic.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = GetEffectPosition(coordinate);
            rect.sizeDelta = GetEffectCellSize() * 1.22f;

            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                var scale = inward
                    ? Mathf.Lerp(startScale, endScale, EaseInOut(t))
                    : Mathf.Lerp(startScale, endScale, EaseOutCubic(t));
                rect.localScale = Vector3.one * scale;
                rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, inward ? -68f : 82f, EaseOutCubic(t)));
                var graphicColor = graphic.color;
                var fadeOut = inward ? EaseOutCubic(t) : EaseInCubic(t);
                graphicColor.a = Mathf.Lerp(peakAlpha, 0f, fadeOut);
                graphic.color = graphicColor;
                yield return null;
            }

            Destroy(graphic.gameObject);
        }

        private IEnumerator AnimatePopImpact(BoardCoordinate coordinate, Color color, PieceKind kind, bool strong)
        {
            if (effectsRoot == null)
            {
                yield break;
            }

            var root = new GameObject(strong ? "PopImpactStrong" : "PopImpact", typeof(RectTransform));
            root.transform.SetParent(effectsRoot, false);
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);

            var offsetSeed = coordinate.x * 31 + coordinate.y * 17 + (int)kind * 13;
            var start = GetEffectPosition(coordinate) + new Vector2((offsetSeed % 7 - 3) * 2.5f, 8f + (offsetSeed % 5) * 1.5f);
            var end = start + new Vector2((offsetSeed % 3 - 1) * 10f, strong ? 42f : 28f);
            rootRect.anchoredPosition = start;
            rootRect.sizeDelta = Vector2.zero;

            var cellSize = GetEffectCellSize();
            var baseScale = strong || kind == PieceKind.Rainbow
                ? 1.28f
                : kind == PieceKind.Line || kind == PieceKind.Burst || kind == PieceKind.Companion
                    ? 1.02f
                    : 0.74f;

            var glowTexture = GetFirstAvailableTexture(catalog?.ToonGlowTexture, catalog?.ToonAuraTexture);
            var glow = CreateEffectGraphic("PopGlow", rootRect, glowTexture, new Color(color.r, color.g, color.b, strong ? 0.26f : 0.14f));
            glow.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            glow.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            glow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            glow.rectTransform.sizeDelta = cellSize * baseScale * 1.28f;

            var popTexture = GetFirstAvailableTexture(catalog?.ToonPopTexture, catalog?.ToonAuraTexture, catalog?.ToonStarTexture);
            var popColor = Color.Lerp(Color.white, color, strong ? 0.42f : 0.25f);
            popColor.a = strong ? 0.96f : 0.78f;
            var pop = CreateEffectGraphic("PopTexture", rootRect, popTexture, popColor);
            var popRect = pop.rectTransform;
            popRect.anchorMin = new Vector2(0.5f, 0.5f);
            popRect.anchorMax = new Vector2(0.5f, 0.5f);
            popRect.pivot = new Vector2(0.5f, 0.5f);
            popRect.sizeDelta = cellSize * baseScale;
            popRect.localRotation = Quaternion.Euler(0f, 0f, (offsetSeed % 2 == 0 ? -1f : 1f) * Mathf.Lerp(8f, 18f, (offsetSeed % 4) / 3f));

            var duration = strong ? 0.36f : 0.28f;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                var popIn = Mathf.Clamp01(t / 0.42f);
                var popOut = Mathf.Clamp01((t - 0.22f) / 0.78f);
                var scale = t < 0.42f
                    ? Mathf.LerpUnclamped(0.18f, 1.1f, EaseOutBack(popIn))
                    : Mathf.Lerp(1.1f, 0.72f, EaseInCubic((t - 0.42f) / 0.58f));
                rootRect.anchoredPosition = Vector2.Lerp(start, end, EaseOutCubic(t));
                rootRect.localScale = Vector3.one * scale;
                rootRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, strong ? 7f : 4f, t) * (offsetSeed % 2 == 0 ? 1f : -1f));

                var popGraphicColor = pop.color;
                popGraphicColor.a = Mathf.Lerp(popColor.a, 0f, EaseInCubic(popOut));
                pop.color = popGraphicColor;

                var glowColor = glow.color;
                glowColor.a = Mathf.Lerp(strong ? 0.26f : 0.14f, 0f, EaseInCubic(t));
                glow.color = glowColor;
                glow.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.72f, 1.36f, EaseOutCubic(t));
                yield return null;
            }

            Destroy(root);
        }

        private IEnumerator AnimateMatchSnap(BoardCoordinate coordinate, Color color, PieceKind kind, bool strong)
        {
            if (effectsRoot == null)
            {
                yield break;
            }

            var root = new GameObject(strong ? "SpecialMatchSnap" : "MatchSnap", typeof(RectTransform));
            root.transform.SetParent(effectsRoot, false);
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = GetEffectPosition(coordinate);
            rootRect.sizeDelta = Vector2.zero;

            var cellSize = GetEffectCellSize();
            var flashTexture = GetFirstAvailableTexture(catalog?.ToonPopTexture, catalog?.ToonStarTexture, catalog?.ToonGlowTexture);
            var flashColor = Color.Lerp(Color.white, color, strong ? 0.32f : 0.2f);
            flashColor.a = strong ? 0.82f : 0.64f;
            var flash = CreateEffectGraphic("SnapFlash", rootRect, flashTexture, flashColor);
            flash.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            flash.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            flash.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            flash.rectTransform.sizeDelta = cellSize * (strong ? 0.92f : 0.72f);

            var ringTexture = GetFirstAvailableTexture(catalog?.ToonRingTexture, catalog?.ToonGlowTexture, catalog?.ToonAuraTexture);
            var ring = CreateEffectGraphic("SnapRing", rootRect, ringTexture, new Color(color.r, color.g, color.b, strong ? 0.28f : 0.18f));
            ring.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            ring.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            ring.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            ring.rectTransform.sizeDelta = cellSize * 1.04f;

            var crumbCount = strong ? 7 : 5;
            var crumbRects = new RectTransform[crumbCount];
            var crumbGraphics = new Graphic[crumbCount];
            for (var i = 0; i < crumbCount; i++)
            {
                var crumb = CreateEffectGraphic($"SnapCrumb_{i}", rootRect, GetSparkleTexture(kind, i), ShiftColor(color, i + 3));
                var rect = crumb.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = Vector2.one * Mathf.Lerp(9f, strong ? 18f : 14f, (i % 3) / 2f);
                crumbRects[i] = rect;
                crumbGraphics[i] = crumb;
            }

            var duration = strong ? 0.3f : 0.24f;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                var flashT = Mathf.Clamp01(t / 0.52f);
                flash.rectTransform.localScale = Vector3.one * Mathf.LerpUnclamped(0.18f, strong ? 1.18f : 1f, EaseOutBack(flashT));
                var flashGraphicColor = flash.color;
                flashGraphicColor.a = Mathf.Lerp(flashColor.a, 0f, EaseInCubic(t));
                flash.color = flashGraphicColor;

                ring.rectTransform.localScale = Vector3.one * Mathf.Lerp(strong ? 0.52f : 0.42f, strong ? 1.55f : 1.22f, EaseOutCubic(t));
                ring.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, strong ? 44f : 28f, t));
                var ringColor = ring.color;
                ringColor.a = Mathf.Lerp(strong ? 0.28f : 0.18f, 0f, EaseInCubic(t));
                ring.color = ringColor;

                for (var i = 0; i < crumbRects.Length; i++)
                {
                    var phase = (i / (float)crumbRects.Length) * Mathf.PI * 2f + 0.18f;
                    var direction = new Vector2(Mathf.Cos(phase), Mathf.Sin(phase));
                    var lift = new Vector2(0f, Mathf.Sin(t * Mathf.PI) * (strong ? 12f : 8f));
                    crumbRects[i].anchoredPosition = direction * Mathf.Lerp(6f, strong ? 54f : 38f, EaseOutCubic(t)) + lift;
                    crumbRects[i].localScale = Vector3.one * Mathf.Lerp(1.05f, 0.2f, EaseInCubic(t));
                    crumbRects[i].localRotation = Quaternion.Euler(0f, 0f, i * 45f + Mathf.Lerp(0f, 160f, t));
                    var crumbColor = crumbGraphics[i].color;
                    crumbColor.a = Mathf.Lerp(0.9f, 0f, EaseInCubic(t));
                    crumbGraphics[i].color = crumbColor;
                }

                yield return null;
            }

            Destroy(root);
        }

        private IEnumerator AnimateMiniShockwave(BoardCoordinate coordinate, Color color, float strength)
        {
            if (effectsRoot == null)
            {
                yield break;
            }

            strength = Mathf.Clamp(strength, 0.45f, 1.9f);
            var root = new GameObject(strength > 1.2f ? "MiniBombShockwave" : "MiniShockwave", typeof(RectTransform));
            root.transform.SetParent(effectsRoot, false);
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = GetEffectPosition(coordinate);
            rootRect.sizeDelta = Vector2.zero;

            var cellSize = GetEffectCellSize();
            var glowTexture = GetFirstAvailableTexture(catalog?.ToonGlowTexture, catalog?.ToonAuraTexture);
            var glow = CreateEffectGraphic("ShockGlow", rootRect, glowTexture, new Color(color.r, color.g, color.b, 0.18f * strength));
            glow.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            glow.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            glow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            glow.rectTransform.sizeDelta = cellSize * Mathf.Lerp(0.9f, 1.45f, strength / 1.9f);

            var ringTexture = GetFirstAvailableTexture(catalog?.ToonRingTexture, catalog?.ToonPopTexture, catalog?.ToonGlowTexture);
            var ring = CreateEffectGraphic("ShockRing", rootRect, ringTexture, new Color(color.r, color.g, color.b, 0.3f * strength));
            ring.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            ring.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            ring.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            ring.rectTransform.sizeDelta = cellSize * 1.08f;

            var coreTexture = GetFirstAvailableTexture(catalog?.ToonPopTexture, catalog?.ToonStarTexture, catalog?.ToonGlowTexture);
            var coreColor = Color.Lerp(Color.white, color, 0.22f);
            coreColor.a = 0.42f * strength;
            var core = CreateEffectGraphic("ShockCore", rootRect, coreTexture, coreColor);
            core.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            core.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            core.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            core.rectTransform.sizeDelta = cellSize * Mathf.Lerp(0.48f, 0.72f, strength / 1.9f);

            var duration = Mathf.Lerp(0.24f, 0.4f, strength / 1.9f);
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                core.rectTransform.localScale = Vector3.one * Mathf.LerpUnclamped(0.12f, 1.05f, EaseOutBack(Mathf.Clamp01(t / 0.42f)));
                ring.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.05f + strength * 0.38f, EaseOutCubic(t));
                ring.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 72f, t));
                glow.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.68f, 1.42f + strength * 0.16f, EaseOutCubic(t));

                var coreGraphicColor = core.color;
                coreGraphicColor.a = Mathf.Lerp(coreColor.a, 0f, EaseInCubic(t));
                core.color = coreGraphicColor;

                var ringColor = ring.color;
                ringColor.a = Mathf.Lerp(0.3f * strength, 0f, EaseInCubic(t));
                ring.color = ringColor;

                var glowColor = glow.color;
                glowColor.a = Mathf.Lerp(0.18f * strength, 0f, EaseInCubic(t));
                glow.color = glowColor;
                yield return null;
            }

            Destroy(root);
        }

        private IEnumerator AnimateExplosionRound(BoardCoordinate coordinate, Color color, PieceKind kind, float intensity, float duration)
        {
            if (effectsRoot == null)
            {
                yield break;
            }

            intensity = Mathf.Clamp(intensity, 0.5f, 2.2f);
            var root = new GameObject("ExplosionRound", typeof(RectTransform));
            root.transform.SetParent(effectsRoot, false);
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = GetEffectPosition(coordinate);
            rootRect.sizeDelta = Vector2.zero;

            var cellSize = GetEffectCellSize();
            var texture = GetFirstAvailableTexture(catalog?.ToonExplosionRoundTexture, catalog?.ToonPopTexture, catalog?.ToonRingTexture, catalog?.ToonGlowTexture);
            var outerColor = Color.Lerp(Color.white, color, kind == PieceKind.Burst ? 0.52f : 0.34f);
            outerColor.a = kind == PieceKind.Burst ? 0.92f : 0.68f;
            var outer = CreateEffectGraphic("ExplosionRoundOuter", rootRect, texture, outerColor);
            outer.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            outer.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            outer.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            outer.rectTransform.sizeDelta = cellSize * Mathf.Lerp(1f, 1.38f, Mathf.Clamp01(intensity - 0.7f));

            var innerColor = Color.Lerp(Color.white, color, 0.2f);
            innerColor.a = kind == PieceKind.Burst ? 0.54f : 0.36f;
            var inner = CreateEffectGraphic("ExplosionRoundCore", rootRect, GetFirstAvailableTexture(catalog?.ToonGlowTexture, texture), innerColor);
            inner.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            inner.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            inner.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            inner.rectTransform.sizeDelta = cellSize * 0.78f;

            var emberCount = kind == PieceKind.Burst ? 10 : 6;
            var emberRects = new RectTransform[emberCount];
            var emberGraphics = new Graphic[emberCount];
            for (var i = 0; i < emberCount; i++)
            {
                var ember = CreateEffectGraphic($"ExplosionRoundEmber_{i}", rootRect, GetBurstTexture(kind, i), ShiftColor(color, i + 7));
                var rect = ember.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = Vector2.one * Mathf.Lerp(11f, kind == PieceKind.Burst ? 28f : 20f, (i % 4) / 3f);
                emberRects[i] = rect;
                emberGraphics[i] = ember;
            }

            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                var popT = Mathf.Clamp01(t / 0.45f);
                var moveT = EaseOutCubic(t);
                outer.rectTransform.localScale = Vector3.one * Mathf.LerpUnclamped(0.16f, 1.12f * intensity, EaseOutBack(popT));
                outer.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-18f, 84f, moveT));

                inner.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.28f, 1.05f + intensity * 0.08f, EaseOutCubic(t));
                inner.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(12f, -42f, moveT));

                var outerGraphicColor = outer.color;
                outerGraphicColor.a = Mathf.Lerp(outerColor.a, 0f, EaseInCubic(t));
                outer.color = outerGraphicColor;

                var innerGraphicColor = inner.color;
                innerGraphicColor.a = Mathf.Lerp(innerColor.a, 0f, EaseInCubic(t));
                inner.color = innerGraphicColor;

                var radius = cellSize.x * Mathf.Lerp(0.18f, kind == PieceKind.Burst ? 0.92f : 0.62f, moveT) * intensity;
                for (var i = 0; i < emberRects.Length; i++)
                {
                    var phase = (i / (float)emberRects.Length) * Mathf.PI * 2f + 0.31f;
                    var direction = new Vector2(Mathf.Cos(phase), Mathf.Sin(phase));
                    var tangent = new Vector2(-direction.y, direction.x);
                    var swirl = tangent * Mathf.Sin(t * Mathf.PI) * 12f * intensity * (i % 2 == 0 ? 1f : -1f);
                    emberRects[i].anchoredPosition = direction * radius + swirl;
                    emberRects[i].localScale = Vector3.one * Mathf.Lerp(1f, 0.14f, EaseInCubic(t));
                    emberRects[i].localRotation = Quaternion.Euler(0f, 0f, i * 31f + Mathf.Lerp(0f, 220f, moveT));
                    var emberColor = emberGraphics[i].color;
                    emberColor.a = Mathf.Lerp(0.95f, 0f, EaseInCubic(t));
                    emberGraphics[i].color = emberColor;
                }

                yield return null;
            }

            Destroy(root);
        }

        private IEnumerator AnimateSpecialSignature(BoardCoordinate coordinate, Color color, PieceKind kind, LineOrientation lineOrientation)
        {
            switch (kind)
            {
                case PieceKind.Line:
                    yield return AnimateDirectionalGlints(coordinate, color, lineOrientation == LineOrientation.Horizontal, 0.36f, 1.05f);
                    break;
                case PieceKind.Burst:
                    StartCoroutine(AnimateMiniShockwave(coordinate, color, 1.72f));
                    StartCoroutine(AnimateRadialPinwheel(coordinate, color, 12, 0.42f, 1.25f));
                    yield return WaitUnscaled(0.42f);
                    break;
                case PieceKind.Rainbow:
                    StartCoroutine(AnimateRainbowVortex(coordinate, 0.58f, 1.42f));
                    StartCoroutine(AnimateRadialPinwheel(coordinate, color, 16, 0.54f, 1.45f));
                    yield return WaitUnscaled(0.56f);
                    break;
                default:
                    yield return AnimateMiniShockwave(coordinate, color, 1.05f);
                    break;
            }
        }

        private IEnumerator AnimateSpecialCreatedGleam(BoardCoordinate coordinate, Color color, PieceKind kind, LineOrientation lineOrientation)
        {
            if (kind == PieceKind.Rainbow)
            {
                StartCoroutine(AnimateRainbowVortex(coordinate, 0.46f, 0.86f));
            }
            else if (kind == PieceKind.Burst)
            {
                StartCoroutine(AnimateMiniShockwave(coordinate, color, 1.18f));
            }
            else if (kind == PieceKind.Line)
            {
                StartCoroutine(AnimateDirectionalGlints(coordinate, color, lineOrientation == LineOrientation.Horizontal, 0.34f, 0.68f));
            }

            yield return WaitUnscaled(0.34f);
        }

        private IEnumerator AnimateDirectionalGlints(BoardCoordinate coordinate, Color color, bool horizontal, float duration, float intensity)
        {
            if (effectsRoot == null || boardRoot == null)
            {
                yield break;
            }

            var root = new GameObject(horizontal ? "LineHorizontalGlints" : "LineVerticalGlints", typeof(RectTransform));
            root.transform.SetParent(effectsRoot, false);
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = GetEffectPosition(coordinate);
            rootRect.sizeDelta = Vector2.zero;

            const int count = 6;
            var axis = horizontal ? Vector2.right : Vector2.up;
            var perpendicular = horizontal ? Vector2.up : Vector2.right;
            var halfLength = horizontal ? boardRoot.rect.width * 0.5f : boardRoot.rect.height * 0.5f;
            var cellSize = GetEffectCellSize();
            var graphics = new Graphic[count];
            var rects = new RectTransform[count];
            for (var i = 0; i < count; i++)
            {
                var graphic = CreateEffectGraphic($"LineGlint_{i}", rootRect, GetFirstAvailableTexture(catalog?.ToonLineTexture, catalog?.ToonSparkleTexture, catalog?.ToonGlowTexture), ShiftColor(color, i));
                var rect = graphic.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = horizontal
                    ? new Vector2(cellSize.x * 0.16f, cellSize.y * Mathf.Lerp(0.46f, 0.7f, (i % 3) / 2f))
                    : new Vector2(cellSize.x * Mathf.Lerp(0.46f, 0.7f, (i % 3) / 2f), cellSize.y * 0.16f);
                graphics[i] = graphic;
                rects[i] = rect;
            }

            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = EaseOutCubic(t);
                for (var i = 0; i < count; i++)
                {
                    var side = i % 2 == 0 ? 1f : -1f;
                    var lane = (i / 2 + 1) / 3f;
                    var wobble = Mathf.Sin((t * 1.35f + i * 0.17f) * Mathf.PI) * 12f * intensity;
                    rects[i].anchoredPosition = axis * side * Mathf.Lerp(10f, halfLength * lane, eased) + perpendicular * wobble;
                    rects[i].localScale = Vector3.one * Mathf.Lerp(0.72f, 0.22f, EaseInCubic(t));
                    rects[i].localRotation = Quaternion.Euler(0f, 0f, (horizontal ? 90f : 0f) + side * Mathf.Lerp(8f, 42f, t));
                    var graphicColor = graphics[i].color;
                    graphicColor.a = Mathf.Lerp(0.86f, 0f, EaseInCubic(t));
                    graphics[i].color = graphicColor;
                }

                yield return null;
            }

            Destroy(root);
        }

        private IEnumerator AnimateRadialPinwheel(BoardCoordinate coordinate, Color color, int count, float duration, float intensity)
        {
            if (effectsRoot == null)
            {
                yield break;
            }

            var root = new GameObject("RadialPinwheel", typeof(RectTransform));
            root.transform.SetParent(effectsRoot, false);
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = GetEffectPosition(coordinate);
            rootRect.sizeDelta = Vector2.zero;

            var rects = new RectTransform[count];
            var graphics = new Graphic[count];
            for (var i = 0; i < count; i++)
            {
                var particleColor = color;
                if (count > 12)
                {
                    particleColor = GetRainbowColor(i, 1f);
                }

                var graphic = CreateEffectGraphic($"Pin_{i}", rootRect, GetSparkleTexture(count > 12 ? PieceKind.Rainbow : PieceKind.Burst, i), particleColor);
                var rect = graphic.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = Vector2.one * Mathf.Lerp(16f, 34f, (i % 4) / 3f);
                rects[i] = rect;
                graphics[i] = graphic;
            }

            var radius = GetEffectCellSize().x * Mathf.Lerp(0.75f, 1.35f, Mathf.Clamp01(intensity - 0.65f));
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = EaseOutCubic(t);
                for (var i = 0; i < count; i++)
                {
                    var phase = (i / (float)count) * Mathf.PI * 2f + t * Mathf.PI * 0.72f;
                    var direction = new Vector2(Mathf.Cos(phase), Mathf.Sin(phase));
                    rects[i].anchoredPosition = direction * Mathf.Lerp(12f, radius, eased);
                    rects[i].localScale = Vector3.one * Mathf.Lerp(0.84f, 0.14f, EaseInCubic(t));
                    rects[i].localRotation = Quaternion.Euler(0f, 0f, Mathf.Rad2Deg * phase + 180f * t);
                    var graphicColor = graphics[i].color;
                    graphicColor.a = Mathf.Lerp(0.95f, 0f, EaseInCubic(t));
                    graphics[i].color = graphicColor;
                }

                yield return null;
            }

            Destroy(root);
        }

        private IEnumerator AnimateRainbowVortex(BoardCoordinate coordinate, float duration, float intensity)
        {
            if (effectsRoot == null)
            {
                yield break;
            }

            var root = new GameObject("RainbowVortex", typeof(RectTransform));
            root.transform.SetParent(effectsRoot, false);
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = GetEffectPosition(coordinate);
            rootRect.sizeDelta = Vector2.zero;

            const int ringCount = 6;
            var rects = new RectTransform[ringCount];
            var graphics = new Graphic[ringCount];
            var texture = GetFirstAvailableTexture(catalog?.ToonRingTexture, catalog?.ToonGlowTexture, catalog?.ToonAuraTexture);
            var cellSize = GetEffectCellSize();
            for (var i = 0; i < ringCount; i++)
            {
                var graphic = CreateEffectGraphic($"PrismRing_{i}", rootRect, texture, GetRainbowColor(i, 0.26f));
                var rect = graphic.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = cellSize * Mathf.Lerp(0.86f, 1.18f, i / (float)(ringCount - 1));
                rects[i] = rect;
                graphics[i] = graphic;
            }

            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = EaseOutCubic(t);
                for (var i = 0; i < ringCount; i++)
                {
                    var delay = i * 0.025f;
                    var localT = Mathf.Clamp01((t - delay) / Mathf.Max(0.01f, 1f - delay));
                    rects[i].localScale = Vector3.one * Mathf.Lerp(0.36f + i * 0.035f, intensity + i * 0.12f, EaseOutCubic(localT));
                    rects[i].localRotation = Quaternion.Euler(0f, 0f, (i % 2 == 0 ? 1f : -1f) * Mathf.Lerp(0f, 180f, eased));
                    var color = GetRainbowColor(i, Mathf.Lerp(0.3f, 0f, EaseInCubic(localT)));
                    graphics[i].color = color;
                }

                yield return null;
            }

            Destroy(root);
        }

        private IEnumerator AnimateBoardResonance(List<BoardVisualEffect> effects)
        {
            if (effectsRoot == null || effects.Count == 0)
            {
                yield break;
            }

            var center = Vector2.zero;
            var color = Color.white;
            foreach (var effect in effects)
            {
                center += GetEffectPosition(effect.Coordinate);
                color = Color.Lerp(color, effect.Color, 0.38f);
            }

            center /= effects.Count;
            var texture = GetFirstAvailableTexture(catalog?.ToonGlowTexture, catalog?.ToonAuraTexture, catalog?.ToonRingTexture);
            var graphic = CreateEffectGraphic("BoardResonance", effectsRoot, texture, new Color(color.r, color.g, color.b, 0.16f));
            var rect = graphic.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = center;
            rect.sizeDelta = GetEffectCellSize() * Mathf.Clamp(1.5f + effects.Count * 0.1f, 1.8f, 3.6f);

            const float duration = 0.36f;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                rect.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.18f, EaseOutCubic(t));
                rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 28f, t));
                var graphicColor = graphic.color;
                graphicColor.a = Mathf.Lerp(0.16f, 0f, EaseInCubic(t));
                graphic.color = graphicColor;
                yield return null;
            }

            Destroy(graphic.gameObject);
        }

        private Graphic CreateEffectGraphic(string name, Transform parent, Texture2D texture, Color color)
        {
            GameObject effectObject;
            Graphic graphic;
            if (texture != null)
            {
                effectObject = new GameObject(name, typeof(RectTransform), typeof(RawImage));
                var rawImage = effectObject.GetComponent<RawImage>();
                rawImage.texture = texture;
                rawImage.uvRect = new Rect(0f, 0f, 1f, 1f);
                graphic = rawImage;
            }
            else
            {
                effectObject = new GameObject(name, typeof(RectTransform), typeof(Image));
                graphic = effectObject.GetComponent<Image>();
            }

            effectObject.transform.SetParent(parent, false);
            graphic.raycastTarget = false;
            graphic.color = color;
            return graphic;
        }

        private Texture2D GetBurstTexture(PieceKind kind, int index)
        {
            if (catalog == null)
            {
                return null;
            }

            var variant = Mathf.Abs(index) % 6;
            switch (kind)
            {
                case PieceKind.Line:
                    return variant switch
                    {
                        0 => GetFirstAvailableTexture(catalog.ToonLineTexture, catalog.ToonSparkleTexture),
                        1 => GetFirstAvailableTexture(catalog.ToonSparkleTexture, catalog.ToonStarTexture),
                        _ => GetFirstAvailableTexture(catalog.ToonGlowTexture, catalog.ToonSparkleTexture)
                    };
                case PieceKind.Burst:
                    return variant switch
                    {
                        0 => GetFirstAvailableTexture(catalog.ToonAuraTexture, catalog.ToonGlowTexture),
                        1 => GetFirstAvailableTexture(catalog.ToonStarTexture, catalog.ToonSparkleTexture),
                        2 => GetFirstAvailableTexture(catalog.ToonSparkleTexture, catalog.ToonStarTexture),
                        _ => GetFirstAvailableTexture(catalog.ToonGlowTexture, catalog.ToonAuraTexture)
                    };
                case PieceKind.Rainbow:
                    return variant switch
                    {
                        0 => GetFirstAvailableTexture(catalog.ToonStarTexture, catalog.ToonSparkleTexture),
                        1 => GetFirstAvailableTexture(catalog.ToonConfettiTexture, catalog.ToonStarTexture),
                        2 => GetFirstAvailableTexture(catalog.ToonSparkleTexture, catalog.ToonStarTexture),
                        3 => GetFirstAvailableTexture(catalog.ToonGlowTexture, catalog.ToonAuraTexture),
                        _ => GetFirstAvailableTexture(catalog.ToonRingTexture, catalog.ToonGlowTexture)
                    };
                case PieceKind.Companion:
                    return variant % 2 == 0
                        ? GetFirstAvailableTexture(catalog.ToonStarTexture, catalog.ToonSparkleTexture)
                        : GetFirstAvailableTexture(catalog.ToonGlowTexture, catalog.ToonSparkleTexture);
                default:
                    return variant switch
                    {
                        0 => GetFirstAvailableTexture(catalog.ToonSparkleTexture, catalog.ToonStarTexture),
                        1 => GetFirstAvailableTexture(catalog.ToonStarTexture, catalog.ToonSparkleTexture),
                        2 => GetFirstAvailableTexture(catalog.ToonConfettiTexture, catalog.ToonSparkleTexture),
                        _ => GetFirstAvailableTexture(catalog.ToonGlowTexture, catalog.ToonSparkleTexture)
                    };
            }
        }

        private Texture2D GetSparkleTexture(PieceKind kind, int index)
        {
            if (catalog == null)
            {
                return null;
            }

            var variant = Mathf.Abs(index) % 4;
            if (kind == PieceKind.Rainbow)
            {
                return variant switch
                {
                    0 => GetFirstAvailableTexture(catalog.ToonStarTexture, catalog.ToonSparkleTexture),
                    1 => GetFirstAvailableTexture(catalog.ToonConfettiTexture, catalog.ToonStarTexture),
                    2 => GetFirstAvailableTexture(catalog.ToonRingTexture, catalog.ToonGlowTexture),
                    _ => GetFirstAvailableTexture(catalog.ToonSparkleTexture, catalog.ToonStarTexture)
                };
            }

            if (kind == PieceKind.Burst)
            {
                return variant == 0
                    ? GetFirstAvailableTexture(catalog.ToonAuraTexture, catalog.ToonGlowTexture)
                    : GetFirstAvailableTexture(catalog.ToonSparkleTexture, catalog.ToonStarTexture);
            }

            return variant == 0
                ? GetFirstAvailableTexture(catalog.ToonStarTexture, catalog.ToonSparkleTexture)
                : GetFirstAvailableTexture(catalog.ToonSparkleTexture, catalog.ToonGlowTexture);
        }

        private static Texture2D GetFirstAvailableTexture(params Texture2D[] textures)
        {
            foreach (var texture in textures)
            {
                if (texture != null)
                {
                    return texture;
                }
            }

            return null;
        }

        private IEnumerator AnimateSoftCoverBreak(BoardCoordinate coordinate, Color fallbackColor, float duration)
        {
            if (effectsRoot == null)
            {
                yield break;
            }

            var brokenSprite = catalog != null ? catalog.SoftCoverBrokenSprite : null;
            if (brokenSprite == null)
            {
                yield return AnimateCellFlash(coordinate, fallbackColor, duration, 0.96f, 1.08f);
                yield break;
            }

            var overlay = new GameObject("SoftCoverBreak", typeof(RectTransform), typeof(Image));
            overlay.transform.SetParent(effectsRoot, false);

            var rect = overlay.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = GetEffectPosition(coordinate);
            rect.sizeDelta = GetEffectCellSize();

            var image = overlay.GetComponent<Image>();
            image.sprite = brokenSprite;
            image.preserveAspect = false;
            image.raycastTarget = false;

            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                var fadeIn = Mathf.Clamp01(t / 0.18f);
                var fadeOut = Mathf.Clamp01((t - 0.18f) / 0.82f);
                image.color = new Color(1f, 1f, 1f, Mathf.Lerp(fadeIn, 0f, EaseInCubic(fadeOut)));
                rect.localScale = Vector3.one * Mathf.Lerp(0.99f, 1.06f, EaseOutCubic(t));
                yield return null;
            }

            Destroy(overlay);
        }

        private IEnumerator AnimateCrateBreak(BoardCoordinate coordinate, Color fallbackColor, float duration)
        {
            if (effectsRoot == null)
            {
                yield break;
            }

            var brokenSprite = catalog != null ? catalog.CrateBlockBrokenSprite : null;
            var shardSprite = catalog != null ? catalog.CrateWoodShardSprite : null;
            if (brokenSprite == null && shardSprite == null)
            {
                yield return AnimateCellFlash(coordinate, fallbackColor, duration, 0.92f, 1.16f);
                yield break;
            }

            duration = Mathf.Max(0.48f, duration);
            var center = GetEffectPosition(coordinate);
            var cellSize = GetEffectCellSize();
            var rootObject = new GameObject("CrateBreak", typeof(RectTransform));
            rootObject.transform.SetParent(effectsRoot, false);

            var rect = rootObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = center;
            rect.sizeDelta = cellSize;

            Image crackImage = null;
            RectTransform crackRect = null;
            if (brokenSprite != null)
            {
                var crackObject = new GameObject("CrateCrackFlash", typeof(RectTransform), typeof(Image));
                crackObject.transform.SetParent(rect, false);
                crackRect = crackObject.GetComponent<RectTransform>();
                crackRect.anchorMin = new Vector2(0.5f, 0.5f);
                crackRect.anchorMax = new Vector2(0.5f, 0.5f);
                crackRect.pivot = new Vector2(0.5f, 0.5f);
                crackRect.sizeDelta = cellSize * 1.08f;

                crackImage = crackObject.GetComponent<Image>();
                crackImage.sprite = brokenSprite;
                crackImage.preserveAspect = true;
                crackImage.raycastTarget = false;
            }

            var sprite = shardSprite != null ? shardSprite : brokenSprite;
            var shardCount = shardSprite != null ? 9 : 6;
            var shardRects = new RectTransform[shardCount];
            var shardImages = new Image[shardCount];
            var startOffsets = new[]
            {
                new Vector2(-0.28f, 0.16f),
                new Vector2(0f, 0.2f),
                new Vector2(0.24f, 0.13f),
                new Vector2(-0.2f, -0.02f),
                new Vector2(0.06f, -0.03f),
                new Vector2(0.27f, -0.08f),
                new Vector2(-0.1f, -0.2f),
                new Vector2(0.17f, -0.19f),
                new Vector2(-0.34f, -0.12f)
            };
            var sideDrifts = new[] { -0.74f, -0.28f, 0.62f, -0.52f, 0.14f, 0.78f, -0.26f, 0.48f, -0.86f };
            var liftAmounts = new[] { 0.26f, 0.35f, 0.22f, 0.18f, 0.28f, 0.16f, 0.1f, 0.2f, 0.12f };
            var fallAmounts = new[] { 0.62f, 0.74f, 0.58f, 0.82f, 0.68f, 0.9f, 0.86f, 0.78f, 0.72f };
            var spinAmounts = new[] { -210f, 176f, 238f, -154f, 118f, 286f, -260f, 196f, -318f };
            var startRotations = new[] { -28f, 8f, 24f, -12f, 36f, -38f, 18f, -24f, 42f };

            for (var i = 0; i < shardCount; i++)
            {
                var shardObject = new GameObject("CrateWoodShard", typeof(RectTransform), typeof(Image));
                shardObject.transform.SetParent(rect, false);
                shardRects[i] = shardObject.GetComponent<RectTransform>();
                shardRects[i].anchorMin = new Vector2(0.5f, 0.5f);
                shardRects[i].anchorMax = new Vector2(0.5f, 0.5f);
                shardRects[i].pivot = new Vector2(0.5f, 0.5f);
                shardRects[i].sizeDelta = new Vector2(cellSize.x * (0.23f + i % 3 * 0.07f), cellSize.y * (0.12f + i % 2 * 0.04f));

                shardImages[i] = shardObject.GetComponent<Image>();
                shardImages[i].sprite = sprite;
                shardImages[i].preserveAspect = true;
                shardImages[i].raycastTarget = false;
                shardImages[i].color = Color.white;
            }

            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                rect.localScale = Vector3.one * Mathf.Lerp(0.98f, 1.04f, EaseOutCubic(Mathf.Clamp01(t / 0.22f)));

                if (crackImage != null)
                {
                    var flashT = Mathf.Clamp01(t / 0.24f);
                    var flashAlpha = Mathf.Lerp(0.9f, 0f, EaseInCubic(flashT));
                    crackRect.localScale = Vector3.one * Mathf.LerpUnclamped(0.86f, 1.18f, EaseOutBack(flashT));
                    crackRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 8f) * 3.5f * (1f - flashT));
                    crackImage.color = new Color(1f, 1f, 1f, flashAlpha);
                }

                var launchT = EaseOutCubic(Mathf.Clamp01(t / 0.72f));
                var fallT = EaseInCubic(t);
                var fadeT = EaseInCubic(Mathf.Clamp01((t - 0.46f) / 0.54f));
                var alpha = Mathf.Lerp(1f, 0f, fadeT);
                for (var i = 0; i < shardCount; i++)
                {
                    var start = new Vector2(startOffsets[i].x * cellSize.x, startOffsets[i].y * cellSize.y);
                    var drift = new Vector2(
                        sideDrifts[i] * cellSize.x * launchT,
                        Mathf.Sin(t * Mathf.PI) * liftAmounts[i] * cellSize.y - fallAmounts[i] * cellSize.y * fallT);
                    var scaleDown = Mathf.Lerp(1f, 0.72f, fadeT);
                    var popScale = Mathf.LerpUnclamped(0.58f, 1f, EaseOutBack(Mathf.Clamp01(t / 0.18f)));

                    shardRects[i].anchoredPosition = start + drift;
                    shardRects[i].localRotation = Quaternion.Euler(0f, 0f, startRotations[i] + spinAmounts[i] * EaseOutCubic(t));
                    shardRects[i].localScale = Vector3.one * popScale * scaleDown;
                    shardImages[i].color = new Color(1f, 1f, 1f, alpha);
                }

                yield return null;
            }

            Destroy(rootObject);
        }

        private IEnumerator AnimateCellFlash(BoardCoordinate coordinate, Color color, float duration, float startScale, float endScale)
        {
            if (effectsRoot == null)
            {
                yield break;
            }

            var flash = new GameObject("CellFlash", typeof(RectTransform), typeof(Image));
            flash.transform.SetParent(effectsRoot, false);
            var rect = flash.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = GetEffectPosition(coordinate);
            rect.sizeDelta = GetEffectCellSize() * 0.94f;

            var image = flash.GetComponent<Image>();
            image.raycastTarget = false;
            image.color = new Color(color.r, color.g, color.b, 0.32f);

            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                rect.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, EaseOutCubic(t));
                var flashColor = image.color;
                flashColor.a = Mathf.Lerp(0.32f, 0f, EaseInCubic(t));
                image.color = flashColor;
                yield return null;
            }

            Destroy(flash);
        }

        private IEnumerator AnimateSpecialFlash(BoardCoordinate coordinate, Color color, PieceKind kind, LineOrientation lineOrientation)
        {
            if (kind == PieceKind.Line)
            {
                var horizontal = lineOrientation == LineOrientation.Horizontal;
                StartCoroutine(AnimateBeamSparks(coordinate, color, horizontal));
                yield return AnimateBeam(coordinate, color, horizontal);
                yield break;
            }

            var scale = kind == PieceKind.Rainbow ? 2.7f : kind == PieceKind.Burst ? 2.15f : 1.8f;
            if (kind == PieceKind.Burst)
            {
                StartCoroutine(AnimateToonRing(coordinate, color, 0.32f, 0.22f, 2.1f, 0.3f, false));
            }

            if (kind == PieceKind.Rainbow)
            {
                StartCoroutine(AnimateToonRing(coordinate, color, 0.38f, 0.36f, 2.15f, 0.24f, false));
            }

            yield return AnimateCellFlash(coordinate, color, 0.28f, 0.8f, scale);
        }

        private IEnumerator AnimateBeam(BoardCoordinate coordinate, Color color, bool horizontal)
        {
            if (effectsRoot == null)
            {
                yield break;
            }

            var texture = GetFirstAvailableTexture(catalog?.ToonLineTexture, catalog?.ToonGlowTexture, catalog?.ToonAuraTexture);
            var graphic = CreateEffectGraphic(horizontal ? "HorizontalBeam" : "VerticalBeam", effectsRoot, texture, new Color(color.r, color.g, color.b, 0.48f));
            var rect = graphic.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = GetEffectPosition(coordinate);
            var cellSize = GetEffectCellSize();
            rect.sizeDelta = horizontal
                ? new Vector2(boardRoot.rect.width, cellSize.y * 0.42f)
                : new Vector2(cellSize.x * 0.42f, boardRoot.rect.height);

            const float duration = 0.28f;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                var widthScale = horizontal
                    ? new Vector3(EaseOutCubic(t), Mathf.Lerp(1f, 0.35f, t), 1f)
                    : new Vector3(Mathf.Lerp(1f, 0.35f, t), EaseOutCubic(t), 1f);
                rect.localScale = widthScale;
                var beamColor = graphic.color;
                beamColor.a = Mathf.Lerp(0.48f, 0f, EaseInCubic(t));
                graphic.color = beamColor;
                yield return null;
            }

            Destroy(graphic.gameObject);
        }

        private IEnumerator AnimateBeamSparks(BoardCoordinate coordinate, Color color, bool horizontal)
        {
            if (effectsRoot == null)
            {
                yield break;
            }

            var root = new GameObject(horizontal ? "HorizontalBeamSparks" : "VerticalBeamSparks", typeof(RectTransform));
            root.transform.SetParent(effectsRoot, false);
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = GetEffectPosition(coordinate);

            const int particleCount = 8;
            var axis = horizontal ? Vector2.right : Vector2.up;
            var perpendicular = horizontal ? Vector2.up : Vector2.right;
            var halfLength = horizontal ? boardRoot.rect.width * 0.5f : boardRoot.rect.height * 0.5f;
            var particles = new RectTransform[particleCount];
            var graphics = new Graphic[particleCount];

            for (var i = 0; i < particleCount; i++)
            {
                var graphic = CreateEffectGraphic($"BeamSpark_{i}", rootRect, GetSparkleTexture(PieceKind.Line, i), ShiftColor(color, i));
                var rect = graphic.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = Vector2.one * Mathf.Lerp(18f, 28f, (i % 3) / 2f);
                particles[i] = rect;
                graphics[i] = graphic;
            }

            const float duration = 0.32f;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = EaseOutCubic(t);
                for (var i = 0; i < particleCount; i++)
                {
                    var side = i % 2 == 0 ? 1f : -1f;
                    var lane = (i / 2 + 1) / 4f;
                    var wobble = Mathf.Sin((t + i * 0.13f) * Mathf.PI) * 14f;
                    particles[i].anchoredPosition = axis * side * Mathf.Lerp(16f, halfLength * lane, eased) + perpendicular * wobble;
                    particles[i].localScale = Vector3.one * Mathf.Lerp(0.62f, 0.18f, EaseInCubic(t));
                    particles[i].localRotation = Quaternion.Euler(0f, 0f, i * 41f + 220f * t);
                    var graphicColor = graphics[i].color;
                    graphicColor.a = Mathf.Lerp(0.85f, 0f, EaseInCubic(t));
                    graphics[i].color = graphicColor;
                }

                yield return null;
            }

            Destroy(root);
        }

        private IEnumerator AnimateScorePopup(BoardCoordinate coordinate, int amount)
        {
            if (effectsRoot == null)
            {
                yield break;
            }

            var scoreColor = GetScorePopupColor(amount);
            var label = UIFactory.CreateText("ScorePopup", effectsRoot, $"+{amount}", amount >= 500 ? 38 : 32, TextAnchor.MiddleCenter, scoreColor);
            label.raycastTarget = false;
            StyleCandyText(label, scoreColor, amount >= 500 ? 38 : 32, new Vector2(2.2f, -2.2f));
            var rect = label.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(amount >= 500 ? 190f : 150f, amount >= 500 ? 62f : 54f);
            var start = GetEffectPosition(coordinate) + new Vector2(0f, 18f);
            var drift = ((coordinate.x + coordinate.y) & 1) == 0 ? 10f : -10f;
            var end = start + new Vector2(drift, amount >= 500 ? 88f : 74f);

            const float duration = 0.52f;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                rect.anchoredPosition = Vector2.Lerp(start, end, EaseOutCubic(t));
                rect.localScale = Vector3.one * Mathf.Lerp(0.72f, amount >= 500 ? 1.18f : 1.08f, EaseOutBack(Mathf.Clamp01(t * 1.35f)));
                rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI) * drift * 0.12f);
                var color = label.color;
                color.a = t < 0.25f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.25f) / 0.75f);
                label.color = color;
                yield return null;
            }

            Destroy(label.gameObject);
        }

        private IEnumerator AnimateCandyTextPopup(BoardCoordinate coordinate, string message, Color accent, float delay)
        {
            if (effectsRoot == null || string.IsNullOrEmpty(message))
            {
                yield break;
            }

            if (delay > 0f)
            {
                yield return WaitUnscaled(delay);
            }

            var root = new GameObject("CandyTextPopup", typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(effectsRoot, false);
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = new Vector2(310f, 100f);

            var group = root.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var glowColor = new Color(accent.r, accent.g, accent.b, 0.28f);
            var glow = CreateEffectGraphic("CandyTextGlow", rootRect, GetFirstAvailableTexture(catalog?.ToonGlowTexture, catalog?.ToonAuraTexture, catalog?.ToonRingTexture), glowColor);
            var glowRect = glow.rectTransform;
            glowRect.anchorMin = new Vector2(0.5f, 0.5f);
            glowRect.anchorMax = new Vector2(0.5f, 0.5f);
            glowRect.pivot = new Vector2(0.5f, 0.5f);
            glowRect.anchoredPosition = Vector2.zero;
            glowRect.sizeDelta = new Vector2(280f, 86f);
            glow.raycastTarget = false;

            var labelColor = Color.Lerp(Color.white, accent, 0.18f);
            labelColor.a = 1f;
            var label = UIFactory.CreateText("CandyText", rootRect, message, 48, TextAnchor.MiddleCenter, labelColor);
            label.raycastTarget = false;
            StyleCandyText(label, accent, 48, new Vector2(3.8f, -4.2f));
            UIFactory.Stretch(label.rectTransform);

            var basePosition = GetEffectPosition(coordinate) + new Vector2(0f, 38f);
            var driftDirection = ((coordinate.x + coordinate.y) & 1) == 0 ? 1f : -1f;
            var startRotation = driftDirection * -5f;
            var endRotation = driftDirection * 4f;

            for (var elapsed = 0f; elapsed < CandyTextDuration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / CandyTextDuration);
                var popT = Mathf.Clamp01(t / 0.28f);
                var settleT = Mathf.Clamp01((t - 0.28f) / 0.72f);
                var fadeT = Mathf.Clamp01((t - 0.56f) / 0.44f);
                var rise = Mathf.Lerp(0f, CandyTextRise, EaseOutCubic(t));
                var side = Mathf.Sin(t * Mathf.PI) * CandyTextSideDrift * driftDirection;

                rootRect.anchoredPosition = basePosition + new Vector2(side, rise);
                rootRect.localScale = Vector3.one * Mathf.Lerp(
                    Mathf.Lerp(0.36f, 1.18f, EaseOutBack(popT)),
                    0.88f,
                    EaseInCubic(settleT));
                rootRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(startRotation, endRotation, EaseInOut(t)));
                group.alpha = fadeT <= 0f ? 1f : Mathf.Lerp(1f, 0f, EaseInCubic(fadeT));

                var glowFade = Mathf.Lerp(0.28f, 0f, EaseInCubic(Mathf.Clamp01((t - 0.18f) / 0.82f)));
                glow.color = new Color(accent.r, accent.g, accent.b, glowFade);
                glowRect.localScale = Vector3.one * Mathf.Lerp(0.88f, 1.42f, EaseOutCubic(t));
                yield return null;
            }

            Destroy(root);
        }

        private static string GetSpecialCreatedText(PieceKind kind)
        {
            return kind switch
            {
                PieceKind.Line => "SWEET!",
                PieceKind.Burst => "TASTY!",
                PieceKind.Rainbow => "DELICIOUS!",
                _ => "NICE!"
            };
        }

        private static string GetSpecialActivatedText(PieceKind kind)
        {
            return kind switch
            {
                PieceKind.Line => "LINE BLAST!",
                PieceKind.Burst => "BOOM!",
                PieceKind.Rainbow => "DIVINE!",
                _ => "POP!"
            };
        }

        private static Color GetCandyTextColor(PieceKind kind)
        {
            return kind switch
            {
                PieceKind.Line => new Color(0.34f, 0.92f, 1f),
                PieceKind.Burst => new Color(1f, 0.5f, 0.2f),
                PieceKind.Rainbow => new Color(1f, 0.78f, 0.16f),
                PieceKind.Companion => new Color(1f, 0.9f, 0.3f),
                _ => new Color(1f, 0.95f, 0.38f)
            };
        }

        private static Color GetScorePopupColor(int amount)
        {
            if (amount >= 1000)
            {
                return new Color(1f, 0.8f, 0.2f);
            }

            if (amount >= 300)
            {
                return new Color(1f, 0.55f, 0.95f);
            }

            if (amount >= 240)
            {
                return new Color(1f, 0.62f, 0.24f);
            }

            if (amount >= 120)
            {
                return new Color(0.48f, 0.95f, 1f);
            }

            return new Color(1f, 0.98f, 0.7f);
        }

        private static void StyleCandyText(Text label, Color accent, int maxSize, Vector2 shadowOffset)
        {
            label.fontStyle = FontStyle.BoldAndItalic;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = Mathf.Max(16, maxSize / 2);
            label.resizeTextMaxSize = maxSize;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.supportRichText = false;

            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.18f, 0.07f, 0.12f, 0.68f);
            shadow.effectDistance = shadowOffset;
            shadow.useGraphicAlpha = true;

            var outline = label.gameObject.AddComponent<Outline>();
            var outlineColor = Color.Lerp(new Color(0.22f, 0.08f, 0.14f, 1f), accent, 0.18f);
            outlineColor.a = 0.92f;
            outline.effectColor = outlineColor;
            outline.effectDistance = new Vector2(2.8f, -2.8f);
            outline.useGraphicAlpha = true;
        }

        private static IEnumerator AnimateTwoPieceSwap(BoardCellView fromView, BoardCellView toView, Vector2 offset, float duration)
        {
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = EaseInOut(Mathf.Clamp01(elapsed / duration));
                fromView.SetPieceOffset(Vector2.Lerp(Vector2.zero, offset, t));
                toView.SetPieceOffset(Vector2.Lerp(Vector2.zero, -offset, t));
                var scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.06f;
                fromView.SetPieceScale(scale);
                toView.SetPieceScale(scale);
                yield return null;
            }

            fromView.ResetPieceVisualState();
            toView.ResetPieceVisualState();
        }

        private static IEnumerator AnimateRejectedPairSwap(BoardCellView fromView, BoardCellView toView, Vector2 offset, float duration)
        {
            var halfDuration = duration * 0.5f;
            for (var elapsed = 0f; elapsed < halfDuration; elapsed += Time.unscaledDeltaTime)
            {
                var t = EaseOutCubic(Mathf.Clamp01(elapsed / halfDuration));
                fromView.SetPieceOffset(Vector2.Lerp(Vector2.zero, offset, t));
                toView.SetPieceOffset(Vector2.Lerp(Vector2.zero, -offset, t));
                yield return null;
            }

            for (var elapsed = 0f; elapsed < halfDuration; elapsed += Time.unscaledDeltaTime)
            {
                var t = EaseInOut(Mathf.Clamp01(elapsed / halfDuration));
                fromView.SetPieceOffset(Vector2.Lerp(offset, Vector2.zero, t));
                toView.SetPieceOffset(Vector2.Lerp(-offset, Vector2.zero, t));
                yield return null;
            }

            fromView.ResetPieceVisualState();
            toView.ResetPieceVisualState();
        }

        private static IEnumerator AnimateRejectedSwap(BoardCellView fromView, Vector2 offset, float duration)
        {
            var halfDuration = duration * 0.5f;
            for (var elapsed = 0f; elapsed < halfDuration; elapsed += Time.unscaledDeltaTime)
            {
                var t = EaseOutCubic(Mathf.Clamp01(elapsed / halfDuration));
                fromView.SetPieceOffset(Vector2.Lerp(Vector2.zero, offset, t));
                yield return null;
            }

            for (var elapsed = 0f; elapsed < halfDuration; elapsed += Time.unscaledDeltaTime)
            {
                var t = EaseInOut(Mathf.Clamp01(elapsed / halfDuration));
                fromView.SetPieceOffset(Vector2.Lerp(offset, Vector2.zero, t));
                yield return null;
            }

            fromView.ResetPieceVisualState();
        }

        private static Color ShiftColor(Color baseColor, int index)
        {
            var tint = 0.82f + (index % 4) * 0.06f;
            return new Color(
                Mathf.Clamp01(baseColor.r * tint + 0.08f),
                Mathf.Clamp01(baseColor.g * tint + 0.08f),
                Mathf.Clamp01(baseColor.b * tint + 0.08f),
                1f);
        }

        private static Color GetRainbowColor(int index, float alpha)
        {
            var hue = Mathf.Repeat(index * 0.145f + 0.02f, 1f);
            var color = Color.HSVToRGB(hue, 0.72f, 1f);
            color.a = alpha;
            return color;
        }

        private static IEnumerator WaitUnscaled(float duration)
        {
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                yield return null;
            }
        }

        private static float EaseInOut(float t)
        {
            return t * t * (3f - 2f * t);
        }

        private static float EaseOutCubic(float t)
        {
            var inverse = 1f - t;
            return 1f - inverse * inverse * inverse;
        }

        private static float EaseInCubic(float t)
        {
            return t * t * t;
        }

        private static float EaseOutBack(float t)
        {
            const float overshoot = 1.70158f;
            var shifted = t - 1f;
            return 1f + shifted * shifted * ((overshoot + 1f) * shifted + overshoot);
        }
    }
}
