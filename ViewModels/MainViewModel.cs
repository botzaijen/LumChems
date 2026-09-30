using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using LumChems.DataAccess;
using LumChems.Helpers;
using LumChems.Models;
using Microsoft.Data.Sqlite;

namespace LumChems.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly DataStore _dataStore = new DataStore();

        public ObservableCollection<ViewModelBase> TreeRoot { get; set; } = new ObservableCollection<ViewModelBase>();
        public ObservableCollection<Chemical> Chemicals { get; set; } = new ObservableCollection<Chemical>();
        public ObservableCollection<PureChemical> PureChemicals { get; set; } = new ObservableCollection<PureChemical>();

        public IEnumerable<PureChemical> AllAvailableIngredients => PureChemicals.Concat(Chemicals.Cast<PureChemical>());

        private ChemicalNode? _selectedFolder;
        public ChemicalNode? SelectedFolder
        {
            get => _selectedFolder;
            set { _selectedFolder = value; OnPropertyChanged(); }
        }

        private Chemical? _selectedChemical;
        public Chemical? SelectedChemical
        {
            get => _selectedChemical;
            set { _selectedChemical = value; OnPropertyChanged(); }
        }

        private PureChemical? _selectedPureChemical;
        public PureChemical? SelectedPureChemical
        {
            get => _selectedPureChemical;
            set
            {
                if (_selectedPureChemical == value) return;

                // Intercept selection change for the Elements tab DataGrid
                if (CheckDirtyAndPrompt(_selectedPureChemical))
                {
                    _selectedPureChemical = value;
                    OnPropertyChanged();
                }
                else
                {
                    // Snap UI back if the user canceled the unsaved changes prompt
                    Application.Current.Dispatcher.BeginInvoke(new Action(() => OnPropertyChanged(nameof(SelectedPureChemical))));
                }
            }
        }

        // Sub-properties
        public PureChemical? SelectedAvailableIngredient { get; set; }
        public IngredientItem? SelectedAssignedIngredient { get; set; }

        private double _inputIngredientAmount;
        public double InputIngredientAmount
        {
            get => _inputIngredientAmount;
            set { _inputIngredientAmount = value; OnPropertyChanged(); }
        }

        private CategoryType _selectedCategoryType;
        public CategoryType SelectedCategoryType
        {
            get => _selectedCategoryType;
            set { _selectedCategoryType = value; OnPropertyChanged(); }
        }

        private int? _inputCategoryValue;
        public int? InputCategoryValue
        {
            get => _inputCategoryValue;
            set { _inputCategoryValue = value; OnPropertyChanged(); }
        }

        public PureChemicalTypeItem? SelectedTypeItem { get; set; }

        // Commands
        public ICommand AddFolderCommand { get; }
        public ICommand DeleteFolderCommand { get; }
        public ICommand AddChemicalCommand { get; }
        public ICommand DeleteChemicalCommand { get; }
        public ICommand SaveTreeCommand { get; }
        public ICommand AddPureCommand { get; }
        public ICommand DeletePureCommand { get; }
        public ICommand AssignIngredientCommand { get; }
        public ICommand RemoveIngredientCommand { get; }
        public ICommand AddTypeCommand { get; }
        public ICommand RemoveTypeCommand { get; }
        public ICommand SaveCommand { get; }

        public MainViewModel()
        {
            LoadAllData();

            // The second parameter limits execution to only when IsDirty == true
            SaveCommand = new RelayCommand(obj => {
                if (obj is Chemical chem)
                {
                    bool isExisting = _dataStore.ChemicalExistsById(chem.Id);
                    if (!isExisting)
                    {
                        if (_dataStore.ChemicalExistsByName(chem.Name))
                        {
                            MessageBox.Show($"A mixture named '{chem.Name}' already exists.", "Duplicate", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                        _dataStore.InsertChemical(chem);
                    }
                    else
                    {
                        _dataStore.UpdateChemical(chem);
                    }
                    _dataStore.SyncIngredients(chem.Id, chem.Ingredients);
                    chem.IsDirty = false;
                }
                else if (obj is ChemicalNode node)
                {
                    _dataStore.UpdateChemicalNode(node);
                    node.IsDirty = false;
                }
                else if (obj is PureChemical pure)
                {
                    string searchName = pure.OriginalName ?? pure.Name;
                    bool isExisting = _dataStore.PureChemicalExists(searchName);

                    if (!isExisting)
                    {
                        if (_dataStore.PureChemicalExists(pure.Name))
                        {
                            MessageBox.Show($"An element named '{pure.Name}' already exists.", "Duplicate", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                        _dataStore.InsertPureChemical(pure);
                        pure.OriginalName = pure.Name;
                    }
                    else
                    {
                        if (pure.OriginalName != pure.Name && _dataStore.PureChemicalExists(pure.Name))
                        {
                            MessageBox.Show($"Cannot rename to '{pure.Name}' because it already exists.", "Duplicate", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                        _dataStore.UpdatePureChemical(pure);
                    }
                    _dataStore.SyncTypes(pure.Name, pure.Types);
                    pure.IsDirty = false;
                }
                OnPropertyChanged(nameof(AllAvailableIngredients));
            }, obj => (obj as ViewModelBase)?.IsDirty == true);

            AddFolderCommand = new RelayCommand(_ => {
                ViewModelBase? current = (ViewModelBase?)SelectedChemical ?? (ViewModelBase?)SelectedFolder;
                if (!CheckDirtyAndPrompt(current)) return;

                var folder = new ChemicalNode { Name = "New Folder" };
                _dataStore.ExecuteNonQuery("INSERT INTO ChemicalNodes (Id, Name) VALUES ($id, $name)", new SqliteParameter("$id", folder.Id), new SqliteParameter("$name", folder.Name));

                TreeRoot.Add(folder);
                SaveTreeCommand.Execute(null);

                folder.StartTracking();
                SelectedFolder = folder;
                SelectedChemical = null;
            });

            DeleteFolderCommand = new RelayCommand(_ => {
                if (SelectedFolder == null) return;
                _dataStore.DeleteChemicalNode(SelectedFolder.Id);
                RemoveItemFromTree(TreeRoot, SelectedFolder);
                SelectedFolder = null;
                SaveTreeCommand.Execute(null);
            }, _ => SelectedFolder != null);

            AddChemicalCommand = new RelayCommand(_ => {
                ViewModelBase? current = (ViewModelBase?)SelectedChemical ?? (ViewModelBase?)SelectedFolder;
                if (!CheckDirtyAndPrompt(current)) return;

                var c = new Chemical { Name = "New Mixture" };
                TreeRoot.Add(c);
                Chemicals.Add(c);
                SaveTreeCommand.Execute(null);

                c.StartTracking();
                c.IsDirty = true; // Mark dirty so it can be saved to the database

                SelectedChemical = c;
                SelectedFolder = null;
                OnPropertyChanged(nameof(AllAvailableIngredients));
            });

            DeleteChemicalCommand = new RelayCommand(_ => {
                if (SelectedChemical == null) return;
                if (_dataStore.ChemicalExistsById(SelectedChemical.Id)) _dataStore.DeleteChemical(SelectedChemical.Id);

                RemoveItemFromTree(TreeRoot, SelectedChemical);
                Chemicals.Remove(SelectedChemical);
                SelectedChemical = null;
                SaveTreeCommand.Execute(null);
                OnPropertyChanged(nameof(AllAvailableIngredients));
            }, _ => SelectedChemical != null);

            SaveTreeCommand = new RelayCommand(_ => _dataStore.SyncTreeOrder(TreeRoot));

            AddPureCommand = new RelayCommand(_ => {
                if (!CheckDirtyAndPrompt(SelectedPureChemical)) return;

                var p = new PureChemical { Name = "New Element" };
                PureChemicals.Add(p);
                p.StartTracking();
                p.IsDirty = true; // Mark dirty so it can be saved to the database

                SelectedPureChemical = p;
                OnPropertyChanged(nameof(AllAvailableIngredients));
            });

            DeletePureCommand = new RelayCommand(_ => {
                if (SelectedPureChemical == null) return;
                string searchName = SelectedPureChemical.OriginalName ?? SelectedPureChemical.Name;
                if (_dataStore.PureChemicalExists(searchName)) _dataStore.DeletePureChemical(searchName);

                PureChemicals.Remove(SelectedPureChemical);
                SelectedPureChemical = null;
                OnPropertyChanged(nameof(AllAvailableIngredients));
            }, _ => SelectedPureChemical != null);

            AssignIngredientCommand = new RelayCommand(_ => {
                if (SelectedChemical != null && SelectedAvailableIngredient != null)
                {
                    SelectedChemical.Ingredients.Add(new IngredientItem { ChemicalRef = SelectedAvailableIngredient, Amount = InputIngredientAmount });
                    SelectedChemical.IsDirty = true;
                }
            }, _ => SelectedChemical != null && SelectedAvailableIngredient != null);

            RemoveIngredientCommand = new RelayCommand(_ => {
                if (SelectedChemical != null && SelectedAssignedIngredient != null)
                {
                    SelectedChemical.Ingredients.Remove(SelectedAssignedIngredient);
                    SelectedChemical.IsDirty = true;
                }
            }, _ => SelectedChemical != null && SelectedAssignedIngredient != null);

            AddTypeCommand = new RelayCommand(_ => {
                if (SelectedPureChemical != null)
                {
                    SelectedPureChemical.Types.Add(new PureChemicalTypeItem { Category = SelectedCategoryType, Value = InputCategoryValue });
                    SelectedPureChemical.IsDirty = true;
                    InputCategoryValue = null;
                }
            }, _ => SelectedPureChemical != null);

            RemoveTypeCommand = new RelayCommand(_ => {
                if (SelectedPureChemical != null && SelectedTypeItem != null)
                {
                    SelectedPureChemical.Types.Remove(SelectedTypeItem);
                    SelectedPureChemical.IsDirty = true;
                }
            }, _ => SelectedPureChemical != null && SelectedTypeItem != null);
        }

        // Handles the unsaved changes prompt globally
        public bool CheckDirtyAndPrompt(ViewModelBase? item)
        {
            if (item == null || !item.IsDirty) return true;

            string name = item.GetType().GetProperty("Name")?.GetValue(item) as string ?? "Item";
            var res = MessageBox.Show($"You have unsaved changes to '{name}'. Do you want to save them?", "Unsaved Changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);

            if (res == MessageBoxResult.Yes)
            {
                SaveCommand.Execute(item);
                return !item.IsDirty; // Returns true if save successfully resolved the dirty state
            }
            else if (res == MessageBoxResult.No)
            {
                // Discard changes by reloading data from the DB to clear out in-memory edits
                item.IsDirty = false;
                Application.Current.Dispatcher.BeginInvoke(new Action(() => LoadAllData()));
                return false; // Return false to abort immediate selection change, as Reload will reset UI anyway
            }

            return false; // Cancel
        }

        private void LoadAllData()
        {
            var data = _dataStore.LoadAll();

            TreeRoot.Clear();
            foreach (var item in data.treeRoot) TreeRoot.Add(item);

            Chemicals.Clear();
            foreach (var item in data.chemicals) Chemicals.Add(item);

            PureChemicals.Clear();
            foreach (var item in data.pureChemicals) PureChemicals.Add(item);

            // Activate change tracking on all loaded objects
            void TrackRecursive(IEnumerable<ViewModelBase> items)
            {
                foreach (var item in items)
                {
                    item.StartTracking();
                    if (item is ChemicalNode node) TrackRecursive(node.Items);
                }
            }
            TrackRecursive(TreeRoot);
            foreach (var item in PureChemicals) item.StartTracking();

            SelectedFolder = null;
            SelectedChemical = null;
            SelectedPureChemical = null;
        }

        private bool RemoveItemFromTree(IList<ViewModelBase> list, ITreeItem target)
        {
            foreach (var item in list)
            {
                if (item == target)
                {
                    list.Remove(item);
                    return true;
                }
                if (item is ChemicalNode node && RemoveItemFromTree(node.Items, target)) return true;
            }
            return false;
        }
    }
}