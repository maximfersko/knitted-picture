using System;
using System.Linq;
using Xunit;

namespace KnittedPicture.Core.Tests
{
    /// <summary>Корзинка пряжи, сундук, ежедневный подарок, достижения и коллекции.</summary>
    public class MetaTests
    {
        private static ProgressService Fresh() => new ProgressService(new PlayerProgress(), levelCount: 15);

        [Fact]
        public void Rescue_price_grows_within_a_level_and_stock_is_used_first()
        {
            Assert.Equal(Economy.Price(BoosterType.Slot), Economy.RescuePrice(BoosterType.Slot, 0));
            Assert.Equal(2 * Economy.Price(BoosterType.Slot), Economy.RescuePrice(BoosterType.Slot, 1));
            Assert.Equal(3 * Economy.Price(BoosterType.Slot), Economy.RescuePrice(BoosterType.Slot, 2));
            Assert.Equal(3 * Economy.Price(BoosterType.Slot), Economy.RescuePrice(BoosterType.Slot, 7));

            var p = new ProgressService(new PlayerProgress { coins = 1000 }, levelCount: 15);
            p.AddStock(BoosterType.Slot, 1);
            Assert.Equal(Payment.FromStock, p.PayRescue(BoosterType.Slot, 2));
            Assert.Equal(1000, p.Coins);

            Assert.Equal(Payment.WithCoins, p.PayRescue(BoosterType.Slot, 1));
            Assert.Equal(1000 - 2 * Economy.Price(BoosterType.Slot), p.Coins);
            Assert.False(p.CanAffordRescue(BoosterType.Slot, 2));
            Assert.Equal(Payment.NotEnough, p.PayRescue(BoosterType.Slot, 2));
        }

        [Fact]
        public void Video_doubles_and_video_boosters_have_a_daily_cap_that_resets_next_day()
        {
            var p = Fresh();
            for (var i = 0; i < Economy.DoublesPerDay; i++)
            {
                Assert.True(p.DoublesLeft(100) > 0);
                p.RecordDouble(100);
            }

            Assert.Equal(0, p.DoublesLeft(100));
            Assert.Equal(Economy.DoublesPerDay, p.DoublesLeft(101));

            for (var i = 0; i < Economy.VideoBoostersPerDay; i++)
            {
                p.RecordVideoBooster(100);
            }

            Assert.Equal(0, p.VideoBoostersLeft(100));
            Assert.Equal(Economy.VideoBoostersPerDay, p.VideoBoostersLeft(101));
        }

        [Fact]
        public void Album_frames_are_bought_once_with_coins_and_only_owned_ones_can_be_selected()
        {
            var white = AlbumFrames.All[1];
            var p = new ProgressService(new PlayerProgress { coins = white.Price - 1 }, levelCount: 15);
            Assert.True(p.FrameOwned(0));
            Assert.Equal(0, p.SelectedFrame.Id);
            Assert.False(p.BuyFrame(white.Id));
            Assert.False(p.SelectFrame(white.Id));

            p.AddCoins(1 + 500);
            Assert.True(p.BuyFrame(white.Id));
            Assert.Equal(500, p.Coins);
            Assert.Equal(white.Id, p.SelectedFrame.Id);
            Assert.False(p.BuyFrame(white.Id));
            Assert.True(p.SelectFrame(0));
            Assert.True(p.SelectFrame(white.Id));

            // Сохранение с чужой рамкой (нет в списке купленных или нет в каталоге) чинится: деревянная по умолчанию.
            var broken = new PlayerProgress { frame = 5 };
            broken.frames.Add(99);
            var repaired = new ProgressService(broken, levelCount: 15);
            Assert.Equal(0, repaired.SelectedFrame.Id);
            Assert.Empty(repaired.Data.frames);
        }

        [Fact]
        public void Frame_prices_climb_and_names_are_unique()
        {
            for (var i = 1; i < AlbumFrames.All.Length; i++)
            {
                Assert.True(AlbumFrames.All[i].Price > AlbumFrames.All[i - 1].Price);
                Assert.Equal(i, AlbumFrames.All[i].Id);
            }

            Assert.Equal(0, AlbumFrames.All[0].Price);
            Assert.Equal(AlbumFrames.All.Length, AlbumFrames.All.Select(f => f.Name).Distinct().Count());
        }

        [Fact]
        public void First_win_drops_a_new_yarn_and_replay_does_not()
        {
            var progress = Fresh();

            var first = progress.CompleteLevel(1, hard: false);
            Assert.InRange(first.Yarn, 0, YarnCatalog.Count - 1);
            Assert.True(first.NewYarn);
            Assert.Equal(1, progress.YarnKinds);

            var replay = progress.CompleteLevel(1, hard: false);
            Assert.Equal(-1, replay.Yarn);
        }

        [Fact]
        public void Yarn_chance_grows_after_empty_wins_and_is_sure_on_the_fourth()
        {
            Assert.Equal(100, YarnCatalog.DropChance(1, false, 0));
            Assert.Equal(100, YarnCatalog.DropChance(YarnCatalog.SureFirstLevels, false, 0));
            Assert.Equal(100, YarnCatalog.DropChance(10, hard: true, 0));
            Assert.Equal(35, YarnCatalog.DropChance(4, false, 0));
            Assert.Equal(50, YarnCatalog.DropChance(4, false, 1));
            Assert.Equal(65, YarnCatalog.DropChance(4, false, 2));
            Assert.Equal(100, YarnCatalog.DropChance(4, false, 3));
        }

        [Fact]
        public void New_pictures_never_go_four_in_a_row_without_yarn_and_the_win_tells_the_next_chance()
        {
            var progress = new ProgressService(new PlayerProgress(), levelCount: 60);
            var dry = 0;
            var drops = 0;
            for (var level = 1; level <= 60; level++)
            {
                var reward = progress.CompleteLevel(level, hard: level % 5 == 0);
                if (reward.Yarn >= 0)
                {
                    dry = 0;
                    drops++;
                    Assert.Equal(0, reward.NextYarnChance);
                }
                else
                {
                    dry++;
                    Assert.True(dry < YarnCatalog.DropPity);
                    Assert.InRange(reward.NextYarnChance, YarnCatalog.DropBasePercent + 1, 100);
                }
            }

            // Мотков заметно меньше, чем картинок, но не единицы.
            Assert.InRange(drops, 25, 50);
        }

        [Fact]
        public void Twelve_yarns_without_an_epic_make_the_next_one_epic()
        {
            var progress = Fresh();
            progress.Data.yarnsSinceEpic = YarnCatalog.EpicPity;
            var yarn = progress.RollYarn(new Random(3));
            Assert.Equal(Rarity.Epic, YarnCatalog.All[yarn].Rarity);
            Assert.Equal(0, progress.Data.yarnsSinceEpic);
        }

        [Fact]
        public void Duplicate_yarn_levels_up_first_and_turns_into_coins_at_max_level()
        {
            var progress = Fresh();
            progress.AddYarn(0);
            Assert.Equal(1, progress.YarnLevel(0));
            var coins = progress.Coins;

            // Повторы до третьего уровня поднимают уровень, монет за них нет.
            var second = progress.Grant(new Reward(0, null, 0, yarn: 0), new Random(1));
            Assert.False(second.NewYarn);
            Assert.True(second.YarnLevelUp);
            Assert.Equal(0, second.DuplicateCoins);
            Assert.Equal(2, progress.YarnLevel(0));
            progress.Grant(new Reward(0, null, 0, yarn: 0), new Random(1));
            Assert.Equal(YarnCatalog.MaxLevel, progress.YarnLevel(0));
            Assert.Equal(coins, progress.Coins);

            // На максимальном уровне повтор — монеты.
            var extra = progress.Grant(new Reward(0, null, 0, yarn: 0), new Random(1));
            Assert.False(extra.YarnLevelUp);
            Assert.Equal(YarnCatalog.DuplicateCoins(Rarity.Common), extra.DuplicateCoins);
            Assert.Equal(coins + extra.DuplicateCoins, progress.Coins);
            Assert.Equal(YarnCatalog.MaxLevel, progress.YarnLevel(0));
        }

        [Fact]
        public void First_yarn_goes_to_work_and_its_perk_grows_with_rarity_and_level()
        {
            var progress = Fresh();
            progress.Data.nextLevel = YarnCatalog.PerksFromLevel;
            Assert.Equal(-1, progress.ActiveYarn);
            Assert.Equal(0, progress.PerkPower(YarnPerk.Coins));

            progress.AddYarn(0); // Меринос: шерсть, обычная → копилка, сила 1
            Assert.Equal(0, progress.ActiveYarn);
            Assert.Equal(1, progress.PerkPower(YarnPerk.Coins));
            Assert.Equal(0, progress.PerkPower(YarnPerk.Gold));

            progress.AddYarn(7); // Шёлк: хлопок и шёлк, эпическая → золото
            Assert.Equal(0, progress.ActiveYarn);
            Assert.False(progress.SetActiveYarn(3));
            Assert.True(progress.SetActiveYarn(7));
            Assert.Equal(3, progress.PerkPower(YarnPerk.Gold));
            progress.AddYarn(7);
            progress.AddYarn(7);
            Assert.Equal(5, progress.PerkPower(YarnPerk.Gold));

            Assert.Equal(YarnPerk.Coins, YarnCatalog.PerkOf(0));
            Assert.Equal(YarnPerk.Gold, YarnCatalog.PerkOf(4));
            Assert.Equal(YarnPerk.Gold, YarnCatalog.PerkOf(8));
            Assert.Equal(YarnPerk.Coins, YarnCatalog.PerkOf(15));
        }

        [Fact]
        public void Perks_open_later_so_beginners_see_just_a_collection()
        {
            var progress = Fresh();
            progress.AddYarn(3);
            Assert.False(progress.PerksOpen);
            Assert.Equal(0, progress.PerkPower(YarnPerk.Coins));
            Assert.Equal(0, progress.CompleteLevel(1, hard: false).PerkCoins);

            progress.Data.nextLevel = YarnCatalog.PerksFromLevel;
            Assert.True(progress.PerksOpen);
            Assert.True(progress.PerkPower(YarnPerk.Coins) > 0);
            Assert.False(progress.TakePerksIntro(YarnCatalog.PerksFromLevel - 1));
            Assert.True(progress.TakePerksIntro(YarnCatalog.PerksFromLevel));
            Assert.False(progress.TakePerksIntro(YarnCatalog.PerksFromLevel));
        }

        [Fact]
        public void Tension_intro_is_shown_once()
        {
            var progress = Fresh();
            Assert.True(progress.TakeTensionIntro());
            Assert.False(progress.TakeTensionIntro());
        }

        [Fact]
        public void Coins_perk_pays_on_new_pictures_only()
        {
            var progress = Fresh();
            progress.Data.nextLevel = YarnCatalog.PerksFromLevel;
            progress.AddYarn(3); // Кашемир: эпическая шерсть, сила 3
            var first = progress.CompleteLevel(10, hard: false);
            Assert.Equal(YarnCatalog.CoinsBonus(3), first.PerkCoins);
            Assert.Equal(Economy.WinReward + first.PerkCoins + first.YarnCoins, first.Total);

            var replay = progress.CompleteLevel(10, hard: false);
            Assert.Equal(0, replay.PerkCoins);
        }

        [Fact]
        public void Tempo_combo_uses_the_yarn_bonus()
        {
            var combo = new TempoCombo(grace: TempoCombo.Grace + 0.5f, rushStitchesPerCoin: 3);
            for (var i = 0; i < TempoCombo.RushFrom; i++)
            {
                combo.OnPlaced();
            }

            Assert.False(combo.Update(TempoCombo.Grace + 0.3f, working: false));
            Assert.Equal(1, Enumerable.Range(0, 3).Count(_ => combo.OnStitch()));
        }

        [Fact]
        public void Full_yarn_set_can_be_claimed_once()
        {
            var progress = Fresh();
            var set = YarnCatalog.Sets[0];
            for (var i = set.First; i < set.First + set.Count; i++)
            {
                progress.AddYarn(i);
            }

            Assert.True(progress.CanClaimSet(set));
            Assert.NotNull(progress.ClaimSet(set, new Random(1)));
            Assert.Null(progress.ClaimSet(set, new Random(1)));
        }

        [Fact]
        public void Star_chest_opens_every_twelve_stars()
        {
            var progress = Fresh();
            for (var id = 1; id <= 3; id++)
            {
                progress.CompleteLevel(id, false, stars: 3);
            }

            Assert.Equal(9, progress.ChestProgress);
            Assert.False(progress.CanOpenChest);

            progress.CompleteLevel(4, false, stars: 3);
            Assert.True(progress.CanOpenChest);

            var coins = progress.Coins;
            var contents = progress.PeekChest();
            Assert.False(contents.Big);
            Assert.Equal(Chest.Choices, contents.Yarns.Length);
            Assert.Null(progress.ClaimChest(contents.Boosters.Length, 0));
            var granted = progress.ClaimChest(0, 1);
            Assert.NotNull(granted);
            Assert.Equal(contents.Boosters[0], granted!.Value.Reward.Booster);
            Assert.Equal(contents.Yarns[1], granted.Value.Reward.Yarn);
            Assert.True(progress.Coins > coins);
            Assert.Equal(0, progress.ChestProgress);
            Assert.False(progress.CanOpenChest);
        }

        [Fact]
        public void Chest_contents_are_stable_distinct_and_big_every_fourth()
        {
            var progress = Fresh();
            var first = progress.PeekChest();
            var again = progress.PeekChest();
            Assert.Equal(first.Coins, again.Coins);
            Assert.Equal(first.Yarns, again.Yarns);
            Assert.Equal(first.Boosters, again.Boosters);

            for (var n = 0; n < 40; n++)
            {
                var contents = Chest.Roll(n, 50, _ => false);
                Assert.Equal(Chest.IsBig(n), contents.Big);
                Assert.Equal(Chest.Choices, contents.Boosters.Length);
                Assert.Equal(contents.Boosters.Length, new System.Collections.Generic.HashSet<BoosterType>(contents.Boosters).Count);
                Assert.Equal(Chest.Choices, new System.Collections.Generic.HashSet<int>(contents.Yarns).Count);
                if (contents.Big)
                {
                    Assert.Equal(1, contents.BoosterCount);
                    Assert.Contains(contents.Yarns, y => YarnCatalog.All[y].Rarity != Rarity.Common);
                }
            }

            Assert.True(Chest.IsBig(3));
            Assert.False(Chest.IsBig(4));

            // Бустеры — только уже открытые: на 6-м уровне есть лишь «+1 слот».
            Assert.Equal(new[] { BoosterType.Slot }, Chest.Roll(0, 6, _ => false).Boosters);
        }

        [Fact]
        public void Reset_progress_starts_chest_and_meta_from_scratch()
        {
            var progress = Fresh();
            for (var id = 1; id <= 8; id++)
            {
                progress.CompleteLevel(id, false, stars: 3);
            }

            var first = progress.PeekChest();
            progress.ClaimChest(0, 0);
            progress.ClaimChest(0, 0);
            Assert.Equal(2, progress.ChestsOpened);

            // Сброс в игре — новый PlayerProgress: сундук, звёзды, пряжа и награды с нуля.
            var reset = Fresh();
            Assert.Equal(0, reset.ChestsOpened);
            Assert.Equal(0, reset.ChestProgress);
            Assert.False(reset.CanOpenChest);
            Assert.False(reset.NextChestBig);
            Assert.Equal(0, reset.TotalStars);
            Assert.Equal(0, reset.YarnKinds);
            Assert.Equal(0, reset.PendingClaims);
            Assert.Equal(Economy.StartCoins, reset.Coins);
            Assert.Equal(first.Coins, reset.PeekChest().Coins);
        }

        [Fact]
        public void Turning_the_clock_back_a_day_gives_no_extra_daily_gift()
        {
            var progress = Fresh();
            var rng = new Random(1);
            Assert.NotNull(progress.ClaimDaily(100, rng));
            Assert.False(progress.CanClaimDaily(100));
            Assert.False(progress.CanClaimDaily(99));
            Assert.True(progress.CanClaimDaily(101));

            // Часы когда-то убежали далеко вперёд: после возврата подарок не заблокирован навсегда.
            Assert.NotNull(progress.ClaimDaily(500, rng));
            Assert.True(progress.CanClaimDaily(102));
        }

        [Fact]
        public void Daily_missions_are_stable_for_the_day_track_levels_and_pay_once()
        {
            var progress = Fresh();
            Assert.False(progress.MissionsOpen);
            Assert.Equal(0, progress.RecordMissions(300, 3, true, 5, 3, 10, 500));

            progress.Data.nextLevel = 10;
            var missions = progress.Missions(300);
            Assert.Equal(DailyMissions.Count, missions.Count);
            Assert.Equal(DailyMissions.Count, missions.Select(m => m.Kind).Distinct().Count());
            Assert.Contains(missions[0].Kind, new[] { MissionKind.Win, MissionKind.Stitches });
            Assert.Equal(missions.Select(m => m.Text), progress.Missions(300).Select(m => m.Text));

            // Сильная победа закрывает всё, кроме «пройди N уровней», — тот растёт по одному.
            for (var i = 0; i < 3; i++)
            {
                progress.RecordMissions(300, 3, true, 5, 3, 10, 500);
            }

            Assert.True(progress.CanClaimMissions(300));
            var coins = progress.Coins;
            Assert.NotNull(progress.ClaimMissions(300, new Random(1)));
            Assert.True(progress.Coins > coins);
            Assert.Null(progress.ClaimMissions(300, new Random(1)));

            // Новый день — новые задания с нуля.
            Assert.Equal(0, progress.MissionsDone(301));
            Assert.False(progress.MissionsClaimed(301));
        }

        [Fact]
        public void Mission_texts_read_naturally()
        {
            Assert.Equal("Пройди 2 уровня", DailyMissions.Text(MissionKind.Win, 2));
            Assert.Equal("Пройди уровень на 3 звезды", DailyMissions.Text(MissionKind.ThreeStars, 1));
            Assert.Equal("Поймай 5 золотых петель", DailyMissions.Text(MissionKind.Gold, 5));
            Assert.Equal("Включи «Разгон» 3 раза", DailyMissions.Text(MissionKind.Rush, 3));
            Assert.Equal("Свяжи 200 петель", DailyMissions.Text(MissionKind.Stitches, 200));
        }

        [Fact]
        public void Pattern_of_the_day_pays_once_a_day_and_counts_days_in_a_row()
        {
            var progress = Fresh();
            Assert.Null(progress.CompletePattern(400, new Random(1)));

            progress.Data.nextLevel = DailyPattern.FromLevel;
            var coins = progress.Coins;
            Assert.NotNull(progress.CompletePattern(400, new Random(1)));
            Assert.Equal(coins + DailyPattern.RewardCoins, progress.Coins);
            Assert.True(progress.PatternDone(400));
            Assert.Null(progress.CompletePattern(400, new Random(1)));

            for (var day = 401; day <= 406; day++)
            {
                var granted = progress.CompletePattern(day, new Random(day));
                Assert.Equal(day == 406, granted!.Value.Reward.Yarn >= 0);
            }

            Assert.Equal(7, progress.PatternStreak(406));
            Assert.Equal(7, progress.PatternStreak(407));
            Assert.Equal(0, progress.PatternStreak(408));
        }

        [Fact]
        public void Pattern_of_the_day_cannot_be_farmed_by_turning_the_clock_back()
        {
            var progress = Fresh();
            progress.Data.nextLevel = DailyPattern.FromLevel;
            Assert.NotNull(progress.CompletePattern(500, new Random(1)));
            Assert.NotNull(progress.CompletePattern(501, new Random(2)));

            // Часы назад на день: вчерашний узор уже связан, повторной награды нет.
            var coins = progress.Coins;
            Assert.True(progress.PatternDone(500));
            Assert.True(progress.PatternAhead(500));
            Assert.Null(progress.CompletePattern(500, new Random(3)));
            Assert.Null(progress.CompletePattern(480, new Random(4)));
            Assert.Equal(coins, progress.Coins);
            Assert.Equal(2, progress.PatternStreak(500));

            // Часы вернулись: следующий день открывает узор, серия продолжается.
            Assert.NotNull(progress.CompletePattern(502, new Random(5)));
            Assert.Equal(3, progress.PatternStreak(502));
        }

        [Fact]
        public void Marathon_counts_stitches_per_week_pays_each_tier_once_and_resets_on_monday()
        {
            var progress = Fresh();
            const int monday = 20003; // день 20003 с 1970 года — понедельник
            var week = WeeklyMarathon.WeekOf(monday);
            Assert.Equal(week, WeeklyMarathon.WeekOf(monday + 6));
            Assert.Equal(week + 1, WeeklyMarathon.WeekOf(monday + 7));
            Assert.Equal(week - 1, WeeklyMarathon.WeekOf(monday - 1));
            Assert.Equal(7, WeeklyMarathon.DaysLeft(monday));
            Assert.Equal(1, WeeklyMarathon.DaysLeft(monday + 6));

            // До уровня открытия петли не копятся.
            progress.RecordMarathon(monday, 500);
            Assert.Equal(0, progress.MarathonStitches(monday));

            progress.Data.nextLevel = WeeklyMarathon.FromLevel;
            progress.RecordMarathon(monday, WeeklyMarathon.Targets[0] - 1);
            Assert.False(progress.CanClaimMarathon(monday));
            progress.RecordMarathon(monday + 2, 1);
            Assert.True(progress.CanClaimMarathon(monday + 2));
            var coins = progress.Coins;
            Assert.NotNull(progress.ClaimMarathon(monday + 2, new Random(1)));
            Assert.Equal(coins + WeeklyMarathon.Rewards[0].Coins, progress.Coins);
            Assert.Null(progress.ClaimMarathon(monday + 2, new Random(1)));
            Assert.Equal(1, progress.MarathonClaimed(monday + 3));

            // Часы назад, на прошлую неделю: марафон замер, наград нет.
            progress.RecordMarathon(monday - 3, 5000);
            Assert.False(progress.CanClaimMarathon(monday - 3));
            Assert.Equal(WeeklyMarathon.Targets[0], progress.MarathonStitches(monday));

            // Новая неделя: счёт с нуля, ступени снова доступны.
            Assert.Equal(0, progress.MarathonStitches(monday + 7));
            progress.RecordMarathon(monday + 7, WeeklyMarathon.Targets[0]);
            Assert.Equal(0, progress.MarathonClaimed(monday + 7));
            Assert.True(progress.CanClaimMarathon(monday + 7));
        }

        [Fact]
        public void Daily_gift_once_a_day_and_a_missed_day_only_pauses_the_week()
        {
            var progress = Fresh();
            var rng = new Random(5);

            Assert.Equal(0, progress.DailyIndex(100));
            Assert.NotNull(progress.ClaimDaily(100, rng));
            Assert.Null(progress.ClaimDaily(100, rng));
            Assert.Equal(1, progress.DailyIndex(101));

            progress.ClaimDaily(101, rng);
            progress.ClaimDaily(102, rng);
            Assert.Equal(3, progress.DailyIndex(103));
            Assert.Equal(3, progress.Data.dailyBest);

            // Пропустил день: неделя не сбрасывается, а продолжается с 4-го дня.
            Assert.Equal(3, progress.DailyIndex(105));
            progress.ClaimDaily(105, rng);
            Assert.Equal(4, progress.DailyIndex(106));

            // Дни подряд для достижения считаются отдельно и после пропуска начинаются заново.
            Assert.Equal(1, progress.Data.dailyStreak);
            Assert.Equal(3, progress.Data.dailyBest);
        }

        [Fact]
        public void Seventh_daily_gift_brings_a_yarn()
        {
            var progress = Fresh();
            Granted? last = null;
            for (var day = 0; day < 7; day++)
            {
                last = progress.ClaimDaily(200 + day, new Random(day));
            }

            Assert.True(last!.Value.Reward.Yarn >= 0);
        }

        [Fact]
        public void Achievements_unlock_from_stats_and_claim_once()
        {
            var progress = Fresh();
            var first = Achievements.All.First(a => a.Title == "Первая петля");
            Assert.False(progress.CanClaimAchievement(first));

            progress.CompleteLevel(1, false);
            Assert.True(progress.CanClaimAchievement(first));
            Assert.Equal(1, progress.PendingClaims);

            Assert.NotNull(progress.ClaimAchievement(first, new Random(1)));
            Assert.False(progress.CanClaimAchievement(first));
            Assert.Equal(0, progress.PendingClaims);

            for (var id = 2; id <= 5; id++)
            {
                progress.CompleteLevel(id, false);
            }

            Assert.True(progress.CanClaimAchievement(Achievements.All.First(a => a.Title == "Пять картинок")));
        }

        [Fact]
        public void Finished_album_collection_gives_a_reward()
        {
            var progress = Fresh();
            for (var id = 1; id <= 5; id++)
            {
                progress.CompleteLevel(id, false);
            }

            Assert.True(progress.CanClaimCollection(0));
            Assert.False(progress.CanClaimCollection(1));
            Assert.NotNull(progress.ClaimCollection(0, new Random(1)));
            Assert.True(progress.IsCollectionClaimed(0));
        }

        [Fact]
        public void Level_record_keeps_fewest_taps_and_clean_win()
        {
            var progress = Fresh();
            Assert.Equal(0, progress.BestTaps(3));

            progress.RecordResult(3, taps: 15, clean: false);
            progress.RecordResult(3, taps: 18, clean: true);

            Assert.Equal(15, progress.BestTaps(3));
            Assert.True(progress.WasClean(3));
            Assert.False(progress.WasClean(2));
        }

        [Fact]
        public void Achievement_and_yarn_ids_match_their_positions()
        {
            // Номера достижений не переиспользуются: на них держатся сохранения игроков.
            Assert.Equal(Achievements.All.Length, Achievements.All.Select(a => a.Id).Distinct().Count());
            Assert.Equal(Enumerable.Range(0, YarnCatalog.Count), YarnCatalog.All.Select(y => y.Id));
            Assert.Equal(YarnCatalog.Count, YarnCatalog.Sets.Sum(s => s.Count));
        }
    }
}
