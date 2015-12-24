using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Models;
using XPlatUtils;

namespace Toggl.Phoebe.Tests.Data.Models
{
    public abstract class ModelTest<T> : Test
        where T : IModel
    {
        protected static readonly string[] ExemptProperties = { "Data" };

        public override async Task SetUp ()
        {
            await base.SetUp ();

            ResetDataCache ();
        }

        private void ResetDataCache ()
        {
            ServiceContainer.Register<DataCache> ();
        }

        [Test]
        public void VerifyPropertyNames ()
        {
            const string fieldPrefix = "Property";
            var type = typeof (T);
            var fields = type.GetFields (BindingFlags.Static | BindingFlags.Public)
                         .Where (t => t.Name.StartsWith (fieldPrefix, StringComparison.Ordinal))
                         .ToList ();

            Assert.That (fields, Has.Count.AtLeast (1), String.Format ("{0} should define some property names.", type.Name));

            // Verify that defined property names have correct value and properties exist
            foreach (var field in fields) {
                var name = field.Name.Substring (fieldPrefix.Length);
                var value = field.GetValue (null);
                Assert.AreEqual (name, value, String.Format ("{0}.{1} property value is {2}.", type.Name, field.Name, value));

                var prop = type.GetProperty (name, BindingFlags.Public | BindingFlags.Instance);
                Assert.NotNull (prop, String.Format ("{0}.{1} property doesn't exist.", type.Name, name));
            }

            // Verify that all public properties have the property name defined
            var fieldNames = fields.Select (f => f.GetValue (null)).ToList ();
            var properties = type.GetProperties (BindingFlags.Instance | BindingFlags.Public);
            foreach (var prop in properties) {
                if (!ExemptProperties.Contains (prop.Name)) {
                    Assert.IsTrue (fieldNames.Contains (prop.Name), String.Format ("{0}.{1}{2} static field is not defined.", type.Name, fieldPrefix, prop.Name));
                }
            }
        }

        [Test]
        public void VerifyConstructors ()
        {
            var type = typeof (T);

            Assert.IsNotNull (type.GetConstructor (new Type[] { }), "Default constructor not found.");
            Assert.IsNotNull (type.GetConstructor (new[] { typeof (Guid) }), "Lazy load constructor not found.");

            var dataProp = type.GetProperty ("Data");
            Assert.IsNotNull (type.GetConstructor (new[] { dataProp.PropertyType }), "Wrapping constructor not found.");
        }

        [Test]
        public void PropertyChangeEvents ()
        {
            var type = typeof (T);

            var properties = type.GetProperties (BindingFlags.Instance | BindingFlags.Public)
                             .Where (prop => prop.CanWrite)
                             .Where (prop => !ExemptProperties.Contains (prop.Name));

            foreach (var prop in properties) {
                var changed = new List<string> ();
                var inst = Activator.CreateInstance<T> ();
                inst.PropertyChanged += (s, e) => changed.Add (e.PropertyName);

                SimulatePropertyChange (inst, prop);

                // Verify that the property changed event was fired:
                Assert.That (changed, Has.Some.Matches<string> (name => name == prop.Name),
                             String.Format ("{0} did not raise a PropertyChanged event.", prop.Name));
            }
        }

        [Test]
        public void ForeignRelations ()
        {
            var type = typeof (T);

            var properties = type.GetProperties (BindingFlags.Instance | BindingFlags.Public)
                             .Where (prop => prop.PropertyType.GetInterfaces ().Contains (typeof (IModel)))
                             .Where (prop => !ExemptProperties.Contains (prop.Name));

            foreach (var prop in properties) {
                var relationAttr = prop.GetCustomAttribute<ModelRelationAttribute> ();
                Assert.IsNotNull (relationAttr, String.Format ("{0} foreign relation property should have the ForeignRelationAttribute.", prop.Name));

                var changeCount = 0;
                var inst = Activator.CreateInstance<T> ();
                inst.PropertyChanged += (s, e) => changeCount += e.PropertyName == prop.Name ? 1 : 0;

                // Test default value:
                var val = (IModel)prop.GetValue (inst);

                if (relationAttr.Required) {
                    Assert.IsNotNull (val, String.Format ("{0} (required) property default value shouldn't be null.", prop.Name));
                    Assert.That (
                    (TestDelegate)delegate {
                        prop.SetValue (inst, null);
                    },
                    Throws.TargetInvocationException.With.InnerException.TypeOf<ArgumentNullException> (),
                    String.Format ("{0} (required) property should throw ArgumentNullException when setting null.", prop.Name)
                    );
                } else {
                    Assert.IsNull (val, String.Format ("{0} (optional) property default value should be null.", prop.Name));
                }

                // Initialize all properties to empty defaults:
                val = (IModel)Activator.CreateInstance (prop.PropertyType);
                prop.SetValue (inst, val);

                // Test setting values:
                changeCount = 0;
                val = (IModel)Activator.CreateInstance (prop.PropertyType);
                prop.SetValue (inst, val);
                Assert.AreEqual (1, changeCount, "Setting the value to new instance should've triggered property changed.");

                changeCount = 0;
                var pk = Guid.NewGuid ();
                val = (IModel)Activator.CreateInstance (prop.PropertyType, pk);
                prop.SetValue (inst, val);
                Assert.AreEqual (1, changeCount, "Setting the value to random item should've triggered property change.");

                if (!relationAttr.Required) {
                    changeCount = 0;
                    val = null;
                    prop.SetValue (inst, val);
                    Assert.AreEqual (1, changeCount, "Setting the value to null should've triggered property change.");
                }
            }
        }

        [Test]
        public async Task TestLazyLoad ()
        {
            var type = typeof (T);
            var isLoadedField = type.BaseType.GetField ("isLoaded",
                                BindingFlags.NonPublic | BindingFlags.Instance);
            var loadingTCSField = type.BaseType.GetField ("loadingTCS",
                                  BindingFlags.NonPublic | BindingFlags.Instance);

            // Create dummy element (with default values) to load:
            var pk = await CreateDummyData ();

            // Test property autoload by setting the value to something else and waiting to see if it is replaced
            // by autoloaded data.
            var properties = type.GetProperties (BindingFlags.Instance | BindingFlags.Public)
                             .Where (prop => prop.CanWrite)
                             .Where (prop => !ExemptProperties.Contains (prop.Name));

            foreach (var prop in properties) {
                ResetDataCache ();

                var inst = (T)Activator.CreateInstance (type, pk);

                var isLoaded = (bool)isLoadedField.GetValue (inst);
                var loadingTCS = (TaskCompletionSource<object>)loadingTCSField.GetValue (inst);
                Assert.False (isLoaded);
                Assert.IsNull (loadingTCS);

                SimulatePropertyChange (inst, prop);

                // Check private field isLoaded to determine that autoload has finished
                loadingTCS = (TaskCompletionSource<object>)loadingTCSField.GetValue (inst);
                isLoaded = (bool)isLoadedField.GetValue (inst);

                if (isLoaded) {
                    Assert.Inconclusive ("Model was loaded too fast. If this happens every time, there is a problem.");
                } else {
                    Assert.NotNull (loadingTCS);
                }
            }
        }

        [Test]
        public void TestOptionalRelationNullId ()
        {
            var type = typeof (T);
            var dataProp = type.GetProperty ("Data");

            var properties = type.GetProperties (BindingFlags.Instance | BindingFlags.Public)
                             .Where (IsOptionalRelationProperty);

            foreach (var prop in properties) {
                var idProp = dataProp.PropertyType.GetProperty (String.Concat (prop.Name, "Id"));
                if (idProp == null) {
                    continue;
                }

                // Create model, get data item, etc
                var inst = Activator.CreateInstance<T> ();
                var data = dataProp.GetValue (inst);
                var initialData = data;
                var val = idProp.GetValue (data);
                Assert.IsNull (val, String.Format ("Initial {0} should be null.", idProp.Name));

                // Assign a dummy model to the property
                var pk = Guid.NewGuid ();
                prop.SetValue (inst, Activator.CreateInstance (prop.PropertyType, pk));
                data = dataProp.GetValue (inst);
                var assignedData = data;
                val = idProp.GetValue (data);
                Assert.AreEqual (pk, val, "Id was not updated.");

                // Assign an empty model to the property
                prop.SetValue (inst, Activator.CreateInstance (prop.PropertyType));
                data = dataProp.GetValue (inst);
                val = idProp.GetValue (data);
                Assert.IsNull (val, String.Format ("{0} should be null for empty model.", idProp.Name));

                // Update data to update model:
                dataProp.SetValue (inst, assignedData);
                var model = (IModel)prop.GetValue (inst);
                Assert.AreEqual (idProp.GetValue (assignedData), model.Id);

                // Update data to update model:
                dataProp.SetValue (inst, initialData);
                model = (IModel)prop.GetValue (inst);
                Assert.IsNull (model);
            }
        }

        [Test]
        public async Task TestLoading ()
        {
            var type = typeof (T);

            // Test load new
            var inst = (T)Activator.CreateInstance (type);
            var loadTask = (Task)type.GetMethod ("LoadAsync").Invoke (inst, new object[0]);
            await loadTask;

            // Test load invalid
            var pk = Guid.NewGuid ();
            inst = (T)Activator.CreateInstance (type, pk);
            loadTask = (Task)type.GetMethod ("LoadAsync").Invoke (inst, new object[0]);
            await loadTask;

            // Test load valid task:
            pk = await CreateDummyData ();
            inst = (T)Activator.CreateInstance (type, pk);
            loadTask = (Task)type.GetMethod ("LoadAsync").Invoke (inst, new object[0]);
            await loadTask;
        }

        [Test]
        public virtual void TestTouching ()
        {
            var type = typeof (T);

            // Create dummy element (with default values) to load:
            var data = CreateDataInstance ();
            var inst = (T)Activator.CreateInstance (typeof (T), data);

            // Touch instance
            type.GetMethod ("Touch").Invoke (inst, new object[0]);

            // Make sure that the underlying data in the model is marked as dirty
            data = (CommonData)type.GetProperty ("Data").GetValue (inst);
            Assert.IsTrue (data.IsDirty);
            Assert.IsFalse (data.RemoteRejected);
        }

        [Test]
        public virtual async Task TestSaving ()
        {
            var type = typeof (T);
            var validData = new Dictionary<PropertyInfo, Func<object>> ();

            var properties = type.GetProperties (BindingFlags.Instance | BindingFlags.Public)
                             .Where (IsRequiredRelationProperty);
            foreach (var prop in properties) {
                validData.Add (prop, () => Activator.CreateInstance (prop.PropertyType, Guid.NewGuid ()));
            }

            await TestSaving (validData);
        }

        protected async Task TestSaving (Dictionary<PropertyInfo, Func<object>> validData)
        {
            var type = typeof (T);

            T inst;
            Task saveTask;

            // Test that we get ValidationError on save when required property not set
            var properties = validData.Keys.ToList ();

            foreach (var exemptProp in properties) {
                inst = Activator.CreateInstance<T> ();

                foreach (var prop in properties) {
                    if (prop == exemptProp) {
                        continue;
                    }

                    prop.SetValue (inst, validData [prop] ());
                }

                saveTask = (Task)type.GetMethod ("SaveAsync").Invoke (inst, new object[0]);
                Assert.That (() => saveTask.GetAwaiter ().GetResult (), Throws.Exception.TypeOf<ValidationException> ());
            }

            // Test saving new object
            inst = Activator.CreateInstance<T> ();

            foreach (var prop in properties) {
                var model = Activator.CreateInstance (prop.PropertyType, Guid.NewGuid ());
                prop.SetValue (inst, model);
            }

            saveTask = (Task)type.GetMethod ("SaveAsync").Invoke (inst, new object[0]);
            await saveTask;
            Assert.AreNotEqual (Guid.Empty, inst.Id, "Id should've been updated after save.");

            // Test saving existing model
            saveTask = (Task)type.GetMethod ("SaveAsync").Invoke (inst, new object[0]);
            await saveTask;
        }

        [Test]
        public async Task TestDeletingLocal ()
        {
            var type = typeof (T);

            // Create dummy element (with default values) to load:
            var data = await PutData (CreateDataInstance ());
            var inst = (T)Activator.CreateInstance (typeof (T), data);

            // Delete via model
            var deleteTask = (Task)type.GetMethod ("DeleteAsync").Invoke (inst, new object[0]);
            await deleteTask;

            // Check that the item has been deleted from the datastore
            Assert.IsNull (await GetDataById (data.GetType(), data.Id));

            // Make sure that the underlying data in the model has reset the IDs
            data = (CommonData)type.GetProperty ("Data").GetValue (inst);
            Assert.AreEqual (Guid.Empty, data.Id);
            Assert.IsNull (data.RemoteId);
        }

        [Test]
        public async Task TestDeletingRemote ()
        {
            var type = typeof (T);

            // Create dummy element (with default values) to load:
            var data = CreateDataInstance ();
            data.RemoteId = 1;
            data = await PutData (data);
            var inst = (T)Activator.CreateInstance (typeof (T), data);

            // Delete via model
            var deleteTask = (Task)type.GetMethod ("DeleteAsync").Invoke (inst, new object[0]);
            await deleteTask;

            // Check that the item has been marked for deletion in the database
            data = await GetDataById (data.GetType(), data.Id);
            Assert.IsNotNull (data);
            Assert.IsNotNull (data.DeletedAt);

            // Make sure that the underlying data in the model has reset the IDs
            data = (CommonData)type.GetProperty ("Data").GetValue (inst);
            Assert.AreEqual (Guid.Empty, data.Id);
            Assert.IsNull (data.RemoteId);
        }

        private async Task<Guid> CreateDummyData ()
        {
            var raw = await PutData (CreateDataInstance ());
            return raw.Id;
        }

        private CommonData CreateDataInstance ()
        {
            var type = typeof (T);
            var dataProp = type.GetProperty ("Data");
            var dataType = dataProp.PropertyType;

            return (CommonData)Activator.CreateInstance (dataType);
        }

        private async Task<CommonData> PutData (CommonData data)
        {
            var putTask = (Task)DataStore.GetType ().GetMethod ("PutAsync")
                          .MakeGenericMethod (data.GetType ())
                          .Invoke (DataStore, new[] { data });
            await putTask;
            return (CommonData)putTask.GetType ().GetProperty ("Result").GetValue (putTask);
        }

        private async Task<CommonData> GetDataById (Type dataType, Guid id)
        {
            var storeType = DataStore.GetType ();
            var tblTask = (Task<string>)storeType.GetMethod ("GetTableNameAsync")
                          .MakeGenericMethod (dataType)
                          .Invoke (DataStore, null);
            var tbl = await tblTask;
            var sql = String.Concat ("SELECT * FROM ", tbl, " WHERE Id=?");
            var rowsTask = (Task)DataStore.GetType ().GetMethod ("QueryAsync")
                           .MakeGenericMethod (dataType)
                           .Invoke (DataStore, new object[] { sql, new object[] { id } });
            await rowsTask;

            var rows = (IEnumerable)rowsTask.GetType ().GetProperty ("Result").GetValue (rowsTask);
            return rows.OfType<CommonData> ().FirstOrDefault ();
        }

        private static void SimulatePropertyChange (object obj, PropertyInfo prop)
        {
            var val = prop.GetValue (obj);

            if (prop.PropertyType == typeof (string)) {
                val = ((string)val ?? String.Empty) + "Test";
            } else if (prop.PropertyType == typeof (int)) {
                val = (int)val + 1;
            } else if (prop.PropertyType == typeof (int?)) {
                val = ((int?)val ?? 0) + 1;
            } else if (prop.PropertyType == typeof (long)) {
                val = (long)val + 1;
            } else if (prop.PropertyType == typeof (long?)) {
                val = ((long?)val ?? 0) + 1;
            } else if (prop.PropertyType == typeof (decimal)) {
                val = (decimal)val + 1;
            } else if (prop.PropertyType == typeof (decimal?)) {
                val = ((decimal?)val ?? 0) + 1;
            } else if (prop.PropertyType == typeof (bool)) {
                val = ! (bool)val;
            } else if (prop.PropertyType == typeof (DateTime)) {
                if ((DateTime)val == DateTime.MinValue) {
                    val = Time.UtcNow;
                } else {
                    val = (DateTime)val + TimeSpan.FromMinutes (1);
                }
            } else if (prop.PropertyType == typeof (DateTime?)) {
                if (val == null || (DateTime?)val == DateTime.MinValue) {
                    val = (DateTime?)Time.UtcNow;
                } else {
                    val = ((DateTime?)val).Value + TimeSpan.FromMinutes (1);
                }
            } else if (prop.PropertyType.IsEnum) {
                val = (int)val + 1;
            } else if (prop.PropertyType.GetInterfaces ().Contains (typeof (IModel))) {
                val = Activator.CreateInstance (prop.PropertyType, Guid.NewGuid ());
            } else {
                throw new InvalidOperationException (String.Format ("Don't know how to handle testing of {0} type.", prop.PropertyType));
            }

            prop.SetValue (obj, val);
        }

        private static bool IsRequiredRelationProperty (PropertyInfo prop)
        {
            var attr = prop.GetCustomAttribute<ModelRelationAttribute> ();
            return attr != null && attr.Required;
        }

        private static bool IsOptionalRelationProperty (PropertyInfo prop)
        {
            var attr = prop.GetCustomAttribute<ModelRelationAttribute> ();
            return attr != null && !attr.Required;
        }
    }
}
