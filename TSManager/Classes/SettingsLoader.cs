using System;
using System.IO;
using System.Text;
using System.Windows;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace TSManager.Classes
{
	public class SettingsLoader
	{
        #region Static Methods
        public static SettingsLoader LoadConfiguration(string ConfigurationName)
        {
			string json = null;
			SettingsLoader CastData = null;

			try
			{
				TextReader reader = new StreamReader(ConfigurationName);
				json = reader.ReadToEnd();
				reader.Close();
			}
			catch( Exception Ex )
			{
				MessageBox.Show("Unable to open configuration file:\n"+Ex.Message, "Application Exception", MessageBoxButton.OK, MessageBoxImage.Error);
				return null;
			}

			try
			{
				CastData = JsonConvert.DeserializeObject<SettingsLoader>(json);
			}
			catch( Newtonsoft.Json.JsonReaderException Ex )
			{
				MessageBox.Show("Unable to read configuration file:\n"+Ex.Message, "Application Exception", MessageBoxButton.OK, MessageBoxImage.Error);
				return null;
			}

            return CastData;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            var fields = this.GetType().GetProperties();

            foreach( var propInfo in fields )
            {
                sb.AppendFormat("{0} = {1}" + Environment.NewLine, propInfo.Name, propInfo.GetValue(this, null));
            }

            return sb.ToString();
        }
        #endregion Static Methods

        #region Configuration Top-Level
		public ServerChild[] Servers { get; set; }
		public int UpdateInterval { get; set; }

		public string Username { get; set; }
		public string Password { get; set; }
		#endregion Configuration Top-Level

		#region Child Level
		public class ServerChild
		{
			public String Name { get; set; }
			public String Address { get; set; }
		}
		#endregion Child Level
	}
}
