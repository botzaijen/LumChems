using System.Linq;
using Xunit;
using LumChems.ViewModels;
using LumChems.Models;

namespace LumChems.Tests
{
    public class MainViewModelTests
    {
        // Tests the AddFolderCommand logic in the ViewModel
        [Fact]
        public void AddFolderCommand_ShouldAppendNewFolderToTreeRoot()
        {
            // Arrange
            var viewModel = new MainViewModel();
            int initialTreeCount = viewModel.TreeRoot.Count;

            // Act
            viewModel.AddFolderCommand.Execute(null);

            // Assert
            Assert.Equal(initialTreeCount + 1, viewModel.TreeRoot.Count);

            var addedNode = viewModel.TreeRoot.Last() as ChemicalNode;
            Assert.NotNull(addedNode);
            Assert.Equal("New Folder", addedNode.Name);
        }

        // Tests the AddChemicalCommand logic
        [Fact]
        public void AddChemicalCommand_ShouldAddMixtureToTreeAndFlatList()
        {
            // Arrange
            var viewModel = new MainViewModel();
            int initialTreeCount = viewModel.TreeRoot.Count;
            int initialMixturesCount = viewModel.Chemicals.Count;

            // Act
            viewModel.AddChemicalCommand.Execute(null);

            // Assert
            Assert.Equal(initialTreeCount + 1, viewModel.TreeRoot.Count);
            Assert.Equal(initialMixturesCount + 1, viewModel.Chemicals.Count);

            Assert.NotNull(viewModel.SelectedChemical);
            Assert.Equal("New Mixture", viewModel.SelectedChemical.Name);
        }

        [Fact]
        public void AssignIngredientCommand_ShouldAddIngredientToMixture_WhenValid()
        {
            // Arrange
            var viewModel = new MainViewModel();
            viewModel.AddChemicalCommand.Execute(null); // Create a mixture
            viewModel.AddPureCommand.Execute(null);     // Create an element

            viewModel.SelectedAvailableIngredient = viewModel.PureChemicals.First();
            viewModel.InputIngredientAmount = 5.5;

            // Act
            viewModel.AssignIngredientCommand.Execute(null);

            // Assert
            Assert.Single(viewModel.SelectedChemical.Ingredients);
            Assert.Equal(5.5, viewModel.SelectedChemical.Ingredients.First().Amount);
            Assert.Equal(viewModel.SelectedAvailableIngredient.Name, viewModel.SelectedChemical.Ingredients.First().ChemicalRef.Name);
        }
    }
}