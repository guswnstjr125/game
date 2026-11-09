using System.Collections.Generic;

namespace DarkLegion.Core
{
    public enum CardZone
    {
        DrawPile,
        Hand,
        DiscardPile,
        ExhaustPile,
        /// <summary>소환된 몬스터 카드. 몬스터가 죽으면 버린 더미로 간다 (`04` §4).</summary>
        OnField
    }

    /// <summary>전투 중 카드 한 장 (인스턴스). 정의는 CardId로 데이터에서 찾는다 (`10`).</summary>
    public sealed class RuntimeCard
    {
        public int InstanceId { get; }
        public string CardId { get; }
        public int Cost { get; set; }
        public bool IsEthereal { get; set; }
        public bool ExhaustOnUse { get; set; }
        public CardZone Zone { get; internal set; }

        public RuntimeCard(int instanceId, string cardId, int cost, bool ethereal = false, bool exhaustOnUse = false)
        {
            InstanceId = instanceId;
            CardId = cardId;
            Cost = cost;
            IsEthereal = ethereal;
            ExhaustOnUse = exhaustOnUse;
            Zone = CardZone.DrawPile;
        }

        public override string ToString() => $"{CardId}#{InstanceId}";
    }

    /// <summary>
    /// 드로우 더미·손패·버린 더미·소멸 더미 (`04` §4). 손패 상한 10, 넘치는 카드는 버린 더미 (D-30).
    /// 드로우 더미가 비면 버린 더미를 BattleRandom으로 섞어 드로우 더미로 만든다. 둘 다 비면 더 뽑지 않는다.
    /// </summary>
    public sealed class RuntimeDeck
    {
        public const int HandLimit = 10;
        public const int TurnStartDraw = 5;

        private readonly List<RuntimeCard> _draw = new List<RuntimeCard>();
        private readonly List<RuntimeCard> _hand = new List<RuntimeCard>();
        private readonly List<RuntimeCard> _discard = new List<RuntimeCard>();
        private readonly List<RuntimeCard> _exhaust = new List<RuntimeCard>();
        private readonly List<RuntimeCard> _field = new List<RuntimeCard>();
        private readonly BattleRandom _random;

        public IReadOnlyList<RuntimeCard> DrawPile => _draw;
        public IReadOnlyList<RuntimeCard> Hand => _hand;
        public IReadOnlyList<RuntimeCard> DiscardPile => _discard;
        public IReadOnlyList<RuntimeCard> ExhaustPile => _exhaust;
        public IReadOnlyList<RuntimeCard> OnField => _field;

        public int ShuffleCount { get; private set; }

        public RuntimeDeck(IEnumerable<RuntimeCard> cards, BattleRandom random, bool shuffle = true)
        {
            _random = random;
            foreach (var c in cards)
            {
                c.Zone = CardZone.DrawPile;
                _draw.Add(c);
            }
            if (shuffle) Shuffle(_draw);
        }

        /// <summary>n장 드로우. 실제로 손패에 들어간 카드 목록을 돌려준다 (넘친 카드는 버린 더미).</summary>
        public List<RuntimeCard> Draw(int n)
        {
            var drawn = new List<RuntimeCard>();
            for (int i = 0; i < n; i++)
            {
                if (_draw.Count == 0)
                {
                    if (_discard.Count == 0) break;
                    _draw.AddRange(_discard);
                    _discard.Clear();
                    foreach (var c in _draw) c.Zone = CardZone.DrawPile;
                    Shuffle(_draw);
                    ShuffleCount++;
                }
                var card = _draw[_draw.Count - 1];
                _draw.RemoveAt(_draw.Count - 1);
                if (AddToHand(card)) drawn.Add(card);
            }
            return drawn;
        }

        /// <summary>손패로 카드를 넣는다 (드로우·융합 결과 등). 10장이면 버린 더미로 (D-30). 넣었으면 true.</summary>
        public bool AddToHand(RuntimeCard card)
        {
            if (_hand.Count >= HandLimit)
            {
                Move(card, CardZone.DiscardPile);
                return false;
            }
            Move(card, CardZone.Hand);
            return true;
        }

        /// <summary>
        /// 카드 사용 후 이동. 몬스터는 필드로, ExhaustOnUse는 소멸, 나머지는 버린 더미.
        /// 비용 지불과 효과 실행은 호출하는 쪽(카드 리졸버, #39)이 한다.
        /// </summary>
        public void MoveAfterUse(RuntimeCard card, bool isMonster)
        {
            if (!_hand.Contains(card)) throw new System.InvalidOperationException($"{card} is not in hand");
            Move(card, isMonster ? CardZone.OnField : card.ExhaustOnUse ? CardZone.ExhaustPile : CardZone.DiscardPile);
        }

        /// <summary>필드의 몬스터가 죽거나 희생되면 그 카드를 버린 더미로 (`04` §4).</summary>
        public void ReturnFromField(RuntimeCard card)
        {
            if (card != null && _field.Contains(card)) Move(card, CardZone.DiscardPile);
        }

        /// <summary>턴 종료: Ethereal은 소멸, 나머지 손패는 전부 버린 더미 (`04` §4, `06` §13.1).</summary>
        public void EndTurn()
        {
            foreach (var card in _hand.ToArray())
            {
                Move(card, card.IsEthereal ? CardZone.ExhaustPile : CardZone.DiscardPile);
            }
        }

        public int TotalCount => _draw.Count + _hand.Count + _discard.Count + _exhaust.Count + _field.Count;

        private void Move(RuntimeCard card, CardZone to)
        {
            ListOf(card.Zone).Remove(card);
            card.Zone = to;
            ListOf(to).Add(card);
        }

        private List<RuntimeCard> ListOf(CardZone zone)
        {
            switch (zone)
            {
                case CardZone.DrawPile: return _draw;
                case CardZone.Hand: return _hand;
                case CardZone.DiscardPile: return _discard;
                case CardZone.ExhaustPile: return _exhaust;
                default: return _field;
            }
        }

        private void Shuffle(List<RuntimeCard> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _random.Next(0, i + 1);
                var t = list[i];
                list[i] = list[j];
                list[j] = t;
            }
        }
    }
}
