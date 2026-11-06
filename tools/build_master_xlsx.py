#!/usr/bin/env python3
"""다크레기온_마스터통합.xlsx를 데이터 정본 Excel에서 생성한다 (이슈 #53).

사용법 (저장소 루트에서):
    python3 tools/build_master_xlsx.py            # 정본과 다르면 마스터통합을 다시 쓴다
    python3 tools/build_master_xlsx.py --check    # 쓰지 않고, 정본과 다른 시트만 출력 (exit 1)

원칙
- 마스터통합은 리뷰용 사본이다. 직접 고치지 말고 정본 Excel을 고친 뒤 다시 생성한다.
- 시트와 원본의 대응은 SOURCES 표 하나에만 둔다. _GapIndex 시트도 이 표로 만든다.
의존성: openpyxl
"""
import pathlib
import sys

import openpyxl
from openpyxl.styles import Font

MASTER = "다크레기온_마스터통합.xlsx"
F04, F06, F08 = "04_밸런스_테이블.xlsx", "06_콘텐츠_마스터.xlsx", "08_카드_데이터베이스.xlsx"
F09, F10, F12 = "09_몬스터_바이블.xlsx", "10_적_바이블.xlsx", "12_상태이상_마스터.xlsx"

# (마스터통합 시트, 원본 파일, 원본 시트). 순서가 곧 시트 순서다
SOURCES = [
    ("Monsters", F09, "Monsters_Master"),
    ("Enemies", F10, "Enemies_Master"),
    ("Cards", F08, "Cards_Master"),
    ("Position_Bonus", F04, "Position_Bonus"),
    ("Player_Stats", F04, "Player_Stats"),
    ("Cards_Effect", F08, "Cards_Effect"),
    ("Cards_Upgrade", F08, "Cards_Upgrade"),
    ("Curse_Cards", F08, "Curse_Cards"),
    ("Cards_FloorUnlock", F08, "Cards_FloorUnlock"),
    ("Floor_Pool_Summary", F08, "Floor_Pool_Summary"),
    ("Fusion_RandomPool", F09, "Fusion_RandomPool"),
    ("Event_FloorPool", F06, "Event_FloorPool"),
    ("Reward_BattleTable", F06, "Reward_BattleTable"),
    ("RestNode_Rules", F06, "RestNode_Rules"),
    ("TreasureNode_Rules", F06, "TreasureNode_Rules"),
    ("MimicNode_Rules", F06, "MimicNode_Rules"),
    ("MapNode_Rest", F06, "MapNode_Rest"),
    ("MapNode_Treasure", F06, "MapNode_Treasure"),
    ("MapNode_Mimic", F06, "MapNode_Mimic"),
    ("Reward_EliteRelic", F06, "Reward_EliteRelic"),
    ("Meta_Unlock", F06, "Meta_Unlock"),
    ("Meta_Currency", F06, "Meta_Currency"),
    ("Meta_Achievement", F06, "Meta_Achievement"),
    ("Status_Master", F12, "Status_Master"),
]
# 일부러 넣지 않는 정본 시트 (마스터통합에서 찾지 말고 원본을 볼 것)
EXCLUDED = [
    ("Relics_MVP", F06), ("Events_MVP", F06), ("Fusion_Recipes", F09),
]


def values(ws):
    """끝의 빈 행·빈 열을 뺀 셀 값 목록."""
    rows = [list(r) for r in ws.iter_rows(values_only=True)]
    while rows and all(v is None for v in rows[-1]):
        rows.pop()
    out = []
    for r in rows:
        last = max([i + 1 for i, v in enumerate(r) if v is not None] or [0])
        out.append(r[:last])
    return out


def gap_index():
    rows = [["Sheet", "Source", "Note"]]
    for name, f, s in SOURCES:
        rows.append([name, f"{f} / {s}", "Y (tools/build_master_xlsx.py로 생성)"])
    for s, f in EXCLUDED:
        rows.append([s, f"{f} / {s}", "N (의도적 제외. 정본 파일을 직접 본다)"])
    return rows


def expected(design):
    books = {}
    out = {}
    for name, f, s in SOURCES:
        if f not in books:
            books[f] = openpyxl.load_workbook(design / f, data_only=True)
        out[name] = values(books[f][s])
    out["_GapIndex"] = gap_index()
    return out


def order():
    names = [n for n, _, _ in SOURCES]
    return names[:3] + ["_GapIndex"] + names[3:]


def run(design, write):
    """정본과 다른 시트 이름 목록을 돌려준다. write=True면 다를 때 파일을 다시 쓴다."""
    design = pathlib.Path(design)
    want = expected(design)
    path = design / MASTER
    stale = []
    if path.exists():
        cur = openpyxl.load_workbook(path, data_only=True)
        if cur.sheetnames != order():
            stale.append("(sheet order)")
        for name in order():
            if name not in cur.sheetnames or values(cur[name]) != want[name]:
                stale.append(name)
    else:
        stale = order()
    if write and stale:
        wb = openpyxl.Workbook()
        wb.remove(wb.active)
        for name in order():
            ws = wb.create_sheet(name)
            for r in want[name]:
                ws.append(r)
            for c in ws[1]:
                c.font = Font(bold=True)
            ws.freeze_panes = "A2"
        wb.save(path)
    return stale


def main():
    design = pathlib.Path(__file__).resolve().parent.parent / "docs" / "design"
    check_only = "--check" in sys.argv
    stale = run(design, write=not check_only)
    for s in stale:
        print(("[STALE] " if check_only else "[WROTE] ") + s)
    print(f"{len(stale)} sheet(s) {'differ from sources' if check_only else 'regenerated'}")
    sys.exit(1 if check_only and stale else 0)


if __name__ == "__main__":
    main()
