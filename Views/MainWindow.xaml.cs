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

            // Extract the dragged item (fallback to PureChemical in case you drag from the other tab later)
            var droppedData = e.Data.GetData(typeof(Chemical)) as PureChemical
                           ?? e.Data.GetData(typeof(PureChemical)) as PureChemical;

            if (droppedData != null)
            {
                // 1. Guard against a mixture containing itself
                if (droppedData == vm.SelectedChemical)
                {
                    MessageBox.Show("A mixture cannot contain itself as an ingredient.", "Invalid Operation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 2. Guard against duplicate ingredients
                if (vm.SelectedChemical.Ingredients.Any(i => i.ChemicalRef == droppedData))
                {
                    // Optional: you could increment the amount here instead of rejecting it
                    MessageBox.Show("This ingredient is already in the mixture.", "Duplicate Ingredient", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 3. Add to the collection
                vm.SelectedChemical.Ingredients.Add(new IngredientItem
                {
                    ChemicalRef = droppedData,
                    Amount = vm.InputIngredientAmount // Uses whatever is currently in the Amount text box
                });

                // Let the drag-and-drop system know we handled this as a Copy, not a Move, 
                // so it doesn't get deleted from the TreeRoot.
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
            // Find the specific item that was double-clicked
            var treeViewItem = FindAncestor<TreeViewItem>((DependencyObject)e.OriginalSource);

            // If the user double-clicked empty space, do nothing
            if (treeViewItem == null) return;

            if (DataContext is MainViewModel vm)
            {
                if (treeViewItem.DataContext is Chemical chem)
                {
                    vm.SelectedChemical = chem;
                    vm.SelectedFolder = null;
                }
                else if (treeViewItem.DataContext is ChemicalNode node)
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