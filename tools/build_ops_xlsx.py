#!/usr/bin/env python3
"""운영 Excel 5개를 설계 MD에서 생성한다.

사용법 (저장소 루트에서):
    python3 tools/build_ops_xlsx.py            # docs/design의 운영 Excel을 다시 쓴다
    python3 tools/build_ops_xlsx.py --check    # 쓰지 않고, MD와 다른 시트만 출력 (exit 1)

원칙
- 표의 내용은 MD가 정본이다. Excel을 직접 고치지 말고 MD를 고친 뒤 다시 생성한다.
- 사람이 채우는 칸(상태·담당·메모·근거·최근 업데이트 등)은 기존 Excel 값을 키(No 항목 문구·ID)로 찾아 보존한다.
- 사람이 채우는 시트(밸런스_백로그·플레이테스트_로그·후속_리스크)는 건드리지 않는다.
의존성: openpyxl
"""
import pathlib
import re
import sys

import openpyxl
from openpyxl.styles import Font

# ---------- MD 파서 ----------


def section(text, heading_re):
    """heading_re에 맞는 제목부터 같은 수준 이하의 다음 제목 전까지."""
    lines = text.split("\n")
    for i, line in enumerate(lines):
        m = re.match(r"(#+) ", line)
        if m and re.search(heading_re, line):
            level = len(m.group(1))
            out = []
            for nxt in lines[i + 1:]:
                m2 = re.match(r"(#+) ", nxt)
                if m2 and len(m2.group(1)) <= level:
                    break
                out.append(nxt)
            return "\n".join(out)
    raise KeyError(f"heading not found: {heading_re}")


def tables(text):
    """text 안의 마크다운 표들을 [[행...], ...] 목록으로."""
    found, cur = [], []
    for line in text.split("\n") + [""]:
        if line.startswith("|"):
            cells = [c.strip() for c in line.strip().strip("|").split("|")]
            if not all(re.fullmatch(r":?-+:?", c) for c in cells):
                cur.append(cells)
        elif cur:
            found.append(cur)
            cur = []
    return found


def first_table(text, heading_re):
    return tables(section(text, heading_re))[0]


def checklist(text, heading_re):
    return [m.group(1).strip() for m in re.finditer(r"^- \[[ xX]\] (.+)$", section(text, heading_re), re.M)]


def issues(text):
    """31 이슈 초안 → [번호, 단계, 제목, 요약, 완료 조건, 테스트]."""
    rows = []
    for m in re.finditer(r"^## #(\d+) [^\n]*?— ([^\n]+)\n(.*?)(?=^## |\Z)", text, re.M | re.S):
        body = m.group(3)

        def field(name):
            f = re.search(rf"^- \*\*{name}\*\*: (.+)$", body, re.M)
            return f.group(1).strip() if f else ""

        def bullets(name):
            b = re.search(rf"^- \*\*{name}\*\*\n((?:  - .+\n?)+)", body, re.M)
            return "\n".join(x.strip()[2:] for x in b.group(1).strip("\n").split("\n")) if b else ""

        rows.append([m.group(1), m.group(2).strip(), field("제목"), field("요약"), bullets("완료 조건"), bullets("테스트")])
    return rows


# ---------- 시트 정의 ----------


def spec(design):
    md = {n: (design / n).read_text(encoding="utf-8") for n in (
        "03_MVP_전층_스펙.md", "10_카드_런타임_매핑_상세.md", "21_적_스킬_의도_상세.md",
        "31_MVP_이슈_초안.md", "32_MVP_검증_시나리오_매트릭스.md", "34_릴리즈_리스크_레지스터.md")}
    s03, s10, s21 = md["03_MVP_전층_스펙.md"], md["10_카드_런타임_매핑_상세.md"], md["21_적_스킬_의도_상세.md"]
    s31, s32, s34 = md["31_MVP_이슈_초안.md"], md["32_MVP_검증_시나리오_매트릭스.md"], md["34_릴리즈_리스크_레지스터.md"]

    def numbered(items, header, default):
        return [header] + [[i + 1, it, *default] for i, it in enumerate(items)]

    floors = {1: r"### 3\.1 웨이브", 2: r"### 4\.1 웨이브", 3: r"### 5\.1 웨이브", 4: r"### 6\.1 웨이브", 5: r"### 7\.1 웨이브"}
    ai = {1: r"## 2\. 1층", 2: r"## 3\. 2층", 3: r"## 4\. 3층", 4: r"## 5\. 4층", 5: r"## 6\. 5층"}
    risk = first_table(s34, r"## 2\. 리스크 목록")

    scen = [["영역", "ID", "단계", "시나리오", "기대 결과", "기준 문서"]]
    for m in re.finditer(r"^## (\d+\. [^\n]+)$", s32, re.M):
        tb = tables(section(s32, re.escape(m.group(0))))
        if tb and tb[0][0][:2] == ["ID", "단계"]:
            scen += [[m.group(1), *r] for r in tb[0][1:]]

    S = {}
    S[("03_전층_운영_체크리스트.xlsx", "노드_템플릿")] = dict(rows=first_table(s03, r"### 노드 타입"))
    for fl, h in floors.items():
        S[("03_전층_운영_체크리스트.xlsx", f"층{fl}_웨이브")] = dict(rows=first_table(s03, h))
    S[("03_전층_운영_체크리스트.xlsx", "구현_체크리스트")] = dict(
        rows=numbered(checklist(s03, r"## 9\. 구현 체크리스트"), ["No", "체크 항목", "상태", "메모"], ["TODO", None]),
        key="체크 항목", keep=("상태", "메모"))
    S[("10_카드_리졸버_작업표.xlsx", "Skill_매핑")] = dict(rows=first_table(s10, r"## 3\. Skill"))
    S[("10_카드_리졸버_작업표.xlsx", "Action_매핑")] = dict(rows=first_table(s10, r"## 4\. Action"))
    S[("10_카드_리졸버_작업표.xlsx", "진행_체크")] = dict(
        rows=numbered(checklist(s10, r"## 6\. 구현 체크리스트"), ["No", "체크 항목", "상태", "담당/메모"], ["TODO", None]),
        key="체크 항목", keep=("상태", "담당/메모"))
    for fl, h in ai.items():
        S[("21_적_AI_작업표.xlsx", f"층{fl}_AI")] = dict(rows=first_table(s21, h))
    S[("21_적_AI_작업표.xlsx", "데이터_키")] = dict(rows=first_table(s21, r"## 7\. 데이터 키"))
    S[("34_리스크_추적표.xlsx", "등급_기준")] = dict(rows=first_table(s34, r"## 1\. 등급 기준"))
    S[("34_리스크_추적표.xlsx", "리스크_목록")] = dict(
        rows=[risk[0] + ["최근 업데이트", "조치 완료일", "비고"]] + [r + [None, None, None] for r in risk[1:]],
        key="ID", keep=("상태", "최근 업데이트", "조치 완료일", "비고"))
    S[("34_리스크_추적표.xlsx", "게이트_체크")] = dict(
        rows=numbered(checklist(s34, r"## 3\. 게이트 체크"), ["No", "항목", "상태", "근거"], ["TODO", None]),
        key="항목", keep=("상태", "근거"))
    S[("35_운영_검증_팩.xlsx", "검증_시나리오")] = dict(rows=scen)
    S[("35_운영_검증_팩.xlsx", "릴리즈_리스크")] = dict(rows=risk, key="ID", keep=("상태",))
    S[("35_운영_검증_팩.xlsx", "이슈_초안")] = dict(
        rows=[["이슈번호", "단계", "제목", "요약", "완료 조건", "테스트"]] + issues(s31), replaces="이슈_초안_1_12")
    S[("35_운영_검증_팩.xlsx", "엑셀_검증_스냅샷")] = dict(rows=[
        ["안내"],
        ["이 시트는 더 이상 갱신하지 않는다. 설계 데이터 정합은 저장소 루트에서 `python3 tools/check_design.py`로 확인하고, 출력 전체를 PR 본문에 붙인다."],
    ])
    return S


# ---------- 병합·비교 ----------


def norm(v):
    return "" if v is None else str(v)


def merged_rows(ws, sp):
    """MD 행에 기존 시트의 보존 칸을 덮어쓴 최종 행."""
    rows = [list(r) for r in sp["rows"]]
    if ws is None or "key" not in sp:
        return rows
    hdr = rows[0]
    old = [list(r) for r in ws.iter_rows(values_only=True)]
    if not old:
        return rows
    ohdr = [norm(c) for c in old[0]]
    if sp["key"] not in ohdr:
        return rows
    ok = ohdr.index(sp["key"])
    prev = {norm(r[ok]): r for r in old[1:] if len(r) > ok and r[ok] is not None}
    ki = hdr.index(sp["key"])
    for r in rows[1:]:
        o = prev.get(norm(r[ki]))
        if not o:
            continue
        for col in sp["keep"]:
            if col in ohdr and col in hdr and ohdr.index(col) < len(o) and o[ohdr.index(col)] not in (None, ""):
                r[hdr.index(col)] = o[ohdr.index(col)]
    return rows


def current_rows(ws):
    rows = [[norm(c) for c in r] for r in ws.iter_rows(values_only=True)]
    while rows and not any(rows[-1]):
        rows.pop()
    return [r[:max((i + 1 for i, c in enumerate(r) if c), default=0)] for r in rows]


def as_cmp(rows):
    return [[norm(c) for c in r][:max((i + 1 for i, c in enumerate(r) if c not in (None, "")), default=0)] for r in rows]


def run(design, write):
    """write=False면 다른 시트 목록만 반환."""
    diffs = []
    S = spec(design)
    books = {}
    for (fname, sname), sp in S.items():
        wb = books.get(fname) or openpyxl.load_workbook(design / fname)
        books[fname] = wb
        old_name = sp.get("replaces")
        ws = wb[sname] if sname in wb.sheetnames else (wb[old_name] if old_name and old_name in wb.sheetnames else None)
        want = merged_rows(ws, sp)
        if ws is not None and ws.title == sname and current_rows(ws) == as_cmp(want):
            continue
        diffs.append(f"{fname} / {sname}")
        if not write:
            continue
        idx = wb.sheetnames.index(ws.title) if ws is not None else len(wb.sheetnames)
        if ws is not None:
            wb.remove(ws)
        new = wb.create_sheet(sname, idx)
        for r in want:
            new.append(r)
        for c in new[1]:
            c.font = Font(bold=True)
    if write:
        for fname, wb in books.items():
            wb.save(design / fname)
    return diffs


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    design = pathlib.Path(args[0] if args else "docs/design")
    check = "--check" in sys.argv
    diffs = run(design, write=not check)
    for d in diffs:
        print(("[STALE] " if check else "[WROTE] ") + d)
    print(f"{len(diffs)} sheet(s) {'differ from MD' if check else 'regenerated'}")
    return 1 if (check and diffs) else 0


if __name__ == "__main__":
    sys.exit(main())
