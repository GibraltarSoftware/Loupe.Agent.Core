using System;
using System.Collections.Generic;
using System.Text;
using Agent.EntityFramework.Test.Entities;

namespace Agent.EntityFramework.Test.Entities
{
    public partial class NorthwindEntities
    {
        public NorthwindEntities(string connectionString)
        : base(connectionString)
        {

        }
    }
}
