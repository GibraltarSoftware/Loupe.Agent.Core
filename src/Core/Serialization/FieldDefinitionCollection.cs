using System;
using System.Collections;
using System.Collections.Generic;



#pragma warning disable 1591
namespace Gibraltar.Serialization
{
    public class FieldDefinitionCollection : IList<FieldDefinition>
    {
        private Dictionary<string, FieldDefinition> m_DefinitionsByName = new Dictionary<string, FieldDefinition>();
        private Dictionary<string, int> m_IndexByName = new Dictionary<string, int>(); 
        private List<FieldDefinition> m_DefinitionsList = new List<FieldDefinition>();
        private readonly object m_Lock = new object(); //MT Safety lock
        private bool m_Locked; //true if we can no longer be modified.


        public bool Locked
        {
            get { return m_Locked; }
            set
            {
                //act as a latch - once set can't be unset
                if (value)
                {
                    m_Locked = value;
                }
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.Collections.Generic.IEnumerator`1" /> that can be used to iterate through the collection.
        /// </returns>
        /// <filterpriority>1</filterpriority>
        public IEnumerator<FieldDefinition> GetEnumerator()
        {
            return m_DefinitionsList.GetEnumerator();
        }


        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Collections.IEnumerator"/> object that can be used to iterate through the collection.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(string fieldName, Type type)
        {
            Add(fieldName, PacketDefinition.GetSerializableType(type));
        }

        public void Add(string fieldName, FieldType fieldType)
        {
            FieldDefinition field = new FieldDefinition(fieldName, fieldType);
            Add(field);
        }

        /// <summary>
        /// Adds an item to the <see cref="T:System.Collections.Generic.ICollection`1" />.
        /// </summary>
        /// <param name="item">The object to add to the <see cref="T:System.Collections.Generic.ICollection`1" />.</param>
        /// <exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only.</exception>
        public void Add(FieldDefinition item)
        {
            lock(m_Lock)
            {
                //make sure it isn't already here by name
                if (m_DefinitionsByName.ContainsKey(item.Name))
                {
                    throw new ArgumentException("There is already a field definition with the provided name.", nameof(item));
                }

                //otherwise we just add it.
                m_DefinitionsByName.Add(item.Name, item);
                m_DefinitionsList.Add(item);
                m_IndexByName.Add(item.Name, m_DefinitionsList.Count-1); // always the most recently added item
            }
        }

        /// <summary>
        /// Removes all items from the <see cref="T:System.Collections.Generic.ICollection`1" />.
        /// </summary>
        /// <exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only. </exception>
        public void Clear()
        {
            lock(m_Lock)
            {
                m_DefinitionsByName.Clear();
                m_DefinitionsList.Clear();
                m_IndexByName.Clear();
            }
        }

        /// <summary>
        /// Determines whether the <see cref="T:System.Collections.Generic.ICollection`1" /> contains a specific value.
        /// </summary>
        /// <returns>
        /// true if <paramref name="item" /> is found in the <see cref="T:System.Collections.Generic.ICollection`1" />; otherwise, false.
        /// </returns>
        /// <param name="item">The object to locate in the <see cref="T:System.Collections.Generic.ICollection`1" />.</param>
        public bool Contains(FieldDefinition item)
        {
            return m_DefinitionsByName.ContainsKey(item.Name);
        }

        /// <summary>
        /// Determines whether the FieldDefinitionCollection contains a FieldDefinition for the specified fieldName.
        /// </summary>
        /// <param name="fieldName">The name of the field of interest.</param>
        /// <returns>True if found, false if not.</returns>
        public bool ContainsKey(string fieldName)
        {
            return m_DefinitionsByName.ContainsKey(fieldName);
        }


        /// <summary>
        /// Copies the elements of the <see cref="T:System.Collections.Generic.ICollection`1"/> to an <see cref="T:System.Array"/>, starting at a particular <see cref="T:System.Array"/> index.
        /// </summary>
        /// <param name="array">The one-dimensional <see cref="T:System.Array"/> that is the destination of the elements copied from <see cref="T:System.Collections.Generic.ICollection`1"/>. The <see cref="T:System.Array"/> must have zero-based indexing.
        ///                 </param><param name="arrayIndex">The zero-based index in <paramref name="array"/> at which copying begins.
        ///                 </param><exception cref="T:System.ArgumentNullException"><paramref name="array"/> is null.
        ///                 </exception><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="arrayIndex"/> is less than 0.
        ///                 </exception><exception cref="T:System.ArgumentException"><paramref name="array"/> is multidimensional.
        ///                     -or-
        ///                 <paramref name="arrayIndex"/> is equal to or greater than the length of <paramref name="array"/>.
        ///                     -or-
        ///                     The number of elements in the source <see cref="T:System.Collections.Generic.ICollection`1"/> is greater than the available space from <paramref name="arrayIndex"/> to the end of the destination <paramref name="array"/>.
        ///                     -or-
        ///                     Type cannot be cast automatically to the type of the destination <paramref name="array"/>.
        ///                 </exception>
        public void CopyTo(FieldDefinition[] array, int arrayIndex)
        {
            m_DefinitionsList.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Removes the first occurrence of a specific object from the <see cref="T:System.Collections.Generic.ICollection`1" />.
        /// </summary>
        /// <returns>
        /// true if <paramref name="item" /> was successfully removed from the <see cref="T:System.Collections.Generic.ICollection`1" />; otherwise, false. This method also returns false if <paramref name="item" /> is not found in the original <see cref="T:System.Collections.Generic.ICollection`1" />.
        /// </returns>
        /// <param name="item">The object to remove from the <see cref="T:System.Collections.Generic.ICollection`1" />.</param>
        /// <exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only.</exception>
        public bool Remove(FieldDefinition item)
        {
            bool itemRemoved = false;
            lock(m_Lock)
            {
                //just try to remove it from the two collections, you never know.
                if (m_DefinitionsList.Remove(item))
                {
                    itemRemoved = true;
                }

                if (m_DefinitionsByName.Remove(item.Name))
                {
                    itemRemoved = true;
                }

                // We don't remove field definitions much (if ever), but if we did, it we need to make
                // sure the index lookup is updated.  And since this is a rare operation, let's just rebuild the index
                m_IndexByName.Clear();
                int index = 0;
                foreach (var fieldDefinition in m_DefinitionsList)
                {
                    m_IndexByName.Add(fieldDefinition.Name, index++);
                }
            }

            return itemRemoved;
        }

        /// <summary>
        /// Gets the number of elements contained in the <see cref="T:System.Collections.Generic.ICollection`1" />.
        /// </summary>
        /// <returns>
        /// The number of elements contained in the <see cref="T:System.Collections.Generic.ICollection`1" />.
        /// </returns>
        public int Count
        {
            get { return m_DefinitionsList.Count; } 
        }

        /// <summary>
        /// Gets a value indicating whether the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only.
        /// </summary>
        /// <returns>
        /// true if the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only; otherwise, false.
        /// </returns>
        public bool IsReadOnly
        {
            get { return m_Locked; }
        }

        /// <summary>
        /// Determines the index of a specific item in the <see cref="T:System.Collections.Generic.IList`1" />.
        /// </summary>
        /// <returns>
        /// The index of <paramref name="item" /> if found in the list; otherwise, -1.
        /// </returns>
        /// <param name="item">The object to locate in the <see cref="T:System.Collections.Generic.IList`1" />.</param>
        public int IndexOf(FieldDefinition item)
        {
            return m_DefinitionsList.IndexOf(item);
        }

        /// <summary>
        /// Determines the index of a FieldDefinition by its specified fieldName.
        /// </summary>
        /// <param name="fieldName">The name of the field of interest.</param>
        /// <returns>The index of the FieldDefinition with the specified fieldName, or -1 if not found.</returns>
        public int IndexOf(string fieldName)
        {
            try
            {
                return m_IndexByName[fieldName];
            }
            catch (Exception)
            {
                return -1;
            }
        }

        /// <summary>
        /// Inserts an item to the <see cref="T:System.Collections.Generic.IList`1" /> at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which <paramref name="item" /> should be inserted.</param>
        /// <param name="item">The object to insert into the <see cref="T:System.Collections.Generic.IList`1" />.</param>
        /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index" /> is not a valid index in the <see cref="T:System.Collections.Generic.IList`1" />.</exception>
        /// <exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.IList`1" /> is read-only.</exception>
        public void Insert(int index, FieldDefinition item)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Removes the <see cref="T:System.Collections.Generic.IList`1" /> item at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the item to remove.</param>
        /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index" /> is not a valid index in the <see cref="T:System.Collections.Generic.IList`1" />.</exception>
        /// <exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.IList`1" /> is read-only.</exception>
        public void RemoveAt(int index)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Gets the element at the specified index.
        /// </summary>
        /// <returns>
        /// The element at the specified index.
        /// </returns>
        /// <param name="index">The zero-based index of the element to get or set.</param>
        /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index" /> is not a valid index in the <see cref="T:System.Collections.Generic.IList`1" />.</exception>
        /// <exception cref="T:System.NotSupportedException">The property is set and the <see cref="T:System.Collections.Generic.IList`1" /> is read-only.</exception>
        public FieldDefinition this[int index]
        {
            get { return m_DefinitionsList[index]; }
            set { throw new NotSupportedException(); }
        }

        /// <summary>
        /// Gets the element at the specified index.
        /// </summary>
        /// <returns>
        /// The element at the specified index.
        /// </returns>
        /// <param name="fieldName">The name of the field to get.</param>
        /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="fieldName" /> is not a valid name in the <see cref="T:System.Collections.Generic.IList`1" />.</exception>
        /// <exception cref="T:System.NotSupportedException">The property is set and the <see cref="T:System.Collections.Generic.IList`1" /> is read-only.</exception>
        public FieldDefinition this[string fieldName]
        {
            get { return m_DefinitionsByName[fieldName]; }
            set { throw new NotSupportedException(); }
        }
    }
}
