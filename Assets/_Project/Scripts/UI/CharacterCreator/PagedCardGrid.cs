using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace TacticsGame.UI.CharacterCreator
{
    /// <summary>
    /// Fixed-page card grid that satisfies the strict no-ScrollView constraint:
    /// items are rendered in flex pages and stepped through with a discrete pager.
    /// Designed for D-pad / joystick navigation as well as mouse.
    /// </summary>
    public class PagedCardGrid<T>
    {
        public class CardBindContext
        {
            public T Item;
            public int IndexInSource;
            public bool IsSelected;
        }

        private readonly VisualElement host;
        private readonly VisualElement pageContainer;
        private readonly VisualElement pager;
        private readonly Button btnPrev;
        private readonly Button btnNext;
        private readonly Label pageLabel;

        private readonly Func<VisualElement> makeCard;
        private readonly Action<VisualElement, CardBindContext> bindCard;

        private List<T> items = new List<T>();
        private int pageSize;
        private int pageIndex;
        private int selectedIndex = -1;

        public Action<int, T> OnSelectionChanged;
        public Action<int, T> OnFocusChanged;

        /// <summary>
        /// Fired whenever the page changes. Parameter is the first item index on the new page.
        /// </summary>
        public Action OnPageChanged;

        public IReadOnlyList<T> Items => items;
        public int SelectedIndex => selectedIndex;
        public T SelectedItem =>
            selectedIndex >= 0 && selectedIndex < items.Count ? items[selectedIndex] : default;

        public IReadOnlyList<VisualElement> CurrentPageCards => currentPageCards;
        private readonly List<VisualElement> currentPageCards = new List<VisualElement>();

        public PagedCardGrid(
            VisualElement host,
            int pageSize,
            Func<VisualElement> makeCard,
            Action<VisualElement, CardBindContext> bindCard
        )
        {
            this.host = host;
            this.pageSize = pageSize;
            this.makeCard = makeCard;
            this.bindCard = bindCard;

            host.Clear();
            host.AddToClassList("paged-grid");

            pageContainer = new VisualElement { name = "PagedGridPage" };
            pageContainer.AddToClassList("paged-grid__page");
            host.Add(pageContainer);

            pager = new VisualElement { name = "PagedGridPager" };
            pager.AddToClassList("paged-grid__pager");

            btnPrev = new Button(GoToPreviousPage) { text = "<" };
            btnPrev.AddToClassList("pager-btn");
            pager.Add(btnPrev);

            pageLabel = new Label("PAGE 1 / 1");
            pageLabel.AddToClassList("pager-label");
            pager.Add(pageLabel);

            btnNext = new Button(GoToNextPage) { text = ">" };
            btnNext.AddToClassList("pager-btn");
            pager.Add(btnNext);

            host.Add(pager);
        }

        public void SetSingleColumn(bool single)
        {
            if (single)
                pageContainer.AddToClassList("paged-grid__page--single-column");
            else
                pageContainer.RemoveFromClassList("paged-grid__page--single-column");
        }

        public void SetItems(List<T> source, int? preserveSelectionAt = null)
        {
            items = source ?? new List<T>();

            if (
                preserveSelectionAt.HasValue
                && preserveSelectionAt.Value >= 0
                && preserveSelectionAt.Value < items.Count
            )
                selectedIndex = preserveSelectionAt.Value;
            else if (selectedIndex >= items.Count)
                selectedIndex = items.Count > 0 ? 0 : -1;

            pageIndex = 0;
            if (selectedIndex >= 0 && pageSize > 0)
                pageIndex = selectedIndex / pageSize;

            Render();
        }

        public void SetSelectedIndex(int index, bool fireCallback = true)
        {
            if (index < 0 || index >= items.Count)
                return;

            selectedIndex = index;
            if (pageSize > 0)
                pageIndex = selectedIndex / pageSize;

            Render();
            if (fireCallback)
                OnSelectionChanged?.Invoke(selectedIndex, items[selectedIndex]);
        }

        public bool TryFocusByPredicate(Predicate<T> predicate, bool fireCallback = false)
        {
            if (predicate == null)
                return false;

            int found = items.FindIndex(predicate);
            if (found < 0)
                return false;

            SetSelectedIndex(found, fireCallback);
            return true;
        }

        public void GoToNextPage()
        {
            int totalPages = GetTotalPages();
            if (pageIndex < totalPages - 1)
            {
                pageIndex++;
                Render();
                OnPageChanged?.Invoke();
            }
        }

        public void GoToPreviousPage()
        {
            if (pageIndex > 0)
            {
                pageIndex--;
                Render();
                OnPageChanged?.Invoke();
            }
        }

        public bool MoveSelection(int delta)
        {
            if (items.Count == 0)
                return false;

            int next = selectedIndex < 0 ? 0 : selectedIndex + delta;
            if (next < 0)
                next = 0;
            if (next >= items.Count)
                next = items.Count - 1;
            SetSelectedIndex(next);
            return true;
        }

        private int GetTotalPages()
        {
            if (pageSize <= 0 || items.Count == 0)
                return 1;
            return (items.Count + pageSize - 1) / pageSize;
        }

        private void Render()
        {
            pageContainer.Clear();
            currentPageCards.Clear();

            int totalPages = GetTotalPages();
            int start = pageIndex * pageSize;
            int end = System.Math.Min(start + pageSize, items.Count);

            for (int i = start; i < end; i++)
            {
                int captured = i;
                VisualElement card = makeCard();
                card.userData = captured;

                CardBindContext ctx = new CardBindContext
                {
                    Item = items[i],
                    IndexInSource = i,
                    IsSelected = i == selectedIndex,
                };

                bindCard(card, ctx);

                card.RegisterCallback<ClickEvent>(_ => SetSelectedIndex(captured));
                card.RegisterCallback<FocusEvent>(_ =>
                    OnFocusChanged?.Invoke(captured, items[captured])
                );
                pageContainer.Add(card);
                currentPageCards.Add(card);
            }

            pageLabel.text = $"PAGE {pageIndex + 1} / {totalPages}";
            btnPrev.SetEnabled(pageIndex > 0);
            btnNext.SetEnabled(pageIndex < totalPages - 1);

            // Hide the pager entirely when there's nothing to page through.
            bool hidePager = totalPages <= 1;
            if (hidePager)
                pager.AddToClassList("paged-grid__pager--hidden");
            else
                pager.RemoveFromClassList("paged-grid__pager--hidden");
        }
    }
}
