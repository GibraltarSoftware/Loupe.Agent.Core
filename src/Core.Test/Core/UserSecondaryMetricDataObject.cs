namespace Loupe.Core.Test.Core
{
    // ToDo: Compile turned off, but left around in case we want to port these test cases to Agent.Test.
    //[EventMetric("EventMetricTests", "Gibraltar.Monitor.Test", "UserSecondaryMetricDataObject")]
    public class UserSecondaryMetricDataObject : UserNonMetricDataObject, IEventMetricTestInterface
    {
        public UserSecondaryMetricDataObject(string instanceName, int instanceNum)
            : base(instanceName, instanceNum)
        {
        }

        #region IEventMetricTestInterface implicit implementation

        public string StringProperty { get { return "implicit: " + String; } }

        public string StringMethod()
        {
            return "Implicit Method Version: " + String;
        }

        public int IntProperty { get { return Int; } }

        #endregion
    }
}
