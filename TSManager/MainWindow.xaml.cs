using System;
using System.Linq;
using System.Text;
using System.Windows;
using TSManager.Classes;
using System.Windows.Data;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Collections.Generic;

namespace TSManager
{
	public partial class MainWindow : Window
	{
		#region Initialisation
		private MonitorThread _MonitorThread;
		private SortDescription _TableSort;
		private Task _MonitorTask;
		private int[] _TableHead; 

		public SettingsLoader Settings;
		public string ActiveTab;
		public MainWindow()
		{
			// Initialise
			Settings = SettingsLoader.LoadConfiguration("TSManager.json");
			_TableHead = new int[2] { -1, -1 };

			InitializeComponent();
			_LoadServerTabs();
		}
		#endregion Initialisation

		#region Shared Functions
		private void _LoadServerTabs()
		{
			// Create tab for each server
			int i=0;
			foreach( var Server in Settings.Servers )
			{
				var ServerTab = new TabItem()
				{
					Name = string.Format("i{0}", i),
					Content = new TabTemplate(),
					Header = Server.Name
				};

				var NewTab = (TabTemplate)ServerTab.Content;
					NewTab.TaskGrid.Sorting += _TaskGrid_Sorting;
					NewTab.EndButton.Click += _EndTask_Click;
				WindowTab.Items.Add(ServerTab);
				i++;
			}
		}

		private void _UpdateTask()
		{
			// Create Task
			_MonitorThread = new MonitorThread(this);
			_MonitorThread.ProgressUpdate += (s, e) => Dispatcher.Invoke(new Action( () => _UpdateFrontend(e) ));

			// Start Task
			_MonitorTask = Task.Run( () => _MonitorThread.MonitorLoop() );
		}

		private void _UpdateFrontend(List<ProcessData> updateData)
		{
			var CurrentTab = _FindTab(ActiveTab);
			if( CurrentTab != null )
			{
				// Get current selection
				var CurrentRow = (ProcessData)CurrentTab.TaskGrid.CurrentItem;
				int SelectedItem = (CurrentRow != null) ? (int)CurrentRow.PID : -1;

				// Update Table Data
				CurrentTab.TaskGrid.ItemsSource = updateData;
				CurrentTab.UpdateLabel.Content = string.Format("Last successful update: {0}", DateTime.Now.ToString("HH:mm:ss tt"));

				// Restore sort order (if applicable)
				if( _TableSort.PropertyName != null )
				{
					CurrentTab.TaskGrid.ColumnFromDisplayIndex(_TableHead[0]).SortDirection = _TableSort.Direction;
					CurrentTab.TaskGrid.Items.SortDescriptions.Add(_TableSort);
				}

				// Restore selection (if applicable)
				if( SelectedItem != -1 )
				{
					// Find our selection again by PID
					var NewSelection = CurrentTab.TaskGrid.Items.OfType<ProcessData>().Single(q => (q.PID == SelectedItem));
					if( NewSelection != null )
					{
						CurrentTab.TaskGrid.SelectedItem = NewSelection;
						CurrentTab.TaskGrid.CurrentItem = NewSelection;
					}			
				}
			}
		}

		private TabTemplate _FindTab(string TabName)
		{
			var Tabs = WindowTab.Items;
			foreach( TabItem tTab in Tabs )
			{
				if( tTab.Header.ToString() != TabName ) continue;
				return (TabTemplate)tTab.Content;
			}

			return null;
		}
		#endregion Shared Functions

		#region Window Events
		private void _TaskGrid_Sorting(object sender, DataGridSortingEventArgs e)
		{
			_TableHead[0] = e.Column.DisplayIndex;
			var SortOrder = ( _TableHead[0] == _TableHead[1] ) ? ListSortDirection.Descending : ListSortDirection.Ascending;

			// Update sort and last sorted header
			_TableSort = new SortDescription(e.Column.SortMemberPath, SortOrder);
			_TableHead[1] = _TableHead[0];
		}

		private void _WindowTab_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			var NewTab = ((TabItem)WindowTab.SelectedItem).Header.ToString();
			if( NewTab != ActiveTab )
			{
				// Clear Sort Variables
				_TableSort = new SortDescription(null, ListSortDirection.Descending);
				_TableHead = new int[2] { -1, -1 };
				ActiveTab = NewTab;

				_FindTab(NewTab).TaskGrid.SelectedItem = null;
				if( _MonitorTask != null && _MonitorTask.IsCompleted )
				{
					// Restart Task (Died for some reason?)
					_MonitorTask = Task.Run( () => _MonitorThread.MonitorLoop() );
					Console.WriteLine("Respawned Async update");
				}
				else
				{
					// Trigger async update of tab
					if( _MonitorThread != null ) _MonitorThread.TriggerUpdate();
				}
			}
		}

		private void _Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			// Stop and wait for task to end
			_MonitorThread.Stop();
			_MonitorTask.Wait();

			// Dispose of thread and task
			_MonitorThread.Dispose();
			_MonitorTask.Dispose();
		}

		private void _EndTask_Click(object sender, EventArgs e)
		{
			var ServerAddress = Settings.Servers.Single( q => (q.Name == ActiveTab) ).Address;
			if( ServerAddress != null )
			{
				var ActiveGrid = _FindTab(ActiveTab).TaskGrid;
				if( ActiveGrid.SelectedItem != null )
				{
					UInt32 ProcessID = ((ProcessData)ActiveGrid.SelectedItem).PID;
					string MessageText = string.Format("Are you sure you wish to end the process:\nPID:{0} on {1}", ProcessID, ServerAddress);

					if( MessageBox.Show(MessageText, "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes )
					{
						if( !_MonitorThread.TerminateProcess(ServerAddress, ProcessID))
						{
							MessageBox.Show("Unable to end process!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
						}
						else
						{
							MessageBox.Show("Process has ended successfully", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
						}
					}
				}
			}
		}

		private void _Window_Loaded(object sender, RoutedEventArgs e)
		{
			_UpdateTask();
		}
		#endregion Window Events
	}
}
