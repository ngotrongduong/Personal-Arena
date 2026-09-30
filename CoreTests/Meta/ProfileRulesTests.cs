using System;
using System.Collections.Generic;
using NUnit.Framework;
using PersonalArena.Core.Meta;
using PersonalArena.Core.Survivor;

namespace PersonalArena.Core.Tests.Meta
{
    public sealed class ProfileRulesTests
    {
        [Test]
        public void NewProfile_Shape()
        {
            PlayerProfile p = ProfileRules.NewProfile();
            Assert.That(p.Version, Is.EqualTo(1)); Assert.That(p.Gold, Is.EqualTo(0));
            Assert.That(p.UnlockedTier, Is.EqualTo(1)); Assert.That(p.SelectedTier, Is.EqualTo(1));
            Assert.That(p.SelectedClassId, Is.EqualTo("warrior")); Assert.That(p.TrainingFocus, Is.EqualTo("balanced"));
            Assert.That(p.Stats, Is.Not.Null); Assert.That(p.Characters, Has.Count.EqualTo(1));
            CharacterProfile c = p.Characters[0];
            Assert.That(c.ClassId, Is.EqualTo("warrior")); Assert.That(c.BrainRunId, Is.EqualTo("warrior-s001"));
            Assert.That(c.Level, Is.EqualTo(0)); Assert.That(c.ActiveLoadout, Is.EqualTo(0));
            Assert.That(c.Loadouts, Has.Length.EqualTo(ProfileRules.LoadoutCount));
            for (int i = 0; i < ProfileRules.LoadoutCount; i++)
            {
                Assert.That(c.Loadouts[i].Name, Is.EqualTo("Bộ " + (i + 1)));
                Assert.That(c.Loadouts[i].Points, Has.Length.EqualTo(StatInfo.SlotCount)); Assert.That(c.Loadouts[i].Points, Is.All.EqualTo(0));
                Assert.That(c.Loadouts[i].Record, Is.Not.Null); Assert.That(c.Loadouts[i].Record.RecentSeconds, Is.Empty);
            }
            Assert.That(ProfileRules.MaxCharacterLevel, Is.EqualTo(190));
            Assert.That(ProfileRules.Sanitize(p), Is.False, "a new profile needs no repair");
            Assert.That(ProfileRules.FindCharacter(p, "warrior"), Is.SameAs(c)); Assert.That(ProfileRules.FindCharacter(p, "mage"), Is.Null);
        }

        [Test]
        public void Sanitize_RepairsTopLevelFields()
        {
            PlayerProfile p = new PlayerProfile
            {
                Version = 0, Gold = -5, UnlockedTier = 14, SelectedTier = 0, SelectedClassId = "ghost", TrainingFocus = "speed",
                Characters = null, Stats = null
            };
            Assert.That(ProfileRules.Sanitize(p), Is.True);
            Assert.That(p.Version, Is.EqualTo(1)); Assert.That(p.Gold, Is.EqualTo(0));
            Assert.That(p.UnlockedTier, Is.EqualTo(10)); Assert.That(p.SelectedTier, Is.EqualTo(1));
            Assert.That(p.SelectedClassId, Is.EqualTo("warrior")); Assert.That(p.TrainingFocus, Is.EqualTo("balanced"));
            Assert.That(p.Stats, Is.Not.Null); Assert.That(p.Characters, Has.Count.EqualTo(1));
            Assert.That(p.Characters[0].ClassId, Is.EqualTo("warrior")); Assert.That(p.Characters[0].Loadouts, Has.Length.EqualTo(5));
            Assert.That(ProfileRules.Sanitize(p), Is.False, "idempotent");

            PlayerProfile q = ProfileRules.NewProfile(); q.UnlockedTier = 0; q.SelectedTier = 7; q.TrainingFocus = " GOLD ";
            Assert.That(ProfileRules.Sanitize(q), Is.True);
            Assert.That(q.UnlockedTier, Is.EqualTo(1)); Assert.That(q.SelectedTier, Is.EqualTo(1)); Assert.That(q.TrainingFocus, Is.EqualTo("gold"));

            PlayerProfile r = ProfileRules.NewProfile(); r.UnlockedTier = 5; r.SelectedTier = 7;
            ProfileRules.Sanitize(r); Assert.That(r.SelectedTier, Is.EqualTo(5), "selected <= unlocked");
        }

        [Test]
        public void Sanitize_RepairsCharacters()
        {
            CharacterProfile mage = ProfileRules.NewCharacter("mage");
            PlayerProfile p = new PlayerProfile { SelectedClassId = "mage" };
            p.Characters.Add(null); p.Characters.Add(new CharacterProfile { ClassId = "" }); p.Characters.Add(mage); p.Characters.Add(ProfileRules.NewCharacter("mage"));
            Assert.That(ProfileRules.Sanitize(p), Is.True);
            Assert.That(p.Characters, Has.Count.EqualTo(2), "nulls, empty ids and duplicates removed; Warrior added");
            Assert.That(p.Characters[0].ClassId, Is.EqualTo("warrior")); Assert.That(p.Characters[1], Is.SameAs(mage));
            Assert.That(p.SelectedClassId, Is.EqualTo("mage"), "a present class stays selected");

            CharacterProfile broken = new CharacterProfile { ClassId = "warrior", Level = 500, ActiveLoadout = 9, Loadouts = new Loadout[2], BrainRunId = null };
            broken.Loadouts[1] = new Loadout { Name = "   ", Points = null, Record = null };
            PlayerProfile q = new PlayerProfile(); q.Characters.Add(broken);
            Assert.That(ProfileRules.Sanitize(q), Is.True);
            Assert.That(broken.Level, Is.EqualTo(ProfileRules.MaxCharacterLevel)); Assert.That(broken.ActiveLoadout, Is.EqualTo(4));
            Assert.That(broken.BrainRunId, Is.EqualTo("warrior-s001")); Assert.That(broken.Loadouts, Has.Length.EqualTo(5));
            for (int i = 0; i < 5; i++)
            {
                Assert.That(broken.Loadouts[i], Is.Not.Null); Assert.That(broken.Loadouts[i].Name, Is.EqualTo("Bộ " + (i + 1)));
                Assert.That(broken.Loadouts[i].Points, Has.Length.EqualTo(StatInfo.SlotCount)); Assert.That(broken.Loadouts[i].Record, Is.Not.Null);
            }

            CharacterProfile negative = ProfileRules.NewCharacter("warrior"); negative.Level = -3; negative.ActiveLoadout = -1;
            PlayerProfile r = new PlayerProfile(); r.Characters.Add(negative);
            ProfileRules.Sanitize(r);
            Assert.That(negative.Level, Is.EqualTo(0)); Assert.That(negative.ActiveLoadout, Is.EqualTo(0));
        }

        [Test]
        public void Sanitize_RepairsLoadoutPoints()
        {
            PlayerProfile p = ProfileRules.NewProfile();
            CharacterProfile c = p.Characters[0]; c.Level = 30;
            Loadout l = c.Loadouts[0];
            l.Points = new int[20];
            l.Points[(int)StatId.MaxHp] = 25;  // cap 20
            l.Points[(int)StatId.Armor] = -4;  // negative
            l.Points[(int)StatId.Growth] = 8;
            l.Points[(int)StatId.Reserved13] = 3; // unused slot
            l.Points[18] = 7;                  // beyond the 16 slots: dropped
            l.Name = "  Một cái tên rất dài hơn mười sáu ký tự  ";
            Assert.That(ProfileRules.Sanitize(p), Is.True);
            Assert.That(l.Points, Has.Length.EqualTo(StatInfo.SlotCount));
            Assert.That(l.Points[(int)StatId.MaxHp], Is.EqualTo(20)); Assert.That(l.Points[(int)StatId.Armor], Is.EqualTo(0));
            Assert.That(l.Points[(int)StatId.Growth], Is.EqualTo(8)); Assert.That(l.Points[(int)StatId.Reserved13], Is.EqualTo(0));
            Assert.That(l.Name.Length, Is.LessThanOrEqualTo(ProfileRules.MaxNameLength)); Assert.That(l.Name, Does.StartWith("Một cái tên"));

            // Overspent: 28 points on level 10 -> remove from the highest stat index down.
            c.Level = 10; Assert.That(ProfileRules.Sanitize(p), Is.True);
            Assert.That(ProfileRules.SpentPoints(l), Is.EqualTo(10));
            Assert.That(l.Points[(int)StatId.Growth], Is.EqualTo(0)); Assert.That(l.Points[(int)StatId.MaxHp], Is.EqualTo(10));
        }

        [Test]
        public void Sanitize_RepairsRecordsAndStats()
        {
            PlayerProfile p = ProfileRules.NewProfile();
            LoadoutRecord record = p.Characters[0].Loadouts[2].Record;
            record.Runs = 3; record.Wins = 7; record.TotalGold = -1; record.TotalMinutes = float.NaN; record.BestSeconds = -2f;
            for (int i = 0; i < 25; i++) record.RecentSeconds.Add(i);
            record.RecentSeconds.Add(float.NaN); record.RecentSeconds.Add(-1f);
            p.Stats.Runs = -1; p.Stats.Wins = -2; p.Stats.GoldEarned = -3; p.Stats.BestSeconds = float.PositiveInfinity; p.Stats.FarmRuns = -4; p.Stats.FarmSessions = -5;
            Assert.That(ProfileRules.Sanitize(p), Is.True);
            Assert.That(record.Wins, Is.EqualTo(3)); Assert.That(record.TotalGold, Is.EqualTo(0));
            Assert.That(record.TotalMinutes, Is.EqualTo(0f)); Assert.That(record.BestSeconds, Is.EqualTo(0f));
            Assert.That(record.RecentSeconds, Has.Count.EqualTo(ProfileRules.RecentCap));
            Assert.That(record.RecentSeconds[0], Is.EqualTo(5f)); Assert.That(record.RecentSeconds[19], Is.EqualTo(24f), "keeps the last 20");
            Assert.That(p.Stats.Runs, Is.EqualTo(0)); Assert.That(p.Stats.Wins, Is.EqualTo(0)); Assert.That(p.Stats.GoldEarned, Is.EqualTo(0));
            Assert.That(p.Stats.BestSeconds, Is.EqualTo(0f)); Assert.That(p.Stats.FarmRuns, Is.EqualTo(0)); Assert.That(p.Stats.FarmSessions, Is.EqualTo(0));

            p.Characters[0].Loadouts[1].Record = new LoadoutRecord { RecentSeconds = null };
            Assert.That(ProfileRules.Sanitize(p), Is.True); Assert.That(p.Characters[0].Loadouts[1].Record.RecentSeconds, Is.Not.Null);
            Assert.Throws<ArgumentNullException>(() => ProfileRules.Sanitize(null));
        }

        [Test]
        public void LevelCost_Values()
        {
            Assert.That(ProfileRules.LevelCost(0), Is.EqualTo(100));
            Assert.That(ProfileRules.LevelCost(1), Is.EqualTo(125));
            Assert.That(ProfileRules.LevelCost(9), Is.EqualTo(745));
            Assert.That(ProfileRules.LevelCost(19), Is.EqualTo(6938), "100 · 1.25^19 = 6938.89…; floor, not the GDD's rounded 6939");
            Assert.That(ProfileRules.LevelCost(150), Is.GreaterThan(ProfileRules.LevelCost(149)));
            Assert.That(ProfileRules.LevelCost(1000), Is.EqualTo(long.MaxValue), "saturates");
            Assert.Throws<ArgumentOutOfRangeException>(() => ProfileRules.LevelCost(-1));
        }

        [Test]
        public void TryBuyLevel_GoldAndLevel()
        {
            PlayerProfile p = ProfileRules.NewProfile(); CharacterProfile c = p.Characters[0];
            p.Gold = 99; Assert.That(ProfileRules.TryBuyLevel(p, c), Is.False); Assert.That(c.Level, Is.EqualTo(0)); Assert.That(p.Gold, Is.EqualTo(99));
            p.Gold = 230; Assert.That(ProfileRules.TryBuyLevel(p, c), Is.True); Assert.That(c.Level, Is.EqualTo(1)); Assert.That(p.Gold, Is.EqualTo(130));
            Assert.That(ProfileRules.TryBuyLevel(p, c), Is.True); Assert.That(c.Level, Is.EqualTo(2)); Assert.That(p.Gold, Is.EqualTo(5));
            c.Level = ProfileRules.MaxCharacterLevel; p.Gold = long.MaxValue;
            Assert.That(ProfileRules.TryBuyLevel(p, c), Is.False, "max level"); Assert.That(c.Level, Is.EqualTo(ProfileRules.MaxCharacterLevel));
            Assert.That(ProfileRules.TryBuyLevel(null, c), Is.False); Assert.That(ProfileRules.TryBuyLevel(p, null), Is.False);
        }

        [Test]
        public void Points_AddRemoveReset_WithCapsAndUnspentLimit()
        {
            CharacterProfile c = ProfileRules.NewCharacter("warrior");
            Assert.That(ProfileRules.TryAddPoint(c, 0, StatId.Might), Is.False, "no unspent point");
            c.Level = 25;
            for (int i = 0; i < 20; i++) Assert.That(ProfileRules.TryAddPoint(c, 0, StatId.Might), Is.True);
            Assert.That(ProfileRules.TryAddPoint(c, 0, StatId.Might), Is.False, "cap 20");
            Assert.That(ProfileRules.TryAddPoint(c, 0, StatId.Reserved13), Is.False, "unused stat");
            Assert.That(ProfileRules.TryAddPoint(c, 0, (StatId)99), Is.False);
            for (int i = 0; i < 5; i++) Assert.That(ProfileRules.TryAddPoint(c, 0, StatId.Luck), Is.True);
            Assert.That(ProfileRules.UnspentPoints(c, 0), Is.EqualTo(0));
            Assert.That(ProfileRules.TryAddPoint(c, 0, StatId.Luck), Is.False, "out of points");
            Assert.That(ProfileRules.SpentPoints(c.Loadouts[0]), Is.EqualTo(25));
            Assert.That(ProfileRules.UnspentPoints(c, 1), Is.EqualTo(25), "each loadout spends the same level separately");

            Assert.That(ProfileRules.TryRemovePoint(c, 0, StatId.Luck), Is.True); Assert.That(ProfileRules.UnspentPoints(c, 0), Is.EqualTo(1));
            Assert.That(ProfileRules.TryRemovePoint(c, 0, StatId.Armor), Is.False, "nothing to remove");
            Assert.That(ProfileRules.TryAddPoint(c, 7, StatId.Armor), Is.False); Assert.That(ProfileRules.TryRemovePoint(c, -1, StatId.Might), Is.False);
            ProfileRules.ResetPoints(c, 0);
            Assert.That(c.Loadouts[0].Points, Is.All.EqualTo(0)); Assert.That(ProfileRules.UnspentPoints(c, 0), Is.EqualTo(25));
            Assert.That(ProfileRules.UnspentPoints(c, 9), Is.EqualTo(0));
        }

        [Test]
        public void ActiveLoadout_AndRename()
        {
            CharacterProfile c = ProfileRules.NewCharacter("warrior");
            Assert.That(ProfileRules.TrySetActiveLoadout(c, 3), Is.True); Assert.That(c.ActiveLoadout, Is.EqualTo(3));
            Assert.That(ProfileRules.TrySetActiveLoadout(c, 5), Is.False); Assert.That(ProfileRules.TrySetActiveLoadout(c, -1), Is.False);
            Assert.That(c.ActiveLoadout, Is.EqualTo(3));
            Assert.That(ProfileRules.TryRename(c, 1, "  Đánh boss  "), Is.True); Assert.That(c.Loadouts[1].Name, Is.EqualTo("Đánh boss"));
            Assert.That(ProfileRules.TryRename(c, 1, "   "), Is.False); Assert.That(ProfileRules.TryRename(c, 1, null), Is.False);
            Assert.That(ProfileRules.TryRename(c, 1, new string('a', 17)), Is.False); Assert.That(ProfileRules.TryRename(c, 1, new string('a', 16)), Is.True);
            Assert.That(ProfileRules.TryRename(c, 5, "x"), Is.False);
        }

        [Test]
        public void ToBuild_UsesActiveLoadout_AndValidates()
        {
            CharacterProfile c = ProfileRules.NewCharacter("warrior"); c.Level = 12;
            ProfileRules.TrySetActiveLoadout(c, 2);
            for (int i = 0; i < 7; i++) ProfileRules.TryAddPoint(c, 2, StatId.Greed);
            for (int i = 0; i < 5; i++) ProfileRules.TryAddPoint(c, 2, StatId.Armor);
            ProfileRules.TryAddPoint(c, 0, StatId.Might);
            CharacterBuild build = ProfileRules.ToBuild(c, 4);
            Assert.That(build.Tier, Is.EqualTo(4)); Assert.That(build.Level, Is.EqualTo(12));
            Assert.That(build.Points[(int)StatId.Greed], Is.EqualTo(7)); Assert.That(build.Points[(int)StatId.Armor], Is.EqualTo(5));
            Assert.That(build.Points[(int)StatId.Might], Is.EqualTo(0));
            Assert.DoesNotThrow(build.Validate);
            Assert.That(ProfileRules.ToBuild(c, 0).Tier, Is.EqualTo(1)); Assert.That(ProfileRules.ToBuild(c, 15).Tier, Is.EqualTo(10));
            Assert.Throws<ArgumentNullException>(() => ProfileRules.ToBuild(null, 1));
        }

        [Test]
        public void RecordRun_WalletRecordStats()
        {
            PlayerProfile p = ProfileRules.NewProfile(); CharacterProfile c = p.Characters[0];
            RunReward first = ProfileRules.RecordRun(p, c, 1, new RunResult { SurvivedSeconds = 300f, End = EndReason.Died, Gold = 120.9f, Tier = 1 });
            Assert.That(first.GoldAdded, Is.EqualTo(120)); Assert.That(first.TierUnlocked, Is.False); Assert.That(first.UnlockedTier, Is.EqualTo(1));
            Assert.That(p.Gold, Is.EqualTo(120));
            RunReward second = ProfileRules.RecordRun(p, c, 1, new RunResult { SurvivedSeconds = 900f, End = EndReason.TimeUp, Gold = 480f, Tier = 1, Farm = true });
            Assert.That(second.GoldAdded, Is.EqualTo(480)); Assert.That(p.Gold, Is.EqualTo(600));

            LoadoutRecord record = c.Loadouts[1].Record;
            Assert.That(record.Runs, Is.EqualTo(2)); Assert.That(record.Wins, Is.EqualTo(0)); Assert.That(record.TotalGold, Is.EqualTo(600));
            Assert.That(record.TotalMinutes, Is.EqualTo(20f).Within(1e-4f)); Assert.That(record.BestSeconds, Is.EqualTo(900f));
            Assert.That(record.RecentSeconds, Is.EqualTo(new List<float> { 300f, 900f }));
            Assert.That(c.Loadouts[0].Record.Runs, Is.EqualTo(0), "other loadouts untouched");
            Assert.That(p.Stats.Runs, Is.EqualTo(2)); Assert.That(p.Stats.FarmRuns, Is.EqualTo(1)); Assert.That(p.Stats.Wins, Is.EqualTo(0));
            Assert.That(p.Stats.GoldEarned, Is.EqualTo(600)); Assert.That(p.Stats.BestSeconds, Is.EqualTo(900f));

            for (int i = 0; i < 25; i++) ProfileRules.RecordRun(p, c, 1, new RunResult { SurvivedSeconds = i, End = EndReason.Died, Gold = 0f });
            Assert.That(record.RecentSeconds, Has.Count.EqualTo(ProfileRules.RecentCap)); Assert.That(record.RecentSeconds[0], Is.EqualTo(5f));

            RunReward odd = ProfileRules.RecordRun(p, c, 1, new RunResult { SurvivedSeconds = float.NaN, End = EndReason.Died, Gold = float.NaN });
            Assert.That(odd.GoldAdded, Is.EqualTo(0)); Assert.That(record.RecentSeconds[19], Is.EqualTo(0f));
            Assert.That(ProfileRules.RecordRun(p, c, 1, new RunResult { Gold = -50f }).GoldAdded, Is.EqualTo(0));
            long before = p.Gold;
            Assert.That(ProfileRules.RecordRun(p, c, 1, new RunResult { Gold = float.PositiveInfinity }).GoldAdded, Is.EqualTo(ProfileRules.MaxRunGold));
            Assert.That(p.Gold, Is.EqualTo(before + ProfileRules.MaxRunGold), "a broken gold value never makes the wallet negative");
            p.Gold = long.MaxValue - 5;
            ProfileRules.RecordRun(p, c, 1, new RunResult { Gold = 100f });
            Assert.That(p.Gold, Is.EqualTo(long.MaxValue), "the wallet saturates instead of overflowing");
            p.Gold = 0;

            ProfileRules.RecordFarmSession(p); Assert.That(p.Stats.FarmSessions, Is.EqualTo(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => ProfileRules.RecordRun(p, c, 5, new RunResult()));
            Assert.Throws<ArgumentNullException>(() => ProfileRules.RecordRun(p, c, 0, null));
        }

        [Test]
        public void RecordRun_WinAtHighestUnlockedTier_UnlocksNext_UpToTen()
        {
            PlayerProfile p = ProfileRules.NewProfile(); CharacterProfile c = p.Characters[0];
            RunReward loss = ProfileRules.RecordRun(p, c, 0, new RunResult { SurvivedSeconds = 900f, End = EndReason.TimeUp, Tier = 1 });
            Assert.That(loss.TierUnlocked, Is.False); Assert.That(p.UnlockedTier, Is.EqualTo(1));
            RunReward win = ProfileRules.RecordRun(p, c, 0, new RunResult { SurvivedSeconds = 700f, End = EndReason.Won, Tier = 1, Gold = 500f });
            Assert.That(win.TierUnlocked, Is.True); Assert.That(win.UnlockedTier, Is.EqualTo(2)); Assert.That(p.UnlockedTier, Is.EqualTo(2));
            Assert.That(c.Loadouts[0].Record.Wins, Is.EqualTo(1)); Assert.That(p.Stats.Wins, Is.EqualTo(1));

            RunReward lowWin = ProfileRules.RecordRun(p, c, 0, new RunResult { SurvivedSeconds = 700f, End = EndReason.Won, Tier = 1 });
            Assert.That(lowWin.TierUnlocked, Is.False, "a win below the highest unlocked tier unlocks nothing"); Assert.That(p.UnlockedTier, Is.EqualTo(2));

            for (int tier = 2; tier <= 10; tier++) ProfileRules.RecordRun(p, c, 0, new RunResult { SurvivedSeconds = 700f, End = EndReason.Won, Tier = tier });
            Assert.That(p.UnlockedTier, Is.EqualTo(10));
            RunReward top = ProfileRules.RecordRun(p, c, 0, new RunResult { SurvivedSeconds = 700f, End = EndReason.Won, Tier = 10 });
            Assert.That(top.TierUnlocked, Is.False); Assert.That(top.UnlockedTier, Is.EqualTo(10)); Assert.That(p.UnlockedTier, Is.EqualTo(10));
        }

        [Test]
        public void Summarize_MedianAndGoldPerMinute()
        {
            Assert.That(ProfileRules.Summarize(new Loadout()).MedianSeconds, Is.EqualTo(0f));
            Assert.That(ProfileRules.Summarize(new Loadout()).GoldPerMinute, Is.EqualTo(0f));
            Assert.That(ProfileRules.Summarize(null).Runs, Is.EqualTo(0));

            PlayerProfile p = ProfileRules.NewProfile(); CharacterProfile c = p.Characters[0];
            ProfileRules.RecordRun(p, c, 0, new RunResult { SurvivedSeconds = 600f, End = EndReason.Died, Gold = 100f });
            ProfileRules.RecordRun(p, c, 0, new RunResult { SurvivedSeconds = 120f, End = EndReason.Died, Gold = 20f });
            ProfileRules.RecordRun(p, c, 0, new RunResult { SurvivedSeconds = 480f, End = EndReason.Won, Gold = 150f });
            LoadoutSummary odd = ProfileRules.Summarize(c.Loadouts[0]);
            Assert.That(odd.Runs, Is.EqualTo(3)); Assert.That(odd.Wins, Is.EqualTo(1)); Assert.That(odd.WinRate, Is.EqualTo(1f / 3f).Within(1e-6f));
            Assert.That(odd.MedianSeconds, Is.EqualTo(480f)); Assert.That(odd.BestSeconds, Is.EqualTo(600f));
            Assert.That(odd.GoldPerMinute, Is.EqualTo(270f / 20f).Within(1e-3f));

            ProfileRules.RecordRun(p, c, 0, new RunResult { SurvivedSeconds = 60f, End = EndReason.Died, Gold = 10f });
            Assert.That(ProfileRules.Summarize(c.Loadouts[0]).MedianSeconds, Is.EqualTo(300f), "even count: mean of the two middle values");
        }

        [Test]
        public void ClassPrices()
        {
            Assert.That(ProfileRules.ClassPrice("warrior"), Is.EqualTo(0)); Assert.That(ProfileRules.ClassPrice("mage"), Is.EqualTo(1500));
            Assert.That(ProfileRules.ClassPrice("archer"), Is.EqualTo(3000)); Assert.That(ProfileRules.ClassPrice("rogue"), Is.EqualTo(-1));
            Assert.That(ProfileRules.ClassPrice(null), Is.EqualTo(-1));
            Assert.That(ProfileRules.IsClassPlayable("warrior"), Is.True);
            Assert.That(ProfileRules.IsClassPlayable("mage"), Is.False); Assert.That(ProfileRules.IsClassPlayable("archer"), Is.False);
        }

        [Test]
        public void ProfileClasses_AreJsonUtilityFriendly()
        {
            foreach (Type type in new[] { typeof(PlayerProfile), typeof(CharacterProfile), typeof(Loadout), typeof(LoadoutRecord), typeof(ProfileStats) })
            {
                Assert.That(Attribute.IsDefined(type, typeof(SerializableAttribute)), Is.True, type.Name);
                Assert.That(type.GetProperties(), Is.Empty, type.Name + " has no properties");
            }
        }
    }
}
