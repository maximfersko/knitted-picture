#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace KnittedPicture.Core
{
    public sealed partial class ProgressService
    {
        // ---------- Задания дня ----------

        public bool MissionsOpen => _data.nextLevel >= DailyMissions.FromLevel;

        /// <summary>Задания на сегодня (новый день — новый набор, прогресс с нуля).</summary>
        public List<Mission> Missions(int today)
        {
            EnsureMissions(today);
            var list = new List<Mission>();
            for (var i = 0; i < _data.missionKinds.Count; i++)
            {
                list.Add(new Mission((MissionKind)_data.missionKinds[i], _data.missionTargets[i], _data.missionValues[i]));
            }

            return list;
        }

        public int MissionsDone(int today) => Missions(today).Count(m => m.Done);

        public bool CanClaimMissions(int today) =>
            MissionsOpen && MissionsDone(today) == DailyMissions.Count && !_data.missionsClaimed;

        public bool MissionsClaimed(int today)
        {
            EnsureMissions(today);
            return _data.missionsClaimed;
        }

        public Reward MissionsReward(int today) => DailyMissions.RewardFor(today, _data.nextLevel);

        public Granted? ClaimMissions(int today, System.Random rng)
        {
            if (!CanClaimMissions(today))
            {
                return null;
            }

            _data.missionsClaimed = true;
            RecordSeason(today, Season.MissionsPoints);
            return Grant(MissionsReward(today), rng);
        }

        /// <summary>Засчитать пройденный уровень в задания дня. Возвращает, сколько заданий стало выполнено именно сейчас.</summary>
        public int RecordMissions(int today, int stars, bool clean, int goldCaught, int rushes, int bestCombo, int stitches)
        {
            if (!MissionsOpen)
            {
                return 0;
            }

            EnsureMissions(today);
            var newlyDone = 0;
            for (var i = 0; i < _data.missionKinds.Count; i++)
            {
                var before = _data.missionValues[i];
                var target = _data.missionTargets[i];
                var value = before;
                switch ((MissionKind)_data.missionKinds[i])
                {
                    case MissionKind.Win: value += 1; break;
                    case MissionKind.ThreeStars: value += stars >= 3 ? 1 : 0; break;
                    case MissionKind.Gold: value += goldCaught; break;
                    case MissionKind.Rush: value += rushes; break;
                    case MissionKind.Combo: value = System.Math.Max(value, bestCombo); break;
                    case MissionKind.Stitches: value += stitches; break;
                    case MissionKind.Clean: value += clean ? 1 : 0; break;
                }

                _data.missionValues[i] = System.Math.Min(value, target);
                if (before < target && value >= target)
                {
                    newlyDone++;
                }
            }

            return newlyDone;
        }

        private void EnsureMissions(int today)
        {
            if (_data.missionDay == today && _data.missionKinds.Count == DailyMissions.Count)
            {
                return;
            }

            _data.missionDay = today;
            _data.missionKinds.Clear();
            _data.missionTargets.Clear();
            _data.missionValues.Clear();
            foreach (var (kind, target) in DailyMissions.Roll(today, _data.nextLevel))
            {
                _data.missionKinds.Add((int)kind);
                _data.missionTargets.Add(target);
                _data.missionValues.Add(0);
            }

            _data.missionsClaimed = false;
        }
    }
}
