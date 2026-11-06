# 다크 레기온 — RuntimeFactory 상세설계

> **문서 역할**: Data(ScriptableObject) → Runtime 변환 단일 진입점, ID·시드·전투 인스턴스 규칙
> **참조 문서**: 05_기술_아키텍처.md §8, 06_전투_코어_상세설계.md §4, 14_Save_Load_상세.md, 07_TriggerBus_상세설계.md
> **구현 시점**: Phase 1-A 직후 (Runtime 타입과 함께)

---

## 1. 원칙

| 규칙 | 설명 |
|------|------|
| 단일 Factory | `RuntimeFactory`만 Runtime 생성 (`new RuntimeEnemy` 직접 금지, 테스트 제외) |
| Data 불변 | ScriptableObject 필드 전투 중 수정 금지 |
| ID 분리 | `DataId`(마스터 ID) vs `UnitId`(전투 인스턴스 GUID) |
| Registry | 생성 전 `CardRegistry`/`EnemyRegistry`/`MonsterRegistry`로 Data 조회 |

---

## 2. 클래스 구조

```csharp
public sealed class RuntimeFactory
{
    private readonly CardRegistry _cards;
    private readonly EnemyRegistry _enemies;
    private readonly MonsterRegistry _monsters;
    private readonly StatusRegistry _statuses;
    private int _unitIdCounter;  // 전투당 "U_0001" 형식

    public RuntimeFactory(/* registries injected */);

    public string NextUnitId() => $"U_{++_unitIdCounter:D4}";
}
```

- **전투마다** `ResetUnitIdCounter()` — BattleManager.Initialize 시
- Save 복원 시: 저장된 `UnitId` 유지, counter는 max+1로 재설정

---

## 3. 생성 API 목록

| 메서드 | 입력 | 출력 | 비고 |
|--------|------|------|------|
| `CreatePlayer` | PlayerRunState, BattleContext | RuntimePlayer | HP/Crystal/Deck는 RunState에서 |
| `CreateEnemy` | string enemyDataId, int floor, int stageIndex | RuntimeEnemy | `16_밸런스_규칙.md` §2 스케일 적용 |
| `CreateMonsterFromCard` | RuntimeCard, RuntimePosition | RuntimeMonster | 소환 카드용 |
| `CreateMonsterFromData` | MonsterData, RuntimePosition | RuntimeMonster | 융합 결과·스킬 소환 |
| `CreateSummonToken` | SummonTemplate, Owner, Position | RuntimeSummon | Duration·IsTemporary |
| `CreateCard` | string cardDataId | RuntimeCard | 덱 추가·보상 |
| `CreateCardFromSave` | CardSaveDTO | RuntimeCard | UpgradeLevel 포함 |
| `CreateStatus` | StatusData, owner, source, stack, duration | RuntimeStatusEffect | StatusFactory 위임 가능 |
| `RegisterBattleTriggers` | BattleContext | void | 유물·플레이어 패시브 (07_TriggerBus_상세설계.md) |

---

## 4. Data → Runtime 매핑 규칙

### 4.1 RuntimePlayer

```csharp
public RuntimePlayer CreatePlayer(PlayerRunState run, BattleContext ctx)
{
    var p = new RuntimePlayer {
        UnitId = NextUnitId(),
        DataId = "PLAYER",
        Team = TeamType.Player,
        MaxHP = new RuntimeStat { BaseValue = run.MaxHp },
        CurrentHP = run.CurrentHp,
        MaxCrystal = run.MaxCrystal,
        CurrentCrystal = 0,  // 턴 시작 시 TurnManager가 채움
    };
    p.Deck = DeckFactory.FromRunDeck(run.Deck, this);  // RuntimeCard 리스트
    return p;
}
```

### 4.2 RuntimeEnemy

```csharp
public RuntimeEnemy CreateEnemy(string dataId, int floor, int stageIndex, BattleContext ctx)
{
    var data = _enemies.Get(dataId);
    var scaled = BalanceScaler.ScaleEnemy(data, floor, stageIndex);  // 04 공식

    return new RuntimeEnemy {
        UnitId = NextUnitId(),
        DataId = dataId,
        Team = TeamType.Enemy,
        EnemyData = data,
        MaxHP = new RuntimeStat { BaseValue = scaled.Hp },
        CurrentHP = scaled.Hp,
        Attack = new RuntimeStat { BaseValue = scaled.Atk },
        Defense = new RuntimeStat { BaseValue = scaled.Def },
        EnemyAI = EnemyAIFactory.Create(data.AiType),
        IsTargetable = true,
        IsDead = false,
    };
}
```

- `ENE_1F_GHO_001` 등 **풀 ID**만 허용 (20_적_바이블)

### 4.3 RuntimeMonster (소환)

```csharp
public RuntimeMonster CreateMonsterFromCard(RuntimeCard card, RuntimePosition pos, BattleContext ctx)
{
    var data = card.Data.MonsterData;  // Monster 카드만
    var m = CreateMonsterFromData(data, pos, ctx);
    m.SourceCard = card;
    m.DataId = data.MonsterId;

    foreach (var trig in data.PassiveTriggers)
        ctx.TriggerBus.Register(trig, m);

    return m;
}
```

| 필드 | 소스 |
|------|------|
| Base HP/ATK/DEF | MonsterData + 카드 UpgradeLevel (25_MVP_보완_시스템_상세.md) |
| Passive | MonsterData.PassiveTriggers → TriggerBus |
| Soulbound | MonsterData.Tags contains Soulbound |

### 4.4 RuntimeSummon (토큰)

```csharp
public RuntimeSummon CreateSummonToken(SummonTemplate t, RuntimeUnit owner, RuntimePosition pos, BattleContext ctx)
{
    return new RuntimeSummon {
        UnitId = NextUnitId(),
        DataId = t.TemplateId,
        Team = owner.Team,
        Owner = owner,
        Duration = t.Duration,       // -1 = Wave 끝까지
        IsTemporary = true,
        MaxHP = new RuntimeStat { BaseValue = t.Hp },
        CurrentHP = t.Hp,
        // ATK/DEF 동일
    };
}
```

### 4.5 RuntimeCard

```csharp
public RuntimeCard CreateCard(string cardDataId, int upgradeLevel = 0)
{
    var data = _cards.Get(cardDataId);
    return new RuntimeCard {
        InstanceId = Guid.NewGuid().ToString("N"),
        Data = data,
        RuntimeCost = data.Crystal + data.UpgradeCostDelta(upgradeLevel),
        UpgradeLevel = upgradeLevel,
        Temporary = false,
        ExhaustWhenPlayed = data.HasKeyword(Keyword.Exhaust),
        Ethereal = data.HasKeyword(Keyword.Ethereal),
    };
}
```

---

## 5. EncounterFactory (전투 씬 조립)

`RuntimeFactory`와 협력. 책임 분리:

| 클래스 | 책임 |
|--------|------|
| RuntimeFactory | 유닛·카드·Status **인스턴스** |
| EncounterFactory | WaveData → 적 스폰 목록 → Factory 호출 → BattleField 배치 |

```csharp
public class EncounterFactory
{
    public void SpawnWave(WaveData wave, BattleContext ctx, RuntimeFactory factory, RuntimeBattleField field)
    {
        foreach (var spawn in wave.Spawns)
        {
            var enemy = factory.CreateEnemy(spawn.EnemyId, ctx.Floor, ctx.StageIndex, ctx);
            field.PlaceEnemy(spawn.Position, enemy);  // 09_BattleField_슬롯_상세설계.md: 슬롯 규칙
        }
    }
}
```

---

## 6. ActionFactory (연동)

Trigger·Effect에서 공통 사용 (`05_기술_아키텍처.md` §8).

```csharp
public static class ActionFactory
{
    public static IReadOnlyList<BattleAction> FromEffects(
        List<EffectDefinition> effects,
        TriggerContext ctx,
        RuntimeUnit owner);
}
```

- `RuntimeFactory`는 **Unit/Card** 생성
- `ActionFactory`는 **BattleAction** 생성 (Resolver와 동일 Effect 파이프)

---

## 7. Save / Load 연동 (14_Save_Load_상세.md)

| 방향 | 규칙 |
|------|------|
| Save | Runtime → DTO: `DataId`, `UnitId`, `CurrentHP`, Status 스냅샷 |
| Load | DTO → `CreateEnemy`/`CreateMonster` **금지** 전투 중 — `BattleSnapshot` 전용 복원 메서드 `RestoreUnitFromSnapshot` |
| 런 덱 | `DeckSaveDTO`는 CardData.Id + UpgradeLevel만 — Factory.CreateCard로 재구성 |

---

## 8. 금지·예외

```
❌ EncounterFactory 밖에서 CreateEnemy 후 필드 배치
❌ CardData 수정 후 CreateCard (Upgrade는 RuntimeCard.UpgradeLevel)
✅ 테스트: MockRuntimeFactory (인터페이스 IRuntimeFactory 권장)
```

---

## 9. 구현 체크리스트

```
□ UnitId 전투 내 유일
□ 적 스탯 16_밸런스_규칙.md 스케일 일원화
□ 소환 시 Passive TriggerBus 등록
□ Death 시 UnregisterAllOwnedBy(owner)
□ Registry miss 시 명확한 예외 + 로그 (CardId/EnemyId)
```

---

*문서 목록: [00_문서_인덱스.md](00_문서_인덱스.md)*
