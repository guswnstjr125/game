# 다크 레기온 — Save / Load 상세

> **문서 역할**: DTO 직렬화, 저장 시점, 파일 레이아웃
> **참조 문서**: 05_기술_아키텍처 §9·§21, 06_전투_코어_상세설계 §17

---

## 1. 원칙

```
Runtime → DTO → JSON → 파일
Runtime 직접 직렬화 금지
```

---

## 2. SaveData

```csharp
[Serializable]
public class SaveData
{
    public string Version = "1.0.0";
    public PlayerSaveDTO Player;
    public DeckSaveDTO Deck;
    public MapSaveDTO Map;
    public List<RelicSaveDTO> Relics;
    public MetaSaveDTO Meta;
    public long RngSeed;
    public int Floor;
    public int StageIndex;
    public int Gold;
}
```

### PlayerSaveDTO

```csharp
public class PlayerSaveDTO
{
    public int CurrentHp;
    public int MaxHp;
    public int CurrentCrystal;
    public int MaxCrystal;
}
```

### DeckSaveDTO

```csharp
public class DeckSaveDTO
{
    public List<string> DrawPile;    // CardData.Id
    public List<string> Hand;
    public List<string> DiscardPile;
    public List<string> ExhaustPile;
}
```

### MapSaveDTO

```csharp
public class MapSaveDTO
{
    public string CurrentNodeId;
    public List<string> VisitedNodeIds;
    public List<MapEdgeDTO> Edges;   // 간선 직렬화
}
```

### MetaSaveDTO

```csharp
public class MetaSaveDTO
{
    public List<string> UnlockedClasses;
    public List<string> PermanentUnlocks;  // 가마솥
    public int MetaSoul;
    public int MetaSigil;
    public List<string> UnlockedCards;
    public List<string> UnlockedRelics;
    public Dictionary<string, bool> AchievementFlags;
}
```

> 메타 진행 상세: [26_메타_언락_설계.md](26_메타_언락_설계.md)

---

## 3. BattleSnapshotDTO (전투 중)

```csharp
public class BattleSnapshotDTO
{
    public int TurnCount;
    public long RngSeed;
    public List<UnitSnapshotDTO> Allies;
    public List<UnitSnapshotDTO> Enemies;
    public DeckSaveDTO Deck;
    public List<string> MainQueueLog;   // 디버그
}
```

---

## 4. 저장 시점

| 시점 | 파일 |
|------|------|
| 스테이지 클리어 | save_slot_N.json |
| 전투 중 (옵션) | battle_snapshot.json |
| 앱 백그라운드 | save_slot_0.json |
| 런 종료 | meta.json 갱신 |

---

## 5. 파일 구조

```
/SaveData/
  save_slot_0.json
  save_slot_1.json
  battle_snapshot.json
  meta.json
```

---

*이전 문서: [15_Battle_Simulator_설계.md](15_Battle_Simulator_설계.md)*
*다음 문서: [27_UI_바인딩_설계.md](27_UI_바인딩_설계.md)*
