using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LumChems.ViewModels;
using LumChems.Helpers;
using LumChems.Models;

namespace ChemicalInventoryApp.Views
{
    public partial class MainWindow : Window
    {
        private Point _startPoint;
        private bool _isDragging;

        public MainWindow()
        {
            InitializeComponent();
        }
        private void Ingredients_Drop(object sender, DragEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm == null || vm.SelectedChemical == null) return;

            var droppedData = e.Data.GetData(typeof(Chemical)) as PureChemical
                           ?? e.Data.GetData(typeof(PureChemical)) as PureChemical;

            if (droppedData != null)
            {
                if (droppedData == vm.SelectedChemical)
                {
                    MessageBox.Show("A mixture cannot contain itself as an ingredient.", "Invalid Operation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (vm.SelectedChemical.Ingredients.Any(i => i.ChemicalRef == droppedData))
                {
                    MessageBox.Show("This ingredient is already in the mixture.", "Duplicate Ingredient", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                vm.SelectedChemical.Ingredients.Add(new IngredientItem
                {
                    ChemicalRef = droppedData,
                    Amount = vm.InputIngredientAmount
                });

                // ADD THIS LINE: Manually mark as dirty when ingredient is dropped
                vm.SelectedChemical.IsDirty = true;

                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        }
        /*private void ChemicalTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is MainViewModel vm)
            {
                if (e.NewValue is Chemical chem)
                {
                    vm.SelectedChemical = chem;
                    vm.SelectedFolder = null;
                }
                else if (e.NewValue is ChemicalNode node)
                {
                    vm.SelectedFolder = node;
                    vm.SelectedChemical = null;
                }
            }
        }*/
        private void ChemicalTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var treeViewItem = FindAncestor<TreeViewItem>((DependencyObject)e.OriginalSource);
            if (treeViewItem == null || !(DataContext is MainViewModel vm)) return;

            var newItem = treeViewItem.DataContext as ViewModelBase;
            if (newItem == null) return;

            // Do nothing if double clicking the item that is already open
            if (newItem == vm.SelectedChemical || newItem == vm.SelectedFolder) return;

            // Check if the currently open tree item has unsaved changes
            ViewModelBase? currentTreeItem = (ViewModelBase?)vm.SelectedChemical ?? (ViewModelBase?)vm.SelectedFolder;

            if (vm.CheckDirtyAndPrompt(currentTreeItem))
            {
                // Change selection if user handled or had no unsaved changes
                if (newItem is Chemical chem)
                {
                    vm.SelectedChemical = chem;
                    vm.SelectedFolder = null;
                }
                else if (newItem is ChemicalNode node)
                {
                    vm.SelectedFolder = node;
                    vm.SelectedChemical = null;
                }
            }
        }

        private void Tree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(null);
        }

        private void Tree_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && !_isDragging)
            {
                Point position = e.GetPosition(null);
                if (Math.Abs(position.X - _startPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(position.Y - _startPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    var treeView = sender as TreeView;
                    var treeViewItem = FindAncestor<TreeViewItem>((DependencyObject)e.OriginalSource);

                    if (treeViewItem != null)
                    {
                        _isDragging = true;
                        DragDrop.DoDragDrop(treeViewItem, treeViewItem.DataContext, DragDropEffects.Move);
                        _isDragging = false;
                    }
                }
            }
        }

        private void Tree_Drop(object sender, DragEventArgs e)
        {
            // Abort if dropped onto the ingredients list instead of the tree
            if (e.Handled) return;

            var droppedData = e.Data.GetData(typeof(Chemical)) ?? e.Data.GetData(typeof(ChemicalNode)) as ITreeItem;
            var targetItem = FindAncestor<TreeViewItem>((DependencyObject)e.OriginalSource);
            var vm = DataContext as MainViewModel;

            if (droppedData != null && vm != null )
            {
                // Remove from old location
                RemoveItem(vm.TreeRoot, droppedData as ITreeItem);

                // Insert at new location
                if (targetItem?.DataContext is ChemicalNode targetFolder)
                {
                    targetFolder.Items.Add((ViewModelBase)droppedData);
                }
                else
                {
                    // Dropped on root
                    vm.TreeRoot.Add((ViewModelBase)droppedData);
                }

                // Immediately save hierarchy state
                vm.SaveTreeCommand.Execute(null);
            }
        }

        private bool RemoveItem(IList<ViewModelBase> list, ITreeItem target)
        {
            foreach (var item in list)
            {
                if (item == target)
                {
                    list.Remove(item);
                    return true;
                }
                if (item is ChemicalNode node && RemoveItem(node.Items, target))
                {
                    return true;
                }
            }
            return false;
        }

        private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T ancestor) return ancestor;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}