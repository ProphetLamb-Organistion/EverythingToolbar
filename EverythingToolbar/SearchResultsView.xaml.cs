﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace EverythingToolbar
{
    public partial class SearchResultsView
    {
        private SearchResult SelectedItem => SearchResultsListView.SelectedItem as SearchResult;

        public SearchResultsView()
        {
            InitializeComponent();

            SearchResultsListView.ItemsSource = EverythingSearch.Instance.SearchResults;
            ((INotifyCollectionChanged)SearchResultsListView.Items).CollectionChanged += OnCollectionChanged;
            ((CollectionView)CollectionViewSource.GetDefaultView(SearchResultsListView.ItemsSource)).GroupDescriptions.Add(new PropertyGroupDescription("IsFavorite"));
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (SearchResultsListView.SelectedIndex == -1 && SearchResultsListView.Items.Count > 0)
                SearchResultsListView.SelectedIndex = 0;
        }

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (!(e.VerticalChange > 0)) return;
            if (e.VerticalOffset > e.ExtentHeight - 2 * e.ViewportHeight)
            {
                EverythingSearch.Instance.QueryBatch();
                ScrollToVerticalOffset(e.VerticalOffset);
            }
        }

        public void ScrollToVerticalOffset(double verticalOffset)
        {
            Dispatcher.Invoke(() =>
            {
                Decorator listViewBorder = VisualTreeHelper.GetChild(SearchResultsListView, 0) as Decorator;
                ScrollViewer listViewScrollViewer = listViewBorder.Child as ScrollViewer;
                listViewScrollViewer.ScrollToVerticalOffset(verticalOffset);
            }, DispatcherPriority.ContextIdle);
        }

        public void SelectNextSearchResult()
        {
            if (SearchResultsListView.SelectedIndex + 1 < SearchResultsListView.Items.Count)
            {
                SearchResultsListView.SelectedIndex++;
                SearchResultsListView.ScrollIntoView(SearchResultsListView.SelectedItem);
            }
        }

        public void SelectPreviousSearchResult()
        {
            if (SearchResultsListView.SelectedIndex > 0)
            {
                SearchResultsListView.SelectedIndex--;
                SearchResultsListView.ScrollIntoView(SelectedItem);
            }
        }

        public void OpenSelectedSearchResult()
        {
            if (SearchResultsListView.SelectedIndex == -1)
                SelectNextSearchResult();

            if (SearchResultsListView.SelectedIndex == -1 || Rules.HandleRule(SelectedItem))
                return;

            SelectedItem?.Open();
        }

        private void OpenFilePath(object sender, RoutedEventArgs e)
        {
            SelectedItem?.OpenPath();
        }

        private void CopyPathToClipBoard(object sender, RoutedEventArgs e)
        {
            SelectedItem?.CopyPathToClipboard();
            EverythingSearch.Instance.Reset();
        }

        private void OpenWith(object sender, RoutedEventArgs e)
        {
            SelectedItem?.OpenWith();
        }

        private void ShowInEverything(object sender, RoutedEventArgs e)
        {
            SelectedItem?.ShowInEverything();
        }

        private void CopyFile(object sender, RoutedEventArgs e)
        {
            SelectedItem?.CopyToClipboard();
            EverythingSearch.Instance.Reset();
        }

        private void Open(object sender, RoutedEventArgs e)
        {
            OpenSelectedSearchResult();
        }

        private void Open(object sender, MouseEventArgs e)
        {
            OpenSelectedSearchResult();
        }

        public void ShowFileProperties(object sender, RoutedEventArgs e)
        {
            SelectedItem?.ShowProperties();
        }

        private void OnOpenWithMenuLoaded(object sender, RoutedEventArgs e)
        {
            MenuItem mi = sender as MenuItem;

            while (mi.Items.Count > 3)
                mi.Items.RemoveAt(0);

            List<Rule> rules = Rules.LoadRules();
            if (mi.Items[0] is MenuItem cMi)
                cMi.Visibility = rules.Count > 0 ? Visibility.Collapsed : Visibility.Visible;

            for (int i = rules.Count - 1; i >= 0; i--)
            {
                Rule rule = rules[i];
                MenuItem ruleMenuItem = new MenuItem() { Header = rule.Name, Tag = rule.Command };
                ruleMenuItem.Click += OpenWithRule;
                mi.Items.Insert(0, ruleMenuItem);
            }
        }

        private void OpenWithRule(object sender, RoutedEventArgs e)
        {
            SearchResult searchResult = SearchResultsListView.SelectedItem as SearchResult;
            string command = (sender as MenuItem)?.Tag?.ToString() ?? "";
            Rules.HandleRule(searchResult, command);
        }

        private void OnListViewItemClicked(object sender, MouseButtonEventArgs e)
        {
            var item = (sender as Border)?.DataContext;
            SearchResultsListView.SelectedIndex = SearchResultsListView.Items.IndexOf(item);
        }

        private void AddToFavorites(object sender, RoutedEventArgs e)
        {
            EverythingSearch.Favorites.AddAndWriteAsync(SelectedItem).ConfigureAwait(false);
        }
    }
}
