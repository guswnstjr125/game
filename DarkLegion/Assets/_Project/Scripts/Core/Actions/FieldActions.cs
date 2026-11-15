namespace DarkLegion.Core
{
    /// <summary>희생 요청의 결과 (`13` §2.5).</summary>
    public enum SacrificeResult
    {
        Ok,
        /// <summary>Player Phase가 아니다 (INVALID_PHASE).</summary>
        InvalidPhase,
        /// <summary>필드의 아군 몬스터가 아니다.</summary>
        NotAlly,
        /// <summary>이미 죽었다 (TARGET_DEAD).</summary>
        TargetDead,
        /// <summary>이미 희생이 예약되어 있다.</summary>
        AlreadyReserved,
        /// <summary>Soulbound 몬스터 (SOULBOUND).</summary>
        Soulbound
    }

    /// <summary>예약한 칸에 유닛을 놓는다 (`06` §8 SummonAction, `09` §2).</summary>
    public sealed class SummonAction : BattleAction
    {
        public RuntimeUnit Unit;
        public SlotPosition Position;

        public override BattleActionType Type => BattleActionType.Summon;

        public override void Execute(BattleContext context)
        {
            context.Field.Put((int)Position, Unit, context);
        }

        public override string ToString() => $"Summon {Unit?.UnitId} → {Position}";
    }

    /// <summary>
    /// 희생 (`13` §2.2): Crystal 지급 → (OnSacrifice 트리거 자리, `13` §2.4) → IsDead → Death 큐.
    /// 실행 전에 대상이 죽었으면 건너뛴다 (TARGET_DEAD, `13` §2.5).
    /// Crystal: Lv1 +1, Lv2 +2, Lv3 +3, 다른 값은 SacrificeCrystalOverride (`13` §2.3, D-04).
    /// 턴의 첫 희생이면 FirstSacrificeBonus를 더한다 (피의 잔·REL_BOSS_1F, D-04·D-15).
    /// </summary>
    public sealed class SacrificeAction : BattleAction
    {
        public RuntimeMonster Target;

        public override BattleActionType Type => BattleActionType.Sacrifice;

        public static int CrystalFor(RuntimeMonster m)
        {
            return m.SacrificeCrystalOverride ?? m.Level;
        }

        public override void Execute(BattleContext context)
        {
            if (Target == null) return;
            Target.SacrificeReserved = false;
            if (Target.IsDead) return;
            int gain = CrystalFor(Target);
            if (context.SacrificesThisTurn == 0) gain += context.FirstSacrificeBonus;
            context.Player.CurrentCrystal += gain;
            context.SacrificesThisTurn++;
            // OnSacrifice 트리거는 TriggerBus 구현 때 여기에 넣는다 (Crystal 지급 직후, Death 큐 등록 전)
            if (Target.MarkDead())
            {
                context.Queue.EnqueueDeath(new DeathAction { Unit = Target, IsSacrifice = true });
            }
        }

        public override string ToString() => $"Sacrifice {Target?.UnitId}";
    }

    /// <summary>Wave 전환 때 토큰 제거 (`09` §6). 사망이 아니라서 묘지·트리거·보상이 없다.</summary>
    public sealed class RemoveTokenAction : BattleAction
    {
        public RuntimeMonster Token;

        public override BattleActionType Type => BattleActionType.Destroy;

        public override void Execute(BattleContext context)
        {
            context.Field.Remove(Token);
            context.Allies.Remove(Token);
        }

        public override string ToString() => $"RemoveToken {Token?.UnitId}";
    }
}
