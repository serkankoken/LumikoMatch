using CharacterMatch3.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CharacterMatch3.Board
{
    public sealed class BoardCellView : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private const int NoPointerId = int.MinValue;
        private const float NormalPieceInset = 8f;
        private const float BearSpecialInset = -18f;
        private const float OtherSpecialInset = 2f;
        private const float GridSpriteOverscan = 38f;
        private const float GridSpriteVisibleWidthRatio = 786f / 1254f;
        private const float GridSpriteVisibleHeightRatio = 819f / 1254f;
        private const float SoftCoverSpriteVisibleWidthRatio = 1008f / 1254f;
        private const float SoftCoverSpriteVisibleHeightRatio = 1025f / 1254f;
        private const float IdleBobAmplitude = 3.2f;
        private const float IdleSwayAmplitude = 0.65f;
        private const float IdleSquashAmplitude = 0.014f;
        private const float IdleTiltAmplitude = 1.65f;
        private const float IdleAngularSpeed = 2.35f;
        private static readonly Color InactiveCellColor = new Color(0f, 0f, 0f, 0.06f);
        private static readonly Color ActiveCellColor = new Color(1f, 1f, 1f, 0.16f);
        private static readonly Color SelectedCellColor = new Color(1f, 0.93f, 0.35f, 0.95f);
        private static readonly Color SoftCoverSingleLayerFallbackColor = new Color(0.65f, 0.93f, 0.98f, 0.5f);
        private static readonly Color SoftCoverMultiLayerFallbackColor = new Color(0.38f, 0.83f, 0.92f, 0.72f);
        private static readonly Color CompanionTokenGlowColor = new Color(1f, 0.78f, 0.18f, 0.38f);
        private static readonly Color CompanionTokenBadgeColor = new Color(1f, 0.93f, 0.45f, 0.9f);
        private static readonly Color CompanionTokenRibbonColor = new Color(0.22f, 0.68f, 0.98f, 0.94f);
        private static readonly Color CompanionTokenArrowColor = new Color(1f, 1f, 1f, 0.98f);
        private static readonly Color CompanionExitGlowColor = new Color(0.28f, 0.95f, 1f, 0.24f);
        private static readonly Color CompanionExitPadColor = new Color(0.06f, 0.48f, 0.72f, 0.72f);
        private static readonly Color CompanionExitCoreColor = new Color(0.84f, 1f, 1f, 0.86f);
        private static readonly Color CompanionExitArrowColor = new Color(0.03f, 0.38f, 0.56f, 0.96f);

        private BoardView boardView;
        private Image background;
        private Image gridImage;
        private Image pieceImage;
        private Image companionExitGlow;
        private Image companionExitPad;
        private Image companionExitCore;
        private Text companionExitArrow;
        private Image companionTokenGlow;
        private Image companionTokenBadge;
        private Image companionTokenRibbon;
        private Text companionTokenArrow;
        private Image softCoverImage;
        private Image specialOverlayPrimary;
        private Image specialOverlaySecondary;
        private Text pieceLabel;
        private Text blockerLabel;
        private bool usingSpecialSprite;
        private bool pieceIdleEnabled;
        private CharacterType currentPieceCharacter;
        private PieceKind currentPieceKind;
        private Vector2 effectPieceOffset = Vector2.zero;
        private Vector2 idlePieceOffset = Vector2.zero;
        private Vector3 idlePieceScale = Vector3.one;
        private Quaternion idlePieceRotation = Quaternion.identity;
        private float effectPieceScale = 1f;
        private float effectPieceAlpha = 1f;
        private float idlePhase;
        private Vector2 pointerDownPosition;
        private float pointerDownTime;
        private int activePointerId = NoPointerId;
        private bool gestureConsumed;
        private bool directionLocked;
        private Vector2Int lockedDirection;

        public BoardCoordinate Coordinate { get; private set; }

        public void Initialize(BoardView owner, BoardCoordinate coordinate)
        {
            boardView = owner;
            Coordinate = coordinate;
            EnsureVisuals();
        }

        public void Refresh(BoardCellState cell, CharacterCatalog catalog, bool selected)
        {
            EnsureVisuals();
            usingSpecialSprite = false;
            DisableIdleMotion();
            currentPieceCharacter = CharacterType.Cat;
            currentPieceKind = PieceKind.Normal;
            ResetPieceVisualState();

            if (cell == null || !cell.Active)
            {
                background.sprite = null;
                background.type = Image.Type.Simple;
                background.color = InactiveCellColor;
                gridImage.enabled = false;
                gridImage.sprite = null;
                pieceImage.enabled = false;
                SetCompanionExitVisible(false);
                SetCompanionTokenVisible(false);
                softCoverImage.enabled = false;
                softCoverImage.sprite = null;
                specialOverlayPrimary.enabled = false;
                specialOverlaySecondary.enabled = false;
                pieceLabel.text = string.Empty;
                blockerLabel.text = string.Empty;
                return;
            }

            var gridSprite = catalog != null ? catalog.GridCellSprite : null;
            var softCoverSprite = catalog != null ? catalog.SoftCoverSprite : null;
            var hasSoftCover = cell.SoftCoverLayers > 0;
            SetCompanionExitVisible(cell.IsCompanionExit);
            SetCompanionTokenVisible(false);
            background.sprite = null;
            background.color = selected
                ? SelectedCellColor
                : gridSprite != null || (hasSoftCover && softCoverSprite != null)
                    ? Color.clear
                    : ActiveCellColor;
            gridImage.enabled = !hasSoftCover && gridSprite != null;
            gridImage.sprite = gridImage.enabled ? gridSprite : null;
            gridImage.color = Color.white;
            softCoverImage.enabled = hasSoftCover;
            specialOverlayPrimary.enabled = false;
            specialOverlaySecondary.enabled = false;
            softCoverImage.sprite = softCoverImage.enabled ? softCoverSprite : null;
            softCoverImage.color = softCoverImage.enabled
                ? softCoverSprite != null
                    ? Color.white
                    : cell.SoftCoverLayers > 1
                        ? SoftCoverMultiLayerFallbackColor
                        : SoftCoverSingleLayerFallbackColor
                : Color.clear;
            ApplySoftCoverBounds(softCoverImage.enabled && softCoverSprite != null);

            blockerLabel.text = string.Empty;
            if (cell.CrateLayers > 0)
            {
                var crateSprite = catalog != null ? catalog.CrateBlockSprite : null;
                pieceImage.enabled = crateSprite != null;
                pieceImage.sprite = crateSprite;
                pieceImage.color = crateSprite != null ? Color.white : Color.clear;
                pieceLabel.text = string.Empty;
                blockerLabel.text = crateSprite != null
                    ? (cell.CrateLayers > 1 ? cell.CrateLayers.ToString() : string.Empty)
                    : (cell.CrateLayers > 1 ? "CR2" : "CR");
                ApplyPieceVisualTransform();
                return;
            }

            if (cell.Piece == null)
            {
                pieceImage.enabled = false;
                pieceLabel.text = string.Empty;
            }
            else if (cell.Piece.Kind == PieceKind.Companion)
            {
                currentPieceKind = cell.Piece.Kind;
                currentPieceCharacter = cell.Piece.Character;
                EnableIdleMotion(cell.Piece);
                SetCompanionTokenVisible(true);
                ApplyCompanionPieceBounds();
                pieceImage.enabled = true;
                pieceImage.sprite = catalog != null ? catalog.GetSprite(cell.Piece.Character) : null;
                pieceImage.color = pieceImage.sprite != null ? Color.white : new Color(1f, 0.92f, 0.4f);
                pieceLabel.text = pieceImage.sprite == null ? "C" : string.Empty;
                ApplyPieceVisualTransform();
            }
            else
            {
                pieceImage.enabled = true;
                currentPieceKind = cell.Piece.Kind;
                var specialSprite = catalog != null
                    ? catalog.GetSpecialSprite(cell.Piece.Character, cell.Piece.Kind, cell.Piece.LineOrientation)
                    : null;
                usingSpecialSprite = specialSprite != null;
                currentPieceCharacter = cell.Piece.Character;
                ApplyPieceBounds(usingSpecialSprite);
                EnableIdleMotion(cell.Piece);
                pieceImage.sprite = specialSprite != null
                    ? specialSprite
                    : catalog != null
                        ? catalog.GetSprite(cell.Piece.Character)
                        : null;
                pieceImage.color = pieceImage.sprite != null
                    ? Color.white
                    : catalog != null
                        ? catalog.GetFallbackColor(cell.Piece.Character)
                        : Color.white;
                pieceLabel.text = string.Empty;
                if (specialSprite == null)
                {
                    ConfigureSpecialOverlay(cell.Piece);
                }

                ApplyPieceVisualTransform();
            }

            if (cell.LockLayers > 0)
            {
                blockerLabel.text = cell.LockLayers > 1 ? "LK2" : "LK";
            }
        }

        private void Update()
        {
            if (!pieceIdleEnabled)
            {
                return;
            }

            UpdateIdleMotion(Time.unscaledTime);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pointerDownPosition = eventData.position;
            pointerDownTime = Time.unscaledTime;
            activePointerId = eventData.pointerId;
            gestureConsumed = false;
            directionLocked = false;
            lockedDirection = Vector2Int.zero;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!IsActivePointer(eventData) || gestureConsumed)
            {
                return;
            }

            TryConsumeSwipe(eventData.position);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!IsActivePointer(eventData))
            {
                return;
            }

            if (!gestureConsumed && TryConsumeSwipe(eventData.position))
            {
                ResetGesture();
                return;
            }

            if (!gestureConsumed && IsTap(eventData.position))
            {
                boardView.CellClicked(Coordinate);
            }

            ResetGesture();
        }

        public void SetPieceOffset(Vector2 offset)
        {
            EnsureVisuals();
            effectPieceOffset = offset;
            ApplyPieceVisualTransform();
        }

        public void SetPieceScale(float scale)
        {
            EnsureVisuals();
            effectPieceScale = scale;
            ApplyPieceVisualTransform();
        }

        public void SetPieceAlpha(float alpha)
        {
            EnsureVisuals();
            effectPieceAlpha = alpha;
            ApplyPieceVisualAlpha();
        }

        public void ResetPieceVisualState()
        {
            effectPieceOffset = Vector2.zero;
            effectPieceScale = 1f;
            effectPieceAlpha = 1f;
            ApplyPieceBounds(usingSpecialSprite);
            ApplyPieceVisualTransform();
            ApplyPieceVisualAlpha();
        }

        private bool TryConsumeSwipe(Vector2 currentPosition)
        {
            if (boardView == null)
            {
                return false;
            }

            var scaledDelta = (currentPosition - pointerDownPosition) * boardView.InputSensitivity;
            if (!directionLocked)
            {
                var dominantDistance = Mathf.Max(Mathf.Abs(scaledDelta.x), Mathf.Abs(scaledDelta.y));
                if (dominantDistance < boardView.MinimumSwipeDistance)
                {
                    return false;
                }

                directionLocked = true;
                lockedDirection = ResolveDominantDirection(scaledDelta);
            }

            gestureConsumed = true;
            boardView.CellSwiped(Coordinate, lockedDirection);
            return true;
        }

        private bool IsTap(Vector2 pointerUpPosition)
        {
            if (boardView == null || !boardView.EnableTapToSelect)
            {
                return false;
            }

            var heldDuration = Time.unscaledTime - pointerDownTime;
            var movement = (pointerUpPosition - pointerDownPosition).magnitude;
            return heldDuration <= boardView.MaximumTapDuration &&
                   movement <= boardView.MaximumTapMovementTolerance;
        }

        private bool IsActivePointer(PointerEventData eventData)
        {
            return activePointerId != NoPointerId && eventData.pointerId == activePointerId;
        }

        private void ResetGesture()
        {
            activePointerId = NoPointerId;
            gestureConsumed = false;
            directionLocked = false;
            lockedDirection = Vector2Int.zero;
        }

        private static Vector2Int ResolveDominantDirection(Vector2 delta)
        {
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            {
                return delta.x >= 0f ? Vector2Int.right : Vector2Int.left;
            }

            return delta.y >= 0f ? Vector2Int.up : Vector2Int.down;
        }

        private void EnsureVisuals()
        {
            if (background != null)
            {
                return;
            }

            background = gameObject.GetComponent<Image>();
            if (background == null)
            {
                background = gameObject.AddComponent<Image>();
            }

            background.color = ActiveCellColor;
            background.raycastTarget = true;
            background.preserveAspect = false;

            gridImage = UIFactory.CreateImage("Grid", transform, Color.white);
            gridImage.preserveAspect = false;
            gridImage.raycastTarget = false;
            UIFactory.SetAnchored(
                gridImage.rectTransform,
                Vector2.zero,
                Vector2.one,
                new Vector2(-GridSpriteOverscan, -GridSpriteOverscan),
                new Vector2(GridSpriteOverscan, GridSpriteOverscan));

            companionExitGlow = UIFactory.CreateImage("CompanionExitGlow", transform, CompanionExitGlowColor);
            companionExitGlow.sprite = UIFactory.GetRoundedRectSprite(96, 48, 24f);
            companionExitGlow.type = Image.Type.Sliced;
            companionExitGlow.raycastTarget = false;
            UIFactory.SetAnchored(companionExitGlow.rectTransform, new Vector2(0.08f, 0.02f), new Vector2(0.92f, 0.46f), Vector2.zero, Vector2.zero);

            companionExitPad = UIFactory.CreateImage("CompanionExitPad", transform, CompanionExitPadColor);
            companionExitPad.sprite = UIFactory.GetRoundedRectSprite(96, 38, 19f);
            companionExitPad.type = Image.Type.Sliced;
            companionExitPad.raycastTarget = false;
            UIFactory.SetAnchored(companionExitPad.rectTransform, new Vector2(0.16f, 0.04f), new Vector2(0.84f, 0.31f), Vector2.zero, Vector2.zero);

            companionExitCore = UIFactory.CreateImage("CompanionExitCore", transform, CompanionExitCoreColor);
            companionExitCore.sprite = UIFactory.GetRoundedRectSprite(72, 20, 10f);
            companionExitCore.type = Image.Type.Sliced;
            companionExitCore.raycastTarget = false;
            UIFactory.SetAnchored(companionExitCore.rectTransform, new Vector2(0.25f, 0.07f), new Vector2(0.75f, 0.19f), Vector2.zero, Vector2.zero);

            companionExitArrow = UIFactory.CreateText("CompanionExitArrow", transform, "\u25BE", 42, TextAnchor.MiddleCenter, CompanionExitArrowColor);
            companionExitArrow.fontStyle = FontStyle.Bold;
            companionExitArrow.raycastTarget = false;
            UIFactory.SetAnchored(companionExitArrow.rectTransform, new Vector2(0.2f, 0.15f), new Vector2(0.8f, 0.55f), Vector2.zero, Vector2.zero);
            SetCompanionExitVisible(false);

            softCoverImage = UIFactory.CreateImage("SoftCover", transform, Color.clear);
            softCoverImage.preserveAspect = false;
            softCoverImage.raycastTarget = false;
            UIFactory.Stretch(softCoverImage.rectTransform);

            companionTokenGlow = UIFactory.CreateImage("CompanionTokenGlow", transform, CompanionTokenGlowColor);
            companionTokenGlow.sprite = UIFactory.GetRoundedRectSprite(96, 96, 48f);
            companionTokenGlow.type = Image.Type.Sliced;
            companionTokenGlow.raycastTarget = false;
            UIFactory.SetAnchored(companionTokenGlow.rectTransform, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.98f), Vector2.zero, Vector2.zero);

            companionTokenBadge = UIFactory.CreateImage("CompanionTokenBadge", transform, CompanionTokenBadgeColor);
            companionTokenBadge.sprite = UIFactory.GetRoundedRectSprite(96, 96, 48f);
            companionTokenBadge.type = Image.Type.Sliced;
            companionTokenBadge.raycastTarget = false;
            UIFactory.SetAnchored(companionTokenBadge.rectTransform, new Vector2(0.12f, 0.11f), new Vector2(0.88f, 0.89f), Vector2.zero, Vector2.zero);

            companionTokenRibbon = UIFactory.CreateImage("CompanionTokenRibbon", transform, CompanionTokenRibbonColor);
            companionTokenRibbon.sprite = UIFactory.GetRoundedRectSprite(72, 26, 13f);
            companionTokenRibbon.type = Image.Type.Sliced;
            companionTokenRibbon.raycastTarget = false;
            UIFactory.SetAnchored(companionTokenRibbon.rectTransform, new Vector2(0.24f, 0.04f), new Vector2(0.76f, 0.24f), Vector2.zero, Vector2.zero);

            companionTokenArrow = UIFactory.CreateText("CompanionTokenArrow", transform, "\u25BE", 28, TextAnchor.MiddleCenter, CompanionTokenArrowColor);
            companionTokenArrow.fontStyle = FontStyle.Bold;
            companionTokenArrow.raycastTarget = false;
            UIFactory.SetAnchored(companionTokenArrow.rectTransform, new Vector2(0.26f, 0.02f), new Vector2(0.74f, 0.26f), Vector2.zero, Vector2.zero);
            SetCompanionTokenVisible(false);

            pieceImage = UIFactory.CreateImage("Piece", transform, Color.white);
            pieceImage.preserveAspect = true;
            pieceImage.raycastTarget = false;
            ApplyPieceBounds(false);

            specialOverlayPrimary = UIFactory.CreateImage("SpecialOverlayPrimary", transform, Color.clear);
            specialOverlayPrimary.raycastTarget = false;
            specialOverlayPrimary.enabled = false;

            specialOverlaySecondary = UIFactory.CreateImage("SpecialOverlaySecondary", transform, Color.clear);
            specialOverlaySecondary.raycastTarget = false;
            specialOverlaySecondary.enabled = false;

            pieceLabel = UIFactory.CreateText("PieceLabel", transform, string.Empty, 28, TextAnchor.MiddleCenter, new Color(0.1f, 0.08f, 0.12f));
            pieceLabel.raycastTarget = false;
            UIFactory.Stretch(pieceLabel.rectTransform);

            blockerLabel = UIFactory.CreateText("BlockerLabel", transform, string.Empty, 20, TextAnchor.LowerCenter, new Color(0.14f, 0.06f, 0.02f));
            blockerLabel.raycastTarget = false;
            UIFactory.SetAnchored(blockerLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(2, 2), new Vector2(-2, -2));
        }

        private void ApplySoftCoverBounds(bool matchGridVisualSize)
        {
            if (!matchGridVisualSize)
            {
                UIFactory.Stretch(softCoverImage.rectTransform);
                return;
            }

            var cellSize = ResolveCellSize();
            var targetVisibleSize = new Vector2(
                (cellSize.x + GridSpriteOverscan * 2f) * GridSpriteVisibleWidthRatio,
                (cellSize.y + GridSpriteOverscan * 2f) * GridSpriteVisibleHeightRatio);
            var softCoverRectSize = new Vector2(
                targetVisibleSize.x / SoftCoverSpriteVisibleWidthRatio,
                targetVisibleSize.y / SoftCoverSpriteVisibleHeightRatio);
            var overscan = new Vector2(
                Mathf.Max(0f, (softCoverRectSize.x - cellSize.x) * 0.5f),
                Mathf.Max(0f, (softCoverRectSize.y - cellSize.y) * 0.5f));

            UIFactory.SetAnchored(
                softCoverImage.rectTransform,
                Vector2.zero,
                Vector2.one,
                new Vector2(-overscan.x, -overscan.y),
                new Vector2(overscan.x, overscan.y));
        }

        private Vector2 ResolveCellSize()
        {
            if (transform.parent != null && transform.parent.TryGetComponent<GridLayoutGroup>(out var grid) &&
                grid.cellSize.x > 1f && grid.cellSize.y > 1f)
            {
                return grid.cellSize;
            }

            var rectTransform = transform as RectTransform;
            if (rectTransform != null && rectTransform.rect.width > 1f && rectTransform.rect.height > 1f)
            {
                return rectTransform.rect.size;
            }

            return new Vector2(100f, 100f);
        }

        private void ConfigureSpecialOverlay(BoardPiece piece)
        {
            switch (piece.Kind)
            {
                case PieceKind.Line:
                    specialOverlayPrimary.enabled = true;
                    specialOverlayPrimary.color = new Color(0.35f, 0.9f, 1f, 0.72f);
                    if (piece.LineOrientation == LineOrientation.Horizontal)
                    {
                        SetOverlayRect(specialOverlayPrimary.rectTransform, new Vector2(0.18f, 0.43f), new Vector2(0.82f, 0.57f));
                    }
                    else
                    {
                        SetOverlayRect(specialOverlayPrimary.rectTransform, new Vector2(0.43f, 0.18f), new Vector2(0.57f, 0.82f));
                    }

                    break;
                case PieceKind.Burst:
                    specialOverlayPrimary.enabled = true;
                    specialOverlaySecondary.enabled = true;
                    specialOverlayPrimary.color = new Color(1f, 0.58f, 0.18f, 0.7f);
                    specialOverlaySecondary.color = new Color(1f, 0.9f, 0.22f, 0.58f);
                    SetOverlayRect(specialOverlayPrimary.rectTransform, new Vector2(0.22f, 0.44f), new Vector2(0.78f, 0.56f));
                    SetOverlayRect(specialOverlaySecondary.rectTransform, new Vector2(0.44f, 0.22f), new Vector2(0.56f, 0.78f));
                    break;
                case PieceKind.Rainbow:
                    specialOverlayPrimary.enabled = true;
                    specialOverlaySecondary.enabled = true;
                    specialOverlayPrimary.color = new Color(1f, 0.9f, 0.18f, 0.66f);
                    specialOverlaySecondary.color = new Color(0.35f, 0.9f, 1f, 0.58f);
                    SetOverlayRect(specialOverlayPrimary.rectTransform, new Vector2(0.18f, 0.35f), new Vector2(0.82f, 0.46f));
                    SetOverlayRect(specialOverlaySecondary.rectTransform, new Vector2(0.18f, 0.54f), new Vector2(0.82f, 0.65f));
                    break;
            }
        }

        private void ApplyPieceBounds(bool specialSprite)
        {
            var inset = NormalPieceInset;
            if (specialSprite)
            {
                inset = currentPieceCharacter == CharacterType.Bear ? BearSpecialInset : OtherSpecialInset;
            }

            UIFactory.SetAnchored(pieceImage.rectTransform, Vector2.zero, Vector2.one, new Vector2(inset, inset), new Vector2(-inset, -inset));
        }

        private void EnableIdleMotion(BoardPiece piece)
        {
            pieceIdleEnabled = true;
            currentPieceKind = piece.Kind;
            currentPieceCharacter = piece.Character;
            idlePhase = piece.Id * 0.47f + Coordinate.x * 0.31f + Coordinate.y * 0.23f + (int)piece.Kind * 0.61f;
            UpdateIdleMotion(Time.unscaledTime);
        }

        private void DisableIdleMotion()
        {
            pieceIdleEnabled = false;
            idlePieceOffset = Vector2.zero;
            idlePieceScale = Vector3.one;
            idlePieceRotation = Quaternion.identity;
        }

        private void UpdateIdleMotion(float time)
        {
            var phase = time * IdleAngularSpeed + idlePhase;
            var bob = Mathf.Sin(phase);
            var sway = Mathf.Sin(phase * 0.73f + 1.1f);
            var breath = Mathf.Sin(phase + Mathf.PI * 0.5f);
            var specialMultiplier = currentPieceKind == PieceKind.Normal ? 1f : 1.12f;

            idlePieceOffset = new Vector2(
                sway * IdleSwayAmplitude * specialMultiplier,
                bob * IdleBobAmplitude * specialMultiplier);
            idlePieceScale = new Vector3(
                1f + breath * IdleSquashAmplitude,
                1f - breath * IdleSquashAmplitude * 0.72f,
                1f);
            idlePieceRotation = Quaternion.Euler(0f, 0f, sway * IdleTiltAmplitude * specialMultiplier);
            ApplyPieceVisualTransform();
        }

        private void ApplyPieceVisualTransform()
        {
            var pieceOffset = pieceIdleEnabled ? effectPieceOffset + idlePieceOffset : effectPieceOffset;
            var pieceScale = pieceIdleEnabled
                ? new Vector3(effectPieceScale * idlePieceScale.x, effectPieceScale * idlePieceScale.y, 1f)
                : new Vector3(effectPieceScale, effectPieceScale, 1f);
            var blockerScale = new Vector3(effectPieceScale, effectPieceScale, 1f);
            var pieceRotation = pieceIdleEnabled ? idlePieceRotation : Quaternion.identity;

            ApplyPieceGraphicTransform(companionTokenGlow.rectTransform, pieceOffset, pieceScale, pieceRotation);
            ApplyPieceGraphicTransform(companionTokenBadge.rectTransform, pieceOffset, pieceScale, pieceRotation);
            ApplyPieceGraphicTransform(companionTokenRibbon.rectTransform, pieceOffset, pieceScale, pieceRotation);
            ApplyPieceGraphicTransform(companionTokenArrow.rectTransform, pieceOffset, pieceScale, pieceRotation);
            ApplyPieceGraphicTransform(pieceImage.rectTransform, pieceOffset, pieceScale, pieceRotation);
            ApplyPieceGraphicTransform(specialOverlayPrimary.rectTransform, pieceOffset, pieceScale, pieceRotation);
            ApplyPieceGraphicTransform(specialOverlaySecondary.rectTransform, pieceOffset, pieceScale, pieceRotation);
            ApplyPieceGraphicTransform(pieceLabel.rectTransform, pieceOffset, pieceScale, pieceRotation);
            ApplyPieceGraphicTransform(blockerLabel.rectTransform, effectPieceOffset, blockerScale, Quaternion.identity);
        }

        private void ApplyPieceVisualAlpha()
        {
            SetGraphicAlpha(pieceImage, effectPieceAlpha);
            SetGraphicAlpha(specialOverlayPrimary, effectPieceAlpha);
            SetGraphicAlpha(specialOverlaySecondary, effectPieceAlpha);
            SetGraphicAlpha(pieceLabel, effectPieceAlpha);
            SetGraphicAlpha(blockerLabel, effectPieceAlpha);
            ApplyCompanionTokenAlpha(effectPieceAlpha);
        }

        private void SetCompanionExitVisible(bool visible)
        {
            companionExitGlow.enabled = visible;
            companionExitPad.enabled = visible;
            companionExitCore.enabled = visible;
            companionExitArrow.enabled = visible;
            companionExitGlow.color = CompanionExitGlowColor;
            companionExitPad.color = CompanionExitPadColor;
            companionExitCore.color = CompanionExitCoreColor;
            companionExitArrow.color = CompanionExitArrowColor;
        }

        private void SetCompanionTokenVisible(bool visible)
        {
            companionTokenGlow.enabled = visible;
            companionTokenBadge.enabled = visible;
            companionTokenRibbon.enabled = visible;
            companionTokenArrow.enabled = visible;
            companionTokenGlow.color = CompanionTokenGlowColor;
            companionTokenBadge.color = CompanionTokenBadgeColor;
            companionTokenRibbon.color = CompanionTokenRibbonColor;
            companionTokenArrow.color = CompanionTokenArrowColor;
        }

        private void ApplyCompanionPieceBounds()
        {
            UIFactory.SetAnchored(pieceImage.rectTransform, new Vector2(0.15f, 0.18f), new Vector2(0.85f, 0.92f), Vector2.zero, Vector2.zero);
        }

        private void ApplyCompanionTokenAlpha(float alpha)
        {
            SetGraphicAlpha(companionTokenGlow, CompanionTokenGlowColor.a * alpha);
            SetGraphicAlpha(companionTokenBadge, CompanionTokenBadgeColor.a * alpha);
            SetGraphicAlpha(companionTokenRibbon, CompanionTokenRibbonColor.a * alpha);
            SetGraphicAlpha(companionTokenArrow, CompanionTokenArrowColor.a * alpha);
        }

        private static void ApplyPieceGraphicTransform(RectTransform rectTransform, Vector2 offset, Vector3 scale, Quaternion rotation)
        {
            rectTransform.anchoredPosition = offset;
            rectTransform.localScale = scale;
            rectTransform.localRotation = rotation;
        }

        private static void SetOverlayRect(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localRotation = Quaternion.identity;
        }

        private static void SetGraphicAlpha(Graphic graphic, float alpha)
        {
            var color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }
    }
}
