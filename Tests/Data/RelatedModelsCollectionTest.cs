using System;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using NUnit.Framework;
using Toggl.Phoebe.Data;
using XPlatUtils;

namespace Toggl.Phoebe.Tests.Data
{
    [TestFixture]
    public class RelatedModelsCollectionTest : Test
    {
        private string tmpDb;

        private IModelStore ModelStore
        {
            get { return ServiceContainer.Resolve<IModelStore> (); }
        }

        public override async Task SetUp ()
        {
            await base.SetUp ();

            tmpDb = Path.GetTempFileName ();
            ServiceContainer.Register<IModelStore> (new TestSqliteStore (tmpDb));
        }

        public override async Task TearDown ()
        {
            ModelStore.Commit ();

            await base.TearDown ();

            File.Delete (tmpDb);
            tmpDb = null;
        }

        [Test]
        public void TestRelationAdding ()
        {
            var item = Model.Update (new ItemModel () {
                Name = "Phone",
                IsPersisted = true,
            });
            var tag = Model.Update (new TagModel () {
                Name = "Wired",
                IsPersisted = true,
            });
            ModelStore.Commit ();

            var inter = item.Tags.Add (tag);
            Assert.AreSame (tag, inter.To);
            Assert.AreEqual (tag.Id, inter.ToId);
            Assert.AreSame (item, inter.From);
            Assert.AreEqual (item.Id, inter.FromId);

            ModelStore.Commit ();

            Assert.AreSame (inter, item.Tags.Single ());
            Assert.AreSame (inter, tag.Items.Single ());
        }

        [Test]
        public void TestManyRelations ()
        {
            var items = new ItemModel[] {
                Model.Update (new ItemModel ()
                {
                    Name = "Phone",
                    IsPersisted = true,
                }),
                Model.Update (new ItemModel ()
                {
                    Name = "Fridge",
                    IsPersisted = true,
                }),
                Model.Update (new ItemModel ()
                {
                    Name = "Cup",
                    IsPersisted = true,
                }),
            };
            var tags = new TagModel[] {
                Model.Update (new TagModel ()
                {
                    Name = "Wired",
                    IsPersisted = true,
                }),
                Model.Update (new TagModel ()
                {
                    Name = "Kitchen",
                    IsPersisted = true,
                }),
                Model.Update (new TagModel ()
                {
                    Name = "Container",
                    IsPersisted = true,
                }),
            };
            items [0].Tags.Add (tags [1]);
            items [1].Tags.Add (tags [0]);
            items [1].Tags.Add (tags [1]);
            items [1].Tags.Add (tags [2]);
            items [2].Tags.Add (tags [2]);
            items [2].Tags.Add (tags [1]);
            ModelStore.Commit ();

            Assert.AreEqual (1, tags [0].Items.Count ());
            Assert.AreEqual (3, tags [1].Items.Count ());
            Assert.AreEqual (2, tags [2].Items.Count ());
            Assert.AreEqual (1, items [0].Tags.Count ());
            Assert.AreEqual (3, items [1].Tags.Count ());
            Assert.AreEqual (2, items [2].Tags.Count ());
        }

        [Test]
        public void TestRelationRemovalByDelete ()
        {
            var item = Model.Update (new ItemModel () {
                Name = "Phone",
                IsPersisted = true,
            });
            var tag = Model.Update (new TagModel () {
                Name = "Wired",
                IsPersisted = true,
            });
            var inter = item.Tags.Add (tag);
            ModelStore.Commit ();

            inter.Delete ();
            ModelStore.Commit ();

            Assert.IsEmpty (item.Tags);
            Assert.IsEmpty (tag.Items);
        }

        [Test]
        public void TestModelDelete ()
        {
            var item = Model.Update (new ItemModel () {
                Name = "Phone",
                IsPersisted = true,
            });
            var tag = Model.Update (new TagModel () {
                Name = "Wired",
                IsPersisted = true,
            });
            item.Tags.Add (tag);
            ModelStore.Commit ();

            item.Delete ();
            ModelStore.Commit ();

            Assert.IsEmpty (tag.Items);
        }

        [Test]
        public void TestAddToNonPersisted ()
        {
            var item = Model.Update (new ItemModel () {
                Name = "Phone",
            });
            var tag = Model.Update (new TagModel () {
                Name = "Wired",
                IsPersisted = true,
            });
            item.Tags.Add (tag);
        }

        [Test]
        public void TestAddNonPersisted ()
        {
            var item = Model.Update (new ItemModel () {
                Name = "Phone",
                IsPersisted = true,
            });
            var tag = Model.Update (new TagModel () {
                Name = "Wired",
            });
            item.Tags.Add (tag);
        }

        [Test]
        public void TestAddBothNonPersisted ()
        {
            var item = Model.Update (new ItemModel () {
                Name = "Phone",
            });
            var tag = Model.Update (new TagModel () {
                Name = "Wired",
            });
            item.Tags.Add (tag);
        }

        [Test]
        public void TestBothNonPersistedToPersisted ()
        {
            var item = Model.Update (new ItemModel () {
                Name = "Phone",
            });
            var tag = Model.Update (new TagModel () {
                Name = "Wired",
            });
            var inter = item.Tags.Add (tag);

            Assert.IsFalse (inter.IsPersisted);

            item.IsPersisted = true;

            Assert.IsFalse (tag.IsPersisted);
            Assert.IsFalse (inter.IsPersisted);

            tag.IsPersisted = true;

            Assert.IsTrue (inter.IsPersisted);
        }

        [Test]
        public void TestPersistedToNonPersisted ()
        {
            var item = Model.Update (new ItemModel () {
                Name = "Phone",
                IsPersisted = true,
            });
            var tag = Model.Update (new TagModel () {
                Name = "Wired",
                IsPersisted = true,
            });
            var inter = item.Tags.Add (tag);

            Assert.IsTrue (inter.IsPersisted);

            item.IsPersisted = false;

            Assert.IsFalse (inter.IsPersisted);
        }

        [Test]
        public void TestRelationRemoval ()
        {
            var item = Model.Update (new ItemModel () {
                Name = "Phone",
                IsPersisted = true,
            });
            var tag = Model.Update (new TagModel () {
                Name = "Wired",
                IsPersisted = true,
            });
            item.Tags.Add (tag);
            ModelStore.Commit ();

            item.Tags.Remove (tag);
            ModelStore.Commit ();

            Assert.IsEmpty (item.Tags);
            Assert.IsEmpty (tag.Items);
        }

        [Test]
        public void TestRelationClear ()
        {
            var item = Model.Update (new ItemModel () {
                Name = "Phone",
                IsPersisted = true,
            });
            var tag = Model.Update (new TagModel () {
                Name = "Wired",
                IsPersisted = true,
            });
            item.Tags.Add (tag);
            ModelStore.Commit ();

            item.Tags.Clear ();
            ModelStore.Commit ();

            Assert.IsEmpty (item.Tags);
            Assert.IsEmpty (tag.Items);
        }

        private class TagModel : Model
        {
            private static string GetPropertyName<T> (Expression<Func<TagModel, T>> expr)
            {
                return expr.ToPropertyName ();
            }

            private readonly RelatedModelsCollection<ItemModel, ItemTagModel, ItemModel, TagModel> itemsCollection;

            public TagModel ()
            {
                itemsCollection = new RelatedModelsCollection<ItemModel, ItemTagModel, ItemModel, TagModel> (this);
            }

            public override void Delete ()
            {
                base.Delete ();
                Items.Clear ();
            }

            private string name;
            public static readonly string PropertyName = GetPropertyName ((m) => m.Name);

            public string Name
            {
                get { return name; }
                set {
                    if (name == value) {
                        return;
                    }

                    ChangePropertyAndNotify (PropertyName, delegate {
                        name = value;
                    });
                }
            }

            public RelatedModelsCollection<ItemModel, ItemTagModel, ItemModel, TagModel> Items
            {
                get { return itemsCollection; }
            }
        }

        private class ItemModel : Model
        {
            private static string GetPropertyName<T> (Expression<Func<ItemModel, T>> expr)
            {
                return expr.ToPropertyName ();
            }

            private readonly RelatedModelsCollection<TagModel, ItemTagModel, ItemModel, TagModel> tagsCollection;

            public ItemModel ()
            {
                tagsCollection = new RelatedModelsCollection<TagModel, ItemTagModel, ItemModel, TagModel> (this);
            }

            public override void Delete ()
            {
                base.Delete ();
                Tags.Clear ();
            }

            private string name;
            public static readonly string PropertyName = GetPropertyName ((m) => m.Name);

            public string Name
            {
                get { return name; }
                set {
                    if (name == value) {
                        return;
                    }

                    ChangePropertyAndNotify (PropertyName, delegate {
                        name = value;
                    });
                }
            }

            public RelatedModelsCollection<TagModel, ItemTagModel, ItemModel, TagModel> Tags
            {
                get { return tagsCollection; }
            }
        }

        private class ItemTagModel : IntermediateModel<ItemModel, TagModel>
        {
            public static implicit operator ItemModel (ItemTagModel m)
            {
                return m.From;
            }

            public static implicit operator TagModel (ItemTagModel m)
            {
                return m.To;
            }
        }

        private class TestSqliteStore : SQLiteModelStore
        {
            public TestSqliteStore (string path) : base (path)
            {
            }

            protected override void CreateTables (SQLite.SQLiteConnection db)
            {
                db.CreateTable<ItemModel> ();
                db.CreateTable<TagModel> ();
                db.CreateTable<ItemTagModel> ();
            }

            protected override void ScheduleCommit ()
            {
                // Only manual commits
            }
        }
    }
}
