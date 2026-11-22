#if UNITY_EDITOR
using System;
using System.IO;
using DarkLegion.Data;
using DarkLegion.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkLegion.Tests.EditMode
{
    [Category("Stage1")]
    public sealed class CardImporterTests
    {
        private static string Source => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "DesignData"));
        private string output;
        private string input;

        [SetUp]
        public void SetUp()
        {
            string id = Guid.NewGuid().ToString("N");
            output = "Assets/CardImportTest_" + id;
            input = Path.Combine(Path.GetTempPath(), "DarkLegionCardImport_" + id);
            Directory.CreateDirectory(input);
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(output)) AssetDatabase.DeleteAsset(output);
            Directory.Delete(input, true);
        }

        [Test]
        public void DATA_02_ImportsAllCardsAndKeepsAssetGuidOnReimport()
        {
            Assert.That(CardImporter.Import(Source, output), Is.Empty);
            Assert.That(AssetDatabase.FindAssets("t:CardData", new[] { output }).Length, Is.EqualTo(98));

            string path = output + "/NEC_MON_001.asset";
            var card = AssetDatabase.LoadAssetAtPath<CardData>(path);
            Assert.That(card.ToDefinition().Hp, Is.EqualTo(10));
            Assert.That(card.ToDefinition().Atk, Is.EqualTo(3));
            Assert.That(card.UnlockFloor, Is.EqualTo(1));
            Assert.That(card.NormalDrop, Is.True);
            string guid = AssetDatabase.AssetPathToGUID(path);

            CopyInput();
            ReplaceFirst("Cards_Master", "\"HP\": 10", "\"HP\": 12");
            Assert.That(CardImporter.Import(input, output), Is.Empty);
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
            Assert.That(AssetDatabase.LoadAssetAtPath<CardData>(path).ToDefinition().Hp, Is.EqualTo(12));
        }

        [Test]
        public void DATA_02_InvalidUnlockReferenceLeavesExistingAssetsUnchanged()
        {
            Assert.That(CardImporter.Import(Source, output), Is.Empty);
            string assetPath = output + "/NEC_MON_001.asset";
            string originalAsset = File.ReadAllText(assetPath);
            CopyInput();
            ReplaceFirst("Cards_Master", "\"HP\": 10", "\"HP\": 12");
            ReplaceFirst("Cards_FloorUnlock", "\"NEC_MON_001\"", "\"NOT_A_CARD\"");

            Assert.That(CardImporter.Import(input, output), Has.Some.Contains("NOT_A_CARD"));
            Assert.That(File.ReadAllText(assetPath), Is.EqualTo(originalAsset));
            Assert.That(AssetDatabase.LoadAssetAtPath<CardData>(assetPath).ToDefinition().Hp, Is.EqualTo(10));
        }

        [TestCase("Cards_Master", "\"Crystal\": 1", "\"Crystal\": -0.4", "비용")]
        [TestCase("Cards_Upgrade", "HP=12;ATK=3;DEF=1", "HP=12;ATK=3;DEF=1;Cost=-0.5", "비용")]
        [TestCase("Cards_Master", "\"source\": \"", "\"source\": {}, \"unused\": \"", "source")]
        public void DATA_02_RejectsInvalidSheetBeforeCreatingAssets(string sheet, string original, string replacement, string error)
        {
            CopyInput();
            ReplaceFirst(sheet, original, replacement);
            Assert.That(CardImporter.Import(input, output), Has.Some.Contains(error));
            Assert.That(AssetDatabase.IsValidFolder(output), Is.False);
        }

        [Test]
        public void DATA_02_RejectsMissingStageOneHandlerBeforeCreatingAssets()
        {
            CopyInput();
            foreach (string sheet in new[] { "Cards_Master", "Cards_Effect", "Cards_Upgrade", "Cards_FloorUnlock", "Cards_Text" })
                ReplaceFirst(sheet, "\"NEC_SKL_003\"", "\"NEC_SKL_TEST\"");

            Assert.That(CardImporter.Import(input, output), Has.Some.Contains("NEC_SKL_003"));
            Assert.That(AssetDatabase.IsValidFolder(output), Is.False);
        }

        private void CopyInput()
        {
            foreach (string file in Directory.GetFiles(Source, "*.json"))
                File.Copy(file, Path.Combine(input, Path.GetFileName(file)));
        }

        private void ReplaceFirst(string sheet, string original, string replacement)
        {
            string path = Path.Combine(input, sheet + ".json");
            string json = File.ReadAllText(path);
            int index = json.IndexOf(original, StringComparison.Ordinal);
            Assert.That(index, Is.GreaterThanOrEqualTo(0));
            File.WriteAllText(path, json.Remove(index, original.Length).Insert(index, replacement));
        }
    }
}
#endif
