using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SpellEditor.Sources.Emulator
{
    /// <summary>
    /// Puts the world tables of the selected emulator behind one compact main tab and owns the
    /// connection. One instance per emulator, MainWindow throws it away when the setting changes.
    /// </summary>
    public class WorldIntegration
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly MainWindow _mainWindow;
        private readonly TabControl _host;
        private readonly List<WorldTableEditor> _editors = new List<WorldTableEditor>();

        private TabItem _tab;
        private ComboBox _tableSelector;
        private ContentControl _editorHost;
        private bool _updatingSelector;

        private readonly object _connectLock = new object();

        private WorldDatabase _database;
        private List<string> _missingTables = new List<string>();
        private uint _spellId;
        private bool _spellSelected;
        private bool _connecting;
        private bool _probedTables;
        private string _lastConnectError;

        public Emulator Emulator { get; }

        public WorldIntegration(MainWindow mainWindow, TabControl host, Emulator emulator)
        {
            _mainWindow = mainWindow;
            _host = host;
            Emulator = emulator;
        }

        public bool Owns(object tab) => ReferenceEquals(tab, _tab);

        /// <summary>Drops the tabs off the strip so a fresh integration can take over.</summary>
        public void RemoveTabs()
        {
            // Step off our tab first, removing the selected one would leave the host in an
            // inconsistent selection state.
            if (ReferenceEquals(_host.SelectedItem, _tab))
                _host.SelectedIndex = 0;

            if (_tab != null)
                _host.Items.Remove(_tab);
            _tab = null;
            _tableSelector = null;
            _editorHost = null;
            _editors.Clear();
            _database = null;
            _probedTables = false;
        }

        public void UpdateTabs(bool spellSelected)
        {
            _spellSelected = spellSelected;
            UpdateTabs();
        }

        private void UpdateTabs()
        {
            if (!WorldDatabase.IsConfigured || !_spellSelected)
            {
                if (_tab != null)
                    _tab.Visibility = Visibility.Collapsed;
                if (ReferenceEquals(_host.SelectedItem, _tab))
                    _host.SelectedIndex = 0;
                return;
            }

            if (_tab == null)
                BuildTab();

            PopulateSelector();
            _tab.Visibility = SelectedEditor != null ? Visibility.Visible : Visibility.Collapsed;
        }

        private bool IsAvailable(WorldTable table) =>
            !_missingTables.Contains(table.Name, StringComparer.OrdinalIgnoreCase);

        private void BuildTab()
        {
            foreach (var table in Emulator.Tables)
            {
                var editor = new WorldTableEditor(table, _mainWindow);
                editor.SetSpell(_spellId);
                editor.SetDatabase(_database);
                editor.Saved += () => InvalidateOthers(editor);
                _editors.Add(editor);
            }

            _tableSelector = new ComboBox
            {
                MinWidth = 280,
                MaxWidth = 520,
                Margin = new Thickness(8, 4, 8, 4),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            _tableSelector.SelectionChanged += TableSelector_SelectionChanged;

            var refreshButton = new Button
            {
                Content = Localise("emu_open_refresh", "OPEN / REFRESH"),
                MinWidth = 125,
                Margin = new Thickness(0, 4, 8, 4),
                Padding = new Thickness(12, 3, 12, 3)
            };
            refreshButton.Click += (sender, args) => Activate(_tab);

            var bar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            bar.Children.Add(new TextBlock
            {
                Text = Localise("emu_table_selector", "Server spell data:"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 4, 0, 4)
            });
            bar.Children.Add(_tableSelector);
            bar.Children.Add(refreshButton);

            _editorHost = new ContentControl();
            var panel = new DockPanel();
            DockPanel.SetDock(bar, Dock.Top);
            panel.Children.Add(bar);
            panel.Children.Add(_editorHost);

            _tab = new TabItem
            {
                Header = Localise("emu_server_db_tab", "Server DB"),
                Content = panel
            };
            _host.Items.Add(_tab);
        }

        private WorldTable SelectedTable => (_tableSelector?.SelectedItem as ComboBoxItem)?.Tag as WorldTable;

        private WorldTableEditor SelectedEditor
        {
            get
            {
                var table = SelectedTable;
                return table == null ? null : _editors.FirstOrDefault(editor => ReferenceEquals(editor.Table, table));
            }
        }

        private void PopulateSelector()
        {
            if (_tableSelector == null)
                return;

            var selectedName = SelectedTable?.Name;
            _updatingSelector = true;
            _tableSelector.Items.Clear();
            foreach (var table in Emulator.Tables.Where(IsAvailable))
            {
                _tableSelector.Items.Add(new ComboBoxItem
                {
                    Content = table.Header,
                    Tag = table
                });
            }

            var selected = _tableSelector.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(item => string.Equals(((WorldTable)item.Tag).Name, selectedName,
                    StringComparison.OrdinalIgnoreCase));
            _tableSelector.SelectedItem = selected ?? _tableSelector.Items.OfType<ComboBoxItem>().FirstOrDefault();
            _updatingSelector = false;
            ShowSelectedEditor();
        }

        private void TableSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updatingSelector)
                return;

            ShowSelectedEditor();
            if (ReferenceEquals(_host.SelectedItem, _tab))
                Activate(_tab);
        }

        private void ShowSelectedEditor()
        {
            if (_editorHost != null)
                _editorHost.Content = SelectedEditor;
        }

        public async void Activate(object tab)
        {
            if (!ReferenceEquals(tab, _tab) || SelectedEditor == null)
                return;

            var editor = SelectedEditor;

            if (!WorldDatabase.IsConfigured)
            {
                editor.ShowMessage(Localise("emu_disabled",
                    "The world database integration is turned off. Turn it on and enter your " +
                    "server details in the settings."), false);
                return;
            }

            if (_database == null || !_probedTables)
            {
                await ConnectAsync(editor);
                if (_database == null)
                    return;

                // Probing may remove a missing table and select the next available editor.
                editor = SelectedEditor;
                if (editor == null)
                    return;
            }

            editor.Refresh(_spellId);
        }

        /// <summary>Connects on first use. Null means the world database is off or unreachable.</summary>
        public WorldDatabase EnsureDatabase()
        {
            if (_database != null)
                return _database;
            if (!WorldDatabase.IsConfigured)
                return null;

            lock (_connectLock)
            {
                if (_database != null)
                    return _database;
                try
                {
                    var database = WorldDatabase.FromConfig();
                    database.TestConnection();
                    _database = database;
                    foreach (var current in _editors)
                        current.SetDatabase(database);
                }
                catch (Exception exception)
                {
                    Logger.Error(exception, $"Failed to connect to the {Emulator.Name} world database");
                    _lastConnectError = exception.Message;
                }
                return _database;
            }
        }

        /// <summary>Editing spell_ranks changes which rows the other tabs inherit.</summary>
        private void InvalidateOthers(WorldTableEditor source)
        {
            foreach (var editor in _editors)
            {
                if (editor != source)
                    editor.Invalidate();
            }
        }

        public void SetSpell(uint spellId)
        {
            _spellId = spellId;
            foreach (var editor in _editors)
                editor.SetSpell(spellId);

            // The rest catch up when they are opened
            if (_database != null && ReferenceEquals(_host.SelectedItem, _tab))
                SelectedEditor?.Refresh(spellId);
        }

        public void RefreshLocalisation()
        {
            if (_tab != null)
                _tab.Header = Localise("emu_server_db_tab", "Server DB");
            PopulateSelector();
            foreach (var editor in _editors)
                editor.RefreshLocalisation();
        }

        private async Task ConnectAsync(WorldTableEditor editor)
        {
            if (_connecting)
                return;

            _connecting = true;
            editor.ShowMessage(Localise("emu_connecting", "Connecting..."), false);
            try
            {
                _missingTables = await Task.Run(() =>
                {
                    var database = EnsureDatabase();
                    if (database == null)
                        throw new Exception(_lastConnectError ?? "the world database could not be reached");
                    return database.FindMissingTables(Emulator.Tables.Select(table => table.Name));
                });
                _probedTables = true;

                UpdateTabs();
                if (_missingTables.Count > 0)
                    Logger.Info($"{Emulator.Name} tables not present in the world database: {string.Join(", ", _missingTables)}");
            }
            catch (Exception exception)
            {
                Logger.Error(exception, $"Failed to connect to the {Emulator.Name} world database");
                _database = null;
                foreach (var current in _editors)
                    current.SetDatabase(null);
                editor.ShowMessage(string.Format(Localise("emu_connect_failed", "Could not connect: {0}"),
                    exception.Message), true);
            }
            finally
            {
                _connecting = false;
            }
        }

        private static string Localise(string key, string fallback)
        {
            var resource = Application.Current?.TryFindResource(key) as string;
            return string.IsNullOrWhiteSpace(resource) ? fallback : resource.Trim();
        }
    }
}
