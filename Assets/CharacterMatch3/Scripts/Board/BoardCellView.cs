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
        private static readonly Color InactiveCellColor = new Color(0f, 0f, 0f, 0.06f);
        private static readonly Color ActiveCellColor = new Color(1f, 1f, 1f, 0.16f);
        private static readonly Color SelectedCellColor = new Color(1f, 0.93f, 0.35f, 0.95f);
        private static readonly Color SoftCoverSingleLayerFallbackColor = new Color(0.65f, 0.93f, 0.98f, 0.5f);
        private static readonly Color SoftCoverMultiLayerFallbackColor = new Color(0.38f, 0.83f, 0.92f, 0.72f);

        private BoardView boardView;
        private Image background;
        private Image pieceImage;
        private Image softCoverImage;
        private Image specialOverlayPrimary;
        private Image specialOverlaySecondary;
        private Text pieceLabel;
        private Text blockerLabel;
        private bool usingSpecialSprite;
        private CharacterType currentPieceCharacter;
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
            currentPieceCharacter = CharacterType.Cat;
            ResetPieceVisualState();

            if (cell == null || !cell.Active)
            {
                background.sprite = null;
                background.color = InactiveCellColor;
                pieceImage.enabled = false;
                softCoverImage.enabled = false;
                softCoverImage.sprite = null;
                specialOverlayPrimary.enabled = false;
                specialOverlaySecondary.enabled = false;
                pieceLabel.text = string.Empty;
                blockerLabel.text = string.Empty;
                return;
            }

            background.sprite = null;
            background.color = selected ? SelectedCellColor : ActiveCellColor;
            softCoverImage.enabled = cell.SoftCoverLayers > 0;
            specialOverlayPrimary.enabled = false;
            specialOverlaySecondary.enabled = false;
            var softCoverSprite = catalog != null ? catalog.SoftCoverSprite : null;
            softCoverImage.sprite = softCoverImage.enabled ? softCoverSprite : null;
            softCoverImage.color = softCoverImage.enabled
                ? softCoverSprite != null
                    ? Color.white
                    : cell.SoftCoverLayers > 1
                        ? SoftCoverMultiLayerFallbackColor
                        : SoftCoverSingleLayerFallbackColor
                : Color.clear;

            blockerLabel.text = string.Empty;
            if (cell.CrateLayers > 0)
            {
                pieceImage.enabled = false;
                pieceLabel.text = string.Empty;
                blockerLabel.text = cell.CrateLayers > 1 ? "CR2" : "CR";
                return;
            }

            if (cell.Piece == null)
            {
                pieceImage.enabled = false;
                pieceLabel.text = string.Empty;
            }
            else if (cell.Piece.Kind == PieceKind.Companion)
            {
                pieceImage.enabled = true;
                pieceImage.sprite = null;
                pieceImage.color = new Color(1f, 0.92f, 0.4f);
                pieceLabel.text = string.Empty;
            }
            else
            {
                pieceImage.enabled = true;
                var specialSprite = catalog != null
                    ? catalog.GetSpecialSprite(cell.Piece.Character, cell.Piece.Kind, cell.Piece.LineOrientation)
                    : null;
                usingSpecialSprite = specialSprite != null;
                currentPieceCharacter = cell.Piece.Character;
                ApplyPieceBounds(usingSpecialSprite);
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
            }

            if (cell.LockLayers > 0)
            {
                blockerLabel.text = cell.LockLayers > 1 ? "LK2" : "LK";
            }
            else if (cell.IsCompanionExit)
            {
                blockerLabel.text = "EXIT";
            }
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
            pieceImage.rectTransform.anchoredPosition = offset;
            specialOverlayPrimary.rectTransform.anchoredPosition = offset;
            specialOverlaySecondary.rectTransform.anchoredPosition = offset;
            pieceLabel.rectTransform.anchoredPosition = offset;
            blockerLabel.rectTransform.anchoredPosition = offset;
        }

        public void SetPieceScale(float scale)
        {
            EnsureVisuals();
            var scaled = new Vector3(scale, scale, 1f);
            pieceImage.rectTransform.localScale = scaled;
            specialOverlayPrimary.rectTransform.localScale = scaled;
            specialOverlaySecondary.rectTransform.localScale = scaled;
            pieceLabel.rectTransform.localScale = scaled;
            blockerLabel.rectTransform.localScale = scaled;
        }

        public void SetPieceAlpha(float alpha)
        {
            EnsureVisuals();
            SetGraphicAlpha(pieceImage, alpha);
            SetGraphicAlpha(specialOverlayPrimary, alpha);
            SetGraphicAlpha(specialOverlaySecondary, alpha);
            SetGraphicAlpha(pieceLabel, alpha);
            SetGraphicAlpha(blockerLabel, alpha);
        }

        public void ResetPieceVisualState()
        {
            SetPieceOffset(Vector2.zero);
            SetPieceScale(1f);
            SetPieceAlpha(1f);
            ApplyPieceBounds(usingSpecialSprite);
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

            softCoverImage = UIFactory.CreateImage("SoftCover", transform, Color.clear);
            softCoverImage.preserveAspect = false;
            softCoverImage.raycastTarget = false;
            UIFactory.Stretch(softCoverImage.rectTransform);

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
