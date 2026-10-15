#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace KnittedPicture.Core
{
    public sealed partial class ProgressService
    {
        // ---------- Коллекции альбома ----------

        public static readonly Reward CollectionReward = new Reward(80, null, 0, DailyGift.RandomYarn);

        public bool IsCollectionDone(CollectionInfo collection)
        {
            for (var id = collection.FirstLevel; id <= collection.LastLevel; id++)
            {
                if (!IsCompleted(id))
                {
                    return false;
                }
            }

            return true;
        }

        public bool CanClaimCollection(int index) =>
            index >= 0 && index < Collections.All.Length && IsCollectionDone(Collections.All[index]) && !_data.collectionsClaimed.Contains(index);

        public bool IsCollectionClaimed(int index) => _data.collectionsClaimed.Contains(index);

        public Granted? ClaimCollection(int index, System.Random rng)
        {
            if (!CanClaimCollection(index))
            {
                return null;
            }

            _data.collectionsClaimed.Add(index);
            return Grant(CollectionReward, rng);
        }
    }
}
