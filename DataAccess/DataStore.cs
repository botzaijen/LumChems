using System.Collections.Generic;
using ChemicalInventoryApp.Helpers;
using ChemicalInventoryApp.Models;
using Microsoft.Data.Sqlite;

namespace ChemicalInventoryApp.DataAccess
{
    public class DataStore
    {
        public bool PureChemicalExists(string name)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var cmd = new SqliteCommand("SELECT COUNT(1) FROM PureChemicals WHERE Name = $name", connection);
            cmd.Parameters.Add(new SqliteParameter("$name", name));
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        public bool ChemicalExistsById(string id)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var cmd = new SqliteCommand("SELECT COUNT(1) FROM Chemicals WHERE Id = $id", connection);
            cmd.Parameters.Add(new SqliteParameter("$id", id));
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        public bool ChemicalExistsByName(string name)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var cmd = new SqliteCommand("SELECT COUNT(1) FROM Chemicals WHERE Name = $name", connection);
            cmd.Parameters.Add(new SqliteParameter("$name", name));
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        private const string ConnectionString = "Data Source=data.db";

        public DataStore()
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS ChemicalNodes (Id TEXT PRIMARY KEY, Name TEXT, ParentId TEXT, OrderIndex INTEGER);
                CREATE TABLE IF NOT EXISTS Chemicals (Id TEXT PRIMARY KEY, Name TEXT, RefNo INTEGER, Vendor TEXT, VendorId TEXT, Tag INTEGER, ParentNodeId TEXT, OrderIndex INTEGER);
                CREATE TABLE IF NOT EXISTS PureChemicals (Name TEXT PRIMARY KEY, Mw REAL);
                CREATE TABLE IF NOT EXISTS ChemicalIngredients (ParentId TEXT, IngredientRef TEXT, IsChemical INTEGER, Amount REAL, PRIMARY KEY(ParentId, IngredientRef, IsChemical));
                CREATE TABLE IF NOT EXISTS ChemicalProperties (ChemicalId TEXT, PropertyType INTEGER, PropertyValue REAL, PRIMARY KEY(ChemicalId, PropertyType));
                CREATE TABLE IF NOT EXISTS PureChemicalTypes (PureChemicalName TEXT, CategoryType INTEGER, CategoryValue INTEGER, PRIMARY KEY(PureChemicalName, CategoryType));
            ";
            command.ExecuteNonQuery();
        }

        public (List<ViewModelBase> treeRoot, List<Chemical> chemicals, List<PureChemical> pureChemicals) LoadAll()
        {
            var pureChemicals = new List<PureChemical>();
            var chemicals = new List<Chemical>();
            var chemicalNodes = new List<ChemicalNode>();
            var rootItems = new List<ViewModelBase>();

            var pureDict = new Dictionary<string, PureChemical>();
            var chemDict = new Dictionary<string, Chemical>();
            var nodeDict = new Dictionary<string, ChemicalNode>();

            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            // 1. Load Pure Elements
            using (var cmd = new SqliteCommand("SELECT Name, Mw FROM PureChemicals", connection))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var pc = new PureChemical
                    {
                        Name = reader.GetString(0),
                        OriginalName = reader.GetString(0),
                        Mw = reader.IsDBNull(1) ? null : reader.GetDouble(1)
                    };
                    pureChemicals.Add(pc);
                    pureDict[pc.Name] = pc;
                }
            }

            // 2. Load Chemical Nodes (Folders)
            using (var cmd = new SqliteCommand("SELECT Id, Name, ParentId, OrderIndex FROM ChemicalNodes", connection))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var node = new ChemicalNode
                    {
                        Id = reader.GetString(0),
                        Name = reader.GetString(1),
                        ParentNodeId = reader.IsDBNull(2) ? null : reader.GetString(2),
                        // Safely checks ordinal 3 for NULL
                        OrderIndex = reader.IsDBNull(3) ? 0 : reader.GetInt32(3)
                    };
                    chemicalNodes.Add(node);
                    nodeDict[node.Id] = node;
                }
            }

            // 3. Load Chemical Mixtures
            using (var cmd = new SqliteCommand("SELECT Id, Name, RefNo, Vendor, VendorId, Tag, ParentNodeId, OrderIndex FROM Chemicals", connection))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var c = new Chemical
                    {
                        Id = reader.GetString(0),
                        Name = reader.GetString(1),
                        OriginalName = reader.GetString(1),
                        RefNo = reader.IsDBNull(2) ? 0 : (uint)reader.GetInt64(2),
                        Vendor = reader.IsDBNull(3) ? null : reader.GetString(3),
                        VendorId = reader.IsDBNull(4) ? null : reader.GetString(4),
                        Tag = reader.IsDBNull(5) ? ChemicalTag.Unspecified : (ChemicalTag)reader.GetInt32(5),
                        ParentNodeId = reader.IsDBNull(6) ? null : reader.GetString(6),
                        OrderIndex = reader.IsDBNull(7) ? 0 : reader.GetInt32(7)
                    };
                    chemicals.Add(c);
                    chemDict[c.Id] = c;

                    pureDict[c.Name] = c;
                    pureDict[c.Id] = c;
                }
            }

            // 4. Load Ingredients and Amounts
            using (var cmd = new SqliteCommand("SELECT ParentId, IngredientRef, Amount FROM ChemicalIngredients", connection))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var parentId = reader.GetString(0);
                    var ingredientRef = reader.GetString(1);
                    var amount = reader.GetDouble(2);

                    if (chemDict.TryGetValue(parentId, out var parent) && pureDict.TryGetValue(ingredientRef, out var ingredient))
                    {
                        parent.Ingredients.Add(new IngredientItem { ChemicalRef = ingredient, Amount = amount });
                    }
                }
            }

            // 5. Load Pure Chemical Types
            using (var cmd = new SqliteCommand("SELECT PureChemicalName, CategoryType, CategoryValue FROM PureChemicalTypes", connection))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var pcName = reader.GetString(0);
                    var category = (CategoryType)reader.GetInt32(1);
                    int? categoryValue = reader.IsDBNull(2) ? null : reader.GetInt32(2);

                    if (pureDict.TryGetValue(pcName, out var pureChem))
                    {
                        pureChem.Types.Add(new PureChemicalTypeItem { Category = category, Value = categoryValue });
                    }
                }
            }

            // 6. Build and Sort Tree Hierarchy
            var allTreeItems = new List<ViewModelBase>();
            allTreeItems.AddRange(chemicalNodes);
            allTreeItems.AddRange(chemicals);

            foreach (var item in allTreeItems)
            {
                var treeItem = (ITreeItem)item;

                // If it has a parent, attach it. Otherwise, put it in the root list.
                if (!string.IsNullOrEmpty(treeItem.ParentNodeId) && nodeDict.TryGetValue(treeItem.ParentNodeId, out var parentFolder))
                {
                    parentFolder.Items.Add(item);
                }
                else
                {
                    rootItems.Add(item);
                }
            }

            // Sort the Root Items
            rootItems = rootItems.OrderBy(x => ((ITreeItem)x).OrderIndex).ToList();

            // Sort items inside each folder
            foreach (var node in chemicalNodes)
            {
                var sortedChildren = node.Items.OrderBy(x => ((ITreeItem)x).OrderIndex).ToList();
                node.Items.Clear();
                foreach (var child in sortedChildren)
                {
                    node.Items.Add(child);
                }
            }

            return (rootItems, chemicals, pureChemicals);
        }

        internal void ExecuteNonQuery(string query, params SqliteParameter[] parameters)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var cmd = new SqliteCommand(query, connection);
            cmd.Parameters.AddRange(parameters);
            cmd.ExecuteNonQuery();
        }

        public void InsertChemical(Chemical c) => ExecuteNonQuery(
            "INSERT INTO Chemicals (Id, Name, RefNo, Vendor, VendorId, Tag, ParentNodeId, OrderIndex) VALUES ($id, $name, $ref, $v, $vid, $tag, $pId, $idx)",
            new SqliteParameter("$id", c.Id), new SqliteParameter("$name", c.Name), new SqliteParameter("$ref", c.RefNo),
            new SqliteParameter("$v", c.Vendor ?? (object)System.DBNull.Value), new SqliteParameter("$vid", c.VendorId ?? (object)System.DBNull.Value),
            new SqliteParameter("$tag", (int)c.Tag), new SqliteParameter("$pId", c.ParentNodeId ?? (object)System.DBNull.Value), new SqliteParameter("$idx", c.OrderIndex));

        public void UpdateChemical(Chemical c) => ExecuteNonQuery(
            "UPDATE Chemicals SET Name = $name, RefNo = $ref, Vendor = $v, VendorId = $vid, Tag = $tag, ParentNodeId = $pId, OrderIndex = $idx WHERE Id = $id",
            new SqliteParameter("$name", c.Name), new SqliteParameter("$id", c.Id), new SqliteParameter("$ref", c.RefNo),
            new SqliteParameter("$v", c.Vendor ?? (object)System.DBNull.Value), new SqliteParameter("$vid", c.VendorId ?? (object)System.DBNull.Value),
            new SqliteParameter("$tag", (int)c.Tag), new SqliteParameter("$pId", c.ParentNodeId ?? (object)System.DBNull.Value), new SqliteParameter("$idx", c.OrderIndex));

        public void UpdateChemicalNode(ChemicalNode node) => ExecuteNonQuery(
            "UPDATE ChemicalNodes SET Name = $name, ParentId = $pId, OrderIndex = $idx WHERE Id = $id",
            new SqliteParameter("$name", node.Name), new SqliteParameter("$pId", node.ParentNodeId ?? (object)System.DBNull.Value),
            new SqliteParameter("$idx", node.OrderIndex), new SqliteParameter("$id", node.Id));

        public void DeleteChemicalNode(string folderId)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            // 1. Recursively delete sub-folders
            var childFolders = new List<string>();
            using (var cmd = new SqliteCommand("SELECT Id FROM ChemicalNodes WHERE ParentId = $id", connection))
            {
                cmd.Parameters.Add(new SqliteParameter("$id", folderId));
                using var reader = cmd.ExecuteReader();
                while (reader.Read()) childFolders.Add(reader.GetString(0));
            }
            foreach (var childFolder in childFolders) DeleteChemicalNode(childFolder);

            // 2. Delete all chemicals inside this folder
            var childChemicals = new List<string>();
            using (var cmd = new SqliteCommand("SELECT Id FROM Chemicals WHERE ParentNodeId = $id", connection))
            {
                cmd.Parameters.Add(new SqliteParameter("$id", folderId));
                using var reader = cmd.ExecuteReader();
                while (reader.Read()) childChemicals.Add(reader.GetString(0));
            }
            foreach (var chemId in childChemicals) DeleteChemical(chemId);

            // 3. Delete the folder itself
            ExecuteNonQuery("DELETE FROM ChemicalNodes WHERE Id = $id", new SqliteParameter("$id", folderId));
        }
        public void DeleteChemical(string id)
        {
            ExecuteNonQuery("DELETE FROM ChemicalIngredients WHERE ParentId = $id OR (IngredientRef = $id AND IsChemical = 1)", new SqliteParameter("$id", id));
            ExecuteNonQuery("DELETE FROM ChemicalProperties WHERE ChemicalId = $id", new SqliteParameter("$id", id));
            ExecuteNonQuery("DELETE FROM Chemicals WHERE Id = $id", new SqliteParameter("$id", id));
        }

        public void InsertPureChemical(PureChemical pc) => ExecuteNonQuery("INSERT INTO PureChemicals (Name, Mw) VALUES ($name, $mw)", new SqliteParameter("$name", pc.Name), new SqliteParameter("$mw", pc.Mw ?? (object)System.DBNull.Value));
        public void UpdatePureChemical(PureChemical pc)
        {
            string oldName = pc.OriginalName ?? pc.Name;
            ExecuteNonQuery("UPDATE PureChemicals SET Name = $newName, Mw = $mw WHERE Name = $oldName", new SqliteParameter("$newName", pc.Name), new SqliteParameter("$oldName", oldName), new SqliteParameter("$mw", pc.Mw ?? (object)System.DBNull.Value));
            pc.OriginalName = pc.Name; 
        }
        public void DeletePureChemical(string name)
        {
            ExecuteNonQuery("DELETE FROM ChemicalIngredients WHERE IngredientRef = $name AND IsChemical = 0", new SqliteParameter("$name", name));
            ExecuteNonQuery("DELETE FROM PureChemicalTypes WHERE PureChemicalName = $name", new SqliteParameter("$name", name));
            ExecuteNonQuery("DELETE FROM PureChemicals WHERE Name = $name", new SqliteParameter("$name", name));
        }
        public void SyncIngredients(string parentId, IEnumerable<IngredientItem> ingredients)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                // Clear existing ingredients for this mixture
                using (var delCmd = new SqliteCommand("DELETE FROM ChemicalIngredients WHERE ParentId = $pId", connection, transaction))
                {
                    delCmd.Parameters.Add(new SqliteParameter("$pId", parentId));
                    delCmd.ExecuteNonQuery();
                }

                // Insert the updated list
                foreach (var item in ingredients)
                {
                    if (item.ChemicalRef == null) continue;

                    bool isChem = item.ChemicalRef is Chemical;
                    string refId = isChem ? ((Chemical)item.ChemicalRef).Id : item.ChemicalRef.Name;

                    using (var insCmd = new SqliteCommand("INSERT INTO ChemicalIngredients (ParentId, IngredientRef, IsChemical, Amount) VALUES ($pId, $ref, $isChem, $amount)", connection, transaction))
                    {
                        insCmd.Parameters.Add(new SqliteParameter("$pId", parentId));
                        insCmd.Parameters.Add(new SqliteParameter("$ref", refId));
                        insCmd.Parameters.Add(new SqliteParameter("$isChem", isChem ? 1 : 0));
                        insCmd.Parameters.Add(new SqliteParameter("$amount", item.Amount));
                        insCmd.ExecuteNonQuery();
                    }
                }
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        public void SyncTypes(string pureChemicalName, IEnumerable<PureChemicalTypeItem> types)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                // Clear existing types for this pure chemical
                using (var delCmd = new SqliteCommand("DELETE FROM PureChemicalTypes WHERE PureChemicalName = $name", connection, transaction))
                {
                    delCmd.Parameters.Add(new SqliteParameter("$name", pureChemicalName));
                    delCmd.ExecuteNonQuery();
                }

                // Insert the current list of types
                foreach (var item in types)
                {
                    using (var insCmd = new SqliteCommand("INSERT INTO PureChemicalTypes (PureChemicalName, CategoryType, CategoryValue) VALUES ($name, $cat, $val)", connection, transaction))
                    {
                        insCmd.Parameters.Add(new SqliteParameter("$name", pureChemicalName));
                        insCmd.Parameters.Add(new SqliteParameter("$cat", (int)item.Category));
                        insCmd.Parameters.Add(new SqliteParameter("$val", item.Value ?? (object)System.DBNull.Value));
                        insCmd.ExecuteNonQuery();
                    }
                }
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        public void SyncTreeOrder(IEnumerable<ViewModelBase> rootNodes)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            int globalOrder = 0;

            void Traverse(IEnumerable<ViewModelBase> items, string? parentId)
            {
                foreach (var item in items)
                {
                    if (item is ChemicalNode node)
                    {
                        node.ParentNodeId = parentId;
                        node.OrderIndex = globalOrder++;
                        using var cmd = new SqliteCommand("UPDATE ChemicalNodes SET ParentId = $pId, OrderIndex = $idx WHERE Id = $id", connection, transaction);
                        cmd.Parameters.Add(new SqliteParameter("$pId", parentId ?? (object)DBNull.Value));
                        cmd.Parameters.Add(new SqliteParameter("$idx", node.OrderIndex));
                        cmd.Parameters.Add(new SqliteParameter("$id", node.Id));
                        cmd.ExecuteNonQuery();
                        Traverse(node.Items, node.Id);
                    }
                    else if (item is Chemical chem)
                    {
                        chem.ParentNodeId = parentId;
                        chem.OrderIndex = globalOrder++;
                        using var cmd = new SqliteCommand("UPDATE Chemicals SET ParentNodeId = $pId, OrderIndex = $idx WHERE Id = $id", connection, transaction);
                        cmd.Parameters.Add(new SqliteParameter("$pId", parentId ?? (object)DBNull.Value));
                        cmd.Parameters.Add(new SqliteParameter("$idx", chem.OrderIndex));
                        cmd.Parameters.Add(new SqliteParameter("$id", chem.Id));
                        cmd.ExecuteNonQuery();
                    }
                }
            }

            try { Traverse(rootNodes, null); transaction.Commit(); }
            catch { transaction.Rollback(); throw; }
        }
    }
}