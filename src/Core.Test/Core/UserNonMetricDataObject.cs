namespace Loupe.Core.Test.Core
{
    // ToDo: Compile turned off, but left around in case we want to port these test cases to Agent.Test.
    public class UserNonMetricDataObject : UserDataObject, IEventMetricTestInterface
    {
        private readonly int m_InstanceNum;
        public UserNonMetricDataObject(string instanceName, int instanceNum)
            : base(instanceName)
        {
            m_InstanceNum = instanceNum;
        }

        /// <summary>
        /// Our numeric instance num (so that inheritors can use it for ther IEventMetricTestInterface implementation)
        /// </summary>
        public int InstanceNum { get { return m_InstanceNum; } }

        #region IEventMetricTestInterface Implementation

        int IEventMetricTestInterface.InstanceNum { get { return m_InstanceNum; } }
        string IEventMetricTestInterface.StringProperty { get { return base.String; } }

        string IEventMetricTestInterface.StringMethod()
        {
            return "Method Version: " + base.String;
        }

        int IEventMetricTestInterface.IntProperty { get { return base.Int; } }

        #endregion
    }
}
