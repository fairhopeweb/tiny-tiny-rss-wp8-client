﻿using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using TinyTinyRSS.Interface;
using CaledosLab.Portable.Logging;
using Windows.Storage;
using Windows.UI.Core;
using System.Threading.Tasks;

// The Blank Application template is documented at http://go.microsoft.com/fwlink/?LinkId=391641

namespace TinyTinyRSS
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public sealed partial class App : Application
    {
        // LogFile Name
        public const string LogFile = "currentLog.txt";
        // Error LogFile Name
        public const string LastLogFile = "lastLog.txt";

        private TransitionCollection transitions;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.Suspending += this.OnSuspending;
            this.Resuming += App_Resuming;
        }

        private async void App_Resuming(object sender, object e)
        {
            Frame rootFrame = Window.Current.Content as Frame;
            var now = DateTime.Now;
            var last = ConnectionSettings.getInstance().supsensionDate;
            if (last != null && rootFrame != null)
            {
                TimeSpan span = now.Subtract(last);
                if (span.Minutes > 10)
                {
                    rootFrame.BackStack.Clear();
                    rootFrame.Navigate(typeof(MainPage));
                }
            }
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used when the application is launched to open a specific file, to display
        /// search results, and so forth.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected async override void OnLaunched(LaunchActivatedEventArgs e)
        {
#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
            {
                this.DebugSettings.EnableFrameRateCounter = true;
            }
#endif
            Task<bool> loginTask = TtRssInterface.getInterface().CheckLogin();
            try
            {
                // init logger
                StorageFolder storage = ApplicationData.Current.LocalFolder;
                StorageFile file = await storage.CreateFileAsync(LogFile, CreationCollisionOption.OpenIfExists);
                
                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated || e.PreviousExecutionState == ApplicationExecutionState.ClosedByUser)
                {
                    StorageFile errorfile = await storage.CreateFileAsync(LastLogFile, CreationCollisionOption.ReplaceExisting);
                    await file.CopyAndReplaceAsync(errorfile);
                    file = await storage.CreateFileAsync(LogFile, CreationCollisionOption.ReplaceExisting);
                }
                Logger.Load(file);   
            }
            catch
            {
                // yeah we can't log the error.
            }
            Frame rootFrame = Window.Current.Content as Frame;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                // TODO: change this value to a cache size that is appropriate for your application
                rootFrame.CacheSize = 1;

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            if (rootFrame.Content == null)
            {
                // Removes the turnstile navigation for startup.
                if (rootFrame.ContentTransitions != null)
                {
                    this.transitions = new TransitionCollection();
                    foreach (var c in rootFrame.ContentTransitions)
                    {
                        this.transitions.Add(c);
                    }
                }

                rootFrame.ContentTransitions = null;
                rootFrame.Navigated += this.RootFrame_FirstNavigated;
                
                // When the navigation stack isn't restored navigate to the first page,
                // configuring the new page by passing required information as a navigation
                // parameter
                if (!rootFrame.Navigate(typeof(MainPage), e.Arguments))
                {
                    throw new Exception("Failed to create initial page");
                }
            }
            await loginTask;
            SystemNavigationManager.GetForCurrentView().BackRequested += App_BackRequested;
            // Ensure the current window is active
            Window.Current.Activate();
        }

        private void App_BackRequested(object sender, BackRequestedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            if (rootFrame != null && rootFrame.SourcePageType == typeof(ArticlePage))
            {
                // Article Page hat eigenen Back Handler
                return;
            }

            if (rootFrame != null && rootFrame.CanGoBack)
            {
                e.Handled = true;
                rootFrame.GoBack();
            }
            if (!e.Handled && rootFrame != null && rootFrame.SourcePageType == typeof(MainPage))
                Current.Exit();
            }

        /// <summary>
        /// Restores the content transitions after the app has launched.
        /// </summary>
        /// <param name="sender">The object where the handler is attached.</param>
        /// <param name="e">Details about the navigation event.</param>
        private void RootFrame_FirstNavigated(object sender, NavigationEventArgs e)
        {
            var rootFrame = sender as Frame;
            rootFrame.ContentTransitions = this.transitions ?? new TransitionCollection() { new NavigationThemeTransition() };
            rootFrame.Navigated -= this.RootFrame_FirstNavigated;
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            Logger.WriteLine("App suspended");
            ConnectionSettings.getInstance().supsensionDate = DateTime.Now;
            FinalizeLogging();
            Logger.ClearLog();
            var deferral = e.SuspendingOperation.GetDeferral();
            deferral.Complete();            
        }

        /// <summary>
        /// Closes the logger.
        /// </summary>
        public static async void FinalizeLogging()
        {            
            StorageFolder storage = ApplicationData.Current.LocalFolder;
            StorageFile file = await storage.CreateFileAsync(LogFile, CreationCollisionOption.OpenIfExists);
            Logger.WriteLine("Last Settings:");
            Logger.WriteLine("Mark read:" + ConnectionSettings.getInstance().markRead);
            Logger.WriteLine("Progress as Cntr:" + ConnectionSettings.getInstance().progressAsCntr);
            Logger.WriteLine("Sort Order:" + ConnectionSettings.getInstance().sortOrder);
            Logger.WriteLine("show Unread Only:" + ConnectionSettings.getInstance().showUnreadOnly);
            Logger.WriteLine("Live Tile Active:" + ConnectionSettings.getInstance().liveTileActive);
            Logger.WriteLine("Channel Uri:" + ConnectionSettings.getInstance().channelUri);
            Logger.Save(file);
        }
    }
}