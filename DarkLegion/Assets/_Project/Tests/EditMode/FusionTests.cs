using System.Collections.Generic;
using System.Linq;
using DarkLegion.Core;
using NUnit.Framework;

namespace DarkLegion.Tests.EditMode
{
    /// <summary>
    /// Lv1 융합 (#43, `13` §3, `01` D-03·D-21·D-22·D-26·D-38). `32` FUS-01~04.
    /// </summary>
    [Category("Stage1")]
    public class FusionTests
    {
        private BattleContext _ctx;
        private int _id;

        private void Setup(int seed, params string[] hand)
        {
            _ctx = new BattleContext(seed) { Cards = StageOneCards.Create(), Fusion = StageOneFusion.Create() };
            var cards = hand.Select(id => _ctx.Cards.Get(id).CreateCard(++_id, false)).ToList();
            _ctx.Player = new RuntimePlayer("P", 80, new RuntimeDeck(cards, _ctx.Random, shuffle: false));
            _ctx.Player.FusionCharge = 1;
            _ctx.StartPlayerTurn();                                       // Crystal 3, 5장 드로우
        }

        private RuntimeCard InHand(string id, int skip = 0) => _ctx.Player.Deck.Hand.Where(c => c.CardId == id).Skip(skip).First();

        [Test]
        public void FUS_02_FixedRecipe_GhoulPlusGhoul_IsCannibalGhoul()
        {
            Setup(1, "NEC_MON_002", "NEC_MON_002", "NEC_SKL_004");
            var a = InHand("NEC_MON_002");
            var b = InHand("NEC_MON_002", 1);

            var r = _ctx.Fuse(a, b);

            Assert.AreEqual(FusionError.None, r.Error);
            Assert.AreEqual("NEC_MON_013", r.Result.CardId);
            Assert.IsTrue(r.Result.FromFusion, "D-38 FromFusion");
            Assert.AreEqual(CardZone.Hand, r.Result.Zone);
            Assert.AreEqual(1, _ctx.Player.CurrentCrystal, "Lv1 + Lv1 비용 2 (D-03): 3 − 2");
            Assert.AreEqual(0, _ctx.Player.FusionCharge);
            Assert.AreEqual(CardZone.Destroyed, a.Zone, "재료는 영구 소멸 (DeadCard)");
            Assert.AreEqual(CardZone.Destroyed, b.Zone);
            Assert.AreEqual(2, _ctx.Player.Deck.Hand.Count, "재료 2장 나가고 결과 1장 들어옴: 3 − 2 + 1");
        }

        [Test]
        public void FUS_02_FixedRecipe_OrderDoesNotMatter_BoneSoldierAndSpearman()
        {
            Setup(1, "NEC_MON_006", "NEC_MON_001");
            var r = _ctx.Fuse(InHand("NEC_MON_006"), InHand("NEC_MON_001"));
            Assert.AreEqual("NEC_MON_014", r.Result.CardId, "FUS_014: A·B 순서 무관");
            Assert.AreEqual("FUS_014", r.RecipeId);
        }

        [Test]
        public void FUS_02_DisabledRecipes_NotInCatalog_GoToRandomPool()
        {
            // FUS_009(뼈 병사 + 해골 토큰)·FUS_002·003(무덤지기)은 재료가 카드가 아니라 비활성 (D-03·D-22).
            // 카드끼리는 뼈 병사 + 뼈 병사 같은 비레시피 조합만 가능하고, 이것은 NEC Lv1 랜덤 풀로 간다
            Setup(3, "NEC_MON_001", "NEC_MON_001");
            var r = _ctx.Fuse(InHand("NEC_MON_001"), InHand("NEC_MON_001", 1));
            Assert.IsTrue(r.Ok);
            Assert.IsNull(r.RecipeId);
            CollectionAssert.Contains(new[] { "NEC_MON_013", "NEC_MON_014", "NEU_MON_007", "NEU_MON_008" }, r.Result.CardId);
        }

        [Test]
        public void FUS_01_ChargeFromNodes3_6_9_Max3_ZeroRejects()
        {
            CollectionAssert.AreEqual(new[] { 0, 0, 1, 0, 0, 1, 0, 0, 1 },
                Enumerable.Range(1, 9).Select(FusionSystem.ChargeForNodeClear).ToArray());

            Setup(1, "NEC_MON_002", "NEC_MON_002");
            _ctx.Player.FusionCharge = 0;
            int crystal = _ctx.Player.CurrentCrystal;
            var a = InHand("NEC_MON_002");
            var r = _ctx.Fuse(a, InHand("NEC_MON_002", 1));
            Assert.AreEqual(FusionError.NoCharge, r.Error);
            Assert.AreEqual(crystal, _ctx.Player.CurrentCrystal, "실패하면 아무것도 바뀌지 않는다");
            Assert.AreEqual(CardZone.Hand, a.Zone);

            for (int node = 1; node <= 18; node++) _ctx.Player.GainFusionCharge(FusionSystem.ChargeForNodeClear(node));
            Assert.AreEqual(3, _ctx.Player.FusionCharge, "1~2층 18노드 = 6회 충전이지만 최대 3");
        }

        [Test]
        public void FUS_04_Cost2_NotEnoughCrystal_Rejected()
        {
            Setup(1, "NEC_MON_002", "NEC_MON_002");
            _ctx.Player.CurrentCrystal = 1;
            Assert.AreEqual(FusionError.NotEnoughCrystal, _ctx.Fuse(InHand("NEC_MON_002"), InHand("NEC_MON_002", 1)).Error);
            Assert.AreEqual(1, _ctx.Player.FusionCharge);
        }

        [Test]
        public void FUS_04_LevelCap_Lv3PlusLv3_Rejected()
        {
            Setup(1);
            _ctx.Cards.Add(new CardDefinition { Id = "NEC_MON_T3", Type = CardType.Monster, Cost = 3, Level = 3, Hp = 30, Atk = 8 });
            var a = _ctx.Cards.Get("NEC_MON_T3").CreateCard(100, false);
            var b = _ctx.Cards.Get("NEC_MON_T3").CreateCard(101, false);
            _ctx.Player.Deck.AddToHand(a);
            _ctx.Player.Deck.AddToHand(b);
            Assert.AreEqual(FusionError.LevelCap, _ctx.Fuse(a, b).Error);
        }

        [Test]
        public void FUS_Rejects_LevelMismatch_Soulbound_NonMonster_SameCard_OutsideMainPhase()
        {
            Setup(1, "NEC_MON_002", "NEC_MON_007", "NEC_MON_005", "NEC_SKL_004", "NEC_MON_001");
            Assert.AreEqual(FusionError.LevelMismatch, _ctx.Fuse(InHand("NEC_MON_002"), InHand("NEC_MON_007")).Error);
            Assert.AreEqual(FusionError.Soulbound, _ctx.Fuse(InHand("NEC_MON_002"), InHand("NEC_MON_005")).Error);
            Assert.AreEqual(FusionError.NotMonster, _ctx.Fuse(InHand("NEC_MON_002"), InHand("NEC_SKL_004")).Error);
            Assert.AreEqual(FusionError.SameCard, _ctx.Fuse(InHand("NEC_MON_002"), InHand("NEC_MON_002")).Error);
            _ctx.State = BattleState.EnemyPhase;
            Assert.AreEqual(FusionError.InvalidPhase, _ctx.Fuse(InHand("NEC_MON_002"), InHand("NEC_MON_001")).Error);
            Assert.AreEqual(5, _ctx.Player.Deck.Hand.Count);
        }

        [Test]
        public void FUS_ResultCanBeSummonedSameTurn_IfCrystalLeft()
        {
            Setup(1, "NEC_MON_002", "NEC_MON_002");
            _ctx.Player.CurrentCrystal = 4;                               // 융합 2 + 식인 구울 비용 2
            var r = _ctx.Fuse(InHand("NEC_MON_002"), InHand("NEC_MON_002", 1));
            Assert.AreEqual(PlayResult.Ok, _ctx.PlayCard(r.Result, null, SlotPosition.Front));
            _ctx.Queue.Process();
            var m = _ctx.Allies.Single();
            Assert.AreEqual(18, m.MaxHP.FinalValue, "식인 구울 HP 18");
            Assert.AreEqual(1, _ctx.FusionHistory.Count);
            Assert.AreEqual("FUS_001", _ctx.FusionHistory[0].RecipeId);
        }

        [Test]
        public void FUS_03_RandomTenThousand_NecLv1_Class80Neu20_NecNeu50_50()
        {
            // `32` §11: 시드 고정, ±1.5%p. 결과는 모두 카드가 있다 (카드 없는 결과·Mutation 가중치 0, D-21)
            var catalog = StageOneFusion.Create();
            var cards = StageOneCards.Create();
            var random = new BattleRandom(2026);
            int nec = 0, necNeuClass = 0;
            const int N = 10000;
            for (int i = 0; i < N; i++)
            {
                var r = FusionSystem.Resolve(catalog, "NEC_MON_001", "NEC_MON_003", 1, random, out _);
                Assert.IsNotNull(cards.Get(r), r);
                if (FusionSystem.ClassOf(r) == "NEC") nec++;
                var m = FusionSystem.Resolve(catalog, "NEC_MON_002", "NEU_MON_006", 1, random, out _);
                Assert.IsNotNull(cards.Get(m), m);
                if (FusionSystem.ClassOf(m) == "NEC") necNeuClass++;
            }
            Assert.AreEqual(0.80, nec / (double)N, 0.015, "같은 클래스: 클래스 80 / NEU 20");
            Assert.AreEqual(0.50, necNeuClass / (double)N, 0.015, "NEU + 클래스: 50 / 50");
        }

    }
}
