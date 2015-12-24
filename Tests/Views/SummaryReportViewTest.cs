using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Json;
using Toggl.Phoebe.Data.Json.Converters;
using Toggl.Phoebe.Data.Reports;
using Toggl.Phoebe.Net;
using XPlatUtils;

namespace Toggl.Phoebe.Tests.Views
{
    [TestFixture]
    public class SummaryReportViewTest : Test
    {
        private UserData user;
        private WorkspaceData workspace;
        private DateTime startTime;
        private DateTime endTime;

        public override async Task SetUp ()
        {
            await base.SetUp ();

            workspace = await DataStore.PutAsync (new WorkspaceData () {
                Name = "Test",
                RemoteId = 9999
            });
            user = await DataStore.PutAsync (new UserData () {
                Name = "John Doe",
                TrackingMode = TrackingMode.StartNew,
                DefaultWorkspaceId = workspace.Id,
                StartOfWeek = DayOfWeek.Monday,
            });
            await SetUpFakeUser (user.Id);

            // configure IReportClient service
            var serviceMock = new Mock<IReportsClient>();

            startTime = ResolveStartDate ( DateTime.Now, ZoomLevel.Week, user);
            endTime = ResolveEndDate ( startTime, ZoomLevel.Week);

            serviceMock.Setup ( x => x.GetReports ( startTime, endTime, Convert.ToInt64 ( workspace.RemoteId)))
                .Returns ( Task.FromResult (CreateEmptyReportJson ( ZoomLevel.Week, startTime)));

            startTime = ResolveStartDate ( DateTime.Now, ZoomLevel.Month, user);
            endTime = ResolveEndDate ( startTime, ZoomLevel.Month);

            serviceMock.Setup ( x => x.GetReports ( startTime, endTime, Convert.ToInt64 ( workspace.RemoteId)))
                .Returns ( Task.FromResult (CreateEmptyReportJson ( ZoomLevel.Month, startTime)));

            startTime = ResolveStartDate ( DateTime.Now, ZoomLevel.Year, user);
            endTime = ResolveEndDate ( startTime, ZoomLevel.Year);

            serviceMock.Setup ( x => x.GetReports ( startTime, endTime, Convert.ToInt64 ( workspace.RemoteId)))
                .Returns ( Task.FromResult (CreateEmptyReportJson ( ZoomLevel.Year, startTime)));

            ServiceContainer.Register<IReportsClient> (serviceMock.Object);
            ServiceContainer.Register<ReportJsonConverter> ();
        }

        [Test]
        public async Task TestCreateWeekReport ()
        {
            var view = new SummaryReportView ();
            view.Period = ZoomLevel.Week;
            await view.Load (0);
            Assert.AreEqual (false, view.IsLoading);
            Assert.AreEqual (false, view.IsError);
            Assert.AreEqual (true, view.ActivityCount == 7);
            Assert.AreEqual (true, view.Projects.Count > 0);
            Assert.AreEqual (true, view.CollapsedProjects.Count > 0);
        }

        [Test]
        public async Task TestCreateMonthReport ()
        {
            var view = new SummaryReportView ();
            view.Period = ZoomLevel.Month;
            await view.Load (0);
            Assert.AreEqual (false, view.IsLoading);
            Assert.AreEqual (false, view.IsError);
            Assert.AreEqual (true, view.ActivityCount > 27);
            Assert.AreEqual (true, view.Projects.Count > 0);
            Assert.AreEqual (true, view.CollapsedProjects.Count > 0);
        }

        [Test]
        public async Task TestCreateYearReport ()
        {
            var view = new SummaryReportView ();
            view.Period = ZoomLevel.Year;
            await view.Load (0);
            Assert.AreEqual (false, view.IsLoading);
            Assert.AreEqual (false, view.IsError);
            Assert.AreEqual (true, view.ActivityCount == 12);
            Assert.AreEqual (true, view.Projects.Count > 0);
            Assert.AreEqual (true, view.CollapsedProjects.Count > 0);
        }

        private ReportJson CreateEmptyReportJson ( ZoomLevel period, DateTime startDate)
        {
            var activityContainer = new ReportActivityJson ();
            var rows = new List<List<string>> ();
            var projectsJsonList = new List<ReportProjectJson> ();

            int total;
            if (period == ZoomLevel.Week) {
                total = 7;
            } else if (period == ZoomLevel.Month) {
                total = 30;
            } else {
                total = 12;
            }

            for (int i = 0; i < total; i++) {
                var activiy = new List<string> ();

                if (period == ZoomLevel.Week) {
                    activiy.Add ( startDate.AddDays (Convert.ToDouble (i)).ToLongDateString());
                } else if (period == ZoomLevel.Month) {
                    activiy.Add ( startDate.AddDays (Convert.ToDouble (i)).ToLongDateString());
                } else {
                    activiy.Add ( startDate.AddMonths (i).ToLongDateString());
                }
                activiy.Add ( "0"); // add totalTime
                activiy.Add ( "0"); // add billableTime
                rows.Add (activiy);
            }
            activityContainer.Rows = rows;

            for (int i = 0; i < 5; i++) {
                var project = new ReportProjectJson ();
                project.Description = new ReportProjectDescJson() {
                    Client = "client",
                    Color = "0",
                    Project = "project"
                };
                project.TotalTime = 1;
                project.Currencies = new List<ReportCurrencyJson> () {
                    new ReportCurrencyJson() {
                        Amount = 1,
                        Currency = "eur"
                    }
                };
                projectsJsonList.Add (project);
            }

            return new ReportJson () {
                Projects = projectsJsonList,
                ActivityContainer = activityContainer,
                TotalBillable = 1,
                TotalGrand = 1,
                TotalCurrencies = new List<ReportCurrencyJson> () {
                    new ReportCurrencyJson() { Amount = 0, Currency = "eur" }
                }
            };
        }

        private DateTime ResolveStartDate ( DateTime currentDate, ZoomLevel period, UserData usr)
        {
            DateTime result;
            if (period == ZoomLevel.Week) {
                result = currentDate.StartOfWeek (usr.StartOfWeek);
            } else if (period == ZoomLevel.Month) {
                result = new DateTime (currentDate.Year, currentDate.Month, 1);
            } else {
                result = new DateTime (currentDate.Year, 1, 1);
            }
            return result;
        }

        private DateTime ResolveEndDate (DateTime start, ZoomLevel period)
        {
            if (period == ZoomLevel.Week) {
                return start.AddDays (6);
            }
            if (period == ZoomLevel.Month) {
                return start.AddMonths (1).AddDays (-1);
            }
            return start.AddYears (1).AddDays (-1);
        }
    }
}
