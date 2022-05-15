using System;
using Gibraltar.Agent;
using Loupe.Configuration;
using Xunit;

namespace Loupe.Agent.EntityFramework.Tests
{
    public class LoupeTestFixture : IDisposable
    {
        public LoupeTestFixture()
        {
            Log.StartSession(new AgentConfiguration()
            {
                Publisher = new PublisherConfiguration
                {
                    ProductName = "Loupe",
                    ApplicationName = "Entity Framework Tests"
                }
            });

            LoupeCommandInterceptor.Register();

            var patch_only = System.Data.Entity.SqlServer.SqlProviderServices.Instance;  //this is just here to force this particular provider into RAM since it's new to EF6
        }

        public string DbConnectionString =
                    "metadata=res://*/Entities.Northwind.csdl|res://*/Entities.Northwind.ssdl|res://*/Entities.Northwind.msl;provider=System.Data.SqlClient;provider connection string='data source=192.168.1.73;initial catalog=Northwind;integrated security=false;user=test;password=test;MultipleActiveResultSets=True;App=EntityFramework'";

        /// <inheritdoc />
        public void Dispose()
        {
            Log.EndSession("Unit test completion");
        }
    }

    [CollectionDefinition("Loupe")]
    public class LoupeTestCollection : ICollectionFixture<LoupeTestFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }
}
