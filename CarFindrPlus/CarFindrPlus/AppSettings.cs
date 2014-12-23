using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using System.Diagnostics;
using Windows.Devices.Geolocation;
//using System.Device.Location;

namespace CarFindrPlus
{
    public class AppSettings
    {
        // Settings Storage
        /// <summary>
        /// Application Settings object
        /// </summary>
        ApplicationDataContainer settings;

        // The key names of the settings
        /// <summary>
        /// Key for Car Latitude
        /// </summary>
        const string CarLocationLatitudeKeyName = "CarLocationLatitudeKeyName";

        /// <summary>
        /// Key for Car Longititude
        /// </summary>
        const string CarLocationLongitudeKeyName = "CarLocationLongitudeKeyName";

        /// <summary>
        /// Key for User Location Latitude
        /// </summary>
        const string UserLocationLatitudeKeyName = "UserLocationLatitudeKeyName";

        /// <summary>
        /// Key for User Location Longitude
        /// </summary>
        const string UserLocationLongitudeKeyName = "UserLocationLongitudeKeyName";

        /// <summary>
        /// Key for Expiration Alarm
        /// </summary>
        const string TimeReminderKeyName = "TimeReminderKeyName";

        /// <summary>
        /// Key for Notes
        /// </summary>
        const string NotesContentKeyName = "NotesContentKeyName";

        // The default values of the settings
        /// <summary>
        /// Car Latitude Default
        /// </summary>
        const double CarLocationLatitudeDefault = 0.0;

        /// <summary>
        /// Car Longitude Default
        /// </summary>
        const double CarLocationLongitudeDefault = 0.0;

        /// <summary>
        /// User Latitude Default
        /// </summary>
        const double UserLocationLatitudeDefault = 0.0;

        /// <summary>
        /// User Longitude Default
        /// </summary>
        const double UserLocationLongtitudeDefault = 0.0;

        /// <summary>
        /// TimeReminderDefault
        /// </summary>
        long TimeReminderDefault = DateTime.Now.AddHours(6).Ticks;

        /// <summary>
        /// NotesContent Default
        /// </summary>
        const string NotesContentDefault = "Add a Note!";

        /// <summary>
        /// Constructor that gets the application settings.
        /// </summary>
        public AppSettings()
        {
            // Get the settings for this application.
            //settings = IsolatedStorageSettings.ApplicationSettings;
            settings = Windows.Storage.ApplicationData.Current.LocalSettings;
        }

        /// <summary>
        /// Update a setting value for our application. If the setting does not
        /// exist, then add the setting.
        /// </summary>
        /// <param name="Key"> Key to access the container to the values </param>
        /// <param name="value"> Value to update to </param>
        /// <returns></returns>
        public bool AddOrUpdateValue(string Key, Object value)
        {
            bool valueChanged = false;

            // If the key exists
            if (settings.Containers.ContainsKey(Key))
            {
                // If the value has changed
                if (settings.Containers[Key] != value)
                {
                    // Store the new value
                    settings.Containers[Key].Values[Key] = value;
                    valueChanged = true;
                }
            }
            // Otherwise create the key.
            else
            {
                //settings.Add(Key, value);
                settings.CreateContainer(Key, Windows.Storage.ApplicationDataCreateDisposition.Always);
                settings.Containers[Key].Values[Key] = value;
                valueChanged = true;
            }
            return valueChanged;
        }

        /// <summary>
        /// Get the current value of the setting, or if it is not found, set the 
        /// setting to the default setting.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="Key"> Key to access container with values </param>
        /// <param name="defaultValue"> Value to default to if Key is empty </param>
        /// <returns></returns>
        public T GetValueOrDefault<T>(string Key, T defaultValue)
        {
            T value;

            // If the key exists, retrieve the value.
            if (settings.Containers.ContainsKey(Key))
            {
                value = (T)settings.Containers[Key].Values[Key];
            }
            // Otherwise, use the default value.
            else
            {
                value = defaultValue;
            }
            return value;
        }

        /// <summary>
        /// Resets the settings.
        /// </summary>
        public async void Reset()
        {
            await Windows.Storage.ApplicationData.Current.ClearAsync(
                Windows.Storage.ApplicationDataLocality.Local);
            CarLocationLatitudeSetting = 0.0;
        }
         
        /// <summary>
        /// Car Latitude Setting get and set
        /// </summary>
        public double CarLocationLatitudeSetting
        {
            get
            {
                return GetValueOrDefault<double>(CarLocationLatitudeKeyName, CarLocationLatitudeDefault);
            }
            set
            {
                AddOrUpdateValue(CarLocationLatitudeKeyName, value);
            }
        }

        /// <summary>
        /// Car Longitude Setting get and set
        /// </summary>
        public double CarLocationLongitudeSetting
        {
            get
            {
                return GetValueOrDefault<double>(CarLocationLongitudeKeyName, CarLocationLongitudeDefault);
            }
            set
            {
                AddOrUpdateValue(CarLocationLongitudeKeyName, value);
            }
        }

        /// <summary>
        /// User Latitude Setting get and set
        /// </summary>
        public double UserLocationLatitudeSetting
        {
            get
            {
                return GetValueOrDefault<double>(UserLocationLatitudeKeyName, UserLocationLatitudeDefault);
            }
            set
            {
                AddOrUpdateValue(UserLocationLatitudeKeyName, value);
            }
        }

        /// <summary>
        /// User Longitude Setting get and set
        /// </summary>
        public double UserLocationLongitudeSetting
        {
            get
            {
                return GetValueOrDefault<double>(UserLocationLongitudeKeyName, CarLocationLongitudeDefault);
            }
            set
            {
                AddOrUpdateValue(UserLocationLongitudeKeyName, value);
            }
        }

        /// <summary>
        /// Reminder Time get and set
        /// </summary>
        public long TimeReminderSetting
        {
            get
            {
                return GetValueOrDefault<long>(TimeReminderKeyName, TimeReminderDefault);
            }
            set
            {
                AddOrUpdateValue(TimeReminderKeyName, value);
            }
        }

        /// <summary>
        /// Notes get and set
        /// </summary>
        public string NotesContentSetting
        {
            get
            {
                return GetValueOrDefault<string>(NotesContentKeyName, NotesContentDefault);
            }
            set
            {
                AddOrUpdateValue(NotesContentKeyName, value);
            }
        }
    }
}
