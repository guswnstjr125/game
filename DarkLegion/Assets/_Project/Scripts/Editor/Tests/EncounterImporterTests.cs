using System;
using System.IO;
using DarkLegion.Core;
using DarkLegion.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkLegion.Editor.Tests
{
    [Category("Stage1")]
    public sealed class EncounterImporterTests
    {
        [TestCase("Encounters.json", "\"Wave\": 1", "\"Wave\": 1.4", "Wave")]
        [TestCase("Encounters.json", "\"Count\": 2", "\"Count\": 2.4", "Count")]
        [TestCase("Encounters.json", "\"NodeType\": \"Battle\"", "\"NodeType\": \"Batel\"", "NodeType")]
        [TestCase("Enemies_Master.json", "\"HP\": ", "\"HP\": 0, \"IgnoredHP\": ", "HP")]
        public void DATA_02_RejectsInvalidNumericOrNodeInputBeforeCreatingAssets(
            string file, string search, string replacement, string error)
        {
            string suffix = Guid.NewGuid().ToString("N");
            string source = Path.Combine(Application.dataPath, "..", "DesignData");
            string output = "Assets/EncounterImportTest_" + suffix;
            string scratch = Path.Combine(Path.GetTempPath(), "DarkLegionEncounterImport_" + suffix);
            Directory.CreateDirectory(scratch);
            try
            {
                File.Copy(Path.Combine(source, "Encounters.json"), Path.Combine(scratch, "Encounters.json"));
                File.Copy(Path.Combine(source, "Enemies_Master.json"), Path.Combine(scratch, "Enemies_Master.json"));
                string path = Path.Combine(scratch, file);
                string json = File.ReadAllText(path);
                int index = json.IndexOf(search, StringComparison.Ordinal);
                Assert.That(index, Is.GreaterThanOrEqualTo(0));
                File.WriteAllText(path, json.Remove(index, search.Length).Insert(index, replacement));

                Assert.That(EncounterImporter.Import(scratch, output), Has.Some.Contains(error));
                Assert.That(AssetDatabase.IsValidFolder(output), Is.False);
            }
            finally
            {
                if (AssetDatabase.IsValidFolder(output)) AssetDatabase.DeleteAsset(output);
                Directory.Delete(scratch, true);
            }
        }

        [Test]
        public void DATA_02_ImportsAllWaveGroupsAndKeepsExistingAssetsOnInvalidReference()
        {
            string suffix = Guid.NewGuid().ToString("N");
            string output = "Assets/EncounterImportTest_" + suffix;
            string scratch = Path.Combine(Path.GetTempPath(), "DarkLegionEncounterImport_" + suffix);
            string source = Path.Combine(Application.dataPath, "..", "DesignData");
            const string file = "Encounters.json";
            string path = output + "/F1_N2_A_W1.asset";
            Directory.CreateDirectory(scratch);
            try
            {
                Assert.That(EncounterImporter.Import(source, output), Is.Empty);
                Assert.That(AssetDatabase.FindAssets("t:EncounterData", new[] { output }).Length, Is.EqualTo(43));
                var asset = AssetDatabase.LoadAssetAtPath<EncounterData>(path);
                Assert.That(asset.Floor, Is.EqualTo(1));
                Assert.That(asset.Node, Is.EqualTo(2));
                Assert.That(asset.Wave, Is.EqualTo(1));
                Assert.That(asset.Enemies[0].Id, Is.EqualTo("ENE_1F_GHO_002"));
                Assert.That(asset.Enemies[1].Id, Is.EqualTo("ENE_1F_GHO_003"));
                Assert.That(asset.Enemies[1].Count, Is.EqualTo(1));
                var restored = new EncounterCatalog();
                asset.AddTo(restored);
                CollectionAssert.AreEqual(new[] { ("ENE_1F_GHO_002", 1), ("ENE_1F_GHO_003", 1) },
                    restored.All[0].Waves[1]);
                string guid = AssetDatabase.AssetPathToGUID(path);
                string original = File.ReadAllText(path);
                Assert.That(EncounterImporter.Import(source, output), Is.Empty);
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));

                File.Copy(Path.Combine(source, "Enemies_Master.json"), Path.Combine(scratch, "Enemies_Master.json"));
                string input = File.ReadAllText(Path.Combine(source, file));
                File.WriteAllText(Path.Combine(scratch, file), input.Replace("ENE_1F_GHO_003", "UNKNOWN_ENEMY"));
                Assert.That(EncounterImporter.Import(scratch, output), Has.Some.Contains("UNKNOWN_ENEMY"));
                Assert.That(File.ReadAllText(path), Is.EqualTo(original));
            }
            finally
            {
                if (AssetDatabase.IsValidFolder(output)) AssetDatabase.DeleteAsset(output);
                Directory.Delete(scratch, true);
            }
        }
    }
}
