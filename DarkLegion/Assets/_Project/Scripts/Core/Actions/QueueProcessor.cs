using System.Collections.Generic;

namespace DarkLegion.Core
{
    /// <summary>
    /// Main·Trigger·Death 세 큐 (`06` §9). Main 액션 1회 실행 직후
    /// Death → Trigger 순으로 비우고, Trigger 1건마다 Death를 다시 비운다.
    /// </summary>
    public sealed class QueueProcessor
    {
        private readonly Queue<BattleAction> _main = new Queue<BattleAction>();
        private readonly Queue<BattleAction> _trigger = new Queue<BattleAction>();
        private readonly Queue<BattleAction> _death = new Queue<BattleAction>();
        private readonly BattleContext _context;

        /// <summary>실행·취소·비상 정리를 알리는 로깅 훅. 연출(Presentation)은 이것을 구독하지 않고 PresentationEvent를 쓴다.</summary>
        public event System.Action<string> Log;

        /// <summary>한도 초과로 비상 정리를 했는지. 전투는 강제 종료 대상이 된다 (`06` §9.5).</summary>
        public bool EmergencyStopped { get; private set; }

        public int TotalEnqueued { get; private set; }

        public QueueProcessor(BattleContext context)
        {
            _context = context;
        }

        public int MainCount => _main.Count;
        public int TriggerCount => _trigger.Count;
        public int DeathCount => _death.Count;

        public void EnqueueMain(BattleAction action) => Enqueue(_main, action);

        public void EnqueueDeath(BattleAction action) => Enqueue(_death, action);

        /// <summary>트리거 액션. 깊이가 MaxTriggerDepth를 넘으면 넣지 않는다.</summary>
        public bool EnqueueTrigger(BattleAction action)
        {
            if (action.TriggerDepth > BattleLimits.MaxTriggerDepth)
            {
                Log?.Invoke($"[Queue] trigger depth {action.TriggerDepth} > {BattleLimits.MaxTriggerDepth}, dropped {action}");
                return false;
            }
            Enqueue(_trigger, action);
            return true;
        }

        private void Enqueue(Queue<BattleAction> queue, BattleAction action)
        {
            if (EmergencyStopped) return;
            TotalEnqueued++;
            // 한도는 한 번에 쌓인 수다. 전투 전체 누적으로 세면 긴 전투가 비상 정지된다 (`06` §9.5)
            if (_main.Count + _trigger.Count + _death.Count >= BattleLimits.MaxQueueCount)
            {
                EmergencyCleanup($"queue count > {BattleLimits.MaxQueueCount}");
                return;
            }
            queue.Enqueue(action);
        }

        public void Process()
        {
            while (_main.Count > 0 && !EmergencyStopped)
            {
                Run(_main.Dequeue());
                FlushDeath();
                FlushTrigger();
            }
            FlushDeath();
        }

        private void FlushDeath()
        {
            while (_death.Count > 0 && !EmergencyStopped)
            {
                Run(_death.Dequeue());
            }
        }

        private void FlushTrigger()
        {
            int iterations = 0;
            while (_trigger.Count > 0 && !EmergencyStopped)
            {
                if (++iterations > BattleLimits.MaxTriggerIterationsPerFlush)
                {
                    EmergencyCleanup($"trigger iterations > {BattleLimits.MaxTriggerIterationsPerFlush}");
                    return;
                }
                Run(_trigger.Dequeue());
                FlushDeath();
            }
        }

        private void Run(BattleAction action)
        {
            if (action.IsCancelled)
            {
                Log?.Invoke($"[Queue] cancelled {action}");
                return;
            }
            Log?.Invoke($"[Queue] execute {action}");
            action.Execute(_context);
        }

        private void EmergencyCleanup(string reason)
        {
            EmergencyStopped = true;
            _main.Clear();
            _trigger.Clear();
            _death.Clear();
            Log?.Invoke($"[Queue] EMERGENCY: {reason} (seed {_context.Random.Seed})");
        }
    }
}
