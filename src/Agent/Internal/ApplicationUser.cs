using System.Collections.Generic;
using Gibraltar.Agent.Data;

namespace Gibraltar.Agent.Internal
{
    internal class ApplicationUser: IApplicationUser
    {
        private readonly Monitor.ApplicationUser m_WrappedObject;

        internal ApplicationUser(Monitor.ApplicationUser wrappedObject)
        {
            m_WrappedObject = wrappedObject;
        }

        public string Key { get { return m_WrappedObject.Key; } set { m_WrappedObject.Key = value; } }

        public string FullyQualifiedUserName { get { return m_WrappedObject.FullyQualifiedUserName; } }

        public string Caption { get { return m_WrappedObject.Caption; } set { m_WrappedObject.Caption = value; } }

        public string EmailAddress { get { return m_WrappedObject.EmailAddress; } set { m_WrappedObject.EmailAddress = value; } }

        public string Phone { get { return m_WrappedObject.Phone; } set { m_WrappedObject.Phone = value; } }

        public string Organization { get { return m_WrappedObject.Organization; } set { m_WrappedObject.Organization = value; } }

        public string Role { get { return m_WrappedObject.Role; } set { m_WrappedObject.Role = value; } }

        public string Tenant { get { return m_WrappedObject.Tenant; } set { m_WrappedObject.Tenant = value; } }

        public Dictionary<string, string> Properties { get { return m_WrappedObject.Properties; } }
    }
}
