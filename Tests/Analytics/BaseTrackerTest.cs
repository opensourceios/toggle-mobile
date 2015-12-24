using System;
using Moq;
using NUnit.Framework;
using Toggl.Phoebe.Analytics;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Net;
using XPlatUtils;
using System.Threading.Tasks;

namespace Toggl.Phoebe.Tests.Analytics
{
    [TestFixture]
    public class BaseTrackerTest : Test
    {
        private TestTracker tracker;

        public override async Task SetUp ()
        {
            await base.SetUp ();
            ServiceContainer.Register<ISettingsStore> (Mock.Of<ISettingsStore> (
                        (store) => store.ExperimentId == (string)null));
            ServiceContainer.Register<ExperimentManager> (new ExperimentManager ());
            tracker = new TestTracker ();
        }

        [Test]
        public void TestSendAppInitTime ()
        {
            Assert.Throws<ArgumentException> (() =>
                tracker.SendAppInitTime (TimeSpan.FromMilliseconds (1000)));
        }

        [Test]
        public void TestAuthChanged()
        {
            Assert.Throws<ArgumentException> (()=>SendAuthMessage (AuthChangeReason.Login), "Start a new session whenever the user changes.");
            Assert.DoesNotThrow (()=>SendAuthMessage (AuthChangeReason.Signup), "Exception being signup where the user just created an account.");
        }

        private void SendAuthMessage (AuthChangeReason reason)
        {
            var authManager = new AuthManager ();
            ServiceContainer.Register<AuthManager> (authManager);
            MessageBus.Send (new AuthChangedMessage (authManager, reason));
        }

        [Test]
        public void TestSendSettingsChangeEvent ()
        {
            Assert.Throws<ArgumentException> (()=> tracker.SendSettingsChangeEvent (SettingName.AskForProject));
            Assert.AreEqual (tracker.CurrentSendData.Label, "AskForProject");

            try {
                tracker.SendSettingsChangeEvent ((SettingName)100);
            } catch (ArgumentException e) {
                Assert.AreNotEqual (TestTracker.SendEventExceptionMessage, e.Message);
            }
        }

        [Test]
        public void TestSendAccountLoginEvent ()
        {
            Assert.Throws<ArgumentException> (()=> tracker.SendAccountLoginEvent (AccountCredentials.Password));
            Assert.AreEqual (tracker.CurrentSendData.Label, "Password");

            try {
                tracker.SendAccountLoginEvent ((AccountCredentials)100);
            } catch (ArgumentException e) {
                Assert.AreNotEqual (TestTracker.SendEventExceptionMessage, e.Message);
            }
        }

        [Test]
        public void TestSendAccountCreateEvent ()
        {
            Assert.Throws<ArgumentException> (()=> tracker.SendAccountCreateEvent (AccountCredentials.Password));
            Assert.AreEqual (tracker.CurrentSendData.Label, "Password");

            try {
                tracker.SendAccountCreateEvent ((AccountCredentials)100);
            } catch (ArgumentException e) {
                Assert.AreNotEqual (TestTracker.SendEventExceptionMessage, e.Message);
            }
        }

        [Test]
        public void TestSendAccountLogoutEvent ()
        {
            Assert.Throws<ArgumentException> (tracker.SendAccountLogoutEvent);
        }

        [Test]
        public void TestSendTimerStartEvent ()
        {
            Assert.Throws<ArgumentException> (()=> tracker.SendTimerStartEvent (TimerStartSource.AppNew));

            try {
                tracker.SendTimerStartEvent ((TimerStartSource)100);
            } catch (ArgumentException e) {
                Assert.AreNotEqual (TestTracker.SendEventExceptionMessage, e.Message);
            }
        }

        [Test]
        public void TestSendTimerStopEvent ()
        {
            Assert.Throws<ArgumentException> (()=> tracker.SendTimerStopEvent (TimerStopSource.App));

            try {
                tracker.SendTimerStopEvent ((TimerStopSource)100);
            } catch (ArgumentException e) {
                Assert.AreNotEqual (TestTracker.SendEventExceptionMessage, e.Message);
            }
        }
    }
}

