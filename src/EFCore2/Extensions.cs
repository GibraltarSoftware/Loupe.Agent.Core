using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Agent.EntityFrameworkCore
{
    /// <summary>
    /// Entity Framework Core and entity object extensions
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Log to Loupe the changes pending in the EF Context
        /// </summary>
        /// <param name="context">An entity framework DbContext</param>
        /// <param name="category">The category to log the changes under</param>
        public static void LogChanges(this DbContext context, string category = EntityFrameworkConfiguration.LogCategory)
        {
            var description = new StringBuilder(1024);

            description.AppendFormat("Change tracking is currently {0}\r\n",
                                     context.ChangeTracker.AutoDetectChangesEnabled ? "enabled" : "disabled");

            int added = 0, modified = 0, deleted = 0;
            var changes = context.ChangeTracker.Entries().ToList();

            if (changes.Any() == false)
            {
                description.AppendLine("There are no changes waiting to be saved");
            }
            else
            {
                description.AppendLine("Pending Changes:");
                foreach (var dbEntityEntry in changes)
                {
                    if ((dbEntityEntry.State == EntityState.Added) ||
                        (dbEntityEntry.State == EntityState.Deleted) ||
                        (dbEntityEntry.State == EntityState.Modified))
                    {
                        switch (dbEntityEntry.State)
                        {
                            case EntityState.Added:
                                added++;
                                break;
                            case EntityState.Deleted:
                                deleted++;
                                break;
                            case EntityState.Modified:
                                modified++;
                                break;
                        }

                        description.AppendFormat("{0} {1}: {2} \r\n", dbEntityEntry.Entity.GetType().Name, dbEntityEntry.State, dbEntityEntry.Entity);
                    }
                }
            }

            //customize up our caption to reflect the number and type of changes to make the data more scanable.
            string caption;

            if (added == 0 && deleted == 0 && modified == 0)
            {
                caption = string.Format("Entity framework context currently has no pending changes and {0:N0} objects in memory", changes.Count);
            }
            else
            {
                caption = string.Format("Entity framework context currently has {1:N0} pending adds, {2:N0} pending modifications, {3:N0} pending modifications, and {0:N0} objects in memory",
                    changes.Count, added, modified, deleted);
            }

            Gibraltar.Agent.Log.Verbose(category, caption, description.ToString());
        }

        /// <summary>
        /// Create an optimal string form of the provided DB parameter value
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string FormatDbValue(this object value)
        {
            if ((ReferenceEquals(value, null)) || (value == DBNull.Value))
                return "(null)";

            if (value is string)
                return "'" + value + "'";

            if ((value is DateTime) || (value is DateTimeOffset))
                return string.Format("{0:G}", value);

            return value.ToString();
        }
    }

}
