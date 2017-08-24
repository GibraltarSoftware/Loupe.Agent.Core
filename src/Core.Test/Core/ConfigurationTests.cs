using Loupe.Configuration;
using NUnit.Framework;

namespace Loupe.Core.Test.Core
{
    [TestFixture]
    public class ConfigurationTests
    {
        [Test]
        public void Listener()
        {
            //get the configuration class
            var curConfiguration = new AgentConfiguration().Listener;

            //store off the current configuration
            bool initialEnableConsole = curConfiguration.EnableConsole;

            //now change each value and see that the change takes, but only changes the property it should.
            curConfiguration.EnableConsole = !initialEnableConsole;
            Assert.AreEqual(curConfiguration.EnableConsole, !initialEnableConsole);
            //Assert.AreEqual(MonitorConfiguration.Listener.EnableConsoleRedirector, curConfiguration.EnableConsoleRedirector);
            
            //now set it back.
            curConfiguration.EnableConsole = initialEnableConsole;
            Assert.AreEqual(curConfiguration.EnableConsole, initialEnableConsole);
            //Assert.AreEqual(MonitorConfiguration.Listener.EnableConsoleRedirector, curConfiguration.EnableConsoleRedirector);

            //now change each value and see that the change takes, but only changes the property it should.
            Assert.AreEqual(curConfiguration.EnableConsole, initialEnableConsole);
            //Assert.AreEqual(MonitorConfiguration.Listener.CatchUnhandledExceptions, curConfiguration.CatchUnhandledExceptions);

            //now set it back.
            Assert.AreEqual(curConfiguration.EnableConsole, initialEnableConsole);
            //Assert.AreEqual(MonitorConfiguration.Listener.CatchUnhandledExceptions, curConfiguration.CatchUnhandledExceptions);
        }
    }
}
