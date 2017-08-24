using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace Loupe.Core.Test
{
    /// <summary>
    /// Test the search query infrastructure.
    /// </summary>
    [TestFixture]
    public class SearchQueryTests
    {
        private class TestDataSet
        {
            private readonly int m_Sequence;
            private readonly DateTime m_StartTime;
            private DateTime m_EndTime;
            private readonly int m_Number;
            private readonly string m_Name;

            private static int m_SequenceCount; // LOCKED BY m_Lock
            private static readonly object m_Lock = new object();

            public int Sequence { get { return m_Sequence; } }
            public DateTime StartTime { get { return m_StartTime; } }
            public DateTime EndTime { get { return m_EndTime; } }
            public int Number { get { return m_Number; } }
            public string Name { get { return m_Name; } }

            /// <summary>
            /// Convert this dataset instance to a string representation.
            /// </summary>
            /// <returns>A string describing this dataset instance.</returns>
            public override string ToString()
            {
                StringBuilder builder = new StringBuilder();

                builder.AppendFormat("[{0}] #{1} ({2}) : {3:T} - ", m_Sequence, m_Number, m_Name, m_StartTime);
                if (m_EndTime < DateTime.MaxValue)
                    builder.AppendFormat("{0:T}", m_EndTime);
                else
                    builder.Append("(Running)");

                return builder.ToString();
            }

            /// <summary>
            /// Constructor for an instance dataset.
            /// </summary>
            /// <param name="number">An integer identifier.</param>
            /// <param name="name">A string identifier.</param>
            /// <param name="startTime">The starting timestamp of this dataset instance.</param>
            /// <param name="runLength">The number of DateTime Ticks this dataset instance ran.</param>
            public TestDataSet(int number, string name, DateTime startTime, long runLength)
            {
                lock (m_Lock)
                {
                    m_Sequence = ++m_SequenceCount; // Set our unique sequence number.
                }

                m_Number = number;

                if (string.IsNullOrEmpty(name))
                    m_Name = string.Empty;
                else
                    m_Name = name;

                if (startTime == DateTime.MinValue)
                    m_StartTime = DateTime.Now;
                else
                    m_StartTime = startTime;

                if (runLength >= 0)
                {
                    m_EndTime = m_StartTime.AddTicks(runLength);
                }
                else
                {
                    m_EndTime = DateTime.MaxValue; // Hasn't finished yet...
                }
            }
        }

        private int GetDataSequence(TestDataSet dataSet)
        {
            return dataSet.Sequence;
        }

        private int GetDataNumber(TestDataSet dataSet)
        {
            return dataSet.Number;
        }

        private string GetDataName(TestDataSet dataSet)
        {
            return dataSet.Name;
        }

        private DateTime GetDataStartTime(TestDataSet dataSet)
        {
            return dataSet.StartTime;
        }

        private DateTime GetDataEndTime(TestDataSet dataSet)
        {
            return dataSet.EndTime;
        }

        private long GetDataRunLength(TestDataSet dataSet)
        {
            if (dataSet.EndTime == DateTime.MaxValue)
                return -1;

            long startTicks = dataSet.StartTime.Ticks;
            long endTicks = dataSet.EndTime.Ticks;

            return endTicks - startTicks;
        }

        private readonly string[] NumberNames = {
                                    "zero", "one", "two", "three", "four",
                                    "five", "six", "seven", "eight", "nine",
                                    "ten", "eleven", "twelve", "thirteen", "fourteen",
                                    "fifteen", "sixteen", "seventeen", "eighteen", "nineteen",
                                    "twenty", "twenty-one", "twenty-two", "twenty-three", "twenty-four",
                                    "twenty-five", "twenty-six", "twenty-seven", "twenty-eight", "twenty-nine",
                                    "thirty", "thirty-one", "thirty-two", "thirty-three", "thirty-four",
                                    "thirty-five", "thirty-six", "thirty-seven", "thirty-eight", "thirty-nine"
                                                };

        private List<TestDataSet> DataSetList;

        [SetUp]
        public void SetUpDataSet()
        {
            DataSetList = new List<TestDataSet>();
            DateTime startTime = DateTime.Now;

            for (int i=0; i<35; i++)
            {
                int modFive = i%5;
                int byFive = i/5;
                DateTime thisStartTime = startTime.AddSeconds(byFive);
                int runLength = (modFive == 0) ? -1 : modFive*10000000;
                DataSetList.Add(new TestDataSet(6 + modFive - byFive, NumberNames[i], thisStartTime, runLength));
            }
        }

        /// <summary>
        /// Test basic LogicNode functionality.
        /// </summary>
        [Test]
        [Explicit("These tests are not written yet")]
        public void TestLogicNode()
        {
            
        }
    }
}
