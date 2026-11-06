# Excel ↔ 설계 MD 교차 검수표 (C-4 ~ M-6)

> 🗄️ **기록용 (archive)** — 설계 패키지를 받기 전(2026-11-06 이전) 시점의 Excel↔MD 교차 검수 기록이다. 현재 정합성은 `tools/check_design.py`로 확인한다.


> 기준일: Excel `다크레기온_프로젝트_전체/*.xlsx` vs MD `23_콘텐츠_설계.md` · `17_카드_데이터베이스.md` · `03_MVP_전층_스펙.md` · `18_클래스_시작덱_설계.md` · `26_메타_언락_설계.md` · `25_MVP_보완_시스템_상세.md`  
> 상태: **OK** 일치 · **△** 부분일치 · **✕** 불일치 · **DOC** 문서만 수정 필요

---

## 1. C-4 — 층별 카드 풀 해금

| 항목 | 설계 MD | Excel | 상태 | 비고 |
|------|---------|-------|------|------|
| 시트 위치 | `08_카드_데이터베이스.xlsx` Cards_FloorUnlock | `08_카드_데이터베이스.xlsx` → **Cards_FloorUnlock** | OK | |
| 컬럼 | Card_ID, UnlockFloor, DropPool_* | 동일 6컬럼 | OK | |
| 1층 누적 32장 | `03_MVP_전층_스펙.md` §3.3 "32장" (NEC 등 기본 시작) | **Floor_Pool_Summary** F1=32 | OK | HOL/BEA 시작은 D-07 별도 누적 |
| 2~5층 누적 | §4~7 "+16/+20/+18" → 48/68/86 (기본 시작) | Summary 48/68/86 | OK | |
| 5층 전체 92장 | §7 "전 클래스 92장" 드롭 풀 | F5 누적 92 | OK | 융합 전용 6장은 풀 제외 |
| 시작 클래스 | HOL/BEA 유효 해금층 1 | `UnlockFloor_IfStartClass=1` | OK | D-07. 기본 UnlockFloor는 유지 |
| 카드별 해금 규칙 | NEC/NEU F1, ELE 일부 F1… | UnlockFloor 1~5 per card | OK | 18 서술과 정렬 알고리즘 일치 |

**MD 링크:** `17_카드_데이터베이스.md`, `03_MVP_전층_스펙.md` §8 — **Cards_FloorUnlock** 명시 OK

---

## 2. C-5 — 일반 전투 골드·드롭

| 항목 | 설계 MD | Excel | 상태 | 비고 |
|------|---------|-------|------|------|
| 시트 | `06_콘텐츠_마스터.xlsx` Reward_BattleTable | **Reward_BattleTable** 15행 (5층×3유형) | OK | |
| BattleType | Normal/Elite/Boss | 컬럼 추가 | OK | |
| 1층 일반 골드 | §8: 5~10 | Normal F1: 5/10 | OK | **§8 정본** |
| 1층 §3.3 문구 | 구 "10~20" | §3.3 → 5~10 + §8 참조 | OK | MD 수정 완료 |
| 엘리트 골드 | §8 층×15~25 | Elite 행 15~25 … | OK | |
| 보스 골드 | §8 층×25~40 | Boss 행 25~40 … | OK | |
| 희귀도 | `23_콘텐츠_설계.md` §5 | Drop_* 컬럼 | OK | Elite Rare×2 Note |

**MD 링크:** `23_콘텐츠_설계.md` §5 — Reward_BattleTable OK (적용 우선순위 문구 보완 완료)

---

## 3. H-5 — 플레이어 기본 스탯

| 항목 | 설계 MD | Excel `04_밸런스_테이블.xlsx` **Player_Stats** | 상태 |
|------|---------|------------------------------|------|
| NEC | 03_MVP_전층_스펙.md §1 HP80, Crystal3 | StartHP80, StartCrystal3 | OK |
| ELE +1 Crystal | `18_클래스_시작덱_설계.md` §7 | StartCrystal **4**, Note +1Crystal | OK |
| BEA 기본 | 18_클래스_시작덱_설계.md | 80/80/0/3 기본 | OK |
| HOL +5 HP | 18_클래스_시작덱_설계.md §7 | StartHP/MaxHP **85**, +5HP | OK |
| NEU +40 Gold | 18_클래스_시작덱_설계.md §7 | StartGold **40**, +40Gold | OK |
| 컬럼명 | Class, StartHP, MaxHP, StartGold, StartCrystal | **동일** | OK |

**MD 링크:** `18_클래스_시작덱_설계.md` — Player_Stats 참조 **데이터 있음** (이전 "없음" 이슈 해소)

---

## 4. H-6 — Rest / Treasure / Mimic

### Rest

| 항목 | 설계 MD | Excel | 상태 |
|------|---------|-------|------|
| 노드 8 Rest | `03_MVP_전층_스펙.md` HP **30%** 또는 카드 강화 1장 | **RestNode_Rules** HealPercent **0.3** | OK |
| 층별 차이 | 03_MVP_전층_스펙.md "전 층 동일" 암시 | FloorScaling "전 층 동일" | OK |
| 선택 불가 | 미명시 | DisableCondition 2종 | OK | Excel이 더 상세 |
| MD 시트명 | `03_MVP_전층_스펙.md` **RestNode_Rules** | **RestNode_Rules** (+ Excel MapNode_Rest 호환 복제) | OK | MD 통일 완료 |

### Treasure / Mimic

| 항목 | 설계 MD | Excel | 상태 |
|------|---------|-------|------|
| Treasure 보상 | `24_콘텐츠_런타임_상세.md`/`23_콘텐츠_설계.md` 맵 가중치 | **TreasureNode_Rules** 층별 Gold/Card/Relic | OK |
| Mimic 50% 전투 | `25_MVP_보완_시스템_상세.md` §4.1 ENE×1.2 | **MimicNode_Rules** MIMIC_DEFAULT | OK |
| Mimic MVP 미등장 | `25_MVP_보완_시스템_상세.md` §4.2 | Note에 명시 | OK |
| MD 링크 | `03_MVP_전층_스펙.md` §8 *Node_Rules 3종 | TreasureNode_Rules · MimicNode_Rules | OK |

---

## 5. M-5 — 엘리트 유물 드롭 풀

| 항목 | 설계 MD | Excel | 상태 |
|------|---------|-------|------|
| 엘리트 유물 3택1 | `03_MVP_전층_스펙.md` §8, §3.3 | **Reward_EliteRelic** 5층×3풀 | OK |
| Rare×2 | `23_콘텐츠_설계.md` §5 | Rarity_Rule "Rare×2" | OK |
| 층별 풀 ID | 미상세 | REL_COM/UNC/RARE/LEG/Boss | OK |

---

## 6. M-6 — 메타 언락

| 항목 | 설계 MD | Excel | 상태 |
|------|---------|-------|------|
| 언락 7종 | `26_메타_언락_설계.md` §2 | **Meta_Unlock** 7행 (`UNL_CLASS_NEU`) | OK |
| 층 클리어 Soul/Sigil | `26_메타_언락_설계.md` §1 표 | **Meta_Currency** 5행 | OK |
| 업적 12종 | `26_메타_언락_설계.md` §3 | **Meta_Achievement** 12행 | OK |
| MD 링크 | `26_메타_언락_설계.md` § 수치 표 | Meta_Unlock · Meta_Currency · Meta_Achievement | OK |

---

## 7. 문서 ↔ Excel 시트명 정합

| MD 파일 | 문구 | Excel 정본 시트 | 상태 |
|---------|------|-----------------|------|
| `03_MVP_전층_스펙.md` L96 | **RestNode_Rules** | RestNode_Rules | OK |
| `03_MVP_전층_스펙.md` §8 | Rest/Treasure/Mimic **\*Node_Rules** | 동일 3시트 | OK |
| Excel 호환 | — | MapNode_Rest/Treasure/Mimic (데이터 복제) | 구현 시 **\*Node_Rules** 우선 |

---

## 8. 종합

| ID | Excel 데이터 | MD 1:1 | 남은 갭 |
|----|--------------|--------|---------|
| C-4 | ✅ | ✅ | 없음 |
| C-5 | ✅ | ✅ | §8 정본 확정·Reward_BattleTable 15행·적용 우선순위 문구 반영 |
| H-5 | ✅ | ✅ | 없음 |
| H-6 | ✅ | ✅ | *Node_Rules 시트명 통일 + MVP(Post-MVP) 범위 분리 반영 |
| H-7 | (=H-5) | ✅ | — |
| M-5 | ✅ | ✅ | 없음 |
| M-6 | ✅ | ✅ | 없음 |

**Claude 업로드 시 확인할 Excel 3개:** `04_밸런스_테이블.xlsx`, `06_콘텐츠_마스터.xlsx`, `08_카드_데이터베이스.xlsx` (+ 선택 `다크레기온_마스터통합.xlsx`)

---

## 9. 역방향 정리 로그 (Monsters 118 -> 97)

| 항목 | 내용 |
|------|------|
| 대상 파일 | `09_몬스터_바이블.xlsx` |
| 기준 | 문서 정본(97종 분포) 기준으로 역방향 정리 |
| 본 시트 결과 | `Monsters_Master` 97종 (헤더 제외 97행) |
| 백업 | `Archive_118to97` 시트에 제외 21종 전체 백업 |
| 연동 정리 | `Fusion_Recipes`/`Fusion_RandomPool`에서 제거 ID 참조 정리 완료 |
| 통합본 동기화 | `다크레기온_마스터통합.xlsx` `Fusion_RandomPool`을 09 정본 기준으로 동기화 |

### 9.1 제외 ID (21종)

| 클래스 | 제외 ID |
|------|------|
| NEC | `MON_NEC_L2_010`, `MON_NEC_L3_011`, `MON_NEC_L3_012` |
| ELE | `MON_ELE_L2_010`, `MON_ELE_L3_011`, `MON_ELE_L3_012` |
| BEA | `MON_BEA_L2_010`, `MON_BEA_L3_011`, `MON_BEA_L3_012` |
| HOL | `MON_HOL_L2_007`, `MON_HOL_L2_008`, `MON_HOL_L2_009`, `MON_HOL_L2_010`, `MON_HOL_L3_011`, `MON_HOL_L3_012` |
| NEU | `MON_NEU_L2_007`, `MON_NEU_L2_008`, `MON_NEU_L3_001`, `MON_NEU_L3_002`, `MON_NEU_L3_003`, `MON_NEU_L4_001` |

### 9.2 참조 보정 메모

- `Fusion_Recipes`: 제거 ID 참조 0건 유지
- `Fusion_RandomPool`: 제거 후 끊긴 3건을 기존 ID로 보정
  - `MON_HOL_L3_003` -> `MON_HOL_L3_002`
  - `MON_HOL_L3_004` -> `MON_HOL_L3_001`
  - `MON_NEU_L2_003` -> `MON_NEU_L2_002`

### 9.3 최종 검증

- 문서 링크 무결성: 깨진 링크 0건
- Monsters_Master: 97종
- Fusion_Recipes invalid ref: 0
- Fusion_RandomPool invalid ref: 0
- 통합본 Fusion_RandomPool invalid ref: 0

---

## 10. 선택사항 전부 반영 로그

| 파일 | 반영 내용 |
|------|-----------|
| `35_운영_검증_팩.xlsx` | `밸런스_백로그`, `플레이테스트_로그` 시트 추가 |
| `34_리스크_추적표.xlsx` | `후속_리스크` 시트 추가 |
| `다크레기온_마스터통합.xlsx` | `Fusion_RandomPool`을 `09_몬스터_바이블.xlsx` 기준으로 동기화 |
| `README.md` / `00_문서_인덱스.md` | 운영 보강 시트 사용 안내 및 설명 반영 |

### 10.1 운영 보강 시트 사용 의도

- `밸런스_백로그`: 위험 카드군(OTK/루프 가능성) 가설 기반 튜닝 트래킹
- `플레이테스트_로그`: 런 단위 결과(시드/턴수/이슈/액션) 누적 기록
- `후속_리스크`: `R-06`, `R-08`, `R-09` 등 High 리스크 후속 증거 관리
