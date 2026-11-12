#!/usr/bin/env python3
"""정본 Excel을 Unity 임포터용 JSON으로 내보낸다 (`01` D-40, `38` §2·§3, 이슈 #35).

사용법 (저장소 루트에서):
    python3 tools/export_game_data.py                 # DarkLegion/DesignData/에 JSON을 쓴다
    python3 tools/export_game_data.py --check         # 쓰지 않고, Excel과 다른 JSON만 출력 (exit 1)
    python3 tools/export_game_data.py --out DIR       # 다른 폴더에 쓴다 (테스트용)

원칙
- 내보내는 시트와 이름은 SHEETS 표 하나에만 둔다. `38` §3 표와 같다.
- check_design.py가 0 failed가 아니면 내보내지 않는다 (--check는 예외: 비교만 한다).
- 생성된 JSON은 손으로 고치지 않는다. Excel을 고친 뒤 다시 내보낸다.
의존성: openpyxl
"""
import json
import pathlib
import re
import subprocess
import sys

import openpyxl

ROOT = pathlib.Path(__file__).resolve().parent.parent
DESIGN = ROOT / "docs" / "design"
DEFAULT_OUT = ROOT / "DarkLegion" / "DesignData"

F04, F06, F08 = "04_밸런스_테이블.xlsx", "06_콘텐츠_마스터.xlsx", "08_카드_데이터베이스.xlsx"
F09, F10, F12, F14 = "09_몬스터_바이블.xlsx", "10_적_바이블.xlsx", "12_상태이상_마스터.xlsx", "14_보스_패턴.xlsx"

# (JSON 이름, 원본 파일, 원본 시트) — `38` §3 표와 같은 순서
SHEETS = [
    ("Cards_Master", F08, "Cards_Master"),
    ("Cards_Effect", F08, "Cards_Effect"),
    ("Cards_Upgrade", F08, "Cards_Upgrade"),
    ("Cards_FloorUnlock", F08, "Cards_FloorUnlock"),
    ("Curse_Cards", F08, "Curse_Cards"),
    ("Cards_Text", F08, "Cards_Text"),
    ("Keywords", F08, "Keywords"),
    ("Monsters_Master", F09, "Monsters_Master"),
    ("Fusion_Recipes", F09, "Fusion_Recipes"),
    ("Fusion_RandomPool", F09, "Fusion_RandomPool"),
    ("Enemies_Master", F10, "Enemies_Master"),
    ("Boss_List", F14, "Boss_List"),
    ("Boss_TurnCycle", F14, "Boss_TurnCycle"),
    ("Boss_Phase", F14, "Boss_Phase"),
    ("Relics_MVP", F06, "Relics_MVP"),
    ("Events_MVP", F06, "Events_MVP"),
    ("Event_FloorPool", F06, "Event_FloorPool"),
    ("Reward_BattleTable", F06, "Reward_BattleTable"),
    ("Reward_EliteRelic", F06, "Reward_EliteRelic"),
    ("RestNode_Rules", F06, "RestNode_Rules"),
    ("Meta_Currency", F06, "Meta_Currency"),
    ("Meta_Unlock", F06, "Meta_Unlock"),
    ("Meta_Achievement", F06, "Meta_Achievement"),
    ("Status_Master", F12, "Status_Master"),
    ("Player_Stats", F04, "Player_Stats"),
    ("Position_Bonus", F04, "Position_Bonus"),
    ("Floor_Scale", F04, "Floor_Scale"),
]

PERCENT = re.compile(r"^(-?\d+(?:\.\d+)?)%$")
LEVEL = re.compile(r"^Lv(\d+)$")
EMPTY_MARKS = {"", "—", "-"}


def convert(v):
    """`38` §2 규칙: 백분율 → 소수, Y/N → bool, Lv1 → 1, 빈 칸·대시 → null."""
    if isinstance(v, str):
        s = v.strip()
        if s in EMPTY_MARKS:
            return None
        if s == "Y":
            return True
        if s == "N":
            return False
        m = PERCENT.match(s)
        if m:
            return round(float(m.group(1)) / 100, 6)
        m = LEVEL.match(s)
        if m:
            return int(m.group(1))
        return s
    if isinstance(v, float) and v.is_integer():
        return int(v)
    return v


def rows_of(ws):
    it = ws.iter_rows(values_only=True)
    header = [h for h in next(it)]
    width = max(i + 1 for i, h in enumerate(header) if h is not None)
    header = header[:width]
    out = []
    for raw in it:
        raw = list(raw[:width]) + [None] * (width - len(raw[:width]))
        if raw[0] is None or (isinstance(raw[0], str) and not raw[0].strip()):
            break  # 첫 열(ID)이 비면 끝 (`38` §2)
        out.append({str(h): convert(v) for h, v in zip(header, raw) if h is not None})
    return out


def render(design):
    """{파일 이름: JSON 문자열}"""
    books = {}
    out = {}
    for name, f, s in SHEETS:
        if f not in books:
            books[f] = openpyxl.load_workbook(design / f, data_only=True, read_only=True)
        doc = {"source": f"{f}/{s}", "rows": rows_of(books[f][s])}
        out[f"{name}.json"] = json.dumps(doc, ensure_ascii=False, indent=2) + "\n"
    return out


def run(design, out_dir, write):
    """Excel과 다른 JSON 파일 이름 목록. write=True면 다른 파일을 다시 쓴다."""
    want = render(pathlib.Path(design))
    out_dir = pathlib.Path(out_dir)
    stale = [n for n, text in want.items()
             if not (out_dir / n).exists() or (out_dir / n).read_text(encoding="utf-8") != text]
    extra = sorted(p.name for p in out_dir.glob("*.json") if p.name not in want) if out_dir.exists() else []
    if write and stale:
        out_dir.mkdir(parents=True, exist_ok=True)
        for n in stale:
            (out_dir / n).write_text(want[n], encoding="utf-8")
    return stale, extra


def design_ok():
    r = subprocess.run([sys.executable, str(ROOT / "tools" / "check_design.py")], capture_output=True, text=True)
    return r.returncode == 0, r.stdout.strip().splitlines()[-1:] if r.stdout else []


def main():
    args = sys.argv[1:]
    check_only = "--check" in args
    out_dir = pathlib.Path(args[args.index("--out") + 1]) if "--out" in args else DEFAULT_OUT
    if not check_only:
        ok, last = design_ok()
        if not ok:
            print("check_design.py가 실패해서 내보내지 않는다:", *last)
            sys.exit(1)
    stale, extra = run(DESIGN, out_dir, write=not check_only)
    for n in stale:
        print(("[STALE] " if check_only else "[WROTE] ") + n)
    for n in extra:
        print(f"[EXTRA] {n} (SHEETS 표에 없는 JSON)")
    print(f"{len(stale)} file(s) {'differ from Excel' if check_only else 'written'} → {out_dir}")
    sys.exit(1 if check_only and (stale or extra) else 0)


if __name__ == "__main__":
    main()
