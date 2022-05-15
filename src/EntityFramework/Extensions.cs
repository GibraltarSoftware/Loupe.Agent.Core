using System;
using System.Data.Entity;
using System.Data.Entity.Validation;
using System.Linq;
using System.Text;

namespace Loupe.Agent.EntityFramework
{
    /// <summary>
    /// Entity framework and entity object extensions
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Log to Loupe the changes pending in the EF Context
        /// </summary>
        /// <param name="context">An entity framework DbContext</param>
        /// <param name="category">The category to log the changes under</param>
        public static void LogChanges (this DbContext context, string category = EntityFrameworkConfiguration.LogCategory )
        {
            var description = new StringBuilder(1024);

            description.AppendFormat("Change tracking is currently {0}\r\n",
                                     context.Configuration.AutoDetectChangesEnabled ? "enabled" : "disabled");

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
        /// Create a single string message for the entity validation errors
        /// </summary>
        /// <param name="ex"></param>
        /// <param name="includeEntityValues">When true the current property values for each entity that has a validation problem will be included.</param>
        /// <returns></returns>
        public static string Format(this DbEntityValidationException ex, bool includeEntityValues = true)
        {
            var messageBuilder = new StringBuilder(1024);

			foreach (var eve in ex.EntityValidationErrors)
			{
			    string action = null;
			    switch (eve.Entry.State)
			    {
			        case EntityState.Added:
			            action = "Added";
			            break;
			        case EntityState.Deleted:
			            action = "Deleted";
			            break;
			        case EntityState.Modified:
			            action = "Modified";
			            break;

			    }

			    messageBuilder.AppendFormat("{0} being {1} has the following validation errors:\r\n", 
                    eve.Entry.Entity.GetType().Name, action);
				foreach (var ve in eve.ValidationErrors)
				{
                    messageBuilder.AppendFormat("- {0}: \"{1}\"\r\n", ve.PropertyName, ve.ErrorMessage);
				}

			    if (includeEntityValues)
			    {
			        messageBuilder.AppendLine("Current Values:");

			        var currentValues = eve.Entry.CurrentValues;

			        foreach (var propertyName in currentValues.PropertyNames)
			        {
			            messageBuilder.AppendFormat("- {0}: \"{1}\"", propertyName, currentValues[propertyName].FormatDbValue());
			        }
			    }

			    messageBuilder.AppendLine();
			}

            return messageBuilder.ToString();
        }


        /// <summary>
        /// Records the exception in Loupe
        /// </summary>
        /// <param name="ex">The entity validation exception to be recorded</param>
        /// <param name="category">The category to log the exception under</param>
        public static void Log(this DbEntityValidationException ex, string category = EntityFrameworkConfiguration.LogCategory + ".Errors")
        {
            var description = ex.Format();
            Gibraltar.Agent.Log.Error(ex, true, category, "Unable to save changes in entity framework context due to validation errors",
                description);
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
