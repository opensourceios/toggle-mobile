using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Data.DataObjects;

namespace Toggl.Phoebe.Tests.Data
{
    public class ForeignRelationManagerTest : Test
    {
        [Test, TestCaseSource ("DataObjectTypes")]
        public void VerifyRelations (Type dataType)
        {
            var relationProps = dataType.GetProperties (BindingFlags.Instance | BindingFlags.Public)
                                .Where (p => p.GetCustomAttribute<ForeignRelationAttribute> () != null)
                                .ToList ();

            // Create test data object:
            var inst = Activator.CreateInstance (dataType);
            foreach (var prop in relationProps) {
                Assert.IsTrue (prop.PropertyType == typeof (Guid) || prop.PropertyType == typeof (Guid?), "Relations should be defined with Guid data type only.");
                prop.SetValue (inst, Guid.NewGuid ());
            }

            var mgr = new ForeignRelationManager ();
            var relations = mgr.GetRelations ((CommonData)inst).ToList ();

            foreach (var relation in relations) {
                var prop = relationProps.FirstOrDefault (p => p.Name == relation.Name);
                Assert.IsNotNull (prop, String.Format ("Couldn't find property for {0}", relation.Name));

                var attr = prop.GetCustomAttribute<ForeignRelationAttribute> ();
                Assert.AreEqual (attr.DataType, relation.Type, "Mismatched relation type");

                Guid? relationId;
                if (relation.Required) {
                    Assert.AreEqual (typeof (Guid), prop.PropertyType, "Required property type should be of type Guid");
                    relationId = (Guid)prop.GetValue (inst);
                } else {
                    Assert.AreEqual (typeof (Guid?), prop.PropertyType, "Optional property type should be of type Guid?");
                    relationId = (Guid?)prop.GetValue (inst);
                }

                Assert.AreEqual (relationId, relation.Id, "Reported relation Id is invalid");
            }

            var missingProps = relationProps.Where (p => relations.All (r => r.Name != p.Name)).ToList ();
            Assert.That (missingProps, Has.Count.EqualTo (0), "Not all properties were returned");
        }

        [Test, TestCaseSource ("DataObjectTypes")]
        public async Task TestQuerying (Type dataType)
        {
            var mgr = new ForeignRelationManager ();

            // Test null relation
            var relation = new ForeignRelation () {
                Type = dataType,
                Id = null,
            };
            Assert.IsNull (await mgr.ToListAsync (relation));

            // Test missing relation
            relation = new ForeignRelation () {
                Type = dataType,
                Id = Guid.NewGuid (),
            };
            Assert.IsNull (await mgr.ToListAsync (relation));

            // Create dummy data:
            var inst = (CommonData)Activator.CreateInstance (dataType);
            inst.Id = relation.Id.Value;

            var putAsyncMethod = DataStore.GetType ().GetMethod ("PutAsync").MakeGenericMethod (dataType);
            await (Task)putAsyncMethod.Invoke (DataStore, new object[] { inst });
            Assert.IsNotNull (await mgr.ToListAsync (relation));
        }

        public static IEnumerable<Type> DataObjectTypes
        {
            get {
                var dataType = typeof (WorkspaceData);
                return from t in dataType.Assembly.GetTypes ()
                       where t.Namespace == dataType.Namespace && !t.IsAbstract && typeof (CommonData).IsAssignableFrom (t)
                       select t;
            }
        }
    }
}
