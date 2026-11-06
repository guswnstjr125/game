# 다크 레기온 — MVP 구현 이슈 템플릿

> 목적: 설계 문서의 체크리스트를 실제 개발 이슈로 바로 전환하기 위한 표준 포맷  
> 기준 문서: `02_개발_로드맵.md`, `03_MVP_전층_스펙.md`, `10_카드_런타임_매핑_상세.md`, `16_밸런스_규칙.md`  
> 범위 기준: MVP는 `03_MVP_전층_스펙.md` 정본(단일 경로 9노드, Shop 옵션 치환, Treasure/Mimic Post-MVP)

---

## 1) 공통 이슈 템플릿 (복붙용)

```md
## 제목
[#{ISSUE_NO}] {작업명}

## 작업 요약
- 무엇을 구현/수정하는지 1~3줄

## 범위
- 포함:
  - 
- 제외:
  - 

## 구현 상세
- [ ] 데이터 구조/인터페이스 반영
- [ ] 런타임 로직 반영
- [ ] UI/연출 반영 (필요 시)
- [ ] 예외/폴백 처리

## 완료 조건 (DoD)
- [ ] 설계 문서 기준 동작 일치
- [ ] 기존 기능 회귀 없음
- [ ] 최소 테스트 케이스 통과
- [ ] 체크리스트 업데이트

## 테스트 케이스
- [ ] 정상 흐름:
- [ ] 경계 조건:
- [ ] 실패/예외:

## 참조 문서
- 
```

---

## 2) 브랜치/커밋/MR 규칙 템플릿

### 브랜치명

```txt
feature/#123-battle-core
fix/#124-wave-transition
chore/#125-doc-sync
```

### 커밋 메시지

```txt
feat: implement queue flush order (#123)
fix: handle mimic stage fallback safely (#124)
docs: align mvp scope across docs (#125)
```

### MR 제목/설명 템플릿

```md
제목: [#123] 전투 코어 Queue 처리 구현

## 작업 요약
- Main/Trigger/Death Queue 처리 순서 구현
- Death 우선 Flush 규칙 반영

## 해결 이슈
- Closes #123

## 주요 변경점
- QueueProcessor 처리 순서 고정
- 관련 테스트 추가

## 검토/참고사항
- `06_전투_코어_상세설계.md`, `07_TriggerBus_상세설계.md` 정합성 확인 필요
```

---

## 3) MVP 우선 이슈 백로그 (권장 생성 순서)

### A. 전투 코어/런타임

1. 전투 코어 (`BattleAction`, `BattleContext`)
2. Queue 시스템 (`Main/Trigger/Death` Flush)
3. Runtime 유닛 계층 (`RuntimeUnit/Player/Enemy`)

### B. 카드/상태/AI

4. 카드 시스템 (`CardRegistry`, `Deck/Hand`, `CardResolver`)
5. 상태이상 시스템 (`Burn/Poison/Weak/Vulnerable`)
6. AI/타겟팅 시스템

### C. 콘텐츠 진행

7. 희생/소환 시스템
8. 보스 패턴
9. Stage/Wave 시스템 (5층 x 9노드)
10. 이벤트/보상/상점 치환

### D. 검증/안정화

11. Battle Simulator
12. 밸런스 자동 검증 (OTK/무한루프 포함)

---

## 4) 시스템별 이슈 템플릿 (즉시 사용)

### 4-1. Stage/Wave 구현 이슈

```md
## 제목
[#{ISSUE_NO}] MVP Stage/Wave 5층x9노드 구현

## 작업 요약
- 5층 x 9노드 단일 경로 생성
- Wave2(50%) 및 층 전환 규칙 반영

## 완료 조건 (DoD)
- [ ] 층당 노드 구성(Battle4/Event2/Elite1/Rest1/Boss1) 일치
- [ ] 보스 클리어 시 다음 층 진입 + HP/덱/유물/골드 유지
- [ ] Wave 간 규칙(상태 유지, 토큰 제거, Crystal/패 리셋) 적용
- [ ] 30턴 타임아웃 패배 동작 확인

## 테스트 케이스
- [ ] 1층~5층 연속 진행 시 진행 불가/소프트락 없음
- [ ] Wave2 없는 노드와 있는 노드 모두 정상 동작
- [ ] 턴 30 초과 시 패배 처리/화면 표기 정상

## 참조 문서
- 03_MVP_전층_스펙.md
- 02_개발_로드맵.md
- 06_전투_코어_상세설계.md
```

### 4-2. Card Resolver 구현 이슈

```md
## 제목
[#{ISSUE_NO}] 카드 92장 ResolverSpec 등록 및 CardId 실행 검증

## 작업 요약
- 카드 92장 Resolver 매핑 등록
- CardId 기준 실행/직렬화 규약 고정

## 완료 조건 (DoD)
- [ ] ResolverSpec 92장 누락 없이 등록
- [ ] TargetRule enum 고정(Front/Back/All/Selected/LowestHp)
- [ ] Params 직렬화(`EffectDefinition[]`) 저장/로드 검증
- [ ] 카드명 의존 없이 CardId만으로 실행 검증

## 테스트 케이스
- [ ] 임의 카드 N장 샘플 실행
- [ ] 저장 후 재시작 시 동일 동작
- [ ] 잘못된 CardId 입력 시 안전 실패 처리

## 참조 문서
- 10_카드_런타임_매핑_상세.md
- 17_카드_데이터베이스.md
```

### 4-3. 보상/상점/Mimic 폴백 이슈

```md
## 제목
[#{ISSUE_NO}] MVP 보상 규칙 및 상점 치환/ Mimic 폴백 구현

## 작업 요약
- Reward_BattleTable 우선 적용
- 상점 옵션 치환(2층+ 노드6 Event 30%) 적용
- Mimic 입력 안전 폴백(Treasure 처리) 반영

## 완료 조건 (DoD)
- [ ] Normal/Elite/Boss 골드 범위가 층별 표와 일치
- [ ] 상점 치환 확률/조건 정상
- [ ] StageType.Mimic 입력 시 크래시 없이 Treasure 처리
- [ ] 실제 MVP 맵에는 Mimic 노드가 생성되지 않음

## 테스트 케이스
- [ ] 층별 보상 표 샘플 검증
- [ ] 상점 치환 확률 시뮬레이션
- [ ] Mimic 강제 입력 회귀 테스트

## 참조 문서
- 03_MVP_전층_스펙.md
- 23_콘텐츠_설계.md
- 24_콘텐츠_런타임_상세.md
- 25_MVP_보완_시스템_상세.md
```

### 4-4. 밸런스 검증 이슈

```md
## 제목
[#{ISSUE_NO}] MVP 밸런스 검증 자동화 (PP/승률/루프)

## 작업 요약
- PP/코스트/상태이상 기준 검증
- Simulator 기반 승률/OTK/무한루프 탐지

## 완료 조건 (DoD)
- [ ] `16_밸런스_규칙.md` 체크리스트 항목 자동/반자동 검증 가능
- [ ] 승률 목표 범위 리포트 출력
- [ ] OTK/무한루프 탐지 결과 저장

## 테스트 케이스
- [ ] 밸런스 주의 카드 세트 검증
- [ ] 실패 케이스 재현 가능

## 참조 문서
- 16_밸런스_규칙.md
- 15_Battle_Simulator_설계.md
```

---

## 5) 빠른 운영 체크

- 이슈 생성 시 제목에 `#이슈번호` 포함
- 커밋 prefix는 `feat/fix/chore/refactor/docs`만 사용
- 커밋 메시지에 이슈 번호 포함
- MR 본문에 작업 요약/해결 이슈/주요 변경점/검토사항 포함
