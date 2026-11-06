#!/usr/bin/env python3
"""다크 레기온 설계 정합성 검사.

사용법 (저장소 루트에서):
    python3 tools/check_design.py            # 기본 경로 docs/design
    python3 tools/check_design.py <설계폴더>

실패한 규칙이 하나라도 있으면 exit code 1.
검사 0건인 규칙 그룹(EMPTY)도 실패로 본다. 조용히 건너뛴 규칙을 잡기 위해서다.
출력 맨 위에 검사한 커밋, 아래에 규칙 그룹별 통과·실패 건수를 찍는다.
PR 본문에는 이 출력을 통째로 붙인다.
의존성: openpyxl
"""
import collections
import math
import pathlib
import re
import subprocess
import sys

import openpyxl

DESIGN = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else "docs/design")

# 정본 기대값 (01 §7)
BOSS = {  # floor: (HP, ATK, DEF)  D-05 / D-09
    1: (47, 8, 1), 2: (59, 9, 2), 3: (73, 11, 2), 4: (87, 14, 3), 5: (101, 16, 3),
}
STACK_MAX = {"Burn": 20, "Poison": 20}  # D-08
START_CLASS_CUM = {  # D-07 / D-17
    None: [32, 48, 68, 86, 92],
    "ELE": [50, 54, 70, 88, 92],
    "HOL": [52, 68, 86, 88, 92],
    "BEA": [52, 64, 70, 86, 92],
}
TURN_LIMITED_RELICS = ("REL_BLOOD_CHALICE", "REL_BOSS_1F", "REL_BOSS_5F")  # D-04 / D-15
NEC_ACT_001_BONUS = 1  # D-20

EFFECT_COLS = ("OnSummon", "Passive", "OnDeath", "OnAttack", "Special")


def split_sentences(text):
    """마침표 기준 문장 분리. 괄호 안 보충 설명은 앞 문장에 붙인다."""
    parts = re.split(r"(?<=다\.)\s+(?!\()", text.strip())
    return [s.strip() for s in parts if s.strip()]


def classify_effect(kind, sentence):
    """D-25 분류 계약. kind: '효과' | '패시브'. 반환: 효과 칸 이름."""
    s = sentence
    if s.startswith("[Deathrattle]"):
        return "OnDeath"
    if kind == "효과" and s.startswith("소환 시"):
        return "OnSummon"  # "소환 시 아군을 희생하면 …"도 소환 시 발동 (NEC_MON_007)
    if re.search(r"이 몬스터(가|를|이)? ?(사망|희생|파괴|죽)", s):
        return "OnDeath"  # 이 몬스터 자신의 사망·희생만. 아군 사망·아군 희생은 해당 없음
    if kind == "효과":
        return "Special"
    if re.search(r"공격할 때마다|공격 시|공격으로", s):
        return "OnAttack"
    return "Passive"


def classify_card(fields):
    """17 카드 항목(dict) → {칸: 문장들}. 키워드는 Special에 [키워드]로 둔다."""
    out = {c: [] for c in EFFECT_COLS}
    for kind in ("효과", "패시브"):
        v = (fields.get(kind) or "").strip()
        if v and v not in ("없음", "—"):
            for s in split_sentences(v):
                out[classify_effect(kind, s)].append(s)
    kw = (fields.get("키워드") or "").strip()
    if kw and kw not in ("없음", "—"):
        out["Special"].append(f"[키워드] {kw}")
    return {c: " / ".join(v) if v else None for c, v in out.items()}


def parse_17(text):
    ent = {}
    for m in re.finditer(r"#### \[([A-Z]+_MON_\d+)\][^\n]*\n((?:- \*\*[^\n]*\n)+)", text):
        ent[m.group(1)] = dict(re.findall(r"- \*\*([^*]+)\*\*: ([^\n]*)", m.group(2)))
    return ent


failures = []
passes = 0


def check(rule, ok, where="", got=None, want=None):
    global passes
    if ok:
        passes += 1
    else:
        failures.append(f"[FAIL] {rule} | {where} | got={got!r} want={want!r}")


_cache = {}


def sheet(fname, sname):
    key = (fname, sname)
    if key not in _cache:
        wb = openpyxl.load_workbook(DESIGN / fname, read_only=True, data_only=True)
        rows = [r for r in wb[sname].iter_rows(values_only=True)]
        hdr = rows[0]
        _cache[key] = [dict(zip(hdr, r)) for r in rows[1:] if r and r[0] is not None]
    return _cache[key]


def md(name):
    return (DESIGN / name).read_text(encoding="utf-8")


def floor_of(boss_id):
    return int(re.search(r"(\d)F", boss_id).group(1))


# ---------- R1 보스 스탯 ----------
def r1_boss():
    for f, s in (("10_적_바이블.xlsx", "Enemies_Master"), ("다크레기온_마스터통합.xlsx", "Enemies")):
        for r in sheet(f, s):
            if "BOS" in r["ENE_ID"]:
                fl = floor_of(r["ENE_ID"])
                check("R1 boss stats", (r["HP"], r["ATK"], r["DEF"]) == BOSS[fl], f"{f}/{s}/{r['ENE_ID']}",
                      (r["HP"], r["ATK"], r["DEF"]), BOSS[fl])
    for f, s in (("10_적_바이블.xlsx", "Boss_List"), ("14_보스_패턴.xlsx", "Boss_List"), ("14_보스_패턴.xlsx", "Boss_Stats")):
        for r in sheet(f, s):
            fl = r.get("Floor") or floor_of(r["Boss_ID"])
            check("R1 boss stats", (r["HP"], r["ATK"], r["DEF"]) == BOSS[fl], f"{f}/{s}/{r['Boss_ID']}",
                  (r["HP"], r["ATK"], r["DEF"]), BOSS[fl])
    for r in sheet("04_밸런스_테이블.xlsx", "Floor_Scale"):
        if str(r.get("Stage", "")).endswith("-9"):
            fl = int(r["Floor"])
            check("R1 Floor_Scale boss", (r["BossHP"], r["BossATK"]) == BOSS[fl][:2], f"04/Floor_Scale/{r['Stage']}",
                  (r["BossHP"], r["BossATK"]), BOSS[fl][:2])
    for r in sheet("14_보스_패턴.xlsx", "Boss_Phase"):
        want = BOSS[floor_of(r["Boss_ID"])][1]
        for k, v in r.items():
            for n in re.findall(r"ATK ?(\d+)", str(v)):
                check("R1 boss phase text ATK", int(n) == want, f"14/Boss_Phase/{r['Boss_ID']} P{r['Phase']}/{k}", int(n), want)
    for r in sheet("14_보스_패턴.xlsx", "Boss_TurnCycle"):
        if r["Action"] in ("SingleAttack", "LifeSteal"):
            want = BOSS[floor_of(r["Boss_ID"])][1]
            check("R1 boss turn damage = ATK", r["Damage"] == want,
                  f"14/Boss_TurnCycle/{r['Boss_ID']} P{r['Phase']} T{r['TurnIndex']}", r["Damage"], want)


# ---------- R2 희생 Crystal ----------
def r2_sacrifice():
    for r in sheet("06_콘텐츠_마스터.xlsx", "Relics_MVP"):
        if r["Relic_ID"] in TURN_LIMITED_RELICS:
            check("R2 relic once-per-turn", "턴당 첫" in str(r["Effect"]), f"06/Relics_MVP/{r['Relic_ID']}",
                  r["Effect"], "'턴당 첫' 포함")
    se = {r["Action"]: r for r in sheet("04_밸런스_테이블.xlsx", "Sacrifice_Economy")}
    check("R2 fragment +1", str(se["수정 골렘 파편"]["CrystalGain"]) == "+1", "04/Sacrifice_Economy",
          se["수정 골렘 파편"]["CrystalGain"], "+1")
    for f in ("08_카드_데이터베이스.xlsx", "다크레기온_마스터통합.xlsx"):
        for r in sheet(f, "Cards_Effect"):
            if r["Card_ID"] == "NEC_ACT_001":
                check("R2 NEC_ACT_001 bonus", r["Value1"] == NEC_ACT_001_BONUS, f"{f}/Cards_Effect", r["Value1"], NEC_ACT_001_BONUS)
    for name in ("13_희생_융합_시스템_상세.md", "17_카드_데이터베이스.md", "16_밸런스_규칙.md"):
        bad = re.findall(r"파편[^\n]{0,30}(?:희생 \+2|:\+2|Crystal 2를)", md(name))
        check("R2 fragment +2 text", not bad, name, bad, [])
    t24 = md("24_콘텐츠_런타임_상세.md")
    for rid in ("REL_BLOOD_CHALICE", "REL_BOSS_1F"):
        line = next((l for l in t24.splitlines() if l.startswith(f"| {rid} ")), "")
        check("R2 24 runtime once-per-turn", "FirstSacrificeThisTurn" in line, f"24/{rid}", line[:80], "FirstSacrificeThisTurn")


# ---------- R3 상태이상 상한 ----------
def r3_status():
    for f in ("12_상태이상_마스터.xlsx", "다크레기온_마스터통합.xlsx"):
        rows = sheet(f, "Status_Master")
        for r in rows:
            name = next((v for v in r.values() if isinstance(v, str) and v in STACK_MAX), None)
            if not name:
                continue
            mx = next((v for k, v in r.items() if k and "max" in str(k).lower()), None)
            check("R3 stack max", mx == STACK_MAX[name], f"{f}/Status_Master/{name}", mx, STACK_MAX[name])
    for name in ("04_시스템_설계.md", "16_밸런스_규칙.md"):
        line = next((l for l in md(name).splitlines() if l.startswith("| Poison")), "")
        check("R3 Poison max 20 in MD", "20" in line, name, line[:80], "최대 20")
    sec = re.search(r"### Poison[^\n]*\n(.*?)\n### ", md("11_상태이상_상세설계.md"), re.S)
    check("R3 Poison max 20 in MD", bool(sec) and "최대 20" in sec.group(1), "11_상태이상_상세설계.md", None, "최대 20")


# ---------- R4 카드 해금 ----------
def r4_unlock():
    for f in ("08_카드_데이터베이스.xlsx", "다크레기온_마스터통합.xlsx"):
        cards = [r for r in sheet(f, "Cards_FloorUnlock") if r["UnlockFloor"] is not None]
        check("R4 drop pool size", len(cards) == 92, f"{f}/Cards_FloorUnlock", len(cards), 92)
        for cls in ("ELE", "HOL", "BEA"):
            miss = [r["Card_ID"] for r in cards if r["Card_ID"].startswith(cls) and r["UnlockFloor_IfStartClass"] != 1]
            check("R4 IfStartClass=1", not miss, f"{f}/{cls}", miss[:3], [])
        for start, want in START_CLASS_CUM.items():
            eff = collections.Counter()
            for r in cards:
                g = r["UnlockFloor_IfStartClass"]
                eff[g if (start and r["Card_ID"].startswith(start) and g) else r["UnlockFloor"]] += 1
            got = [sum(eff[k] for k in range(1, fl + 1)) for fl in range(1, 6)]
            check("R4 cumulative", got == want, f"{f}/start={start or 'NEC·NEU'}", got, want)
        wb = openpyxl.load_workbook(DESIGN / f, read_only=True, data_only=True)
        summ = {}
        for row in wb["Floor_Pool_Summary"].iter_rows(values_only=True):
            if row and isinstance(row[0], str) and row[0].startswith(("Default", "Start ")) and isinstance(row[1], int):
                key = None if row[0].startswith("Default") else row[0].split()[1]
                summ[key] = list(row[1:6])
        for start, want in START_CLASS_CUM.items():
            check("R4 Floor_Pool_Summary values", summ.get(start) == want, f"{f}/Floor_Pool_Summary/{start or 'Default'}",
                  summ.get(start), want)


# ---------- R5 융합 ----------
def r5_fusion():
    card = {r["MON_ID"]: r["Card_ID"] for r in sheet("09_몬스터_바이블.xlsx", "Monsters_Master")}
    for r in sheet("09_몬스터_바이블.xlsx", "Fusion_Recipes"):
        if r["Weight"]:
            for k in ("MaterialA", "MaterialB", "Result_MON_ID"):
                check("R5 active recipe has cards", card.get(r[k]) is not None, f"09/Fusion_Recipes/{r['Recipe_ID']}/{k}", r[k], "Card_ID")
    pools = collections.defaultdict(lambda: collections.Counter())
    for f in ("09_몬스터_바이블.xlsx", "다크레기온_마스터통합.xlsx"):
        for r in sheet(f, "Fusion_RandomPool"):
            if r["OutputClass"] == "MUT":
                check("R5 mutation weight 0", not r["Weight"], f"{f}/{r['Pool_ID']}", r["Weight"], 0)
            elif r["Weight"]:
                check("R5 pool result has card", card.get(r["ResultMON_ID"]) is not None, f"{f}/{r['Pool_ID']}", r["ResultMON_ID"], "Card_ID")
            if f.startswith("09") and "+" not in str(r["InputClass"]) and r["Weight"]:
                pools[(r["InputClass"], r["InputLevel"])]["CLS" if r["OutputClass"] == r["InputClass"] else r["OutputClass"]] += r["Weight"]
    for (ic, lv), c in pools.items():
        want = {"CLS": 100} if (ic == "NEU" or lv != "Lv1") else {"CLS": 80, "NEU": 20}
        check("R5 pool split (D-21)", dict(c) == want, f"09/Fusion_RandomPool/{ic} {lv}", dict(c), want)
    a = [(r["Pool_ID"], r["Weight"]) for r in sheet("09_몬스터_바이블.xlsx", "Fusion_RandomPool")]
    b = [(r["Pool_ID"], r["Weight"]) for r in sheet("다크레기온_마스터통합.xlsx", "Fusion_RandomPool")]
    check("R5 master RandomPool sync", a == b, "09 vs 마스터통합", len(b), len(a))


# ---------- R6 카드 수·동기화 ----------
def r6_cards():
    cm = sheet("08_카드_데이터베이스.xlsx", "Cards_Master")
    check("R6 Cards_Master 98", len(cm) == 98, "08/Cards_Master", len(cm), 98)
    a = [tuple(r.values()) for r in cm]
    b = [tuple(r.values()) for r in sheet("다크레기온_마스터통합.xlsx", "Cards")]
    check("R6 master Cards sync", a == b, "08 Cards_Master vs 마스터통합 Cards", len(b), len(a))
    for s in ("Cards_Effect", "Cards_Upgrade"):
        a = {r["Card_ID"]: tuple(r.values()) for r in sheet("08_카드_데이터베이스.xlsx", s)}
        b = {r["Card_ID"]: tuple(r.values()) for r in sheet("다크레기온_마스터통합.xlsx", s)}
        diff = sorted(k for k in a.keys() | b.keys() if a.get(k) != b.get(k))
        check(f"R6 master {s} sync", not diff, "08 vs 마스터통합", diff[:5], [])


# ---------- R7 금지 문구 ----------
def r7_text():
    for p in sorted(DESIGN.glob("*.md")):
        t = p.read_text(encoding="utf-8")
        for m in re.finditer(r"[^\n]*1\.5625[^\n]*", t):
            line = m.group(0)
            if "~~" in line or "1.25²" in line or "아니" in line or "틀" in line:
                continue
            check("R7 no 1.5625", False, p.name, line.strip()[:80], "Weak×Vulnerable = 0.9375")


    t15 = md("15_Battle_Simulator_설계.md")
    check("R7 15 simulator flags", "< 20% or > 80%" not in t15 and "RUN_CLEAR" in t15, "15 §5", None, "D-04/D-14 플래그")


# ---------- R8 D-01 스모크 ----------
def dmg(base, weak=False, vuln=False, df=0, crit=False):
    x = base * (1.5 if crit else 1) * (0.75 if weak else 1) - df
    return max(1, math.floor(x * (1.25 if vuln else 1)))


def r8_damage():
    cases = [  # (args, want, note)
        (dict(base=5, df=2), 3, "구울 ATK5 → 뼈 병사 Front"),
        (dict(base=3, df=2), 1, "뼈 병사 ATK3 → Front DEF1+1"),
        (dict(base=5, weak=True, vuln=True, df=2), 2, "해골 Weak → Front DEF2 Vuln"),
        (dict(base=8, df=2), 6, "1F 보스 ATK8 → 뼈 병사 Front"),
        (dict(base=8, df=5), 3, "1F 보스 ATK8 → 역병 쥐 Front"),
    ]
    for args, want, note in cases:
        check("R8 D-01", dmg(**args) == want, note, dmg(**args), want)


# ---------- R9 이벤트 ----------
def r9_events():
    ids = {r["Event_ID"] for r in sheet("06_콘텐츠_마스터.xlsx", "Events_MVP")}
    check("R9 events 8", len(ids) == 8, "06/Events_MVP", len(ids), 8)
    for f in ("06_콘텐츠_마스터.xlsx", "다크레기온_마스터통합.xlsx"):
        rows = sheet(f, "Event_FloorPool")
        check("R9 pool ids", {r["Event_ID"] for r in rows} == ids, f"{f}/Event_FloorPool",
              sorted({r["Event_ID"] for r in rows} - ids), [])
        for fl in range(1, 6):
            tot = sum(int(str(r[f"Floor_{fl}"]).rstrip("%")) for r in rows)
            active = sum(1 for r in rows if str(r[f"Floor_{fl}"]) != "0%")
            check("R9 floor sum 100", tot == 100, f"{f}/Floor_{fl}", tot, 100)
            check("R9 active 6", active == 6, f"{f}/Floor_{fl}", active, 6)
        for r in rows:
            common = r["Note"] == "공통"
            for fl in range(1, 6):
                v = str(r[f"Floor_{fl}"])
                ok = v == "14%" if common else v in ("22%", "0%")
                check("R9 D-23 weights", ok, f"{f}/{r['Event_ID']}/Floor_{fl}", v, "14%" if common else "22% or 0%")


# ---------- R10 Exhaust MD ↔ Excel ----------
def r10_exhaust():
    t = md("17_카드_데이터베이스.md")
    for r in sheet("08_카드_데이터베이스.xlsx", "Cards_Effect"):
        m = re.search(r"#### \[" + r["Card_ID"] + r"\][^\n]*\n(?:- \*\*[^\n]*\n)*?- \*\*키워드\*\*: ([^\n]*)", t)
        if not m:
            continue
        check("R10 Exhaust MD=Excel", ("Exhaust" in m.group(1)) == (r["Exhaust"] == "Y"), f"{r['Card_ID']}",
              (m.group(1), r["Exhaust"]), "일치")


# ---------- R11 몬스터 효과 MD ↔ Excel (D-25) ----------
def r11_monster_effects():
    ent = parse_17(md("17_카드_데이터베이스.md"))
    for r in sheet("09_몬스터_바이블.xlsx", "Monsters_Master"):
        cid = r["Card_ID"]
        if not cid or cid not in ent:
            continue
        want = classify_card(ent[cid])
        for col in EFFECT_COLS:
            check("R11 monster effect column", r[col] == want[col], f"09/Monsters_Master/{cid}/{col}",
                  (r[col] or "")[:50], (want[col] or "")[:50])


# ---------- R12 통합본 동기화 ----------
def r12_master_sync():
    a = [tuple(r.values()) for r in sheet("09_몬스터_바이블.xlsx", "Monsters_Master")]
    b = [tuple(r.values()) for r in sheet("다크레기온_마스터통합.xlsx", "Monsters")]
    diff = [x[0] for x, y in zip(a, b) if x != y]
    check("R12 master Monsters sync", len(a) == len(b) and not diff, "09 Monsters_Master vs 마스터통합 Monsters",
          diff[:5] or len(b), len(a))
    wb = openpyxl.load_workbook(DESIGN / "다크레기온_마스터통합.xlsx", read_only=True)
    gap = {r["Sheet"]: r for r in sheet("다크레기온_마스터통합.xlsx", "_GapIndex")}
    for s in wb.sheetnames:
        if s != "_GapIndex":
            check("R12 _GapIndex lists sheet", s in gap, f"마스터통합/{s}", None, "_GapIndex 행")
    for s in ("Relics_MVP", "Events_MVP", "Fusion_Recipes"):
        check("R12 _GapIndex intentional exclusion", s in gap and str(gap[s]["Note"]).startswith("N"),
              f"_GapIndex/{s}", gap.get(s, {}).get("Note"), "N (의도적 제외)")


# ---------- R13 분류 계약 고정 테스트 (손으로 적은 기대값) ----------
CLASSIFY_FIXTURES = [  # (Card_ID, 칸, 그 칸에 있어야 할 문장 일부)
    ("NEC_MON_007", "OnSummon", "소환 시 아군 몬스터 1마리를 희생하면"),
    ("NEC_MON_002", "OnDeath", "[Deathrattle] 무작위 아군 1명"),
    ("ELE_MON_003", "OnDeath", "[Deathrattle] 인접한 적 전체"),
    ("NEU_MON_001", "OnDeath", "이 몬스터를 희생하면 Crystal 1"),
    ("NEU_MON_007", "Passive", "Front 위치에서 아군 전체가 받는 피해"),
    ("NEU_MON_007", "OnDeath", "이 몬스터를 희생하면 Crystal 3"),
    ("NEC_MON_010", "OnAttack", "공격으로 적을 처치할 때마다"),
    ("NEC_MON_012", "Passive", "아군 몬스터가 사망할 때마다"),
    ("ELE_MON_002", "OnAttack", "공격할 때마다 대상에게 Burn 1"),
]
CLASSIFY_UNITS = [  # (kind, 문장, 기대 칸)
    ("효과", "소환 시 아군 몬스터 1마리를 희생하면 카드를 2장 드로우한다.", "OnSummon"),
    ("효과", "[Deathrattle] 적 단일 대상의 Burn 스택을 즉시 폭발(Ignite)시킨다.", "OnDeath"),
    ("패시브", "아군 몬스터가 사망할 때마다 이 몬스터의 HP를 3 회복한다.", "Passive"),
    ("패시브", "이 몬스터를 희생하면 Crystal 3을 획득한다.", "OnDeath"),
    ("패시브", "이 몬스터가 공격으로 적을 처치할 때마다 Crystal을 1 획득한다.", "OnAttack"),
    ("효과", "이 몬스터의 공격은 적 전체를 동시에 타격한다.", "Special"),
]


def r13_classify_contract():
    for kind, s, want in CLASSIFY_UNITS:
        got = classify_effect(kind, s)
        check("R13 classify unit", got == want, s[:40], got, want)
    rows = {r["Card_ID"]: r for r in sheet("09_몬스터_바이블.xlsx", "Monsters_Master") if r["Card_ID"]}
    for cid, col, frag in CLASSIFY_FIXTURES:
        cell = str(rows.get(cid, {}).get(col) or "")
        check("R13 fixture column", frag in cell, f"09/Monsters_Master/{cid}/{col}", cell[:50], frag)
        others = [c for c in EFFECT_COLS if c != col and frag in str(rows.get(cid, {}).get(c) or "")]
        check("R13 fixture not elsewhere", not others, f"09/Monsters_Master/{cid}", others, [])


# ---------- R14 운영 Excel ↔ MD ----------
def r14_ops_xlsx():
    sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
    import build_ops_xlsx
    stale = build_ops_xlsx.run(DESIGN, write=False)
    check("R14 ops xlsx generated from MD", not stale, "운영 Excel 5개", stale,
          "[] (python3 tools/build_ops_xlsx.py로 다시 생성)")


# ---------- R15 문서 구조 ----------
def r15_doc_structure():
    index = md("00_문서_인덱스.md")
    files = sorted(p.relative_to(DESIGN).as_posix() for p in DESIGN.rglob("*") if p.is_file())
    for f in files:
        if f not in ("00_문서_인덱스.md",):
            check("R15 file listed in 00 index", f in index or pathlib.Path(f).name in index, f, None, "00_문서_인덱스.md에 등재")
    numbered = sorted(p for p in DESIGN.glob("[0-9][0-9]_*.md") if not p.name.startswith("00_"))
    for p in numbered:
        t = p.read_text(encoding="utf-8")
        check("R15 no prev/next links", not re.search(r"\*(이전|다음) 문서:", t), p.name, None, "문서 목록 링크로 통일")
        check("R15 footer", t.rstrip().endswith("*문서 목록: [00_문서_인덱스.md](00_문서_인덱스.md)*"), p.name, t.rstrip()[-40:], "문서 목록 footer")
    top = max(int(p.name[:2]) for p in numbered)
    check("R15 index range", f"01~{top:02d}" in index or f"01`~`{top:02d}" in index, "00_문서_인덱스.md", None, f"01~{top:02d}")
    log = DESIGN / "archive" / "35_리네임_매핑_로그.md"
    old_names = []
    if log.exists():
        for a, b in re.findall(r"^\| `([^`]+\.md)` \| `([^`]+\.md)`", log.read_text(encoding="utf-8"), re.M):
            if a != b and not (DESIGN / a).exists():
                old_names.append(a)
    for p in sorted(DESIGN.glob("*.md")):
        t = p.read_text(encoding="utf-8")
        bad = [n for n in old_names if n in t]
        check("R15 no renamed-away file names", not bad, p.name, bad[:3], [])
        for n in set(re.findall(r"\]\(([^)#\s]+\.(?:md|xlsx))\)", t)):
            check("R15 link target exists", (p.parent / n).exists(), p.name, n, "존재하는 파일")
    d01 = md("01_MVP_설계_동결_선언.md")
    rows = [int(x) for x in re.findall(r"^\| D-(\d+) \|", d01, re.M)]
    secs = [int(x) for x in re.findall(r"^### D-(\d+)\.", d01, re.M)]
    check("R15 D-log table contiguous", rows == list(range(1, len(rows) + 1)), "01 §7 표", rows[-3:], f"1..{len(rows)}")
    check("R15 D-log sections match table", secs == rows, "01 §7 본문", secs[-3:], rows[-3:])


RULES = (r1_boss, r2_sacrifice, r3_status, r4_unlock, r5_fusion, r6_cards,
         r7_text, r8_damage, r9_events, r10_exhaust, r11_monster_effects, r12_master_sync,
         r13_classify_contract, r14_ops_xlsx, r15_doc_structure)


def git_revision():
    """검사 대상 폴더의 커밋 해시. 커밋 안 된 변경이 있으면 +dirty."""
    def run(*args):
        return subprocess.run(["git", "-C", str(DESIGN), *args], capture_output=True, text=True, check=True).stdout.strip()
    try:
        rev = run("rev-parse", "--short", "HEAD")
        dirty = run("status", "--porcelain", "--", ".", str(pathlib.Path(__file__).resolve()))
        return rev + ("+dirty" if dirty else "")
    except (OSError, subprocess.CalledProcessError):
        return "unknown (git 아님)"


def main():
    print(f"check_design.py  commit={git_revision()}  design={DESIGN}  rule_groups={len(RULES)}\n")
    rows = []
    for fn in RULES:
        p0, f0 = passes, len(failures)
        try:
            fn()
        except Exception as e:  # 시트·열 이름이 바뀌면 여기서 드러난다
            failures.append(f"[ERROR] {fn.__name__}: {type(e).__name__}: {e}")
        p, f = passes - p0, len(failures) - f0
        if p + f == 0:
            failures.append(f"[EMPTY] {fn.__name__}: 검사 0건 (규칙이 실행되지 않음)")
            f = 1
        new = failures[f0:]
        status = ("OK" if not new else "ERROR" if any(x.startswith("[ERROR]") for x in new)
                  else "EMPTY" if any(x.startswith("[EMPTY]") for x in new) else "FAIL")
        rows.append((fn.__name__, p, f, status))
    width = max(len(r[0]) for r in rows)
    print(f"{'rule'.ljust(width)}  passed  failed  status")
    for name, p, f, status in rows:
        print(f"{name.ljust(width)}  {p:>6}  {f:>6}  {status}")
    if failures:
        print()
    for line in failures:
        print(line)
    ok_groups = sum(1 for r in rows if r[3] == "OK")
    print(f"\n{passes} passed, {len(failures)} failed ({ok_groups}/{len(RULES)} rule groups OK)")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
