using System;
using System.Text;
using System.Linq;
using System.Windows;
using System.Threading;
using System.Management;
using System.ComponentModel;
using System.Windows.Controls;
using System.Collections.Generic;

namespace TSManager.Classes
{
	class MonitorThread : IDisposable
	{
		#region Initialisation
		public event EventHandler<List<ProcessData>> ProgressUpdate;
		private AutoResetEvent _Sleeper;
		private MainWindow _Parent;
		private bool _Running;

		public MonitorThread(MainWindow p)
		{
			_Sleeper = new AutoResetEvent(false);
			_Running = true;
			_Parent = p;
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if( disposing )
			{
				_Sleeper.Dispose();
			}
		}
		#endregion Initialisation

		#region Task Controls
		public void Stop()
		{
			this._Running = false;
			this._Sleeper.Set();
		}

		public void TriggerUpdate()
		{
			this._Sleeper.Set();
		}
		#endregion Task Controls

		#region Task Loop
		public void MonitorLoop()
		{
			while( _Running )
			{
				try
				{
					if( _Parent.ActiveTab != null )
					{
						var ServerAddress = _Parent.Settings.Servers.Single( q => (q.Name == _Parent.ActiveTab) ).Address;
						var PerfDict = new Dictionary<UInt32, UInt64[]>();
						var Data = new List<ProcessData>();

						// Open WMI Connection to Server
						if( _Parent.Settings.Username != "" )
						{
							// Use supplied credentials
							var ConnectionOpt = new ConnectionOptions()
							{
								Username = _Parent.Settings.Username,
								Password = _Parent.Settings.Password,
								Impersonation = ImpersonationLevel.Impersonate,
								EnablePrivileges = true
							};

							ManagementScope Server = new ManagementScope(string.Format(@"\\{0}\root\cimv2", ServerAddress), ConnectionOpt);
						}
						else
						{
							// Use logged-in authentication
							ManagementScope Server = new ManagementScope(string.Format(@"\\{0}\root\cimv2", ServerAddress));
						}

						// Query Win32_PerfFormattedData on remote server
						SelectQuery WMIQuery = new SelectQuery("SELECT * FROM Win32_PerfFormattedData_PerfProc_Process");
						using( var SearchElem = new ManagementObjectSearcher(Server, WMIQuery) )
						{
							foreach( ManagementObject ProcessData in SearchElem.Get() )
							{
								var prData = new UInt64[2]
								{
									Convert.ToUInt64(ProcessData["PercentProcessorTime"]),
									Convert.ToUInt64(ProcessData["WorkingSet"])
								};

								var ProcessID = Convert.ToUInt32(ProcessData["IDProcess"]);
								if( !PerfDict.ContainsKey(ProcessID) ) PerfDict.Add(ProcessID, prData);
							}
						}

						// Query Win32_Process on remote server
						SelectQuery WMIQuery2 = new SelectQuery("SELECT * FROM Win32_Process");
						using( var SearchElem = new ManagementObjectSearcher(Server, WMIQuery2) )
						{
							foreach( ManagementObject ProcessData in SearchElem.Get() )
							{
								var OwnerData = ProcessData.InvokeMethod("GetOwner", null, null);
								var ProcessID = Convert.ToUInt32(ProcessData["ProcessId"]);
								var pData = new ProcessData()
								{
									Username	= string.Format("{0}\\{1}", OwnerData["Domain"], OwnerData["User"]),
									Description = ProcessData["Description"].ToString(),
									Name		= ProcessData["Name"].ToString(),
									PID			= ProcessID,
									Memory		= "",
									CPU			= 0
								};

								if( pData.Name == "System Idle Process" || pData.Name == "System" ) continue;
								if( PerfDict.ContainsKey(ProcessID) )
								{
									// Add Performance Data to Process Data class
									var PerfData = PerfDict[ProcessID];
										pData.Memory	= _FormatBytes((long)PerfData[1]);
										pData.CPU		= PerfData[0];
								}

								// Add process data to List
								Data.Add(pData);
							}
						}

						// Send event to update GUI
						ProgressUpdate(this, Data);
					}

					// Wait for next update interval
					_Sleeper.WaitOne(TimeSpan.FromSeconds(_Parent.Settings.UpdateInterval));
				}
				catch( Exception Ex )
				{
					if( Ex.Message.Contains("found") || Ex.Message.Contains("no matching element") ) continue; // Thanks, WMI

					MessageBox.Show(string.Format("Remote Management Error:\n{0}", Ex.Message), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
					break; // Break out of running loop
				}
			}
		}

		public bool TerminateProcess(string ServerAddress, UInt32 ProcessID)
		{
			// Open WMI Connection to Server
			if( _Parent.Settings.Username != "" )
			{
				// Use supplied credentials
				var ConnectionOpt = new ConnectionOptions()
				{
					Username = _Parent.Settings.Username,
					Password = _Parent.Settings.Password,
					Impersonation = ImpersonationLevel.Impersonate,
					EnablePrivileges = true
				};

				ManagementScope Server = new ManagementScope(string.Format(@"\\{0}\root\cimv2", ServerAddress), ConnectionOpt);
			}
			else
			{
				// Use logged-in authentication
				ManagementScope Server = new ManagementScope(string.Format(@"\\{0}\root\cimv2", ServerAddress));
			}

			// Find the requested process
			SelectQuery WMIQuery2 = new SelectQuery(string.Format("SELECT * FROM Win32_Process WHERE ProcessId = {0}", ProcessID));
			using( var SearchElem = new ManagementObjectSearcher(Server, WMIQuery2) )
			{
				foreach( ManagementObject ProcessData in SearchElem.Get() )
				{
					// Verify PID and Terminate Process
					var PID = Convert.ToUInt32(ProcessData["ProcessId"]);
					if( PID == ProcessID )
					{
						var ReturnCode = (UInt32)ProcessData.InvokeMethod("Terminate", null);
						return ( ReturnCode == 0 ) ? true : false;
					}
				}
			}

			return false;
		}

		private string _FormatBytes(long bytes)
		{
			const int scale = 1024;
			string[] orders = new string[] { "GB", "MB", "KB", "Bytes" };
			long max = (long)Math.Pow(scale, orders.Length - 1);
 
			foreach (string order in orders)
			{
				if ( bytes > max ) return string.Format("{0:##.##} {1}", decimal.Divide( bytes, max ), order);
				max /= scale;
			}

			return "0 Bytes";
		}
		#endregion Task Loop
	}

	#region Process Data Class
	public class ProcessData
	{
		public string Description { get; set; }
		public string Username { get; set; }
		public string Memory { get; set; }
		public string Name { get; set; }

		public UInt32 PID { get; set; }
		public UInt64 CPU { get; set; }
	}
	#endregion Process Data Class
}
