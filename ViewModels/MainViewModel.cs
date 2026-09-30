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
        public ICommand AddFolderCommand { get; }
        public ICommand SaveTreeCommand { get; }

        public ObservableCollection<Chemical> Chemicals { get; set; } = new ObservableCollection<Chemical>();
        public ObservableCollection<PureChemical> PureChemicals { get; set; } = new ObservableCollection<PureChemical>();

        public IEnumerable<PureChemical> AllAvailableIngredients => PureChemicals.Concat(Chemicals.Cast<PureChemical>());

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
            set { _selectedPureChemical = value; OnPropertyChanged(); }
        }

        public PureChemical? SelectedAvailableIngredient { get; set; }
        public IngredientItem? SelectedAssignedIngredient { get; set; }
        
        private double _inputIngredientAmount;
        public double InputIngredientAmount
        {
            get => _inputIngredientAmount;
            set { _inputIngredientAmount = value; OnPropertyChanged(); }
        }

        public ICommand AddChemicalCommand { get; }
        public ICommand DeleteChemicalCommand { get; }
        public ICommand AddPureCommand { get; }
        public ICommand DeletePureCommand { get; }
        public ICommand AssignIngredientCommand { get; }
        public ICommand RemoveIngredientCommand { get; }
        public ICommand SaveCommand { get; }
        // Properties for Types UI
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

        private PureChemicalTypeItem? _selectedTypeItem;
        public PureChemicalTypeItem? SelectedTypeItem
        {
            get => _selectedTypeItem;
            set { _selectedTypeItem = value; OnPropertyChanged(); }
        }

        // Commands
        public ICommand AddTypeCommand { get; }
        public ICommand RemoveTypeCommand { get; }
        
        // Track the active folder
        private ChemicalNode? _selectedFolder;
        public ChemicalNode? SelectedFolder
        {
            get => _selectedFolder;
            set { _selectedFolder = value; OnPropertyChanged(); }
        }
        public ICommand DeleteFolderCommand { get; }
        // Helper method for recursive UI removal
        private bool RemoveItemFromTree(IList<ViewModelBase> list, ITreeItem target)
        {
            foreach (var item in list)
            {
                if (item == target)
                {
                    list.Remove(item);
                    return true;
                }
                if (item is ChemicalNode node && RemoveItemFromTree(node.Items, target))
                    return true;
            }
            return false;
        }
        public MainViewModel()
        {
            var data = _dataStore.LoadAll();
            // Assign the pre-built, sorted tree structure to the UI
            TreeRoot = new ObservableCollection<ViewModelBase>(data.treeRoot);
            Chemicals = new ObservableCollection<Chemical>(data.chemicals);
            PureChemicals = new ObservableCollection<PureChemical>(data.pureChemicals);

            SaveCommand = new RelayCommand(obj => {
                if (obj is Chemical chem)
                {
                    bool isExisting = _dataStore.ChemicalExistsById(chem.Id);

                    if (!isExisting)
                    {
                        if (_dataStore.ChemicalExistsByName(chem.Name))
                        {
                            MessageBox.Show($"A mixture named '{chem.Name}' already exists.", "Duplicate Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                        _dataStore.InsertChemical(chem);
                    }
                    else
                    {
                        _dataStore.UpdateChemical(chem);
                    }
                    _dataStore.SyncIngredients(chem.Id, chem.Ingredients);
                }
                else if (obj is ChemicalNode node)
                {
                    _dataStore.UpdateChemicalNode(node);
                }
                else if (obj is PureChemical pure)
                {
                    string searchName = pure.OriginalName ?? pure.Name;
                    bool isExisting = _dataStore.PureChemicalExists(searchName);

                    if (!isExisting)
                    {
                        if (_dataStore.PureChemicalExists(pure.Name))
                        {
                            MessageBox.Show($"An element named '{pure.Name}' already exists.", "Duplicate Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                        _dataStore.InsertPureChemical(pure);
                        pure.OriginalName = pure.Name; // Sync tracking after successful insert
                    }
                    else
                    {
                        // Check if the user is trying to rename it to something that already exists
                        if (pure.OriginalName != pure.Name && _dataStore.PureChemicalExists(pure.Name))
                        {
                            MessageBox.Show($"Cannot rename to '{pure.Name}' because it already exists.", "Duplicate Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                        _dataStore.UpdatePureChemical(pure);
                    }
                    _dataStore.SyncTypes(pure.Name, pure.Types);
                }
                OnPropertyChanged(nameof(AllAvailableIngredients));
            });

            SaveTreeCommand = new RelayCommand(_ => {
                _dataStore.SyncTreeOrder(TreeRoot);
            });
            
            AddChemicalCommand = new RelayCommand(_ => {
                var c = new Chemical { Name = "New Mixture" };
                TreeRoot.Add(c);
                Chemicals.Add(c);
                SelectedChemical = c;
                OnPropertyChanged(nameof(AllAvailableIngredients));
                SaveTreeCommand.Execute(null);
            });

            DeleteChemicalCommand = new RelayCommand(_ => {
                if (SelectedChemical == null) return;

                if (_dataStore.ChemicalExistsById(SelectedChemical.Id))
                    _dataStore.DeleteChemical(SelectedChemical.Id);

                Chemicals.Remove(SelectedChemical);
                SelectedChemical = null;
                OnPropertyChanged(nameof(AllAvailableIngredients));
                SaveTreeCommand.Execute(null);
            }, _ => SelectedChemical != null);

            AddPureCommand = new RelayCommand(_ => {
                var p = new PureChemical { Name = "New Element" };
                // REMOVED: _dataStore.InsertPureChemical(p);
                PureChemicals.Add(p);
                SelectedPureChemical = p;
                OnPropertyChanged(nameof(AllAvailableIngredients));
            });

            DeletePureCommand = new RelayCommand(_ => {
                if (SelectedPureChemical == null) return;

                string searchName = SelectedPureChemical.OriginalName ?? SelectedPureChemical.Name;
                if (_dataStore.PureChemicalExists(searchName))
                    _dataStore.DeletePureChemical(searchName);

                PureChemicals.Remove(SelectedPureChemical);
                SelectedPureChemical = null;
                OnPropertyChanged(nameof(AllAvailableIngredients));
            }, _ => SelectedPureChemical != null);

            AssignIngredientCommand = new RelayCommand(_ => {
                if (SelectedChemical != null && SelectedAvailableIngredient != null)
                {
                    SelectedChemical.Ingredients.Add(new IngredientItem { ChemicalRef = SelectedAvailableIngredient, Amount = InputIngredientAmount });
                }
            }, _ => SelectedChemical != null && SelectedAvailableIngredient != null);

            RemoveIngredientCommand = new RelayCommand(_ => {
                if (SelectedChemical != null && SelectedAssignedIngredient != null)
                {
                    SelectedChemical.Ingredients.Remove(SelectedAssignedIngredient);
                }
            }, _ => SelectedChemical != null && SelectedAssignedIngredient != null);
            
            AddTypeCommand = new RelayCommand(_ => {
                if (SelectedPureChemical != null)
                {
                    SelectedPureChemical.Types.Add(new PureChemicalTypeItem
                    {
                        Category = SelectedCategoryType,
                        Value = InputCategoryValue
                    });
                    InputCategoryValue = null; // Clear input field after adding
                }
            }, _ => SelectedPureChemical != null);

            RemoveTypeCommand = new RelayCommand(_ => {
                if (SelectedPureChemical != null && SelectedTypeItem != null)
                {
                    SelectedPureChemical.Types.Remove(SelectedTypeItem);
                }
            }, _ => SelectedPureChemical != null && SelectedTypeItem != null);
            
            
            AddFolderCommand = new RelayCommand(_ => {
                var folder = new ChemicalNode { Name = "New Folder" };
                if (_dataStore is not null)
                {
                    _dataStore.ExecuteNonQuery("INSERT INTO ChemicalNodes (Id, Name) VALUES ($id, $name)", new SqliteParameter("$id", folder.Id), new SqliteParameter("$name", folder.Name));
                }
                TreeRoot.Add(folder);

                SaveTreeCommand.Execute(null);
            });

            
            DeleteFolderCommand = new RelayCommand(_ => {
                if (SelectedFolder == null) return;

                // Delete from DB (cascades automatically)
                _dataStore.DeleteChemicalNode(SelectedFolder.Id);

                // Remove from UI Tree recursively
                RemoveItemFromTree(TreeRoot, SelectedFolder);

                SelectedFolder = null;
                SaveTreeCommand.Execute(null);
            }, _ => SelectedFolder != null);
        }
    }
}