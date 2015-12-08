﻿using CaledosLab.Portable.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using TinyTinyRSS.Classes;
using TinyTinyRSS.Interface;
using TinyTinyRSS.Interface.Classes;
using Windows.ApplicationModel.Resources;
using Windows.Foundation;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace TinyTinyRSS
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : AbstractArticlePage
    {
        private bool validConnection = false;
        private bool feedListUpdate = false;
		private int initialIndex = 0;
        public Rect TogglePaneButtonRect
        {
            get;
            private set;
        }

        public MainPage()
        {
            this.InitializeComponent();
            this.Loaded += PageLoaded;
            ArticlesCollection = new ObservableCollection<WrappedArticle>();
            _showUnreadOnly = ConnectionSettings.getInstance().showUnreadOnly;
            _sortOrder = ConnectionSettings.getInstance().sortOrder;
            _moreArticles = true;
            _moreArticlesLoading = false;
            //RegisterForShare();
            //BuildLocalizedApplicationBar();
            //UpdateLocalizedApplicationBar(true);
        }

        private async void PageLoaded(object sender, RoutedEventArgs e)
        {
		
			// TODO if server empty goto settingspage
            try
            {
                validConnection = await TtRssInterface.getInterface().CheckLogin();
                if (validConnection)
                {
                    await TtRssInterface.getInterface().getCounters();
                    Task specialFeedsTask = UpdateSpecialFeeds();
                    Task allFeedsTask = UpdateAllFeedsList(true);
					Task<bool> headlinesTask = null;
					if (!initialized)
					{
						headlinesTask = LoadHeadlines();
					}
					if (ConnectionSettings.getInstance().selectedFeed <= 0)
					{
						switch (ConnectionSettings.getInstance().selectedFeed)
						{
							case -3: FeedTitle.Text = loader.GetString("FreshFeedsText"); break;
							case -1: FeedTitle.Text = loader.GetString("StarredFeedsText"); break;
							case -2: FeedTitle.Text = loader.GetString("PublishedFeedsText"); break;
							case -6: FeedTitle.Text = loader.GetString("RecentlyReadFeedText"); break;
							case -4: FeedTitle.Text = loader.GetString("AllFeedsTitleText"); break;
							case 0: FeedTitle.Text = loader.GetString("ArchivedFeedsText"); break;
						}
					}
					else
					{
						try
						{
							FeedTitle.Text = TtRssInterface.getInterface().getFeedById(ConnectionSettings.getInstance().selectedFeed).title;
						}
						catch (TtRssException ex)
						{
							checkException(ex);
						}
					}
					await specialFeedsTask;
					await allFeedsTask;
                    await PushNotificationHelper.UpdateNotificationChannel();
                    if (!initialized)
					{
						bool result = await headlinesTask;
						if (!result)
						{
							return;
						}
						HeadlinesView.DataContext = ArticlesCollection;
					}
					var sv = (ScrollViewer)VisualTreeHelper.GetChild(VisualTreeHelper.GetChild(this.HeadlinesView, 0), 0);
					sv.ViewChanged += ViewChanged;
					HeadlinesView.ScrollIntoView(ArticlesCollection[initialIndex], ScrollIntoViewAlignment.Leading);
                }
                else
                {
                    MessageDialog msgbox = new MessageDialog(loader.GetString("NoConnection"));
                    await msgbox.ShowAsync();
                }

            }
            catch (TtRssException ex)
            {
                checkException(ex);
            }
        }

        /// <summary>
        /// Callback when the SplitView's Pane is toggled open or close.  When the Pane is not visible
        /// then the floating hamburger may be occluding other content in the app unless it is aware.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TogglePaneButton_Checked(object sender, RoutedEventArgs e)
        {
            this.CheckTogglePaneButtonSizeChanged();
        }

        protected override void SetProgressBar(bool on, bool showText)
        {
        }

        /// <summary>
        /// Check for the conditions where the navigation pane does not occupy the space under the floating
        /// hamburger button and trigger the event.
        /// </summary>
        private void CheckTogglePaneButtonSizeChanged()
        {
            if (this.RootSplitView.DisplayMode == SplitViewDisplayMode.Inline ||
                this.RootSplitView.DisplayMode == SplitViewDisplayMode.Overlay)
            {
                var transform = this.TogglePaneButton.TransformToVisual(this);
                var rect = transform.TransformBounds(new Rect(0, 0, this.TogglePaneButton.ActualWidth, this.TogglePaneButton.ActualHeight));
                this.TogglePaneButtonRect = rect;
            }
            else
            {
                this.TogglePaneButtonRect = new Rect();
            }
        }

        private async void FeedTapped(object sender, TappedRoutedEventArgs e)
        {
            if (validConnection)
            {
                int feedId = -3;
                if (sender == Fresh)
                {
                    feedId = (int)FeedId.Fresh;
                }
                else if (sender == Archived)
                {
                    feedId =  (int)FeedId.Archived;
                }
                else if (sender == Starred)
                {
                    feedId =  (int)FeedId.Starred;
                }
                else if (sender == All)
                {
                    feedId =  (int)FeedId.All;
                }
                else if (sender == Published)
                {
                    feedId =  (int)FeedId.Published;
                }
                else if (sender == Recent)
                {
                    feedId =  (int)FeedId.RecentlyRead;
                }
                ConnectionSettings.getInstance().selectedFeed = feedId;
                await LoadHeadlines();
            }
            else
            {
                MessageDialog msgbox = new MessageDialog(loader.GetString("NoConnection"));
                await msgbox.ShowAsync();
            }
        }

        private Category getCategoryById(List<Category> categories, int id)
        {
            foreach (Category cat in categories)
            {
                if (cat.id == id)
                {
                    return cat;
                }
            }
            return null;
        }

        private async void AllFeedsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!feedListUpdate)
            {
                ExtendedFeed selected = AllFeedsList.SelectedItem as ExtendedFeed;
                if (selected == null)
                {
                    return;
                }
                ConnectionSettings.getInstance().selectedFeed = selected.feed.id;
                await LoadHeadlines();
                AllFeedsList.SelectedItem = null;
            }
        }
        private async Task UpdateAllFeedsList(bool refresh)
        {
            try
            {
                feedListUpdate = true;
                List<Feed> theFeeds = await TtRssInterface.getInterface().getFeeds(refresh);
                theFeeds.Sort();
                List<Category> categories = await TtRssInterface.getInterface().getCategories();

                List<ExtendedFeed> extendedFeeds = new List<ExtendedFeed>();
                foreach (Feed feed in theFeeds)
                {
                    extendedFeeds.Add(new ExtendedFeed(feed, getCategoryById(categories, feed.cat_id)));
                }

                var ordered =
                    from feed in extendedFeeds
                    orderby feed.cat
                    group feed by feed.cat into feedByTitle
                    select feedByTitle;

                groupedFeeds.Source = ordered;
                feedListUpdate = false;
            }
            catch (TtRssException ex)
            {
                checkException(ex);
            }
        }
        private async Task UpdateSpecialFeeds()
        {
            // Counters
            try
            {
                // Unread
                int unread = await TtRssInterface.getInterface().getUnReadCount(true);
                Task tsk = PushNotificationHelper.UpdateLiveTile(unread);
                if (unread != 0)
                {
                    Fresh.Text = loader.GetString("FreshFeedsText") + " (" + unread + ")";
                }
                else
                {
                    Fresh.Text = loader.GetString("FreshFeedsText") + " (-)";
                }
                await tsk;
                // Starred
                int starredCount = await TtRssInterface.getInterface().getCountForFeed(false, (int)FeedId.Starred);
                if (starredCount != 0)
                {
                    Starred.Text = loader.GetString("StarredFeedsText") + " (" + starredCount + ")";
                }
                else
                {
                    Starred.Text = loader.GetString("StarredFeedsText") + " (-)";
                }
                // Archived
                int archCount = await TtRssInterface.getInterface().getCountForFeed(false, (int)FeedId.Archived);
                if (archCount != 0)
                {
                    Archived.Text = loader.GetString("ArchivedFeedsText") + " (" + archCount + ")";
                }
                else
                {
                    Archived.Text = loader.GetString("ArchivedFeedsText") + " (-)";
                }
                // Published
                int publishedCount = await TtRssInterface.getInterface().getCountForFeed(false, (int)FeedId.Published);
                if (publishedCount != 0)
                {
                    Published.Text = loader.GetString("PublishedFeedsText") + " (" + publishedCount + ")";
                }
                else
                {
                    Published.Text = loader.GetString("PublishedFeedsText") + " (-)";
                }
            }
            catch (TtRssException ex)
            {
                checkException(ex);
            }
        }
        /// <summary>
        /// Execute actions matching the touched app bar button.
        /// </summary>
        /// <param name="sender">Button that has been touched</param>
        /// <param name="e">Events</param>
        private async void AppBarButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender.Equals(this.settingsAppBarButton))
            {
                Frame.Navigate(typeof(SettingsPage));
            }
            else if (sender.Equals(this.infoAppBarButton))
            {
                var uri = new Uri("https://thescientist.eu/?p=1057");
                await Windows.System.Launcher.LaunchUriAsync(uri);
            }
            else if (sender.Equals(this.refreshAppBarButton))
            {
                if (!validConnection)
                {
                    validConnection = await TtRssInterface.getInterface().CheckLogin();
                }
                if (!validConnection)
                {
                    MessageDialog msgbox = new MessageDialog(loader.GetString("NoConnection"));
                    await msgbox.ShowAsync();
                    return;
                }

                try
                {
                    await TtRssInterface.getInterface().getCounters();
                    await UpdateAllFeedsList(true);
                    await UpdateSpecialFeeds();
                }
                catch (TtRssException ex)
                {
                    checkException(ex);
                }
            }
        }

        private async void ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (e.IsIntermediate)
            {
                return;
            }
            ScrollViewer sv = (ScrollViewer)sender;
            var verticalOffsetValue = sv.VerticalOffset;
            var maxVerticalOffsetValue = sv.ExtentHeight - sv.ViewportHeight;
            if (verticalOffsetValue >= 0.95 * maxVerticalOffsetValue)
            {
                await LoadMoreHeadlines();
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is NavigationObject)
            {
                initialized = true;
                // Fix Backstack
                if (Frame.BackStack.Count > 1)
                {
                    Frame.BackStack.RemoveAt(Frame.BackStack.Count - 1);
                    Frame.BackStack.RemoveAt(Frame.BackStack.Count - 1);
                }
                NavigationObject nav = e.Parameter as NavigationObject;
                _sortOrder = nav._sortOrder;
                _showUnreadOnly = nav._showUnreadOnly;
                ArticlesCollection = nav.ArticlesCollection;
                HeadlinesView.DataContext = ArticlesCollection;
                initialIndex = nav.selectedIndex;
                //UpdateLocalizedApplicationBar(true);
                Logger.WriteLine("NavigatedTo HeadlinesPage from ArticlePage for Feed " + ConnectionSettings.getInstance().selectedFeed);
            }
            else
            {
                initialized = false;
                Logger.WriteLine("NavigatedTo HeadlinesPage for Feed " + ConnectionSettings.getInstance().selectedFeed);
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
        }

        /*private void MultiSelectAppBarButton_Click(object sender, RoutedEventArgs e)
        {
            if (MultiSelectAppBarButton.IsChecked == true)
            {
                if (HeadlinesView.SelectedItem != null)
                {
                    UpdateLocalizedApplicationBar(false);
                }
                else
                {
                    UpdateLocalizedApplicationBar(true);
                }
                HeadlinesView.SelectionMode = ListViewSelectionMode.Multiple;
            }
            else
            {
                HeadlinesView.SelectedItems.Clear();
                HeadlinesView.SelectionMode = ListViewSelectionMode.Single;
                UpdateLocalizedApplicationBar(true);
            }
        }*/

        protected override void updateCount(bool p)
        {
            //nothing.
        }
        protected override int getSelectedIdx()
        {
            return HeadlinesView.SelectedIndex;
        }

        private void HeadlinesView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (HeadlinesView.SelectionMode == ListViewSelectionMode.Single)
            {
                NavigationObject parameter = new NavigationObject();
                parameter.selectedIndex = HeadlinesView.SelectedIndex;
                parameter.feedId = ConnectionSettings.getInstance().selectedFeed;
                parameter._showUnreadOnly = _showUnreadOnly;
                parameter._sortOrder = _sortOrder; 
                parameter.ArticlesCollection = new ObservableCollection<WrappedArticle>();
                foreach (WrappedArticle article in ArticlesCollection)
                {
                    parameter.ArticlesCollection.Add(article);
                }

                Frame.Navigate(typeof(ArticlePage), parameter);
            }
            else
            {
                if (HeadlinesView.SelectedItems == null || HeadlinesView.SelectedItems.Count == 0)
                {
                    //UpdateLocalizedApplicationBar(true);
                    return;
                }
                else
                {
                    //UpdateLocalizedApplicationBar(false);
                    return;
                }
            }
        }

        private void Icon_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            Image img = sender as Image;
            if (img != null)
            {
                img.Visibility = Visibility.Collapsed;
            }
        }

        private async void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (validConnection)
            {
                int feedId = -3;
                if (sender == Fresh.Parent)
                {
                    feedId = (int)FeedId.Fresh;
                }
                else if (sender == Archived.Parent)
                {
                    feedId = (int)FeedId.Archived;
                }
                else if (sender == Starred.Parent)
                {
                    feedId = (int)FeedId.Starred;
                }
                else if (sender == All.Parent)
                {
                    feedId = (int)FeedId.All;
                }
                else if (sender == Published.Parent)
                {
                    feedId = (int)FeedId.Published;
                }
                else if (sender == Recent.Parent)
                {
                    feedId = (int)FeedId.RecentlyRead;
                }
                ConnectionSettings.getInstance().selectedFeed = feedId;
                await LoadHeadlines();
            }
            else
            {
                MessageDialog msgbox = new MessageDialog(loader.GetString("NoConnection"));
                await msgbox.ShowAsync();
            }
        }
    }
}
