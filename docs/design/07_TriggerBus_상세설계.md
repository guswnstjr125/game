# 다크 레기온 — TriggerBus 상세설계

> **문서 역할**: 구독 등록/해제, Priority·충돌, 유물·카드·패시브 연결, Flush 연동 (구현 정본)
> **참조 문서**: 05_기술_아키텍처.md §5, 06_전투_코어_상세설계.md §10, 23_콘텐츠_설계.md §2, 24_콘텐츠_런타임_상세.md §1
> **구현 시점**: Phase 1-B(Queue) 직후 ~ Phase 1-C 전 (**Phase 3 TriggerBus 본격 연동 전 최소 골격 필수**)

---

## 1. 목적·경계

| 항목 | 규칙 |
|------|------|
| TriggerBus | 이벤트 **발행** + 구독자 **조회** + `BattleAction` **생성 요청**만 |
| 금지 | Trigger 콜백에서 HP 직접 수정, `Execute()` 직접 호출, Main Queue 우회 |
| 출력 | `EnqueueTrigger(BattleAction)` 또는 `EnqueueTrigger(IEnumerable<BattleAction>)` |

---

## 2. 핵심 타입

### 2.1 TriggerEventType (06_전투_코어_상세설계.md §10.2 정본)

```csharp
public enum TriggerEventType
{
    OnBattleStart,
    OnTurnStart,
    OnTurnEnd,
    OnCardPlayed,
    OnDamageTaken,
    OnDamageDealt,
    OnHeal,
    OnDeath,
    OnSummon,
    OnSacrifice,
    OnStatusApplied,
    OnDraw,
    // 확장 (유물·패시브)
    OnAllyDeath,           // 21 REL_CURSED_SKULL
    OnStatusApplyAttempt,  // 21 REL_FORBIDDEN_CANDLE
}
```

### 2.2 TriggerContext (이벤트 페이로드)

```csharp
public sealed class TriggerContext
{
    public TriggerEventType EventType;
    public BattleContext Battle;
    public RuntimeUnit Source;      // 행동 주체 (없으면 null)
    public RuntimeUnit Target;      // 대상 (없으면 null)
    public object Payload;          // 타입별 DTO (아래 §3)
    public int TriggerDepth;        // 중첩 깊이 (MaxTriggerDepth 검사)
}
```

### 2.3 ITriggerSubscriber

```csharp
public interface ITriggerSubscriber
{
    string SubscriberId { get; }           // 디버그·중복 검사
    TriggerEventType[] ListenedEvents { get; }
    TriggerPriority Priority { get; }      // Before / On / After
    int OrderInPriority { get; }            // 동일 Priority 내 순서 (작을수록 먼저)

  /// 반환 Action만 Queue에 넣음. null/빈 리스트 = 무시
    IReadOnlyList<BattleAction> OnTrigger(TriggerContext ctx);
}
```

### 2.4 TriggerDefinition (Data → Subscriber 브릿지)

ScriptableObject·카드·유물이 공통으로 참조.

```csharp
[CreateAssetMenu]
public class TriggerDefinition : ScriptableObject
{
    public string TriggerId;
    public TriggerEventType EventType;
    public TriggerPriority Priority;
    public int OrderInPriority;
    public TriggerCondition Condition;     // Scriptable 조건 (§4)
    public List<EffectDefinition> Effects; // ActionFactory가 BattleAction으로 변환
}
```

---

## 3. Payload DTO (이벤트별)

| EventType | Payload 타입 | 필드 예 |
|-----------|--------------|---------|
| OnDamageTaken / OnDamageDealt | `DamageEventPayload` | BaseDamage, FinalDamage, IsCritical, DamageType |
| OnDeath | `DeathEventPayload` | WasPlayerUnit, DeathrattlePending |
| OnCardPlayed | `CardPlayedPayload` | RuntimeCard, CardData.Id |
| OnSacrifice | `SacrificePayload` | SacrificedUnit, CrystalGained |
| OnStatusApplied | `StatusAppliedPayload` | StatusId, Stack, Duration |
| OnDraw | `DrawPayload` | RuntimeCard |
| OnStatusApplyAttempt | `StatusApplyAttemptPayload` | StatusId, Stack, WasNegated |

---

## 4. 구독 등록 / 해제

### 4.1 수명 주기

```
[전투 진입]
  BattleManager.Initialize()
    → TriggerBus.Clear()
    → RuntimeFactory.RegisterBattleTriggers(context)  // 08_RuntimeFactory_상세설계.md
    → RelicManager.RegisterRelicTriggers(runRelics)
    → 각 RuntimeMonster: CardData.PassiveTriggers 등록

[전투 종료]
  BattleManager.Cleanup()
    → TriggerBus.Clear()   // 전투 전용 구독 전부 제거
```

### 4.2 등록 API

```csharp
public class TriggerBus
{
    public void Register(ITriggerSubscriber subscriber);
    public void Register(TriggerDefinition def, RuntimeUnit owner);  // 유물·패시브
    public void Unregister(string subscriberId);
    public void UnregisterAllOwnedBy(RuntimeUnit owner);  // 몬스터 사망·소환 해제 시
}
```

| 소스 | 등록 시점 | 해제 시점 |
|------|-----------|-----------|
| 유물 (런 전역) | 전투 시작 `RelicManager` | 전투 종료 Clear |
| 카드 패시브 (Monster) | `SummonAction` 완료 후 필드 배치 | `DeathAction` 슬롯 제거 직전 `UnregisterAllOwnedBy` |
| 카드 일회 (Skill) | 없음 — `OnCardPlayed`는 CardResolver가 직접 Action 생성 | — |
| 적 스킬 | 없음 — EnemyAI가 Intent→Action | — |
| TriggerDefinition | `Register(def, owner)` | owner 사망 또는 전투 종료 |

### 4.3 중복 등록

- 동일 `SubscriberId` 재등록 시 **기존 항목 제거 후 삽입** (경고 로그 1회).
- 동일 유물 2개 보유 불가 (런 규칙). 카드 패시브는 카드 인스턴스(`RuntimeCard.InstanceId`)별 1회.

---

## 5. 발행(Publish) · Flush 연동

### 5.1 Publish 규칙

```csharp
public void Publish(TriggerContext ctx)
{
    // 1) 구독자 수집 (EventType 일치)
    // 2) Priority: Before → On → After
    // 3) 동일 Priority: OrderInPriority 오름차순, 동률이면 Register 순서
    // 4) 각 구독자: OnTrigger → 반환 Action을 TriggerQueue에 Enqueue
    // 5) TriggerDepth++ (Enqueue 시 MaxTriggerDepth 검사)
}
```

**Publish는 동기적으로 Action만 Enqueue.** Flush는 `QueueProcessor`가 담당 (05_기술_아키텍처.md 정본).

### 5.2 호출 위치 (대표)

| 시점 | EventType | 호출 주체 |
|------|-----------|-----------|
| DamageAction Execute 내 | OnDamageTaken / OnDamageDealt | DamageAction (파이프라인 06_전투_코어_상세설계.md §11) |
| DeathAction Execute 내 | OnDeath | DeathAction |
| TurnManager | OnTurnStart / OnTurnEnd | TurnManager |
| CardResolver 완료 후 | OnCardPlayed | CardResolver |
| SacrificeAction | OnSacrifice | SacrificeAction |
| SummonAction | OnSummon | SummonAction |

### 5.3 Priority 충돌·상쇄

| 상황 | 처리 |
|------|------|
| 동일 Priority 다수 | OrderInPriority → 등록 순 (결정적) |
| Before에서 무효화 | `NegateStatusApply` 등은 Payload에 `WasNegated=true` 설정, After 구독자는 스킵 |
| 서로 반대 효과 | **별도 Merge 없음** — Queue 순서대로 실행. 기획 의도는 OrderInPriority로 조정 |
| OnDeath 연쇄 | Death Queue Flush **이후** Trigger Flush에서 Deathrattle 처리 (06_전투_코어_상세설계.md §12) |

---

## 6. 소스별 연결 방식

### 6.1 유물 (24_콘텐츠_런타임_상세.md §1)

```csharp
public class RelicTriggerAdapter : ITriggerSubscriber
{
    private readonly RelicData _data;
    private readonly RuntimeRelic _runtime;

    public IReadOnlyList<BattleAction> OnTrigger(TriggerContext ctx)
    {
        if (!RelicConditionEvaluator.Evaluate(_data.Condition, ctx)) return Empty;
        return ActionFactory.FromEffects(_data.Effects, ctx);
    }
}
```

- `REL_FORBIDDEN_CANDLE`: `OnStatusApplyAttempt` + Before, `NegateStatusApplyAction` 1회 (런당 플래그 `RuntimeRelic.Charges`)

### 6.2 몬스터 패시브 (CardData)

```csharp
// MonsterData.PassiveTriggers: List<TriggerDefinition>
// 소환 시 TriggerBus.Register(def, runtimeMonster)
```

- Deathrattle: `TriggerDefinition`에 `EventType=OnDeath`, `Priority=On`, Effects=Deathrattle 목록

### 6.3 카드 스킬/액션 (일회 효과)

- **TriggerBus 미사용** — `CardResolver` → `EffectDefinition` → Main Queue (06_전투_코어_상세설계.md 원칙 유지)
- 키워드 Ethereal/Echo는 **TurnManager + DeckManager** (25_MVP_보완_시스템_상세.md)

### 6.4 적

- AI → `BattleAction` 직접 Main Queue (TriggerBus 없음)

---

## 7. 조건(Condition) 평가

```csharp
public abstract class TriggerCondition : ScriptableObject
{
    public abstract bool Evaluate(TriggerContext ctx, RuntimeUnit owner);
}
```

| Condition 예 | 용도 |
|--------------|------|
| `TeamIsPlayerCondition` | 아군 사망 유물 |
| `HpRatioBelowCondition` | REL_ABYSS_HEART (0.5) |
| `OncePerRunCondition` | REL_FORBIDDEN_CANDLE |
| `SacrificedUnitIsPlayerCondition` | REL_BLOOD_CHALICE |

**조건 실패 시 Action 0건 반환** (Skip).

---

## 8. 무한 루프·안전

| 상수 | 값 | 동작 |
|------|-----|------|
| MaxTriggerDepth | 32 | Enqueue 시 초과 → 해당 Action 폐기 + 로그 |
| MaxTriggerIterationsPerFlush | 20 | FlushTriggerQueue 루프 상한 |
| MaxQueueCount | 500 | Main+Trigger+Death 합산 |

`TriggerDepth`는 Publish 체인마다 +1. 한 Main Action에서 Trigger가 Trigger를 Publish해도 Depth로 제한.

---

## 9. 디버그

- Editor: 전투 로그에 `[Trigger] Event / SubscriberId / ActionCount` 출력 (Development 빌드만)
- `TriggerBus.GetSubscribersFor(TriggerEventType)` — 테스트용

---

## 10. 구현 체크리스트

```
□ Publish는 Enqueue만, Execute 없음
□ 전투 종료 Clear로 유물·몬스터 구독 누수 없음
□ 몬스터 Death 시 UnregisterAllOwnedBy
□ Before/On/After + OrderInPriority 정렬 결정적
□ Damage/Death 파이프라인 위치가 06_전투_코어_상세설계.md §11·§12와 일치
```
