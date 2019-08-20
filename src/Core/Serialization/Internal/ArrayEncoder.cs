
using System;
using System.Reflection;



namespace Loupe.Core.Serialization.Internal
{
    /// <summary>
    /// This helper class implements an enhanced run length encoding strategy
    /// to compress arrays.  It's enhanced in that it has an optimization for
    /// the case of a sequence of unique values.  The length of the sequence
    /// is written as a negative value.  This avoind the waste of preceding each
    /// value with a runlength of 1 as would occur in classic RLE encoding.
    /// </summary>
    /// <typeparam name="T">Type of value contained in the array</typeparam>
    internal class ArrayEncoder<T>
    {
        private readonly MethodInfo m_ReadMethod;
        private readonly MethodInfo m_WriteMethod;

        public ArrayEncoder()
        {
            string readMethodName = "Read" + typeof(T).Name;
            m_ReadMethod = typeof(IFieldReader).GetMethod(readMethodName);
            m_WriteMethod = typeof(IFieldWriter).GetMethod("Write", new Type[] {typeof(T)});
        }

        private static bool AreEqual(T left, T right)
        {
            // We have to check if left is null (right can be checked by Equals itself)
            if (ReferenceEquals(left, null))
            {
                // If right is also null, we're equal; otherwise, we're unequal!
                return ReferenceEquals(right, null);
            }
            return left.Equals(right);
        }

        /// <summary>
        /// This helper method uses reflection to invoke the proper method to read
        /// a value from the stream of type T.
        /// </summary>
        private T ReadValue(IFieldReader reader)
        {
            return (T)m_ReadMethod.Invoke(reader, new object[0]);
        }

        /// <summary>
        /// This helper method uses reflection to invoke the proper method to write
        /// a value to the stream of type T.
        /// </summary>
        private void WriteValue(IFieldWriter writer, T value)
        {
            m_WriteMethod.Invoke(writer, new object[] {value});
        }

        /// <summary>
        /// Reads an array of type T from the stream.
        /// </summary>
        /// <param name="reader">Data stream to read</param>
        /// <returns>Array of type T</returns>
        public T[] Read(IFieldReader reader)
        {
            // The array always starts with its length.  Since the length can't be
            // negative we pass it unsigned (which gives slightly better compression)
            int length = (int)reader.ReadUInt32();
            T[] array = new T[length];

            // The array values are stored as a seuqnece of runs.  Each run represents
            // either a sequence of repeating values or a sequence of unique values.
            int index = 0;
            while (index < length)
            {
                int runLength = reader.ReadInt32();
                if (runLength > 0)
                {
                    // a positive runLength indicates a run of repeating values.
                    // So, we only need to store the actual value once.
                    T value = ReadValue(reader);
                    for (int i = 0; i < runLength; i++)
                        array[index++] = value;
                }
                else // runLength < 0
                {
                    // a negative runLength indicates a run of unique values
                    for (int i = runLength; i < 0; i++)
                    {
                        // in this case, we need to store each value
                        T value = ReadValue(reader);
                        array[index++] = value;
                    }
                }
            }

            return array;
        }

        /// <summary>
        /// Writes an array of type T to the stream.
        /// </summary>
        /// <param name="array">Data to be written</param>
        /// <param name="writer">Stream to write the data into</param>
        public void Write(T[] array, IFieldWriter writer)
        {
            writer.Write((UInt32)array.Length);

            int currentIndex = 0;
            int peekIndex = currentIndex + 1;
            int runLength = 0;

            // iterate across the array writing out a series of "runs" in which each
            // run is either a repetition of the same value or a sequence of unique values.
            while (currentIndex < array.Length)
            {
                // check for the end of the array
                if (peekIndex < array.Length)
                {
                    // is this the start of a new run?
                    if (runLength == 0)
                    {
                        // is this a run or repeated values?
                        if (AreEqual(array[peekIndex], array[peekIndex - 1]))
                            // since the first two values match, we know we have a run of at least 2 repeating values
                            runLength = 2;
                        else
                            // if the first two values differ, we have a run of at least 1 unique value
                            runLength = -1;
                        peekIndex += 1;
                    }
                    else if (runLength > 0)
                    {
                        // is the run of repeating values continuing?
                        if (AreEqual(array[peekIndex], array[peekIndex - 1]))
                        {
                            runLength += 1;
                            peekIndex += 1;
                        }
                        else
                            WriteRun(array, writer, ref runLength, ref currentIndex, ref peekIndex);
                    }
                    else // runLength < 0
                    {
                        // is the run of unique values continuing?
                        if (!AreEqual(array[peekIndex], array[peekIndex - 1]))
                        {
                            runLength -= 1; // we decrement because we're accumulating a negative length
                            peekIndex += 1;
                        }
                        else
                        {
                            // don't include the last value because it is part of the next (repeating) run
                            WriteRun(array, writer, ref runLength, ref currentIndex, ref peekIndex);
                        }
                    }
                }
                else
                    WriteRun(array, writer, ref runLength, ref currentIndex, ref peekIndex);
            }
        }

        /// <summary>
        /// Helper method to write out a single run (either repating or unique values)
        /// </summary>
        private void WriteRun(T[] array, IFieldWriter writer,
                              ref int runLength, ref int currentIndex, ref int peekIndex)
        {
            // This handles the edge case of the last run containing only one value
            if (currentIndex == array.Length - 1)
                runLength = -1;

            // Write the length of the run first
            writer.Write(runLength);

            // is this a repeating run?
            if (runLength > 0)
            {
                // for a repeating run, write the value once but advance the index by runLength
                WriteValue(writer, array[currentIndex]);
                currentIndex += runLength;
            }
            else
            {
                // for a unique run, write each value
                while (runLength < 0)
                {
                    WriteValue(writer, array[currentIndex++]);
                    runLength++;
                }
            }

            // Having written this run, get ready for the next one
            peekIndex = currentIndex + 1;
            runLength = 0;
        }
    }
}
