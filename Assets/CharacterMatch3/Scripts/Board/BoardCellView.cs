using CharacterMatch3.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CharacterMatch3.Board
{
    public sealed class BoardCellView : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private const int NoPointerId = int.MinValue;

        private BoardView boardView;
        private Image background;
        private Image pieceImage;
        private Image softCoverImage;
        private Text pieceLabel;
        private Text blockerLabel;
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

            if (cell == null || !cell.Active)
            {
                background.color = new Color(0f, 0f, 0f, 0.12f);
                pieceImage.enabled = false;
                softCoverImage.enabled = false;
                pieceLabel.text = string.Empty;
                blockerLabel.text = string.Empty;
                return;
            }

            background.color = selected ? new Color(1f, 0.93f, 0.35f, 0.95f) : new Color(1f, 1f, 1f, 0.36f);
            softCoverImage.enabled = cell.SoftCoverLayers > 0;
            softCoverImage.color = cell.SoftCoverLayers > 1
                ? new Color(0.38f, 0.83f, 0.92f, 0.72f)
                : new Color(0.65f, 0.93f, 0.98f, 0.5f);

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
                pieceLabel.text = "PAL";
            }
            else
            {
                pieceImage.enabled = true;
                pieceImage.sprite = catalog != null ? catalog.GetSprite(cell.Piece.Character) : null;
                pieceImage.color = pieceImage.sprite != null
                    ? Color.white
                    : catalog != null
                        ? catalog.GetFallbackColor(cell.Piece.Character)
                        : Color.white;
                pieceLabel.text = GetPieceLabel(cell.Piece);
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
            pieceLabel.rectTransform.anchoredPosition = offset;
            blockerLabel.rectTransform.anchoredPosition = offset;
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

            background.color = new Color(1f, 1f, 1f, 0.35f);
            var mask = gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            softCoverImage = UIFactory.CreateImage("SoftCover", transform, Color.clear);
            UIFactory.Stretch(softCoverImage.rectTransform);

            pieceImage = UIFactory.CreateImage("Piece", transform, Color.white);
            pieceImage.preserveAspect = true;
            UIFactory.SetAnchored(pieceImage.rectTransform, Vector2.zero, Vector2.one, new Vector2(8, 8), new Vector2(-8, -8));

            pieceLabel = UIFactory.CreateText("PieceLabel", transform, string.Empty, 28, TextAnchor.MiddleCenter, new Color(0.1f, 0.08f, 0.12f));
            UIFactory.Stretch(pieceLabel.rectTransform);

            blockerLabel = UIFactory.CreateText("BlockerLabel", transform, string.Empty, 20, TextAnchor.LowerCenter, new Color(0.14f, 0.06f, 0.02f));
            UIFactory.SetAnchored(blockerLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(2, 2), new Vector2(-2, -2));
        }

        private static string GetPieceLabel(BoardPiece piece)
        {
            return piece.Kind switch
            {
                PieceKind.Line => piece.LineOrientation == LineOrientation.Horizontal ? "LINE H" : "LINE V",
                PieceKind.Burst => "BURST",
                PieceKind.Rainbow => "RAIN",
                _ => string.Empty
            };
        }
    }
}
