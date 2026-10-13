#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Рабочие нити станка: у каждого вяжущего клубка своя нить к спице. Конец нити пружиной догоняет
    /// петлю, которую клубок вяжет сейчас; нить поднимается от клубка вверх и дугой заходит к спице сверху,
    /// чуть покачивается. Каждая петля «утягивает» нить: витки бегут к спице. Клубок перестал вязать —
    /// нить мягко сматывается обратно в него. В «Разгоне» нить толще и светлее.
    /// </summary>
    public sealed class YarnRopes : MonoBehaviour
    {
        /// <summary>За сколько конец нити доходит до новой петли (тогда петля и садится).</summary>
        public const float Reach = 0.1f;

        private const float Idle = 0.5f;

        private readonly Dictionary<int, Strand> _strands = new Dictionary<int, Strand>();
        private RectTransform _layer = null!;
        private float _clock;

        public static YarnRopes Create(RectTransform parent)
        {
            var layer = Ui.Stretch(Ui.Node(parent, "Ropes"));
            var ropes = layer.gameObject.AddComponent<YarnRopes>();
            ropes._layer = layer;
            return ropes;
        }

        /// <summary>Клубок из слота slot вяжет петлю: нить тянется от from (верх клубка) к to (точка на спице).</summary>
        public void Pull(int slot, Vector3 fromWorld, Vector3 toWorld, Color color, bool rush)
        {
            if (!_strands.TryGetValue(slot, out var strand) || strand.Rope == null)
            {
                var rope = Ui.Node(_layer, "Rope" + slot).gameObject.AddComponent<YarnRope>();
                Ui.Stretch(rope.rectTransform);
                rope.raycastTarget = false;
                strand = new Strand(rope);
                var start = Local(fromWorld);
                strand.End = start;
                strand.Velocity = Vector2.zero;
                strand.Rope.Reach = 0f;
                _strands[slot] = strand;
            }

            strand.From = Local(fromWorld);
            strand.Target = Local(toWorld);
            strand.LastPull = _clock;
            strand.Rush = rush;
            strand.Rope.color = rush ? Color.Lerp(color, Color.white, 0.25f) : color;
            strand.Tug = 1f;
        }

        /// <summary>Клубок кончился или ушёл со станка: нить сматывается.</summary>
        public void Release(int slot)
        {
            if (_strands.TryGetValue(slot, out var strand))
            {
                strand.LastPull = -99f;
            }
        }

        public void ReleaseAll()
        {
            foreach (var strand in _strands.Values)
            {
                strand.LastPull = -99f;
            }
        }

        private Vector2 Local(Vector3 world) => _layer.InverseTransformPoint(world);

        private void Update()
        {
            var dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            _clock += dt;
            var gone = new List<int>();
            foreach (var pair in _strands)
            {
                var s = pair.Value;
                if (s.Rope == null)
                {
                    gone.Add(pair.Key);
                    continue;
                }

                var active = _clock - s.LastPull < Idle;

                // Конец нити — критически затухающая пружина: быстро, без дрожи.
                var target = active ? s.Target : s.From;
                const float w = 26f;
                s.Velocity += (w * w * (target - s.End) - 2f * w * s.Velocity) * dt;
                s.End += s.Velocity * dt;

                var reach = s.Rope.Reach;
                reach = Mathf.MoveTowards(reach, active ? 1f : 0f, dt * (active ? 7f : 3.5f));
                s.Tug = Mathf.MoveTowards(s.Tug, 0f, dt * 6f);

                // Форма: от клубка вверх, дугой к спице сверху; натяг петли выпрямляет дугу, ветер чуть качает.
                var start = s.From;
                var end = s.End;
                var span = Mathf.Abs(end.y - start.y);
                var slack = 1f - 0.35f * s.Tug;
                var sway = Mathf.Sin(_clock * 1.3f + pair.Key * 1.7f) * 5f;
                s.Rope.Start = start;
                s.Rope.Control1 = start + new Vector2(sway, (span * 0.45f + 30f) * slack);
                s.Rope.Control2 = end + new Vector2(-sway * 0.5f + (start.x - end.x) * 0.15f, (36f + span * 0.12f) * slack);
                s.Rope.End = end;
                s.Rope.Thickness = s.Rush ? 5f : 3.6f;
                s.Rope.Flow += dt * (active ? 1.2f + 2.5f * s.Tug : 0f);
                s.Rope.Reach = reach;
                s.Rope.Rebuild();

                if (!active && reach <= 0f)
                {
                    Destroy(s.Rope.gameObject);
                    gone.Add(pair.Key);
                }
            }

            foreach (var key in gone)
            {
                _strands.Remove(key);
            }
        }

        private sealed class Strand
        {
            public Strand(YarnRope rope)
            {
                Rope = rope;
            }

            public YarnRope Rope { get; }

            public Vector2 From;
            public Vector2 Target;
            public Vector2 End;
            public Vector2 Velocity;
            public float LastPull;
            public float Tug;
            public bool Rush;
        }
    }
}
