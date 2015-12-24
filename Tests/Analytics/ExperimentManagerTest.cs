using System;
using Moq;
using NUnit.Framework;
using Toggl.Phoebe.Analytics;
using Toggl.Phoebe.Data;
using XPlatUtils;
using System.Threading.Tasks;

namespace Toggl.Phoebe.Tests.Analytics
{
    [TestFixture]
    public class ExperimentManagerTest : Test
    {
        private ExperimentManager manager;

        public override async Task SetUp ()
        {
            await base.SetUp ();

            ServiceContainer.Register<ISettingsStore> (Mock.Of<ISettingsStore> (
                        (store) => store.ExperimentId == (string)null));
            ServiceContainer.Register<ITimeProvider> (Mock.Of<ITimeProvider> (
                        (p) => p.Now == new DateTime (2014, 1, 1) &&
            p.TimeZoneId == "UTC" &&
            p.UtcNow == new DateTime (2014, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

            manager = new ExperimentManager (typeof (Registry));
        }

        [Test]
        public void TestRandomOverride()
        {
            var rand = new Random (1);
            manager.rand = new Random (1);

            for (var i = 0; i < 100; i++) {
                Assert.AreEqual (rand.Next (i), manager.RandomNumber (i));
            }
        }

        [Test]
        public void TestFreshInstallChoices ()
        {
            var list = manager.GetPossibleNextExperiments (true);
            Assert.That (list, Is.EquivalentTo (new [] {
                Registry.AnyTime,
                Registry.FreshInstallOnly,
                Registry.SetupSomething,
                Registry.ValidEternety,
            }));
        }

        [Test]
        public void TestUpgradeChoices ()
        {
            var list = manager.GetPossibleNextExperiments (false);
            Assert.That (list, Is.EquivalentTo (new [] {
                Registry.AnyTime,
                Registry.SetupSomething,
                Registry.ValidEternety,
            }));
        }

        [Test]
        public void TestStartOfTimeChoices ()
        {
            ServiceContainer.Register<ITimeProvider> (Mock.Of<ITimeProvider> (
                        (p) => p.Now == new DateTime (1, 1, 1) &&
            p.TimeZoneId == "UTC" &&
            p.UtcNow == new DateTime (1, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

            var list = manager.GetPossibleNextExperiments (false);
            Assert.That (list, Is.EquivalentTo (new [] {
                Registry.AnyTime,
                Registry.SetupSomething,
                Registry.EndInPast,
            }));
        }

        [Test]
        public void TestEndOfTimeChoices ()
        {
            ServiceContainer.Register<ITimeProvider> (Mock.Of<ITimeProvider> (
                        (p) => p.Now == new DateTime (9999, 1, 1) &&
            p.TimeZoneId == "UTC" &&
            p.UtcNow == new DateTime (9999, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

            var list = manager.GetPossibleNextExperiments (false);
            Assert.That (list, Is.EquivalentTo (new [] {
                Registry.AnyTime,
                Registry.SetupSomething,
                Registry.StartInFuture,
            }));
        }

        [Test]
        public void TestRestore()
        {
            ServiceContainer.Register<ISettingsStore> (Mock.Of<ISettingsStore> (
                        (store) => store.ExperimentId == "always"));

            manager = new ExperimentManager (typeof (Registry));
            Assert.AreEqual (Registry.AnyTime, manager.CurrentExperiment);
        }

        [Test]
        public void TestRestoreDisabled()
        {
            ServiceContainer.Register<ISettingsStore> (Mock.Of<ISettingsStore> (
                        (store) => store.ExperimentId == "disabled"));

            manager = new ExperimentManager (typeof (Registry));
            Assert.IsNull (manager.CurrentExperiment);
        }

        [Test]
        public void TestRestoreInvalid()
        {
            ServiceContainer.Register<ISettingsStore> (Mock.Of<ISettingsStore> (
                        (store) => store.ExperimentId == "someInvalidId"));

            manager = new ExperimentManager (typeof (Registry));
            Assert.IsNull (manager.CurrentExperiment);
        }

        [Test]
        public void TestNextExperiment()
        {
            Assert.IsNull (manager.CurrentExperiment);

            var choices = manager.GetPossibleNextExperiments (false);
            Assert.Contains (Registry.AnyTime, choices);

            // Find the correct seed
            int seed = 0;
            var idx = choices.IndexOf (Registry.AnyTime);
            while (new Random (seed).Next (choices.Count + 1) != idx) {
                seed += 1;
            }

            manager.rand = new Random (seed);
            manager.NextExperiment (false);
            Assert.AreEqual (Registry.AnyTime, manager.CurrentExperiment);
        }

        [Test]
        public void TestNextExperimentNone()
        {
            Assert.IsNull (manager.CurrentExperiment);

            var choices = manager.GetPossibleNextExperiments (false);

            // Find the correct seed
            int seed = 0;
            while (new Random (seed).Next (choices.Count + 1) != choices.Count) {
                seed += 1;
            }

            manager.rand = new Random (seed);
            manager.NextExperiment (false);
            Assert.IsNull (manager.CurrentExperiment);
        }
        [Test]
        public void TestSetup()
        {
            var choices = manager.GetPossibleNextExperiments (false);
            Assert.Contains (Registry.SetupSomething, choices);

            // Find the correct seed
            int seed = 0;
            var idx = choices.IndexOf (Registry.SetupSomething);
            while (new Random (seed).Next (choices.Count + 1) != idx) {
                seed += 1;
            }

            manager.rand = new Random (seed);
            Assert.Throws<SuccessException> (() => manager.NextExperiment (false));
        }

        private class Registry
        {
            public static Experiment AnyTime = new Experiment ()
            {
                Id = "always",
            };

            public static Experiment Disabled = new Experiment ()
            {
                Id = "disabled",
                Enabled = false,
            };

            public static Experiment FreshInstallOnly = new Experiment()
            {
                Id = "freshInstall",
                FreshInstallOnly = true,
            };

            public static Experiment SetupSomething = new Experiment ()
            {
                Id = "setupSomething",
                SetUp = delegate {
                    throw new SuccessException ("It works!");
                },
            };

            public static Experiment StartInFuture = new Experiment ()
            {
                Id = "startInFuture",
                StartTime = new DateTime (2050, 1, 1),
            };

            public static Experiment EndInPast = new Experiment ()
            {
                Id = "endInFuture",
                EndTime = new DateTime (2000, 1, 1),
            };

            public static Experiment ValidEternety = new Experiment ()
            {
                Id = "validForever",
                StartTime = new DateTime (2000, 1, 1),
                EndTime = new DateTime (3000, 1, 1),
            };
        }
    }
}
