using System;
using System.Linq;
using Microsoft.Data.Sqlite;
using Xunit;
using LumChems.DataAccess;
using LumChems.Models;

namespace LumChems.Tests
{
    public class DataStoreTests
    {
        // Generates a unique in-memory DB connection string for each test to ensure isolation
        private string GetInMemoryConnectionString() => $"Data Source={Guid.NewGuid()};Mode=Memory;Cache=Shared";

        [Fact]
        public void InsertPureChemical_ShouldSaveAndRetrieveCorrectly()
        {
            // Arrange
            var dbString = GetInMemoryConnectionString();
            using var keepAlive = new SqliteConnection(dbString);
            keepAlive.Open(); // Keeps the in-memory DB alive for the duration of the test

            var dataStore = new DataStore(dbString);
            var element = new PureChemical { Name = "Oxygen", Mw = 15.999 };

            // Act
            dataStore.InsertPureChemical(element);
            var (tree, mixtures, elements) = dataStore.LoadAll();

            // Assert
            var savedElement = elements.FirstOrDefault(e => e.Name == "Oxygen");
            Assert.NotNull(savedElement);
            Assert.Equal(15.999, savedElement.Mw);
        }

        [Fact]
        public void DeleteChemicalNode_ShouldCascadeDeleteContents()
        {
            // Arrange
            var dbString = GetInMemoryConnectionString();
            using var keepAlive = new SqliteConnection(dbString);
            keepAlive.Open();
            var dataStore = new DataStore(dbString);

            var folder = new ChemicalNode { Name = "Acids", Id = Guid.NewGuid().ToString() };
            var childMixture = new Chemical { Name = "Hydrochloric Acid", ParentNodeId = folder.Id };

            dataStore.ExecuteNonQuery("INSERT INTO ChemicalNodes (Id, Name) VALUES ($id, $name)",
                new SqliteParameter("$id", folder.Id), new SqliteParameter("$name", folder.Name));
            dataStore.InsertChemical(childMixture);

            // Act
            dataStore.DeleteChemicalNode(folder.Id);
            var (tree, mixtures, elements) = dataStore.LoadAll();

            // Assert
            Assert.Empty(tree);
            Assert.Empty(mixtures); // The child mixture should be deleted automatically
        }
    }
}