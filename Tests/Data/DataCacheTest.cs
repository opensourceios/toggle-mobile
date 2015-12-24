using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Data.DataObjects;

namespace Toggl.Phoebe.Tests.Data
{
    [TestFixture]
    public class DataCacheTest : Test
    {
        [Test]
        public async Task TestGetInvalid ()
        {
            var cache = new DataCache ();
            var data = await cache.GetAsync<WorkspaceData> (Guid.NewGuid ());
            Assert.IsNull (data);
        }

        [Test]
        public async Task TestGetHit ()
        {
            var data = await DataStore.PutAsync (new WorkspaceData () {
                Name = "Testing",
            });
            var pk = data.Id;

            var cache = new DataCache ();
            data = await cache.GetAsync<WorkspaceData> (pk);
            Assert.IsNotNull (data);
            Assert.AreEqual (pk, data.Id);
            Assert.AreEqual ("Testing", data.Name);
        }

        [Test]
        public async Task TestGetHitMultiple ()
        {
            var data = await DataStore.PutAsync (new WorkspaceData () {
                Name = "Testing",
            });
            var pk = data.Id;

            var cache = new DataCache ();
            var tasks = new List<Task<WorkspaceData>> (10);
            for (var i = 0; i < 2; i++) {
                tasks.Add (cache.GetAsync<WorkspaceData> (pk));
                tasks.Add (cache.GetAsync<WorkspaceData> (pk));
            }

            await Task.WhenAll (tasks);
            var results = tasks.Select (t => t.Result);
            data = results.First ();
            Assert.That (results, Has.All.Matches<WorkspaceData> (r => Object.ReferenceEquals (r, data)));
        }

        [Test]
        public void TestTryGetInvalid ()
        {
            var cache = new DataCache ();
            WorkspaceData data;
            var success = cache.TryGetCached (Guid.NewGuid (), out data);
            Assert.IsFalse (success);
            Assert.IsNull (data);
        }

        [Test]
        public async Task TestTryGetMiss ()
        {
            var data = await DataStore.PutAsync (new WorkspaceData () {
                Name = "Testing",
            });
            var pk = data.Id;

            var cache = new DataCache ();
            var success = cache.TryGetCached (pk, out data);
            Assert.IsFalse (success);
            Assert.IsNull (data);
        }

        [Test]
        public async Task TestTryGetLoading ()
        {
            var data = await DataStore.PutAsync (new WorkspaceData () {
                Name = "Testing",
            });
            var pk = data.Id;

            var cache = new DataCache ();
            await cache.GetAsync<WorkspaceData> (pk);

            var success = cache.TryGetCached (pk, out data);
            if (success) {
                Assert.IsNotNull (data);
                Assert.Inconclusive ("Data was loaded before we could check that cache was empty. If it happens all the time, there is a problem.");
            } else {
                Assert.IsNull (data);
            }
        }

        [Test]
        public async Task TestTryGetHit ()
        {
            var data = await DataStore.PutAsync (new WorkspaceData () {
                Name = "Testing",
            });
            var pk = data.Id;

            var cache = new DataCache ();
            await cache.GetAsync<WorkspaceData> (pk);

            var success = cache.TryGetCached (pk, out data);
            Assert.IsTrue (success);
            Assert.IsNotNull (data);
            Assert.AreEqual (pk, data.Id);
        }

        [Test]
        public async Task TestTryGetTrimSize ()
        {
            var pks = await DataStore.ExecuteInTransactionAsync (ctx => {
                var ids = new List<Guid> ();

                for (var i = 0; i < 150; i++) {
                    var ws = ctx.Put (new WorkspaceData () {
                        Name = String.Format ("Space #{0}", i + 1),
                    });
                    ids.Add (ws.Id);
                }

                return ids;
            });

            WorkspaceData data;
            var datas = new List<WorkspaceData> ();
            var cache = new DataCache (100, TimeSpan.FromMinutes (5));
            foreach (var pk in pks) {
                data = await cache.GetAsync<WorkspaceData> (pk);
                Assert.NotNull (data);

                datas.Add (data);
            }

            var success = cache.TryGetCached<WorkspaceData> (pks.First (), out data);
            Assert.IsFalse (success);

            success = cache.TryGetCached<WorkspaceData> (pks.Last (), out data);
            Assert.IsTrue (success);
        }

        [Test]
        public async Task TestUpdateCached ()
        {
            var data = await DataStore.PutAsync (new WorkspaceData () {
                Name = "Testing",
            });
            var pk = data.Id;

            var cache = new DataCache ();
            data = await cache.GetAsync<WorkspaceData> (pk);
            Assert.IsNotNull (data);

            await DataStore.PutAsync (new WorkspaceData () {
                Id = pk,
                Name = "Foo",
            });

            var success = cache.TryGetCached (pk, out data);
            Assert.IsTrue (success);
            Assert.IsNotNull (data);
            Assert.AreEqual ("Foo", data.Name);
        }

        [Test]
        public async Task TestEvictCached ()
        {
            var data = await DataStore.PutAsync (new WorkspaceData () {
                Name = "Testing",
            });
            var pk = data.Id;

            var cache = new DataCache ();
            data = await cache.GetAsync<WorkspaceData> (pk);
            Assert.IsNotNull (data);

            await DataStore.DeleteAsync (data);

            var success = cache.TryGetCached (pk, out data);
            Assert.IsFalse (success);
            Assert.IsNull (data);
        }
    }
}
