using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using NUnit.Framework;

namespace Loupe.Agent.Test.LogMessages
{
    [TestFixture]
    public class StringTests
    {
        [Test]
        [Ignore("local debugging test")]
        public void TestStringHashCodeCollision()
        {
            const int maxCount = 262000;
            int tableCount = maxCount;
            int tableSize = 0;
            while (tableCount > 0)
            {
                tableCount >>= 1;
                tableSize++;
            }
            tableSize = 1 << tableSize;
            tableCount = tableSize * 4;

            Dictionary<int, string> stringTable = new Dictionary<int, string>(tableSize);
            Dictionary<string, List<string>> collisionTable = new Dictionary<string, List<string>>();
            Guid guid = Guid.NewGuid();
            string guidString = guid.ToString();
            int totalCount = 0;
            while (stringTable.Count < maxCount && totalCount < tableCount)
            {
                totalCount++; // Make sure we don't run forever if guids don't fill up our table.
                string tableString;
                if (stringTable.TryGetValue(guidString.GetHashCode(), out tableString))
                {
                    if (tableString != guidString)
                    {
                        Assert.AreEqual(tableString.GetHashCode(), guidString.GetHashCode());
                        //Trace.TraceInformation("\"{0}\" and \"{1}\" both have hash code: {2}",
                        //                       tableString, guidString, guidString.GetHashCode());

                        List<string> collisionList;
                        if (collisionTable.TryGetValue(tableString, out collisionList) == false)
                        {
                            collisionList = new List<string>();
                            collisionTable.Add(tableString, collisionList);
                        }
                        if (collisionList.Contains(guidString) == false)
                            collisionList.Add(guidString);
                    }
                    // Otherwise we just found the exact same Guid string again!
                }
                else
                {
                    stringTable.Add(guidString.GetHashCode(), guidString);
                }

                guid = Guid.NewGuid();
                guidString = guid.ToString();
            }

            int hashCollisionCount = collisionTable.Count;
            Trace.TraceInformation("Collision table found {0} of {1} hash codes with collisions ({2:F3} %)",
                                   hashCollisionCount, stringTable.Count, (hashCollisionCount * 100) / stringTable.Count);

            if (hashCollisionCount > 0)
            {
                foreach (KeyValuePair<string, List<string>> entry in collisionTable)
                {
                    string key = entry.Key;
                    List<string> valueList = entry.Value;
                    StringBuilder output = new StringBuilder();
                    output.AppendFormat("{0:x8} : {1}", key.GetHashCode(), key);
                    foreach (string value in valueList)
                    {
                        output.AppendFormat(", {0}", value);
                    }

                    Trace.WriteLine(output.ToString());
                }
            }
        }

        [Test]
        public void ReadOnlyValues()
        {
            StringContainer stringContainer = new StringContainer();
            Assert.IsTrue(ReferenceEquals(stringContainer.ReadOnlyExplicitProperty, stringContainer.ExplicitProperty));
            Assert.IsTrue(ReferenceEquals(stringContainer.ReadOnlyExplicitProperty, stringContainer.ImplicitProperty));
        }

        [Test]
        public void DictionaryStrings()
        {
            Dictionary<string, string> dictionary = new Dictionary<string, string>();

            for(int curTestLoop = 0;curTestLoop < 2000; curTestLoop++)
            {
                AddDictionaryString(dictionary);
            }
        }

        [Test]
        public void ListStrings()
        {
            List<string> list = new List<string>();

            for (int curTestLoop = 0; curTestLoop < 2000; curTestLoop++)
            {
                AddListString(list);
            }
        }

        private static void AddDictionaryString(Dictionary<string, string> dictionary)
        {
            StringContainer stringContainer = new StringContainer(dictionary.Count);

            dictionary.Add(stringContainer.ReadOnlyExplicitProperty, stringContainer.ReadOnlyExplicitProperty);

            string testArticle;
            testArticle = dictionary[stringContainer.ReadOnlyExplicitProperty]; //this is getting the value
            Assert.IsTrue(ReferenceEquals(testArticle, stringContainer.ReadOnlyExplicitProperty));

            dictionary.TryGetValue(stringContainer.ReadOnlyExplicitProperty, out testArticle);
            Assert.IsTrue(ReferenceEquals(testArticle, stringContainer.ReadOnlyExplicitProperty));

            foreach (KeyValuePair<string, string> keyValuePair in dictionary)
            {
                if (keyValuePair.Key.Equals(stringContainer.ReadOnlyExplicitProperty))
                {
                    Assert.IsTrue(ReferenceEquals(keyValuePair.Key, stringContainer.ReadOnlyExplicitProperty));
                    Assert.IsTrue(ReferenceEquals(keyValuePair.Value, stringContainer.ReadOnlyExplicitProperty));
                }
            }            
        }

        private static void AddListString(List<string> list)
        {
            StringContainer stringContainer = new StringContainer(list.Count);

            list.Add(stringContainer.ReadOnlyExplicitProperty);

            string testArticle;
            testArticle = list[list.Count - 1]; //this is getting the value
            Assert.IsTrue(ReferenceEquals(testArticle, stringContainer.ReadOnlyExplicitProperty));

            string[] array = list.ToArray();
            testArticle = array[list.Count - 1];
            Assert.IsTrue(ReferenceEquals(testArticle, stringContainer.ReadOnlyExplicitProperty));
        }
    }

    internal class StringContainer
    {
        private readonly string m_ReadOnlyString;
        private string m_UpdatableString;

        public StringContainer()
            : this(0)
        {            
        }

        public StringContainer(int stringIndex)
        {
            //set all of our values to their defaults.
            string baseline = string.Format("{0} {1} - {2}", "String Container", stringIndex, DateTimeOffset.Now);
            m_ReadOnlyString = baseline;
            m_UpdatableString = baseline;
            ImplicitProperty = baseline;
        }

        private string m_ImplicitProperty;
        public string ImplicitProperty { get { return m_ImplicitProperty; } set { m_ImplicitProperty = value; } }

        public string ExplicitProperty
        {
            get { return m_UpdatableString; }
            set { m_UpdatableString = value; }
        }

        public string ReadOnlyExplicitProperty
        {
            get
            {
                return m_ReadOnlyString;
            }
        }
    }
}
