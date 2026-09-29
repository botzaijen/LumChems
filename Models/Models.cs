using System;
using System.Collections.ObjectModel;
using ChemicalInventoryApp.Helpers;

namespace ChemicalInventoryApp.Models
{
    public enum ChemicalTag { Unspecified, Reagent, Solvent, Catalyst, Toxic }
    public enum PropertyType { Density, PhLevel, Viscosity, BoilingPoint }
    public enum CategoryType { HazardClass, StorageGroup, ToxicityLevel }

    public class ChemicalProperty : ViewModelBase
    {
        private PropertyType _type;
        private double? _value;

        public PropertyType Type { get => _type; set { _type = value; OnPropertyChanged(); } }
        public double? Value { get => _value; set { _value = value; OnPropertyChanged(); } }
    }

    public class PureChemicalTypeItem : ViewModelBase
    {
        private CategoryType _category;
        private int? _value;

        public CategoryType Category { get => _category; set { _category = value; OnPropertyChanged(); } }
        public int? Value { get => _value; set { _value = value; OnPropertyChanged(); } }
    }
    public interface ITreeItem
    {
        string Id { get; }
        string? ParentNodeId { get; set; }
        int OrderIndex { get; set; }
    }

    public class ChemicalNode : ViewModelBase, ITreeItem
    {
        private string _name = "New Folder";
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
        public string? ParentNodeId { get; set; }
        public int OrderIndex { get; set; }

        // Holds both ChemicalNodes (sub-folders) and Chemicals (mixtures)
        public ObservableCollection<ViewModelBase> Items { get; set; } = new ObservableCollection<ViewModelBase>();
    }
    public class IngredientItem : ViewModelBase
    {
        private double _amount;
        public PureChemical ChemicalRef { get; set; }
        
        public double Amount { get => _amount; set { _amount = value; OnPropertyChanged(); } }
        public string DisplayName => ChemicalRef?.Name ?? "Unknown";
    }

    public class PureChemical : ViewModelBase
    {
        private string _name = string.Empty;
        private double? _mw;
        
        public string? OriginalName { get; set; } 
        public string Name { get => _name; set { _name = value; if (OriginalName == null) OriginalName = value; OnPropertyChanged(); } }
        
        public double? Mw { get => _mw; set { _mw = value; OnPropertyChanged(); } }
        public ObservableCollection<PureChemicalTypeItem> Types { get; set; } = new ObservableCollection<PureChemicalTypeItem>();
    }

    public class Chemical : PureChemical, ITreeItem
    {
        private uint _refNo;
        private string? _vendor;
        private string? _vendorId;
        private ChemicalTag _tag = ChemicalTag.Unspecified;

        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        public uint RefNo { get => _refNo; set { _refNo = value; OnPropertyChanged(); } }
        public string? Vendor { get => _vendor; set { _vendor = value; OnPropertyChanged(); } }
        public string? VendorId { get => _vendorId; set { _vendorId = value; OnPropertyChanged(); } }
        public ChemicalTag Tag { get => _tag; set { _tag = value; OnPropertyChanged(); } }
        
        // Properties for Tree Hierarchy
        private string? _parentNodeId;
        public string? ParentNodeId { get => _parentNodeId; set { _parentNodeId = value; OnPropertyChanged(); } }

        private int _orderIndex;
        public int OrderIndex { get => _orderIndex; set { _orderIndex = value; OnPropertyChanged(); } }

        public ObservableCollection<ChemicalProperty> Properties { get; set; } = new ObservableCollection<ChemicalProperty>();
        public ObservableCollection<IngredientItem> Ingredients { get; set; } = new ObservableCollection<IngredientItem>();
    }
}