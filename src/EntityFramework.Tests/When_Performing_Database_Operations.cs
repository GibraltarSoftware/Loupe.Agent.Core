using System;
using System.Linq;
using System.Threading.Tasks;
using Agent.EntityFramework.Test.Entities;
using Gibraltar.Agent;
using Xunit;

namespace Loupe.Agent.EntityFramework.Tests
{
    [Collection("Loupe")]
    public class When_Performing_Database_Operations
    {
        private const string LogCategory = "Entity Framework Tests";

        private readonly LoupeTestFixture _fixture;

        public When_Performing_Database_Operations(LoupeTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void Can_Log_Simple_Query()
        {
            using (var ctx = new NorthwindEntities(_fixture.DbConnectionString))
            {
                var results = from c in ctx.Customers select c;

                results.Count();
            }
        }

        [Fact]
        public void Can_Log_Simple_Update()
        {
            try
            {
                using (var ctx = new NorthwindEntities(_fixture.DbConnectionString))
                {
                    var newCustomer = ctx.Customers.Add(new Customer());
                    newCustomer.Address = "Address Line 1";
                    newCustomer.City = "Springfield";
                    newCustomer.CustomerID = "AE" + ctx.Customers.Count();
                    newCustomer.CompanyName = "Our Company" + ctx.Customers.Count();
                    newCustomer.ContactName = "John Doe";
                    newCustomer.ContactTitle = "Senior Manager";
                    newCustomer.Region = "midwest";
                    newCustomer.PostalCode = "50501";

                    ctx.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                Log.RecordException(ex, LogCategory, true);
                throw; //we want to be sure we fail the unit test.
            }
        }

        [Fact]
        public async Task Can_Log_Async_Update()
        {
            try
            {
                using (var ctx = new NorthwindEntities(_fixture.DbConnectionString))
                {
                    var newCustomer = ctx.Customers.Add(new Customer());
                    newCustomer.Address = "Address Line 1";
                    newCustomer.City = "Springfield";
                    newCustomer.CustomerID = "AE" + ctx.Customers.Count();
                    newCustomer.CompanyName = "Our Company" + ctx.Customers.Count();
                    newCustomer.ContactName = "John Doe";
                    newCustomer.ContactTitle = "Senior Manager";
                    newCustomer.Region = "midwest";
                    newCustomer.PostalCode = "50501";

                    await ctx.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Log.RecordException(ex, LogCategory, true);
                throw; //we want to be sure we fail the unit test.
            }
        }

        [Fact]
        public void Can_Log_Transactional_Update()
        {
            try
            {
                using (var ctx = new NorthwindEntities(_fixture.DbConnectionString))
                {
                    var newCustomer = ctx.Customers.Add(new Customer());
                    newCustomer.Address = "Address Line 1";
                    newCustomer.City = "Springfield";
                    newCustomer.CustomerID = "AE" + ctx.Customers.Count();
                    newCustomer.CompanyName = "Our Company" + ctx.Customers.Count();
                    newCustomer.ContactName = "John Doe";
                    newCustomer.ContactTitle = "Senior Manager";
                    newCustomer.Region = "midwest";
                    newCustomer.PostalCode = "50501";

                    ctx.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                Log.RecordException(ex, LogCategory, true);
                throw; //we want to be sure we fail the unit test.
            }
        }
    }
}
