using NUnit.Framework;
using PersonalArena.Core.Meta;
using PersonalArena.Core.Survivor;

namespace PersonalArena.View.Tests
{
    public sealed class ClassViewLogicTests
    {
        [TestCase("warrior", "Warrior", "Chiến binh", "CHIẾN BINH")]
        [TestCase("mage", "Mage", "Pháp sư", "PHÁP SƯ")]
        [TestCase("archer", "Archer", "Cung thủ", "CUNG THỦ")]
        public void Names_RoundTripBetweenClassAndBehavior(string classId, string behavior, string display, string upper)
        {
            Assert.That(ClassViewLogic.BehaviorName(classId), Is.EqualTo(behavior));
            Assert.That(ClassViewLogic.ClassIdOfBehavior(behavior), Is.EqualTo(classId));
            Assert.That(ClassViewLogic.ClassIdOfBehavior(behavior.ToLowerInvariant()), Is.EqualTo(classId));
            Assert.That(ClassViewLogic.DisplayName(classId), Is.EqualTo(display));
            Assert.That(ClassViewLogic.UpperName(classId), Is.EqualTo(upper));
            Assert.That(ClassViewLogic.AiTitle(classId), Is.EqualTo("AI " + upper));
            Assert.That(ClassViewLogic.PlayStyle(classId), Is.Not.Empty);
        }

        [Test]
        public void Names_UnknownIdsFallBackOnlyForDisplay()
        {
            Assert.That(ClassViewLogic.ClassIds, Is.EqualTo(new[] { "warrior", "mage", "archer" }));
            Assert.That(ClassViewLogic.ClassIdOfBehavior("Paladin"), Is.Null);
            Assert.That(ClassViewLogic.ClassIdOfBehavior(null), Is.Null);
            Assert.That(ClassViewLogic.BehaviorName(null), Is.EqualTo("Warrior"));
            Assert.That(ClassViewLogic.DisplayName("MAGE"), Is.EqualTo("Pháp sư"));
        }

        [Test]
        public void Shop_FollowsGoldOwnershipAndSelection()
        {
            PlayerProfile profile = ProfileRules.NewProfile();

            Assert.That(ClassViewLogic.ShopState(profile, "warrior"), Is.EqualTo(ClassShopState.Selected));
            Assert.That(ClassViewLogic.ShopState(profile, "mage"), Is.EqualTo(ClassShopState.TooExpensive));
            Assert.That(ClassViewLogic.ShopState(profile, "archer"), Is.EqualTo(ClassShopState.TooExpensive));
            Assert.That(ClassViewLogic.ShopState(profile, "paladin"), Is.EqualTo(ClassShopState.Unavailable));
            Assert.That(ClassViewLogic.ShopStatus(profile, "mage"), Is.EqualTo("Thiếu 1.500 vàng"));
            Assert.That(ClassViewLogic.ShopStatus(profile, "paladin"), Is.EqualTo("Chưa mở"));

            profile.Gold = 2000;
            Assert.That(ClassViewLogic.ShopState(profile, "mage"), Is.EqualTo(ClassShopState.Buyable));
            Assert.That(ClassViewLogic.ShopStatus(profile, "mage"), Is.EqualTo("Giá 1.500 vàng"));
            Assert.That(ClassViewLogic.ShopStatus(profile, "archer"), Is.EqualTo("Thiếu 1.000 vàng"));

            Assert.That(ProfileRules.TryBuyClass(profile, "mage"), Is.True);
            Assert.That(profile.Gold, Is.EqualTo(500));
            Assert.That(ClassViewLogic.ShopState(profile, "mage"), Is.EqualTo(ClassShopState.Owned));
            Assert.That(ClassViewLogic.ShopStatus(profile, "mage"), Does.StartWith("Đã có"));
            Assert.That(ClassViewLogic.SelectedClassId(profile), Is.EqualTo("warrior"), "buying does not select");

            Assert.That(ProfileRules.TrySelectClass(profile, "mage"), Is.True);
            Assert.That(ClassViewLogic.ShopState(profile, "mage"), Is.EqualTo(ClassShopState.Selected));
            Assert.That(ClassViewLogic.ShopState(profile, "warrior"), Is.EqualTo(ClassShopState.Owned));
            Assert.That(ClassViewLogic.ShopStatus(profile, "mage"), Is.EqualTo("Đang dùng"));
            Assert.That(ClassViewLogic.SelectedClassId(profile), Is.EqualTo("mage"));
            Assert.That(ClassViewLogic.SelectedCharacter(profile).ClassId, Is.EqualTo("mage"));
            Assert.That(ClassViewLogic.SelectedCharacter(profile).BrainRunId, Is.EqualTo("mage-s001"));
        }

        [Test]
        public void Shop_UnownedSelectionShowsTheWarrior()
        {
            PlayerProfile profile = ProfileRules.NewProfile();
            profile.SelectedClassId = "archer";

            Assert.That(ClassViewLogic.SelectedClassId(profile), Is.EqualTo("warrior"));
            Assert.That(ClassViewLogic.SelectedCharacter(profile).ClassId, Is.EqualTo("warrior"));
            Assert.That(ClassViewLogic.SelectedClassId(null), Is.EqualTo("warrior"));
            Assert.That(ClassViewLogic.SelectedCharacter(null), Is.Null);
        }

        [Test]
        public void Shop_ButtonsMatchTheState()
        {
            Assert.That(ClassViewLogic.BuyLabel("mage"), Is.EqualTo("Mua (1.500 vàng)"));
            Assert.That(ClassViewLogic.BuyLabel("archer"), Is.EqualTo("Mua (3.000 vàng)"));

            Assert.That(ClassViewLogic.ShowsBuy(ClassShopState.Buyable), Is.True);
            Assert.That(ClassViewLogic.ShowsBuy(ClassShopState.TooExpensive), Is.True);
            Assert.That(ClassViewLogic.ShowsBuy(ClassShopState.Owned), Is.False);
            Assert.That(ClassViewLogic.ShowsBuy(ClassShopState.Unavailable), Is.False);
            Assert.That(ClassViewLogic.CanBuy(ClassShopState.Buyable), Is.True);
            Assert.That(ClassViewLogic.CanBuy(ClassShopState.TooExpensive), Is.False);
            Assert.That(ClassViewLogic.CanSelect(ClassShopState.Owned), Is.True);
            Assert.That(ClassViewLogic.CanSelect(ClassShopState.Selected), Is.False);
            Assert.That(ClassViewLogic.CanSelect(ClassShopState.Buyable), Is.False);
            Assert.That(ClassViewLogic.SelectLabel(ClassShopState.Selected), Is.EqualTo("Đang dùng"));
            Assert.That(ClassViewLogic.SelectLabel(ClassShopState.Owned), Is.EqualTo("Chọn"));
        }

        [Test]
        public void TrainAction_SwitchesWhenAnotherClassTrains()
        {
            Assert.That(ClassViewLogic.TrainAction(TrainingState.Idle, null, "Mage"), Is.EqualTo(TrainButtonAction.Start));
            Assert.That(ClassViewLogic.TrainAction(TrainingState.Stopped, "Warrior", "Mage"), Is.EqualTo(TrainButtonAction.Start));
            Assert.That(ClassViewLogic.TrainAction(TrainingState.Error, "Warrior", "Mage"), Is.EqualTo(TrainButtonAction.Start));
            Assert.That(ClassViewLogic.TrainAction(TrainingState.Training, "Mage", "Mage"), Is.EqualTo(TrainButtonAction.Stop));
            Assert.That(ClassViewLogic.TrainAction(TrainingState.Training, "mage", "Mage"), Is.EqualTo(TrainButtonAction.Stop));
            Assert.That(ClassViewLogic.TrainAction(TrainingState.Starting, null, "Mage"), Is.EqualTo(TrainButtonAction.Stop),
                "no status yet (just started) counts as the selected class");
            Assert.That(ClassViewLogic.TrainAction(TrainingState.Training, ClassViewLogic.StatusBehavior(true, ""), "Mage"),
                Is.EqualTo(TrainButtonAction.SwitchClass), "an older service that does not report its behavior only ever trained the Warrior");
            Assert.That(ClassViewLogic.StatusBehavior(true, null), Is.EqualTo("Warrior"));
            Assert.That(ClassViewLogic.StatusBehavior(true, " Mage "), Is.EqualTo("Mage"));
            Assert.That(ClassViewLogic.StatusBehavior(false, "Mage"), Is.Null);
            Assert.That(ClassViewLogic.TrainAction(TrainingState.Training, "Warrior", "Mage"), Is.EqualTo(TrainButtonAction.SwitchClass));
            Assert.That(ClassViewLogic.TrainAction(TrainingState.Starting, "Archer", "Warrior"), Is.EqualTo(TrainButtonAction.SwitchClass));
            Assert.That(ClassViewLogic.TrainAction(TrainingState.Stopping, "Warrior", "Mage"), Is.EqualTo(TrainButtonAction.None));
            Assert.That(ClassViewLogic.TrainAction(TrainingState.External, "Warrior", "Mage"), Is.EqualTo(TrainButtonAction.None));
            Assert.That(ClassViewLogic.TrainAction(TrainingState.Unavailable, null, "Mage"), Is.EqualTo(TrainButtonAction.None));

            Assert.That(ClassViewLogic.IsOtherClass("Warrior", "Mage"), Is.True);
            Assert.That(ClassViewLogic.IsOtherClass(" Mage ", "mage"), Is.False);
            Assert.That(ClassViewLogic.IsOtherClass("", "Mage"), Is.False);
        }

        [Test]
        public void TrainRunId_StartsABoughtClassUnderItsOwnRunName()
        {
            Assert.That(ClassViewLogic.TrainRunId("warrior-s001-b2", "warrior-s001", "warrior", true, true),
                Is.EqualTo("warrior-s001-b2"), "a resumable branch always wins");
            Assert.That(ClassViewLogic.TrainRunId(null, "mage-s001", "mage", false, false), Is.EqualTo("mage-s001"));
            Assert.That(ClassViewLogic.TrainRunId("", "archer-s001", "archer", false, false), Is.EqualTo("archer-s001"));
            Assert.That(ClassViewLogic.TrainRunId(null, "mage-s001", "mage", true, false), Is.Null, "folder exists: the service resumes it");
            Assert.That(ClassViewLogic.TrainRunId(null, "mage-s001", "mage", false, true), Is.Null, "the class already has a run");
            Assert.That(ClassViewLogic.TrainRunId(null, "warrior-s001", "mage", false, false), Is.Null, "never train a class under another class's run");
            Assert.That(ClassViewLogic.TrainRunId(null, "../mage", "mage", false, false), Is.Null);
            Assert.That(ClassViewLogic.TrainRunId(null, null, "mage", false, false), Is.Null);
        }

        [Test]
        public void OwnerTraining_SendsTheSelectedClassBuild()
        {
            PlayerProfile profile = ProfileRules.NewProfile();
            profile.Gold = 5000;
            Assert.That(ProfileRules.TryBuyClass(profile, "mage"), Is.True);
            CharacterProfile mage = ProfileRules.FindCharacter(profile, "mage");
            mage.Level = 2;
            Assert.That(ProfileRules.TryAddPoint(mage, mage.ActiveLoadout, StatId.MaxHp), Is.True);
            Assert.That(ProfileRules.TryAddPoint(mage, mage.ActiveLoadout, StatId.MaxHp), Is.True);

            OwnerTraining warriorRun = MetaViewLogic.OwnerTrainingFor(profile);
            Assert.That(warriorRun.Points, Is.Null, "the level-0 Warrior keeps random builds");

            Assert.That(ProfileRules.TrySelectClass(profile, "mage"), Is.True);
            OwnerTraining mageRun = MetaViewLogic.OwnerTrainingFor(profile);
            Assert.That(mageRun.Points, Is.Not.Null);
            Assert.That(mageRun.Points[(int)StatId.MaxHp], Is.EqualTo(2), "the Mage's own points are trained");
            Assert.That(OwnerTraining.Arguments(mageRun), Does.Contain(" --owner-build 2,"));
        }

        [Test]
        public void TrainingTexts_NameTheClass()
        {
            Assert.That(ClassViewLogic.SwitchTrainLabel("mage"), Is.EqualTo("HUẤN LUYỆN PHÁP SƯ"));
            Assert.That(ClassViewLogic.SwitchNotice("Warrior", "mage"), Does.Contain("Chiến binh").And.Contain("Pháp sư"));
            Assert.That(ClassViewLogic.OtherClassTrainingLine("Archer", "mage"), Does.Contain("Cung thủ").And.Contain("Pháp sư"));
            Assert.That(ClassViewLogic.OtherClassTrainingLine("Robot", "mage"), Does.Contain("Robot"));

            Assert.That(ClassViewLogic.NoBrainText("mage", true), Does.Contain("Chưa có não Mage").And.Contain("HUẤN LUYỆN AI"));
            Assert.That(ClassViewLogic.NoBrainText("archer", false), Does.Contain("Chưa có não Archer").And.Not.Contain("HUẤN LUYỆN AI"));
            Assert.That(ClassViewLogic.NoBrainText("warrior", true), Does.StartWith("Chiến binh chưa có bộ não"));
        }
    }
}
