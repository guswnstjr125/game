# 다크 레기온 — MVP 구현 이슈·PR 템플릿

> 목적: 구현 이슈와 PR을 같은 형식으로 쓰기 위한 템플릿. 실제 이슈 목록과 본문은 `31_MVP_이슈_초안.md`이고, GitHub 이슈 #34~#47로 등록되어 있다 (`31` §0).
> 범위 기준: `03_MVP_전층_스펙.md` (단일 경로 9노드, 상점은 2~5층 노드6 고정 `01` D-34, Treasure/Mimic Post-MVP)

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

## 2) 브랜치·커밋·PR 규칙 (GitHub)

### 브랜치

- `main`에 직접 올리지 않는다. 작업 브랜치에서 PR을 연다.
- 이름 예: `feat/34-battle-core`, `fix/41-intent-target`, `docs/49-doc-sync`. 에이전트가 만든 브랜치(`claude/…`, `cursor/…`)도 같은 PR 절차를 따른다.

### 커밋 메시지

```txt
feat: implement queue flush order (#36)
fix: single-target attack skips player while allies remain (#41)
docs: align save rules with D-37 (#49)
```

- 접두어는 `feat` `fix` `refactor` `test` `docs` `chore`만 쓴다.
- 끝에 GitHub 이슈 번호를 붙인다.

### PR 본문

```md
## 작업 요약
- Main/Trigger/Death Queue 처리 순서 구현

## 해결 이슈
- Closes #36

## 변경점
- QueueProcessor 처리 순서 고정, CORE-02 테스트 추가

## 검사
- Unity Test Runner EditMode: 통과 N / 실패 0
- (설계 수치를 바꾼 경우) `python3 tools/check_design.py` 출력 전체

## 관련 결정
- `01` D-NN (결정 주체)
```

- **작성자는 자기 PR을 병합하지 않는다.** 리뷰(Grok 또는 본인 재검토) 뒤 병합한다.
- 사용자 결정이 필요한 항목은 결정 근거를 이슈나 PR 댓글에 남긴 뒤 닫는다 (`.claude/skills/game-design/SKILL.md` §2.4).

---

*문서 목록: [00_문서_인덱스.md](00_문서_인덱스.md)*
