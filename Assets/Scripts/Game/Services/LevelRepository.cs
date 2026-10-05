#nullable enable

using System.Collections.Generic;
using System.Linq;
using KnittedPicture.Core;
using UnityEngine;

namespace KnittedPicture.Game
{
    /// <summary>Уровни из Assets/Resources/Levels, по порядку id.</summary>
    public sealed class LevelRepository
    {
        private readonly Dictionary<int, LevelData> _levels = new Dictionary<int, LevelData>();

        public LevelRepository()
        {
            foreach (var asset in Resources.LoadAll<TextAsset>("Levels"))
            {
                var level = JsonUtility.FromJson<LevelData>(asset.text);
                var errors = LevelValidator.Validate(level);
                if (errors.Count > 0)
                {
                    Debug.LogError($"Уровень {asset.name} пропущен: {string.Join("; ", errors)}");
                    continue;
                }

                _levels[level.id] = level;
            }

            Ids = _levels.Keys.OrderBy(id => id).ToList();
        }

        public IReadOnlyList<int> Ids { get; }

        public int Count => Ids.Count;

        public LevelData Get(int id) => _levels[id];

        public bool Has(int id) => _levels.ContainsKey(id);
    }
}
