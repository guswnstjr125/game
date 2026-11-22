using System.Collections.Generic;
using System.Linq;
using DarkLegion.Core;
using NUnit.Framework;

namespace DarkLegion.Tests.EditMode
{
    /// <summary>
    /// 시트 행 → Core 정의 (#35 Unity 비의존 부분, `38` §3·§4.1·§5, `01` D-71). 행은 손으로 적는다 (`32` §11.2).
    /// 실제 export JSON과 픽스처의 대조는 <see cref="DesignDataParityTests"/>.
    /// </summary>
    [Category("Stage1")]
    public class GameDataLoaderTests
    {
        private static DataRow R(params (string col, object val)[] kv)
        {
            var d = new Dictionary<string, object>();
            foreach (var (col, val) in kv) d[col] = val is int i ? (double)i : val;
            return new DataRow(d);
        }

        private static DataSheet S(string name, params DataRow[] rows) => new DataSheet(name, name, rows.ToList());

        private static DataRow MasterRow(string id, string type, string cls, object level, string rarity, int cost, object hp = null, object atk = null, object def = null)
            => R(("ID", id), ("타입", type), ("클래스", cls), ("레벨", level), ("희귀도", rarity), ("Crystal", cost), ("HP", hp), ("ATK", atk), ("DEF", def));

        private static DataRow Upgrade(string id, string up) => R(("Card_ID", id), ("Up_Params", up));

        private static DataRow Effect(string id, int v1, int v2, int v3, bool exhaust) =>
            R(("Card_ID", id), ("Value1", v1), ("Value2", v2), ("Value3", v3), ("Exhaust", exhaust));

        // ---------- 카드 ----------

        [Test]
        public void Cards_MonsterStatsUpgradeSoulboundAndMonsterEffects()
        {
            var data = new GameData(
                S("Cards_Master", MasterRow("NEC_MON_005", "Monster", "NEC", 1, "U", 1, 5, 2, 4), MasterRow("NEU_MON_007", "Monster", "NEU", 2, "U", 2, 25, 4, 4)),
                S("Cards_Upgrade", Upgrade("NEC_MON_005", "HP=7;ATK=2;DEF=4"), Upgrade("NEU_MON_007", "HP=27;ATK=4;DEF=4")),
                S("Monsters_Master",
                    R(("MON_ID", "MON_NEC_L1_005"), ("Card_ID", "NEC_MON_005"), ("Special", "[키워드] Soulbound")),
                    R(("MON_ID", "MON_NEU_L2_001"), ("Card_ID", "NEU_MON_007"), ("Special", null))),
                S("Monster_Effects",
                    R(("Card_ID", "NEC_MON_005"), ("Effect", "ModifyAtk"), ("Value", 1)),
                    R(("Card_ID", "NEU_MON_007"), ("Effect", "DamageReduction"), ("Value", 1)),
                    R(("Card_ID", "NEU_MON_007"), ("Effect", "SacrificeCrystal"), ("Value", 3))));

            var r = GameDataLoader.Load(data);
            Assert.IsEmpty(r.Errors);
            var wraith = r.Cards.Get("NEC_MON_005");
            Assert.AreEqual(CardType.Monster, wraith.Type);
            Assert.AreEqual((1, 1, 5, 2, 4), (wraith.Cost, wraith.Level, wraith.Hp, wraith.Atk, wraith.Def));
            Assert.AreEqual((7, 2, 4), (wraith.UpHp.Value, wraith.UpAtk.Value, wraith.UpDef.Value));
            Assert.IsTrue(wraith.Soulbound, "Monsters_Master Special");
            Assert.AreEqual(1, wraith.Params["Value"]);
            Assert.IsNull(wraith.SacrificeCrystal);

            var golem = r.Cards.Get("NEU_MON_007");
            Assert.IsFalse(golem.Soulbound);
            Assert.AreEqual(3, golem.SacrificeCrystal, "SacrificeCrystal 행");
            Assert.AreEqual(1, golem.Params["Value"], "그 밖의 첫 행 Value");
            Assert.AreEqual(1, golem.Params.Count);
        }

        [Test]
        public void Cards_SkillActionValueNames_D71()
        {
            var data = new GameData(
                S("Cards_Master",
                    MasterRow("NEC_SKL_002", "Skill", "NEC", null, "U", 1),
                    MasterRow("NEC_ACT_002", "Action", "NEC", null, "U", 2),
                    MasterRow("NEC_ACT_003", "Action", "NEC", null, "U", 2),
                    MasterRow("NEC_ACT_004", "Action", "NEC", null, "R", 3),
                    MasterRow("NEU_ACT_001", "Action", "NEU", null, "C", 0)),
                S("Cards_Effect",
                    Effect("NEC_SKL_002", 3, 2, 0, false),
                    Effect("NEC_ACT_002", 3, 4, 2, true),
                    Effect("NEC_ACT_003", 0, 0, 0, true),
                    Effect("NEC_ACT_004", 15, 50, 0, true),
                    Effect("NEU_ACT_001", 2, 1, 0, false)),
                S("Cards_Upgrade",
                    Upgrade("NEC_SKL_002", "Cost=0"),
                    Upgrade("NEC_ACT_002", "TokenHp=6"),
                    Upgrade("NEC_ACT_003", "DamageMult=1.2"),
                    Upgrade("NEC_ACT_004", "Damage=19"),
                    Upgrade("NEU_ACT_001", "Draw=3")),
                S("Cards_Text", R(("Card_ID", "NEU_ACT_001"), ("Keyword", "Ethereal")), R(("Card_ID", "NEC_ACT_002"), ("Keyword", "Exhaust"))));

            var r = GameDataLoader.Load(data);
            Assert.IsEmpty(r.Errors);

            var barrier = r.Cards.Get("NEC_SKL_002");
            Assert.AreEqual(CardType.Skill, barrier.Type);
            CollectionAssert.AreEquivalent(new Dictionary<string, double> { ["Shield"] = 3, ["FrontBonus"] = 2 }, barrier.Params);
            Assert.AreEqual(0, barrier.UpCost);
            Assert.IsFalse(barrier.Exhaust);

            var army = r.Cards.Get("NEC_ACT_002");
            CollectionAssert.AreEquivalent(new Dictionary<string, double> { ["Tokens"] = 3, ["TokenHp"] = 4, ["TokenAtk"] = 2 }, army.Params);
            CollectionAssert.AreEquivalent(new Dictionary<string, double> { ["TokenHp"] = 6 }, army.UpParams);
            Assert.IsTrue(army.Exhaust);
            Assert.IsNull(army.UpCost);

            var whirl = r.Cards.Get("NEC_ACT_003");
            Assert.IsEmpty(whirl.Params, "Value 칸을 읽지 않는다 (기본 배율 1은 코드)");
            Assert.AreEqual(1.2, whirl.Param("DamageMult", true, 1.0));
            Assert.AreEqual(1.0, whirl.Param("DamageMult", false, 1.0));

            var drain = r.Cards.Get("NEC_ACT_004");
            Assert.AreEqual(15, drain.Params["Damage"]);
            Assert.AreEqual(0.5, drain.Params["HealRatio"], "50 ÷ 100");
            Assert.AreEqual(19, drain.UpParams["Damage"]);

            var opp = r.Cards.Get("NEU_ACT_001");
            Assert.IsTrue(opp.Ethereal, "Cards_Text Keyword");
            Assert.AreEqual(1, opp.Params["Discard"]);
            Assert.IsFalse(r.Cards.Get("NEC_ACT_002").Ethereal);
        }

        [Test]
        public void Cards_ErrorsForDuplicateIdBadReferenceBadUpParamsAndType()
        {
            var data = new GameData(
                S("Cards_Master", MasterRow("A_MON_001", "Monster", "A", 1, "C", 1, 5, 1, 0), MasterRow("A_MON_001", "Monster", "A", 1, "C", 1, 5, 1, 0),
                    MasterRow("A_XXX_001", "Weird", "A", null, "C", 1)),
                S("Cards_Upgrade", Upgrade("A_MON_001", "HP=7;ATK"), Upgrade("NOPE_001", "Cost=0")));

            var r = GameDataLoader.Load(data);
            Assert.IsTrue(r.Errors.Any(e => e.Contains("duplicate") && e.Contains("A_MON_001")), string.Join("\n", r.Errors));
            Assert.IsTrue(r.Errors.Any(e => e.Contains("Up_Params") && e.Contains("A_MON_001")));
            Assert.IsTrue(r.Errors.Any(e => e.Contains("NOPE_001")), "없는 카드를 가리키는 행");
            Assert.IsTrue(r.Errors.Any(e => e.Contains("A_XXX_001") && e.Contains("Weird")));
            Assert.IsFalse(r.Ok);
        }

        // ---------- 융합 ----------

        private static GameData FusionData() => new GameData(
            S("Cards_Master",
                MasterRow("NEC_MON_001", "Monster", "NEC", 1, "C", 1, 10, 3, 1),
                MasterRow("NEC_MON_006", "Monster", "NEC", 1, "C", 1, 5, 3, 3),
                MasterRow("NEC_MON_014", "Monster", "NEC", 2, "U", 2, 14, 6, 3),
                MasterRow("NEC_MON_015", "Monster", "NEC", 2, "C", 2, 10, 4, 1),
                MasterRow("NEU_MON_002", "Monster", "NEU", 1, "U", 1, 10, 2, 1)),
            S("Monsters_Master",
                R(("MON_ID", "MON_NEC_L1_001"), ("Card_ID", "NEC_MON_001")),
                R(("MON_ID", "MON_NEC_L1_006"), ("Card_ID", "NEC_MON_006")),
                R(("MON_ID", "MON_NEC_L2_006"), ("Card_ID", "NEC_MON_014")),
                R(("MON_ID", "MON_NEC_L1_009"), ("Card_ID", null)),
                R(("MON_ID", "MON_NEU_L1_002"), ("Card_ID", "NEU_MON_002"))),
            S("Fusion_Recipes",
                R(("Recipe_ID", "FUS_014"), ("MaterialA", "MON_NEC_L1_001"), ("MaterialB", "MON_NEC_L1_006"), ("Result_MON_ID", "MON_NEC_L2_006"), ("Type", "Fixed"), ("Weight", 100)),
                R(("Recipe_ID", "FUS_002"), ("MaterialA", "MON_NEC_L1_001"), ("MaterialB", "MON_NEC_L1_009"), ("Result_MON_ID", "MON_NEC_L2_006"), ("Type", "Fixed"), ("Weight", 0)),
                R(("Recipe_ID", "FUS_007"), ("MaterialA", "MON_NEU_L1_002"), ("MaterialB", "MON_NEC_L1_001"), ("Result_MON_ID", "MON_NEC_L2_006"), ("Type", "Random"), ("Weight", 50))),
            S("Fusion_RandomPool",
                R(("Pool_ID", "P1"), ("InputClass", "NEC"), ("InputLevel", 1), ("ResultMON_ID", "MON_NEC_L2_006"), ("Weight", 40)),
                R(("Pool_ID", "P2"), ("InputClass", "NEC"), ("InputLevel", 1), ("ResultMON_ID", "MON_NEC_L1_009"), ("Weight", 0)),
                R(("Pool_ID", "P3"), ("InputClass", "NEC+NEU"), ("InputLevel", 1), ("ResultMON_ID", "MON_NEC_L2_006"), ("Weight", 50))),
            S("Monster_Effects", R(("Card_ID", "NEU_MON_002"), ("Effect", "FusionAsClass"), ("Value", null))));

        [Test]
        public void Fusion_FixedOnlyPositiveWeight_RandomRowsAreNotRecipes()
        {
            var r = GameDataLoader.Load(FusionData());
            Assert.IsEmpty(r.Errors, string.Join("\n", r.Errors));
            Assert.IsTrue(r.Fusion.TryGetFixed("NEC_MON_006", "NEC_MON_001", out var id, out var result));
            Assert.AreEqual(("FUS_014", "NEC_MON_014"), (id, result));
            Assert.IsFalse(r.Fusion.TryGetFixed("NEU_MON_002", "NEC_MON_001", out _, out _), "Type=Random 행은 고정 레시피가 아니다 (D-71)");
            Assert.AreEqual(1, r.Fusion.FixedRecipes.Count(), "Weight 0(FUS_002)은 넣지 않는다");
        }

        [Test]
        public void Fusion_PoolsMapMonIdToCardId_SkipWeightZero_MimicAndCommons()
        {
            var r = GameDataLoader.Load(FusionData());
            CollectionAssert.AreEqual(new[] { ("NEC_MON_014", 40) }, r.Fusion.Pool("NEC", 1).ToArray());
            CollectionAssert.AreEqual(new[] { ("NEC_MON_014", 50) }, r.Fusion.Pool("NEC+NEU", 1).ToArray());
            Assert.IsTrue(r.Fusion.IsClassMimic("NEU_MON_002"), "Monster_Effects FusionAsClass");
            CollectionAssert.AreEqual(new[] { "NEC_MON_015" }, r.Fusion.Commons("NEC", 2).ToArray(), "희귀도 C, Lv2, NEC 몬스터 (D-71)");
            CollectionAssert.AreEqual(new[] { "NEC_MON_001", "NEC_MON_006" }, r.Fusion.Commons("NEC", 1).ToArray());
        }

        [Test]
        public void Fusion_PositiveWeightWithoutCard_IsError()
        {
            var data = FusionData();
            var bad = new GameData(data.Sheets.Where(s => s.Name != "Fusion_RandomPool").Concat(new[]
            {
                S("Fusion_RandomPool", R(("Pool_ID", "PX"), ("InputClass", "NEC"), ("InputLevel", 1), ("ResultMON_ID", "MON_NEC_L1_009"), ("Weight", 10)))
            }).ToArray());
            var r = GameDataLoader.Load(bad);
            Assert.IsTrue(r.Errors.Any(e => e.Contains("PX") && e.Contains("MON_NEC_L1_009")), string.Join("\n", r.Errors));
        }

        // ---------- 적 행동 ----------

        [Test]
        public void Enemies_ActionsInOrder_SummonDrainChanceDefaults_SpawnStats()
        {
            var data = new GameData(
                S("Enemies_Master",
                    R(("ENE_ID", "ENE_1F_GHO_001"), ("HP", 14), ("ATK", 5), ("DEF", 1)),
                    R(("ENE_ID", "ENE_1F_ELT_001"), ("HP", 40), ("ATK", 7), ("DEF", 2))),
                S("Enemy_Actions",
                    R(("ENE_ID", "ENE_1F_ELT_001"), ("Order", 2), ("Action_ID", "DRAIN"), ("Condition", "Self.HpPct<60"), ("Chance", null), ("ActionType", "LifeSteal"), ("Value", 1), ("Status", null), ("Duration", null), ("Target", "Back"), ("Note", "피해의 40% 회복")),
                    R(("ENE_ID", "ENE_1F_ELT_001"), ("Order", 1), ("Action_ID", "SUMMON"), ("Condition", "Self.SummonCount<2"), ("Chance", null), ("ActionType", "Summon"), ("Value", "ENE_1F_GHO_001"), ("Status", null), ("Duration", null), ("Target", "EnemySlot"), ("Note", null)),
                    R(("ENE_ID", "ENE_1F_GHO_001"), ("Order", 1), ("Action_ID", "HEAVY"), ("Condition", "Self.HpPct>50"), ("Chance", 0.3), ("ActionType", "Attack"), ("Value", 1.5), ("Status", null), ("Duration", null), ("Target", "FrontLine"), ("Note", null)),
                    R(("ENE_ID", "ENE_1F_GHO_001"), ("Order", 2), ("Action_ID", "POISON"), ("Condition", null), ("Chance", null), ("ActionType", "ApplyStatus"), ("Value", null), ("Status", "Poison"), ("Duration", 2), ("Target", "FrontLine"), ("Note", null))));

            var r = GameDataLoader.Load(data);
            Assert.IsEmpty(r.Errors, string.Join("\n", r.Errors));
            var elite = r.EnemyKits.Get("ENE_1F_ELT_001");
            Assert.AreEqual(new[] { "SUMMON", "DRAIN" }, elite.Select(a => a.ActionId).ToArray(), "Order 순서");
            Assert.AreEqual("ENE_1F_GHO_001", elite[0].SpawnId);
            Assert.AreEqual(1.0, elite[0].Chance, "빈 Chance = 1");
            Assert.AreEqual(0.4, elite[1].DrainRatio, 1e-9, "Note 피해의 40% 회복");
            Assert.AreEqual("", elite[1].Status);

            var ghoul = r.EnemyKits.Get("ENE_1F_GHO_001");
            Assert.AreEqual((0.3, "Attack", 1.5, "FrontLine"), (ghoul[0].Chance, ghoul[0].ActionType, ghoul[0].Value, ghoul[0].Target));
            Assert.AreEqual(("Poison", 2, 0.0, ""), (ghoul[1].Status, ghoul[1].Duration, ghoul[1].Value, ghoul[1].Condition));
            Assert.AreEqual((14, 5, 1), r.EnemyKits.SpawnStats("ENE_1F_GHO_001"));
        }

        [Test]
        public void Enemies_UnknownEnemyOrSpawn_IsError()
        {
            var data = new GameData(
                S("Enemies_Master", R(("ENE_ID", "ENE_A"), ("HP", 10), ("ATK", 1), ("DEF", 0))),
                S("Enemy_Actions",
                    R(("ENE_ID", "ENE_A"), ("Order", 1), ("Action_ID", "SUM"), ("ActionType", "Summon"), ("Value", "ENE_NOPE"), ("Target", "EnemySlot")),
                    R(("ENE_ID", "ENE_GHOST"), ("Order", 1), ("Action_ID", "HIT"), ("ActionType", "Attack"), ("Value", 1), ("Target", "FrontLine"))));
            var r = GameDataLoader.Load(data);
            Assert.IsTrue(r.Errors.Any(e => e.Contains("ENE_NOPE")), string.Join("\n", r.Errors));
            Assert.IsTrue(r.Errors.Any(e => e.Contains("ENE_GHOST")));
        }

        // ---------- 보스 ----------

        [Test]
        public void Bosses_TurnCycleKeyedByEnemyId_RowOrderKept()
        {
            var data = new GameData(
                S("Enemies_Master", R(("ENE_ID", "ENE_2F_BOS_001"), ("HP", 60), ("ATK", 9), ("DEF", 2))),
                S("Boss_List", R(("Boss_ID", "BOS_2F_001"), ("ENE_ID", "ENE_2F_BOS_001"))),
                S("Boss_TurnCycle",
                    R(("Boss_ID", "BOS_2F_001"), ("Phase", 3), ("TurnIndex", 0), ("Action", "ApplyBurn"), ("Damage", 0), ("StatusEffect", "Burn+1"), ("TargetRule", "All"), ("EnqueueTwice", false), ("Note", "매 턴")),
                    R(("Boss_ID", "BOS_2F_001"), ("Phase", 3), ("TurnIndex", 0), ("Action", "SingleAttack"), ("Damage", 9), ("StatusEffect", null), ("TargetRule", "Front"), ("EnqueueTwice", true), ("Note", null))));
            var r = GameDataLoader.Load(data);
            Assert.IsEmpty(r.Errors, string.Join("\n", r.Errors));
            var steps = r.BossPatterns.Get("ENE_2F_BOS_001");
            Assert.AreEqual(2, steps.Count);
            Assert.AreEqual((3, 0, "ApplyBurn", 0, "Burn+1", "All", false), (steps[0].Phase, steps[0].TurnIndex, steps[0].Action, steps[0].Damage, steps[0].StatusEffect, steps[0].TargetRule, steps[0].EnqueueTwice));
            Assert.AreEqual(("SingleAttack", 9, "", true), (steps[1].Action, steps[1].Damage, steps[1].StatusEffect, steps[1].EnqueueTwice));
        }

        [Test]
        public void Bosses_ReadOnceOnPhaseEnter_ThresholdPct_CurseId_AndUnknownAction()
        {
            var data = new GameData(
                S("Enemies_Master", R(("ENE_ID", "ENE_3F_BOS_001"), ("HP", 73), ("ATK", 11), ("DEF", 2))),
                S("Boss_List", R(("Boss_ID", "BOS_3F_001"), ("ENE_ID", "ENE_3F_BOS_001"))),
                S("Boss_TurnCycle",
                    R(("Boss_ID", "BOS_3F_001"), ("Phase", 2), ("TurnIndex", 0), ("Action", "Summon"), ("Damage", 0), ("Note", "FLS_003 ×1"), ("OnceOnPhaseEnter", true), ("TargetRule", null)),
                    R(("Boss_ID", "BOS_3F_001"), ("Phase", 3), ("TurnIndex", 0), ("Action", "SelfHeal"), ("Damage", 6), ("TargetRule", "Self"), ("EnqueueTwice", false)),
                    R(("Boss_ID", "BOS_3F_001"), ("Phase", 1), ("TurnIndex", 2), ("Action", "Execute"), ("Damage", 0), ("TargetRule", "LowestHpPct"), ("ThresholdPct", 30)),
                    R(("Boss_ID", "BOS_3F_001"), ("Phase", 1), ("TurnIndex", 1), ("Action", "AddCurseCard"), ("Damage", 0), ("TargetRule", "Player"), ("CurseId", "CUR_001"))));
            var r = GameDataLoader.Load(data);
            Assert.IsEmpty(r.Errors, string.Join("\n", r.Errors));
            var steps = r.BossPatterns.Get("ENE_3F_BOS_001");
            Assert.AreEqual(4, steps.Count);
            Assert.AreEqual((true, 0, ""), (steps[0].OnceOnPhaseEnter, steps[0].ThresholdPct, steps[0].CurseId));
            Assert.AreEqual(("SelfHeal", 6, "Self", false), (steps[1].Action, steps[1].Damage, steps[1].TargetRule, steps[1].OnceOnPhaseEnter));
            Assert.AreEqual((30, ""), (steps[2].ThresholdPct, steps[2].CurseId));
            Assert.AreEqual(("CUR_001", 0), (steps[3].CurseId, steps[3].ThresholdPct));

            var ctx = new BattleContext(1);
            ctx.Player = new RuntimePlayer("P", 80, new RuntimeDeck(new RuntimeCard[0], ctx.Random));
            var boss = new RuntimeEnemy("B", "ENE_3F_BOS_001", 73, 11, 2, true);
            ctx.Field.PlaceEnemy(boss, SlotPosition.Front, ctx);
            Assert.DoesNotThrow(() =>
            {
                foreach (var step in steps)
                {
                    foreach (var action in step.ToActions(boss.DataId))
                    {
                        EnemyAi.Execute(ctx, boss, action);
                        ctx.Queue.Process();
                    }
                }
            });
        }

        [Test]
        public void Bosses_UnknownBossId_IsError()
        {
            var data = new GameData(
                S("Boss_List", R(("Boss_ID", "BOS_1F_001"), ("ENE_ID", "ENE_MISSING"))),
                S("Boss_TurnCycle", R(("Boss_ID", "BOS_9F_001"), ("Phase", 1), ("TurnIndex", 0), ("Action", "AoE"), ("Damage", 3))));
            var r = GameDataLoader.Load(data);
            Assert.IsTrue(r.Errors.Any(e => e.Contains("BOS_9F_001")), string.Join("\n", r.Errors));
            Assert.IsTrue(r.Errors.Any(e => e.Contains("ENE_MISSING")));
        }

        // ---------- 전투 편성 ----------

        [Test]
        public void Encounters_GroupByNodeVariantWave_ErrorsForUnknownEnemyAndZeroCount()
        {
            var data = new GameData(
                S("Enemies_Master", R(("ENE_ID", "ENE_A"), ("HP", 10), ("ATK", 1), ("DEF", 0)), R(("ENE_ID", "ENE_B"), ("HP", 12), ("ATK", 2), ("DEF", 1))),
                S("Encounters",
                    R(("Floor", 1), ("Node", 5), ("NodeType", "Elite"), ("Variant", "B"), ("Wave", 1), ("Enemy_ID", "ENE_B"), ("Count", 2)),
                    R(("Floor", 1), ("Node", 5), ("NodeType", "Elite"), ("Variant", "A"), ("Wave", 1), ("Enemy_ID", "ENE_A"), ("Count", 1)),
                    R(("Floor", 1), ("Node", 5), ("NodeType", "Elite"), ("Variant", "A"), ("Wave", 1), ("Enemy_ID", "ENE_B"), ("Count", 1)),
                    R(("Floor", 1), ("Node", 7), ("NodeType", "Battle"), ("Variant", "A"), ("Wave", 2), ("Enemy_ID", "ENE_A"), ("Count", 3)),
                    R(("Floor", 1), ("Node", 7), ("NodeType", "Battle"), ("Variant", "A"), ("Wave", 1), ("Enemy_ID", "ENE_B"), ("Count", 1)),
                    R(("Floor", 2), ("Node", 1), ("NodeType", "Battle"), ("Variant", "A"), ("Wave", 1), ("Enemy_ID", "ENE_NOPE"), ("Count", 1)),
                    R(("Floor", 2), ("Node", 2), ("NodeType", "Battle"), ("Variant", "A"), ("Wave", 1), ("Enemy_ID", "ENE_A"), ("Count", 0))));

            var r = GameDataLoader.Load(data);
            var elite = r.Encounters.Variants(1, 5);
            CollectionAssert.AreEqual(new[] { "A", "B" }, elite.Select(e => e.Variant).ToArray(), "A, B 순서");
            CollectionAssert.AreEqual(new[] { ("ENE_A", 1), ("ENE_B", 1) }, elite[0].Waves[1].ToArray(), "행 순서 유지");
            var n7 = r.Encounters.Variants(1, 7).Single();
            CollectionAssert.AreEqual(new[] { 1, 2 }, n7.Waves.Keys.ToArray(), "Wave 번호 순");
            Assert.AreEqual(("ENE_A", 3), n7.Waves[2].Single());
            Assert.IsTrue(r.Errors.Any(e => e.Contains("ENE_NOPE")), string.Join(" | ", r.Errors));
            Assert.IsTrue(r.Errors.Any(e => e.Contains("F2-N2") && e.Contains("Count")));
            Assert.AreEqual(0, r.Encounters.Variants(2, 1).Count + r.Encounters.Variants(2, 2).Count, "오류 행은 넣지 않는다");
        }

        [Test]
        public void GameData_FromJsonTexts()
        {
            var data = GameData.FromJson(new Dictionary<string, string>
            {
                ["Cards_Master"] = "{\"source\":\"x\",\"rows\":[{\"ID\":\"NEC_MON_001\",\"타입\":\"Monster\",\"클래스\":\"NEC\",\"레벨\":1,\"희귀도\":\"C\",\"Crystal\":1,\"HP\":10,\"ATK\":3,\"DEF\":1}]}"
            });
            var r = GameDataLoader.Load(data);
            Assert.IsEmpty(r.Errors);
            Assert.AreEqual(10, r.Cards.Get("NEC_MON_001").Hp);
        }
    }
}
