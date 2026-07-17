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

            public BoardVisualEffect(BoardVisualEffectType type, BoardCoordinate coordinate, Color color, PieceKind pieceKind = PieceKind.Normal)
            {
                Type = type;
                Coordinate = coordinate;
                Color = color;
                PieceKind = pieceKind;
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

        public void QueueCompanionDelivered(BoardCoordinate coordinate)
        {
            pendingPreRefreshEffects.Add(new BoardVisualEffect(BoardVisualEffectType.CompanionDelivered, coordinate, new Color(1f, 0.92f, 0.42f), PieceKind.Companion));
        }

        public void QueueSpecialActivated(BoardCoordinate coordinate, PieceKind kind)
        {
            pendingPreRefreshEffects.Add(new BoardVisualEffect(BoardVisualEffectType.SpecialActivated, coordinate, GetSpecialColor(kind), kind));
        }

        public void QueueSpecialCreated(BoardCoordinate coordinate, PieceKind kind)
        {
            pendingPostRefreshEffects.Add(new BoardVisualEffect(BoardVisualEffectType.SpecialCreated, coordinate, GetSpecialColor(kind), kind));
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
                yield return AnimateTwoPieceSwap(fromView, toView, offset, 0.16f);
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
                    duration = Mathf.Clamp(0.18f + fromOffset.magnitude / 1300f, 0.2f, 0.34f);
                    delay = Mathf.Clamp((model.Height - coordinate.y) * 0.01f, 0f, 0.08f);
                }
                else
                {
                    var offsetY = Mathf.Max(cellSize.y * 0.9f, spawnTopY - currentPosition.y);
                    fromOffset = new Vector2(0f, offsetY);
                    duration = Mathf.Clamp(0.22f + offsetY / 1900f, 0.24f, 0.36f);
                    delay = Mathf.Clamp((model.Height - 1 - coordinate.y) * 0.016f, 0f, 0.12f);
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
                    SpawnBurst(effect.Coordinate, effect.Color, effect.PieceKind == PieceKind.Rainbow ? 18 : 10, 88f);
                    if (view != null)
                    {
                        yield return AnimatePiecePop(view, 0.28f);
                    }

                    break;
                case BoardVisualEffectType.BlockerHit:
                    SpawnBurst(effect.Coordinate, effect.Color, 7, 48f);
                    if (view != null)
                    {
                        yield return AnimatePieceShake(view, 0.16f, 12f);
                    }

                    break;
                case BoardVisualEffectType.CompanionDelivered:
                    SpawnBurst(effect.Coordinate, effect.Color, 18, 108f);
                    if (view != null)
                    {
                        yield return AnimatePiecePop(view, 0.3f);
                    }

                    break;
                case BoardVisualEffectType.SpecialActivated:
                    SpawnBurst(effect.Coordinate, effect.Color, effect.PieceKind == PieceKind.Rainbow ? 24 : 16, 120f);
                    StartCoroutine(AnimateSpecialFlash(effect.Coordinate, effect.Color, effect.PieceKind));
                    if (view != null)
                    {
                        yield return AnimatePiecePulse(view, 0.22f, 1.22f);
                    }

                    break;
                case BoardVisualEffectType.SpecialCreated:
                    if (afterRefresh)
                    {
                        SpawnBurst(effect.Coordinate, effect.Color, 14, 64f);
                        StartCoroutine(AnimateCellFlash(effect.Coordinate, effect.Color, 0.24f, 0.55f, 1.16f));
                        if (view != null)
                        {
                            yield return AnimatePieceSpawn(view, 0.24f);
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

        private void SpawnBurst(BoardCoordinate coordinate, Color color, int particleCount, float radius)
        {
            if (effectsRoot == null)
            {
                return;
            }

            StartCoroutine(AnimateBurst(coordinate, color, particleCount, radius, 0.34f));
        }

        private IEnumerator AnimateBurst(BoardCoordinate coordinate, Color color, int particleCount, float radius, float duration)
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
            var graphics = new Image[particleCount];
            for (var i = 0; i < particleCount; i++)
            {
                var particle = new GameObject($"Particle_{i}", typeof(RectTransform), typeof(Image));
                particle.transform.SetParent(rootRect, false);
                var rect = particle.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = Vector2.one * Mathf.Lerp(12f, 22f, (i % 3) / 2f);
                rect.localRotation = Quaternion.Euler(0f, 0f, i * 37f);

                var image = particle.GetComponent<Image>();
                image.raycastTarget = false;
                image.color = ShiftColor(color, i);
                particles[i] = rect;
                graphics[i] = image;
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
                    var drift = direction * radius * Mathf.Lerp(0.2f, 1f, ((i * 17) % 11) / 10f);
                    particles[i].anchoredPosition = drift * moveT;
                    particles[i].localScale = Vector3.one * Mathf.Lerp(1.25f, 0.2f, t);
                    particles[i].localRotation = Quaternion.Euler(0f, 0f, i * 37f + 220f * t);
                    var particleColor = graphics[i].color;
                    particleColor.a = fade;
                    graphics[i].color = particleColor;
                }

                yield return null;
            }

            Destroy(root);
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

        private IEnumerator AnimateSpecialFlash(BoardCoordinate coordinate, Color color, PieceKind kind)
        {
            if (kind == PieceKind.Line)
            {
                StartCoroutine(AnimateBeam(coordinate, color, true));
                yield return AnimateBeam(coordinate, color, false);
                yield break;
            }

            var scale = kind == PieceKind.Rainbow ? 2.6f : 1.8f;
            yield return AnimateCellFlash(coordinate, color, 0.28f, 0.8f, scale);
        }

        private IEnumerator AnimateBeam(BoardCoordinate coordinate, Color color, bool horizontal)
        {
            if (effectsRoot == null)
            {
                yield break;
            }

            var beam = new GameObject(horizontal ? "HorizontalBeam" : "VerticalBeam", typeof(RectTransform), typeof(Image));
            beam.transform.SetParent(effectsRoot, false);
            var rect = beam.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = GetEffectPosition(coordinate);
            var cellSize = GetEffectCellSize();
            rect.sizeDelta = horizontal
                ? new Vector2(boardRoot.rect.width, cellSize.y * 0.42f)
                : new Vector2(cellSize.x * 0.42f, boardRoot.rect.height);

            var image = beam.GetComponent<Image>();
            image.raycastTarget = false;
            image.color = new Color(color.r, color.g, color.b, 0.44f);

            const float duration = 0.22f;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                var widthScale = horizontal
                    ? new Vector3(EaseOutCubic(t), Mathf.Lerp(1f, 0.35f, t), 1f)
                    : new Vector3(Mathf.Lerp(1f, 0.35f, t), EaseOutCubic(t), 1f);
                rect.localScale = widthScale;
                var beamColor = image.color;
                beamColor.a = Mathf.Lerp(0.44f, 0f, EaseInCubic(t));
                image.color = beamColor;
                yield return null;
            }

            Destroy(beam);
        }

        private IEnumerator AnimateScorePopup(BoardCoordinate coordinate, int amount)
        {
            if (effectsRoot == null)
            {
                yield break;
            }

            var label = UIFactory.CreateText("ScorePopup", effectsRoot, $"+{amount}", 30, TextAnchor.MiddleCenter, new Color(1f, 0.98f, 0.7f));
            label.raycastTarget = false;
            var rect = label.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(150f, 54f);
            var start = GetEffectPosition(coordinate) + new Vector2(0f, 18f);
            var end = start + new Vector2(0f, 74f);

            const float duration = 0.52f;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                rect.anchoredPosition = Vector2.Lerp(start, end, EaseOutCubic(t));
                rect.localScale = Vector3.one * Mathf.Lerp(0.78f, 1.08f, EaseOutBack(Mathf.Clamp01(t * 1.35f)));
                var color = label.color;
                color.a = t < 0.25f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.25f) / 0.75f);
                label.color = color;
                yield return null;
            }

            Destroy(label.gameObject);
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
