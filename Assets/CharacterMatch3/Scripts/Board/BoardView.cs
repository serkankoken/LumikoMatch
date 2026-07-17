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
        private BoardController controller;
        private BoardModel model;
        private CharacterCatalog catalog;
        private BoardCoordinate? selectedCoordinate;

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

            for (var i = boardRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(boardRoot.GetChild(i).gameObject);
            }

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
                yield return AnimateTwoPieceSwap(fromView, toView, offset, 0.12f);
            }
            else
            {
                yield return AnimateRejectedSwap(fromView, offset * 0.34f, 0.2f);
            }

            RefreshAll();
        }

        public IEnumerator AnimateBoardSettled()
        {
            yield return new WaitForSeconds(0.08f);
            RefreshAll();
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
                return;
            }

            var root = new GameObject("BoardRoot", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(transform, false);
            boardRoot = root.GetComponent<RectTransform>();
            boardRoot.sizeDelta = new Vector2(940, 940);
            root.GetComponent<Image>().color = new Color(0.15f, 0.28f, 0.38f, 0.32f);
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

        private static IEnumerator AnimateTwoPieceSwap(BoardCellView fromView, BoardCellView toView, Vector2 offset, float duration)
        {
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                fromView.SetPieceOffset(Vector2.Lerp(Vector2.zero, offset, t));
                toView.SetPieceOffset(Vector2.Lerp(Vector2.zero, -offset, t));
                yield return null;
            }

            fromView.SetPieceOffset(Vector2.zero);
            toView.SetPieceOffset(Vector2.zero);
        }

        private static IEnumerator AnimateRejectedSwap(BoardCellView fromView, Vector2 offset, float duration)
        {
            var halfDuration = duration * 0.5f;
            for (var elapsed = 0f; elapsed < halfDuration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / halfDuration);
                fromView.SetPieceOffset(Vector2.Lerp(Vector2.zero, offset, t));
                yield return null;
            }

            for (var elapsed = 0f; elapsed < halfDuration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / halfDuration);
                fromView.SetPieceOffset(Vector2.Lerp(offset, Vector2.zero, t));
                yield return null;
            }

            fromView.SetPieceOffset(Vector2.zero);
        }
    }
}
