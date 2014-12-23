using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows;
using Windows.Data.Xml.Dom;
using Windows.Devices.Geolocation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Services.Maps;
using Windows.System.Display;
using Windows.UI;
using Windows.UI.Notifications;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Maps;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=391641

namespace CarFindrPlus
{
    /// <summary>
    /// The main navigation code under the XAML
    /// </summary>
    public sealed partial class MainPage : Page
    {
        /// <summary>
        /// Object that controls the screen's time-out
        /// </summary>
        private DisplayRequest displayRequest;

        /// <summary>
        /// Global Geolocator Object - This is necessary to track the Car location
        /// </summary>
        private Geolocator CarGeolocator;

        /// <summary>
        /// Global geolocator to track location of user
        /// </summary>
        private Geolocator UserGeolocator;

        /// <summary>
        /// Geopsition to store car location
        /// </summary>
        private Geoposition CarGeoposition;

        /// <summary>
        /// Geopostition to store user location
        /// </summary>
        private Geoposition UserGeoposition;

        /// <summary>
        /// Placeholder UIElement to represent Car's geoposition
        /// </summary>
        private MapIcon CarPushpin;

        /// <summary>
        /// Placer UIElement to represent User's geoposition
        /// </summary>
        private MapIcon UserPushpin;

        /// <summary>
        /// Object that I created to store user information
        /// </summary>
        private AppSettings MainSettings;

        /// <summary>
        /// Global object to track location address in text - not coordinates
        /// </summary>
        private MapLocationFinderResult ParkdInformation;

        /// <summary>
        /// Constant double to convert meters to miles
        /// </summary>
        private const double METERS_TO_MILES = 0.000621371;

        /// <summary>
        /// Magic Numbers - This will be used to set accuracy of a geolocator to the nearest 5m
        /// </summary>
        private const int FIVE_METERS = 5;

        /// <summary>
        /// Magic Numbers - This will be used to set the update threshold at each 50m
        /// </summary>
        private const int FIFTY_METERS = 50;

        /// <summary>
        /// Constant number to use for zoom
        /// </summary>
        private const int MapZoom = 17;

        private const int MapHeight = 325;

        
        public ToastNotifier toastNotifier = ToastNotificationManager.CreateToastNotifier();
        public XmlDocument toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText04);
        public ScheduledToastNotification customAlarmScheduledToast;


        /// <summary>
        /// Constructor of the Main Page
        /// </summary>
        public MainPage()
        {
            this.InitializeComponent();

            this.NavigationCacheMode = NavigationCacheMode.Required;

            CarPushpin = new MapIcon();
            UserPushpin = new MapIcon();
            MainSettings = new AppSettings();

            CarGeolocator = new Geolocator();     // Main object to locate GeoCoordinates
            CarGeolocator.DesiredAccuracyInMeters = FIVE_METERS;
            CarGeolocator.MovementThreshold = FIFTY_METERS;            // in meters
            CarGeolocator.PositionChanged += CarGeolocator_PositionChanged;

            UserGeolocator = null;

            displayRequest = new Windows.System.Display.DisplayRequest();
            displayRequest.RequestActive(); // stops sleep

            XmlNodeList toastText = toastXml.GetElementsByTagName("text");
            (toastText[0] as XmlElement).InnerText = "Your parking's going to expire soon";
            ToastNotification toast = new ToastNotification(toastXml);
            //toastNotifier.Show(toast);

            ParkitButton.Tapped += ParkitButton_Tapped;
            NoteTextBox.GotFocus += NoteTextBox_GotFocus;
            ExpirationReminderSlider.ValueChanged += ExpirationReminderSlider_ValueChanged;
            CancelButton.Tapped += CancelButton_Tapped;
            NoteTextBox.LostFocus += NoteTextBox_LostFocus;
            //ExpirationTimeLabel.Tapped += ExpirationTimeLabel_Tapped;

            TimePickerObject.TimeChanged += TimePickerObject_TimeChanged;

            NoteTextBox.Text = MainSettings.GetValueOrDefault("NotesContentKeyName", "Add a Note!");
            //ExpirationTimeLabel.Text = MainSettings.GetValueOrDefault("TimeReminderKeyName", DateTime.Now.AddHours(6)).ToString("t");
            //ApplicationView.GetForCurrentView().SetDesiredBoundsMode(ApplicationViewBoundsMode.UseCoreWindow);
        }

        void TimePickerObject_TimeChanged(object sender, TimePickerValueChangedEventArgs e)
        {
            long timeDiff = TimePickerObject.Time.Ticks - DateTime.Now.TimeOfDay.Ticks;
            MainSettings.AddOrUpdateValue("TimeReminderKeyName", DateTime.Now.Ticks + timeDiff);
            if(customAlarmScheduledToast != null)
            {
                customAlarmScheduledToast = new ScheduledToastNotification(toastXml,
                    new DateTime(MainSettings.GetValueOrDefault("TimeReminderKeyName", DateTime.Now.Ticks + timeDiff)));
            }
        }

        /// <summary>
        /// Lost Focus event for the Notes Textbox - Saves information after user is done typing
        /// </summary>
        /// <param name="sender"> sender of the event </param>
        /// <param name="e"> information about the event </param>
        void NoteTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // save information
            MainSettings.AddOrUpdateValue("NotesContentKeyName", NoteTextBox.Text);
        }

        /// <summary>
        /// Tapped event handler for the cancel button
        /// Disables tracking for the User's location, and only tracks the car's location
        /// </summary>
        /// <param name="sender"> sender of the event </param>
        /// <param name="e"> information about the event </param>
        void CancelButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // reset GUI
            if(UserGeolocator != null)
            {
                UserGeolocator.PositionChanged -= UserGeolocator_PositionChanged;
            }
            CarGeolocator.PositionChanged += CarGeolocator_PositionChanged;
            MainMap.MapElements.Remove(UserPushpin);
            CarPushpin.Title = "Parkit here?";

            ParkitButton.Visibility = Visibility.Visible;
            CancelButton.Visibility = Visibility.Collapsed;
            
            // delete user information
            MainSettings.Reset();
            MainSettings.AddOrUpdateValue("CarLocationLatitudeKeyName", 0.0);
        }

        /// <summary>
        /// Event Handler for the Expiration Reminder Slider when user changes the value
        /// Enables alarm
        /// </summary>
        /// <param name="sender"> Object which indicates where the event occured </param> 
        /// <param name="e"> An object that contains information about the event </param> 
        void ExpirationReminderSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            ExpirationReminderSlider.Value = Math.Round(ExpirationReminderSlider.Value, 0);

            

            if (ExpirationReminderSlider.Value == 1)
            {
                customAlarmScheduledToast = new ScheduledToastNotification(toastXml, 
                    new DateTime(MainSettings.GetValueOrDefault("TimeReminderKeyName", DateTime.Now.Ticks + 20000000)));
                toastNotifier.AddToSchedule(customAlarmScheduledToast);

                ExpirationTimeLabel.Foreground = (SolidColorBrush)App.Current.Resources["PhoneAccentBrush"];
                
                return;
            }
            else
            {
                //if(toastNotifier.GetScheduledToastNotifications[0].equals(customAlarmScheduledToast))
                {
                    toastNotifier.RemoveFromSchedule(customAlarmScheduledToast);
                }
                
            }
            if ((Visibility)App.Current.RequestedTheme == Windows.UI.Xaml.Visibility.Collapsed)
            {
                ExpirationTimeLabel.Foreground = new SolidColorBrush(Colors.WhiteSmoke);
            }
            else
            {
                ExpirationTimeLabel.Foreground = new SolidColorBrush(Colors.DarkGray);
            }
        }

        /// <summary>
        /// Another Event Handler - To handle the GotFocused event when user clicks on the note textbox
        /// </summary>
        /// <param name="sender"> Object which indicates where the event occured </param> 
        /// <param name="e"> An object that contains information about the event </param> 
        void NoteTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            //NoteTextBox.Background.Opacity = 0.0;
            //NoteTextBox.Style
        }

        /// <summary>
        /// Event Handler - When the user clicks the button
        /// </summary>
        /// <param name="sender"> Object which indicates where the event occured </param> 
        /// <param name="e"> An object that contains information about the event </param> 
        async void ParkitButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            ParkdInformation = await MapLocationFinder.FindLocationsAtAsync(CarPushpin.Location);
            if (ParkdInformation.Locations[0] != null)
            {
                string address = ParkdInformation.Locations[0].Address.BuildingName + " " +
                ParkdInformation.Locations[0].Address.StreetNumber + " " +
                ParkdInformation.Locations[0].Address.Street + " " +
                ParkdInformation.Locations[0].Address.Town;
                LocationTextBox.Text = address;
            }
            // Set Car Location
            CarGeolocator.PositionChanged -= CarGeolocator_PositionChanged;
            CarPushpin.Title = "Parkd!";
            //CarGeolocator = null; // stop updates for this object
            // create new pushpin for user location

            ParkitButton.Visibility = Visibility.Collapsed;
            CancelButton.Visibility = Visibility.Visible;
            getUserLocation();
        }

        /// <summary>
        /// Method to get user's location
        /// </summary>
        private async void getUserLocation()
        {
            UserGeolocator = new Geolocator();     // Main object to locate GeoCoordinates
            UserGeolocator.DesiredAccuracyInMeters = FIVE_METERS;
            UserGeolocator.MovementThreshold = FIFTY_METERS;            // in meters
            UserGeolocator.PositionChanged += CarGeolocator_PositionChanged;

            UserGeoposition = null;               // WP8 is bad and needs inefficient repititions...

            if (UserGeolocator.LocationStatus == PositionStatus.Disabled) // checks if Location Settings are enabled
            {
                var enableLocationDialog = new MessageDialog("Please enable Location Settings"
                    + "\nCarFindr requires your location to function properly");
                await enableLocationDialog.ShowAsync();
                await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings-location:")); // redirects to location settings page
                App.Current.Exit(); // closes app safely
            }

            try
            {
                //MyGeoPosition = await MyGeolocator.GetGeopositionAsync(); // gets location
                //MyGeoPosition = await MyGeolocator.GetGeopositionAsync(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(7));   // gets approximate location in 7 sec
                UserGeoposition = await UserGeolocator.GetGeopositionAsync(); // Default is accurate, but inconsistent
                //CarGeoCoordinate = new GeoCoordinate(CarGeoposition.Coordinate.Latitude, CarGeoposition.Coordinate.Longitude);
                //MainSettings.AddOrUpdateValue("CurrentLocationSetting", CarGeoCoordinate); // later implementation of data?
                //MainSettings.AddOrUpdateValue("UserLocationKeyName", CarGeoposition);

                DrawUserPushPin(null);
            }
            catch (UnauthorizedAccessException)
            {
                /*var enableLocationDialog = new MessageDialog("Please enable Location Settings"
                    + "\nCarFindr requires your location to function properly");
                Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    enableLocationDialog.ShowAsync();
                });
                 * */
                Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings-location:")); // redirects to location settings page
                App.Current.Exit(); // closes app safely
            }
            catch (Exception ex) // I hate this
            {
                // Something else happened while acquiring the location.
                var unknownErrorDialog = new MessageDialog(ex.Message);
                unknownErrorDialog.ShowAsync();
                App.Current.Exit();
            }
        }

        /// <summary>
        /// Event handler - updates user location after moved
        /// </summary>
        /// <param name="sender"> Object which indicates where the event occured </param> 
        /// <param name="e"> An object that contains information about the event </param> 
        async void UserGeolocator_PositionChanged(Geolocator sender, PositionChangedEventArgs args)
        {
            DrawUserPushPin(args);
            MapRouteFinderResult routeResult =
                await MapRouteFinder.GetDrivingRouteAsync(
                CarPushpin.Location,
                UserPushpin.Location,
                MapRouteOptimization.Distance,
                MapRouteRestrictions.None);

            //UserGeoposition.Coordinate.Latitude = args.Position.Coordinate.Latitude;
            // update GUI
            DistanceLeftLabel.Text = (routeResult.Route.LengthInMeters / METERS_TO_MILES).ToString(); // distance in miles
        }

        /// <summary>
        /// Helper Method - Updates GUI and draws User Pushpin
        /// </summary>
        private async void DrawUserPushPin(PositionChangedEventArgs args)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => // runs GUI Update related stuff
            {
                if (UserPushpin.Location != null) // already existing, remove and update
                {
                    MainMap.MapElements.Remove(UserPushpin);
                }
                else // not existing yet, first one
                {
                    UserPushpin.Title = "Your Location";
                    UserPushpin.NormalizedAnchorPoint = new Point(0.5, 1.0);
                    CarPushpin.Title = "Parkd!";
                }
                if(UserGeoposition != null)
                {
                    UserPushpin.Location = new Geopoint(new BasicGeoposition()
                    {
                        Latitude = UserGeoposition.Coordinate.Point.Position.Latitude,
                        Longitude = UserGeoposition.Coordinate.Point.Position.Longitude // obsolete as of 8.1, but works...?
                    });
                }
                else
                {
                    UserPushpin.Location = new Geopoint(new BasicGeoposition()
                    {
                        Latitude = args.Position.Coordinate.Latitude,
                        Longitude = args.Position.Coordinate.Longitude
                    });
                }
                //MainMap.MapElements.Remove(CarPushpin);
                MainMap.MapElements.Add(UserPushpin);
                //MainSettings.AddOrUpdateValue("UserLocationKeyName", UserPushpin.Location);
                MainSettings.AddOrUpdateValue("UserLocationLatitudeKeyName", UserPushpin.Location.Position.Latitude);
                MainSettings.AddOrUpdateValue("UserLocationLongitudeKeyName", UserPushpin.Location.Position.Longitude);
            });
        }


        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.
        /// This parameter is typically used to configure the page.</param>
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            // TODO: Prepare page for display here.
            if (MainSettings.GetValueOrDefault("CarLocationLatitudeKeyName", 0.0) != 0.0)
            {
                Geopoint CarGeopoint = new Geopoint(new BasicGeoposition()
                {
                    Latitude = MainSettings.GetValueOrDefault("CarLocationLatitudeKeyName", 0.0),
                    Longitude = MainSettings.GetValueOrDefault("CarLocationLongitudeKeyName", 0.0)
                });
                Geopoint UserGeopoint = new Geopoint(new BasicGeoposition()
                {
                    Latitude = MainSettings.GetValueOrDefault("UserLocationLatitudeKeyName", 0.0),
                    Longitude = MainSettings.GetValueOrDefault("UserLocationLongitudeKeyName", 0.0)
                });

                CarPushpin.Location = CarGeopoint;
                CarPushpin.Title = "Parkd!";
                UserPushpin.Location = UserGeopoint;
                UserPushpin.Title = "Your Location";
                MainMap.Center = CarGeopoint;
                MainMap.ZoomLevel = MapZoom;
                MainMap.Height = MapHeight;
                MainMap.Margin = new Thickness(0, 0, 0, 0);
                LocationTextBox.Margin = new Thickness(0, 0, 0, 0);

                ParkitButton.Visibility = Visibility.Collapsed;
                CancelButton.Visibility = Visibility.Visible;
                //DrawCarPushPin(); // I AM A FAILURE WOW 
                //DrawUserPushPin();

                // Reverse geocode to specified location
                ParkdInformation = await MapLocationFinder.FindLocationsAtAsync(CarPushpin.Location);
                if (ParkdInformation.Locations[0] != null)
                {
                    string address = ParkdInformation.Locations[0].Address.BuildingName + " " +
                    ParkdInformation.Locations[0].Address.StreetNumber + " " +
                    ParkdInformation.Locations[0].Address.Street + " " +
                    ParkdInformation.Locations[0].Address.Town;
                    LocationTextBox.Text = address;
                }
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, () =>
                {
                    MainMap.MapElements.Add(CarPushpin);
                    MainMap.MapElements.Add(UserPushpin);
                });
                getUserLocation();
            }
            else
            {
                getCarLocation();
            }
            //getUserLocation();
            // TODO: If your application contains multiple pages, ensure that you are
            // handling the hardware Back button by registering for the
            // Windows.Phone.UI.Input.HardwareButtons.BackPressed event.
            // If you are using the NavigationHelper provided by some templates,
            // this event is handled for you.
        }

        /// <summary>
        /// Method to get Car Location
        /// Same as method to get User Location but with different objects for different purposes
        /// </summary>
        private async void getCarLocation()
        {
            if (CarGeolocator.LocationStatus == PositionStatus.Disabled) // checks if Location Settings are enabled
            {
                var enableLocationDialog = new MessageDialog("Please enable Location Settings"
                    + "\nCarFindr requires your location to function properly");
                await enableLocationDialog.ShowAsync();
                await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings-location:")); // redirects to location settings page
                App.Current.Exit(); // closes app safely
            }

            try
            {
                CarGeoposition = await CarGeolocator.GetGeopositionAsync(); // Default is accurate, but inconsistent
                
                BasicGeoposition queryHint = new BasicGeoposition();
                queryHint.Latitude = CarGeoposition.Coordinate.Latitude;
                queryHint.Longitude = CarGeoposition.Coordinate.Longitude;
                Geopoint hintPoint = new Geopoint(queryHint);

                // Reverse geocode to specified location
                ParkdInformation = await MapLocationFinder.FindLocationsAtAsync(hintPoint); // null pointer
                DrawCarPushPin(null);
            }
            catch (UnauthorizedAccessException)
            {
                /*
                var enableLocationDialog = new MessageDialog("Please enable Location Settings"
                    + "\nCarFindr requires your location to function properly");
                enableLocationDialog.ShowAsync();
                 * */
                Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings-location:")); // redirects to location settings page
                App.Current.Exit(); // closes app safely
            }
            catch (Exception ex) // I hate this
            {
                // Something else happened while acquiring the location.
                var unknownErrorDialog = new MessageDialog(ex.Message);
                unknownErrorDialog.ShowAsync();
                App.Current.Exit();
            }
        }

        /// <summary>
        /// Event Handler for change in Car position
        /// </summary>
        /// <param name="sender"> Object which indicates where the event occured </param> 
        /// <param name="e"> An object that contains information about the event </param> 
        void CarGeolocator_PositionChanged(Geolocator sender, PositionChangedEventArgs args)
        {
            //CarGeoposition.Coordinate = args.Position.Coordinate; not valid, done already
            // update GUI
            DrawCarPushPin(args);
            Geopoint CarGeopoint = new Geopoint(new BasicGeoposition()
            {
                Latitude = args.Position.Coordinate.Latitude,
                Longitude = args.Position.Coordinate.Longitude
            });
            //MainSettings.AddOrUpdateValue("CarLocationKeyName", test);
            MainSettings.AddOrUpdateValue("CarLocationLatitudeKeyName", CarGeopoint.Position.Latitude);
            MainSettings.AddOrUpdateValue("CarLocationLongitudeKeyName", CarGeopoint.Position.Longitude);
        }

        /// <summary>
        /// Helper Method - Updates GUI and CarPushpin
        /// </summary>
        private async void DrawCarPushPin(PositionChangedEventArgs args)
        {
            /*
            if (MainMap.MapElements[0] != null) // removes previous car pushpin
            {
                MainMap.MapElements.RemoveAt(0);
            }
            */
            Geopoint ChangedPosition = new Geopoint(new BasicGeoposition()
            {
                Latitude = args.Position.Coordinate.Latitude,
                Longitude = args.Position.Coordinate.Longitude
            });

            ParkdInformation = await MapLocationFinder.FindLocationsAtAsync(ChangedPosition); 

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (CarPushpin.Location != null) // already existing, remove and update
                {
                    MainMap.MapElements.Remove(CarPushpin);
                }
                else // not existing yet, first one
                {
                    CarPushpin.Title = "Parkit here?";
                    CarPushpin.NormalizedAnchorPoint = new Point(0.5, 1.0);
                    
                    if(ParkdInformation != null)
                    {
                        string address = ParkdInformation.Locations[0].Address.BuildingName +
                        ParkdInformation.Locations[0].Address.StreetNumber +
                        ParkdInformation.Locations[0].Address.Street +
                        ParkdInformation.Locations[0].Address.Town;
                        LocationTextBox.Text = address;
                    }
                }
                if (CarGeoposition != null)
                {
                    CarPushpin.Location = new Geopoint(new BasicGeoposition()
                    {
                        Latitude = CarGeoposition.Coordinate.Latitude,
                        Longitude = CarGeoposition.Coordinate.Longitude
                    });
                }
                else
                {
                    CarPushpin.Location = ChangedPosition;
                }
                //MainMap.MapElements.Remove(CarPushpin);
                MainMap.MapElements.Add(CarPushpin);
                MainMap.Center = CarPushpin.Location;
                MainMap.ZoomLevel = MapZoom;
                MainMap.Height = MapHeight;
                MainMap.Margin = new Thickness(0, 0, 0, 0);
                LocationTextBox.Margin = new Thickness(0, 0, 0, 0);
                //MainSettings.AddOrUpdateValue("CarLocationKeyName", CarPushpin.Location);
                MainSettings.AddOrUpdateValue("CarLocationLatitudeKeyName", CarPushpin.Location.Position.Latitude);
                MainSettings.AddOrUpdateValue("CarLocationLongitudeKeyName", CarPushpin.Location.Position.Longitude);
            });
        }
    }
}
