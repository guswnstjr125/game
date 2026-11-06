# 다크 레기온 — MVP 보완 시스템 상세 (키워드·강화·소모품·Mimic)

> **문서 역할**: `23_콘텐츠_설계.md`·`24_콘텐츠_런타임_상세.md`에 이름만 있고 흐름이 없던 시스템의 **MVP 최소 정본**
> **참조 문서**: 23_콘텐츠_설계 §3, 17_카드_데이터베이스, 03_MVP_전층_스펙, 24_콘텐츠_런타임_상세
> **우선순위**: Phase 2~5 구현 중 해당 기능 터치 시 적용 (Post-MVP 확장 가능)

---

## 1. Ethereal / Echo (키워드 런타임)

### 1.1 Ethereal

| 항목 | 규칙 |
|------|------|
| 정의 | 턴 종료 시 **패(Hand)에 남아 있으면** Exhaust (소멸 pile) |
| 타이밍 | `TurnManager.TurnEnd` — `OnTurnEnd` Trigger **이후**, Discard **이전** |
| 사용함 | 일반 Discard로 이동 (Exhaust 아님) |
| 전투 종료 | Hand에 남은 Ethereal도 Discard 처리 (Exhaust 아님) |

```
TurnEnd (플레이어)
  → Status TurnEnd Tick
  → TriggerBus OnTurnEnd
  → DeckManager.ExhaustEtherealInHand()   // Ethereal만 ExhaustPile
  → DeckManager.DiscardRemainingHand()    // 나머지 Discard
```

### 1.2 Echo

| 항목 | 규칙 |
|------|------|
| 정의 | **이번 턴에 사용(Play)한** Echo 카드 1장을 다음 턴 시작 시 Hand에 임시 추가 |
| 저장 | `RuntimePlayer.EchoQueue: List<string>` — CardData.Id |
| 부여 | CardResolver Play 완료 시 `Echo` 키워드면 Id 추가 (중복 Id는 스택 1회만) |
| 소비 | `OnTurnStart` → `CreateCard(id, temp:true)` → Hand 추가, EchoQueue 클리어 |
| 비용 | 복제본 `RuntimeCost = 0`, `Temporary = true`, 턴 종료 Discard |

MVP 카드에 Echo 없으면 **시스템만 구현**, 콘텐츠는 이후 추가.

---

## 2. 카드 강화 (Upgrade)

### 2.1 ID 체계 (MVP)

- **별도 CardId 없음** — 동일 `NEC_MON_001` + `RuntimeCard.UpgradeLevel` (0~1 MVP)

```csharp
public class RuntimeCard
{
    public int UpgradeLevel;  // 0 = 기본, 1 = 강화 1회
}
```

Save: `DeckSaveDTO` 항목을 `{ "cardId", upgradeLevel }` 쌍으로 저장.

### 2.2 강화 효과 (MVP 단일 규칙)

| UpgradeLevel | 효과 |
|--------------|------|
| 0 | CardData 기본 Effect |
| 1 | **Monster**: MaxHP +20%, ATK +1 (소환 시 RuntimeFactory 적용) |
| 1 | **Skill/Action**: `EffectDefinition` 첫 Damage/Heal 값 +25% (반올림) |

- 피의 제단 `UpgradeCard(1)`: 덱에서 **플레이어 선택 1장** → UpgradeLevel 0→1 (이미 1이면 선택 불가 목록에서 제외)

### 2.3 UI

- Rest 노드: "카드 1장 강화" (`03_MVP_전층_스펙.md`) — 동일 `UpgradeService.UpgradeOne(card)`
- 이벤트: `UpgradeCard(1)` Effect → Upgrade UI

---

## 3. 소모품 (Consumable) — MVP

### 3.1 데이터

```csharp
public class ConsumableData : ScriptableObject
{
    public string ConsumableId;   // CON_HP_SMALL, CON_PURIFY
    public int ShopPrice;         // 25
    public List<RunEffectDefinition> UseEffects;  // 전투 밖 RunEffect
}
```

### 3.2 MVP 2종

| ID | 효과 | 사용 시점 |
|----|------|-----------|
| CON_HP_SMALL | PlayerHp +15 (MaxHp 초과 불가) | 맵 노드 (전투 전) |
| CON_PURIFY | 부정적 상태 1스택 제거 (런 디버프 MVP 없으면 Poison 대신 "저주 카드 1장 제거"로 대체 가능) |

### 3.3 인벤토리

```csharp
public class RunInventory
{
    public List<string> ConsumableIds;  // 최대 3칸 (MVP)
}
```

- 상점 구매 → 인벤토리 추가
- 전투 중 사용 **MVP 불가** (맵 UI에서만)
- Save: `SaveData.Consumables`

---

## 4. Mimic (함정 보물) — Post-MVP 준비 스펙

### 4.1 노드 동작

| 단계 | 동작 |
|------|------|
| 진입 | Treasure UI와 동일 "상자 열기" |
| 50% | 일반 Treasure 보상 (골드/카드 1) |
| 50% | **강제 전투** — `MIMIC_1F` 적 1체 (ENE_1F_GHO_002 스탯 ×1.2) |
| 승리 | Treasure 상위 보상 (골드 +50%) |
| 패배 | 일반 런 패배 |

### 4.2 MVP 맵

- 03_MVP_전층_스펙.md: 층당 단일 경로 — **Mimic 노드 타입 없음**, 가중치만 `24_콘텐츠_런타임_상세.md` §5에 존재
- **MVP**: Mimic **미등장** (코드·데이터만 준비, `StageType.Mimic` → Treasure 폴백)
- Post-MVP: `24_콘텐츠_런타임_상세.md` 맵 분기에서 Mimic 가중치 1% 적용
- MVP 구현 체크: `StageType.Mimic` 입력 시 크래시 없이 Treasure 흐름으로 안전 폴백

---

## 5. Post-MVP (참고만)

| 항목 | 문서 |
|------|------|
| 로컬라이제이션 | KeywordData.DisplayName / CardData LocKey |
| Save 암호화 | 16 확장 — checksum + optional AES |

---

## 6. 구현 Phase 매핑

| 시스템 | Phase |
|--------|-------|
| Ethereal/Echo | 2-A DeckManager + 1-C TurnManager |
| Upgrade | 5-B Event / Rest |
| Consumable | 5-B Shop |
| Mimic | 5 (스텁) / Post-MVP |

---

*문서 목록: [00_문서_인덱스.md](00_문서_인덱스.md)*
