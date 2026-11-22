using System;
using System.IO;
using DarkLegion.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkLegion.Editor.Tests
{
    [Category("Stage1")]
    public sealed class RestRuleImporterTests
    {
        [Test]
        public void DATA_02_ImportsRestRulesAndPreservesAssetsOnInvalidPercent()
        {
            string suffix = Guid.NewGuid().ToString("N");
            string source = Path.Combine(Application.dataPath, "..", "DesignData");
            string scratch = Path.Combine(Path.GetTempPath(), "DarkLegionRestImport_" + suffix);
            string output = "Assets/RestImportTest_" + suffix;
            string healPath = output + "/REST_HEAL.asset";
            const string file = "RestNode_Rules.json";
            Directory.CreateDirectory(scratch);
            try
            {
                Assert.That(RestRuleImporter.Import(source, output), Is.Empty);
                Assert.That(AssetDatabase.FindAssets("t:RestRuleData", new[] { output }).Length, Is.EqualTo(2));
                Assert.That(AssetDatabase.LoadAssetAtPath<RestRuleData>(healPath).HealPercent, Is.EqualTo(0.3));
                Assert.That(AssetDatabase.LoadAssetAtPath<RestRuleData>(output + "/REST_UPGRADE.asset").HealPercent,
                    Is.EqualTo(0));
                string guid = AssetDatabase.AssetPathToGUID(healPath);
                Assert.That(RestRuleImporter.Import(source, output), Is.Empty);
                Assert.That(AssetDatabase.AssetPathToGUID(healPath), Is.EqualTo(guid));
                string before = File.ReadAllText(healPath);

                string json = File.ReadAllText(Path.Combine(source, file));
                File.WriteAllText(Path.Combine(scratch, file), json.Replace("\"HealPercent\": 0.3", "\"HealPercent\": 1.3"));
                Assert.That(RestRuleImporter.Import(scratch, output), Has.Some.Contains("HealPercent"));
                Assert.That(File.ReadAllText(healPath), Is.EqualTo(before));
            }
            finally
            {
                if (AssetDatabase.IsValidFolder(output)) AssetDatabase.DeleteAsset(output);
                Directory.Delete(scratch, true);
            }
        }
    }
}
