using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Microsoft.Band;
using Microsoft.Band.Sensors;

namespace ReadBandData
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            Microsoft.ApplicationInsights.WindowsAppInitializer.InitializeAsync(
                Microsoft.ApplicationInsights.WindowsCollectors.Metadata |
                Microsoft.ApplicationInsights.WindowsCollectors.Session);
            this.InitializeComponent();
            this.Suspending += OnSuspending;
        }

        public IBandClient connectedBand { get; set; }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
            {
                this.DebugSettings.EnableFrameRateCounter = true;
            }
#endif
            Frame rootFrame = Window.Current.Content as Frame;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    //TODO: Load state from previously suspended application
                }

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    // When the navigation stack isn't restored navigate to the first page,
                    // configuring the new page by passing required information as a navigation
                    // parameter
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                }
                // Ensure the current window is active
                Window.Current.Activate();
            }

            connectedBand = ConnectBand().Result;
        }
        

        private async Task<IBandClient> ConnectBand()
        {
            var pairedBands = await BandClientManager.Instance.GetBandsAsync();
            if (pairedBands.Length < 1)
            {
                throw new Exception("Unable to find paired band");
            }
            var bandClient = await BandClientManager.Instance.ConnectAsync(pairedBands[0]);
            
            ConnectSensorOfType<IBandSensor<IBandHeartRateReading>, IBandHeartRateReading>(bandClient.SensorManager.HeartRate, HandleHeartRateReachingChanged);
            ConnectSensorOfType<IBandSensor<IBandAccelerometerReading>, IBandAccelerometerReading>(bandClient.SensorManager.Accelerometer, HandleAccelerometerReachingChanged);
            return bandClient;
        }

        private async void ConnectSensorOfType<T, T1>(T bandSensor, EventHandler<BandSensorReadingEventArgs<T1>> onReadingChanged) where T : IBandSensor<T1>
            where T1 : IBandSensorReading
        {
            if (!bandSensor.IsSupported)
            {
                throw new Exception($"{bandSensor.GetType()} not supported");
            }
            bool sensorConsentGranted = bandSensor.GetCurrentUserConsent() == UserConsent.Granted;
            if (!sensorConsentGranted)
            {
                sensorConsentGranted = await bandSensor.RequestUserConsentAsync();
            }

            if (!sensorConsentGranted)
            {
                throw new Exception($"{bandSensor.GetType()} consent not granted");
            }

            bandSensor.ReadingChanged += onReadingChanged;
            await bandSensor.StartReadingsAsync();
        }

        private async void HandleHeartRateReachingChanged(object sender, BandSensorReadingEventArgs<IBandHeartRateReading> e)
        {
            var storageFolder = KnownFolders.DocumentsLibrary;

            var file = await storageFolder.CreateFileAsync("heartrate.csv", CreationCollisionOption.OpenIfExists);
    

            await FileIO.AppendLinesAsync(file, new List<string> {e.SensorReading.HeartRate.ToString()});

        }

        private async void HandleAccelerometerReachingChanged(object sender, BandSensorReadingEventArgs<IBandAccelerometerReading> e)
        {
            var storageFolder = KnownFolders.DocumentsLibrary;

            var file = await storageFolder.CreateFileAsync("accelerometer.csv", CreationCollisionOption.OpenIfExists);
            
            var stringToAppend =
                $"{e.SensorReading.AccelerationX}, {e.SensorReading.AccelerationY}, {e.SensorReading.AccelerationZ}";

            await FileIO.AppendLinesAsync(file, new List<string> { stringToAppend });

        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
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
            connectedBand.SensorManager.Accelerometer.StopReadingsAsync();
            connectedBand.SensorManager.Accelerometer.ReadingChanged -= HandleAccelerometerReachingChanged;
            connectedBand.SensorManager.HeartRate.StopReadingsAsync();
            connectedBand.SensorManager.HeartRate.ReadingChanged -= HandleHeartRateReachingChanged;

            
            var deferral = e.SuspendingOperation.GetDeferral();
            //TODO: Save application state and stop any background activity
            deferral.Complete();
        }
    }
}
