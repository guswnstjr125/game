using System;
using System.IO;
using System.Linq;
using DarkLegion.Core;
using DarkLegion.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkLegion.Editor.Tests
{
    [Category("Stage1")]
    public sealed class EnemyImporterTests
    {
        [Test]
        public void DATA_02_ImportsEnemyStatsAndOrderedActionsWithoutChangingGuid()
        {
            string output = "Assets/EnemyImportTest_" + Guid.NewGuid().ToString("N");
            string input = Path.Combine(Application.dataPath, "..", "DesignData");
            const string enemyId = "ENE_1F_GHO_001";
            string path = output + "/" + enemyId + ".asset";
            try
            {
                Assert.That(EnemyImporter.Import(input, output), Is.Empty);
                string[] assets = AssetDatabase.FindAssets("t:EnemyData", new[] { output });
                Assert.That(assets.Length, Is.EqualTo(28));
                var enemy = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
                Resources.UnloadAsset(enemy);
                enemy = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
                var catalog = new EnemyKitCatalog();
                foreach (string assetGuid in assets)
                    AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(assetGuid)).AddTo(catalog);
                Assert.That(catalog.Ids.Count(), Is.EqualTo(23));
                Assert.That(catalog.Ids.Sum(id => catalog.Get(id).Count), Is.EqualTo(65));
                Assert.That(catalog.SpawnStats(enemyId), Is.EqualTo((14, 5, 1)));
                Assert.That(enemy.DisplayName, Is.Not.Empty);
                var kit = catalog.Get(enemyId);
                Assert.That(kit.Count, Is.EqualTo(2));
                Assert.That(kit[0].ActionId, Is.EqualTo("HEAVY"));
                Assert.That(kit[0].Chance, Is.EqualTo(0.3));
                Assert.That(kit[0].Value, Is.EqualTo(1.5));
                Assert.That(kit[1].ActionId, Is.EqualTo("BASIC"));
                Assert.That(kit[1].Chance, Is.EqualTo(1));
                var abyssEnemy = AssetDatabase.LoadAssetAtPath<EnemyData>(output + "/ENE_5F_ABY_001.asset");
                Assert.That(abyssEnemy.DurationSpecAt(0), Is.EqualTo("1/1"));
                var judge = AssetDatabase.LoadAssetAtPath<EnemyData>(output + "/ENE_4F_JUD_001.asset");
                Assert.That(judge.DurationSpecAt(0), Is.EqualTo("2"));
                string guid = AssetDatabase.AssetPathToGUID(path);
                Assert.That(EnemyImporter.Import(input, output), Is.Empty);
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
            }
            finally
            {
                if (AssetDatabase.IsValidFolder(output)) AssetDatabase.DeleteAsset(output);
            }
        }

        [TestCase("Enemies_Master.json", "\"HP\": 14", "\"HP\": 0", "HP")]
        [TestCase("Enemy_Actions.json", "\"Chance\": 0.3", "\"Chance\": 1.3", "Chance")]
        [TestCase("Enemy_Actions.json", "\"ENE_ID\": \"ENE_1F_GHO_001\"", "\"ENE_ID\": \"UNKNOWN_ENEMY\"", "UNKNOWN_ENEMY")]
        [TestCase("Enemy_Actions.json", "\"Status\": \"Poison\"", "\"Status\": \"UnknownStatus\"", "UnknownStatus")]
        [TestCase("Enemy_Actions.json", "\"Value\": 1.5", "\"Value\": \"bad\"", "Value")]
        [TestCase("Enemy_Actions.json", "\"Duration\": 2", "\"Duration\": 2.5", "Duration")]
        public void DATA_02_InvalidInputLeavesExistingAssetsUnchanged(
            string file, string search, string replacement, string error)
        {
            string suffix = Guid.NewGuid().ToString("N");
            string output = "Assets/EnemyImportTest_" + suffix;
            string input = Path.Combine(Application.dataPath, "..", "DesignData");
            string scratch = Path.Combine(Path.GetTempPath(), "DarkLegionEnemyImport_" + suffix);
            Directory.CreateDirectory(scratch);
            try
            {
                Assert.That(EnemyImporter.Import(input, output), Is.Empty);
                string assetPath = output + "/ENE_1F_GHO_001.asset";
                string before = File.ReadAllText(assetPath);
                foreach (string name in new[] { "Enemies_Master.json", "Enemy_Actions.json", "Status_Master.json" })
                    File.Copy(Path.Combine(input, name), Path.Combine(scratch, name));
                string path = Path.Combine(scratch, file);
                string json = File.ReadAllText(path);
                int at = json.IndexOf(search, StringComparison.Ordinal);
                Assert.That(at, Is.GreaterThanOrEqualTo(0));
                File.WriteAllText(path, json.Remove(at, search.Length).Insert(at, replacement));
                Assert.That(EnemyImporter.Import(scratch, output), Has.Some.Contains(error));
                Assert.That(File.ReadAllText(assetPath), Is.EqualTo(before));
            }
            finally
            {
                if (AssetDatabase.IsValidFolder(output)) AssetDatabase.DeleteAsset(output);
                Directory.Delete(scratch, true);
            }
        }
    }
}
