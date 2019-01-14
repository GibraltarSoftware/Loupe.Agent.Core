using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Gibraltar
{
    /// <summary>
    /// Provides a way to ensure a string is a reference to a single copy instead of creating multiple copies.
    /// </summary>
    public static class StringReference
    {
        private const int DefaultCollectionSize = 1024;
        private const int MinimumRebuildSize = 10240;
        private const double FreeSpaceRebuildRatio = 0.66;
        private static readonly object s_Lock = new object(); //Multithread Protection lock
        private static Dictionary<int, WeakStringCollection> s_StringReferences = new Dictionary<int, WeakStringCollection>(DefaultCollectionSize); //PROTECTED BY LOCK

        private volatile static bool s_DisableCache;
        private static long s_PeakReferenceSize; //PROTECTED BY LOCK

        /// <summary>
        /// Indicates if the reference cache is disabled.
        /// </summary>
        /// <remarks>When disabled each method returns immediately and the input string is returned.  This allows comparision of 
        /// behavior with and without the cache without changing code.</remarks>
        public static bool Disabled
        {
            get
            {
                return s_DisableCache;
            }
            set
            {
                s_DisableCache = value;
            }
        }

        /// <summary>
        /// Swap the provided string for its common reference
        /// </summary>
        /// <param name="baseline">The string to be looked up and exchanged for its reference.</param>
        /// <remarks>If the baseline isn't already in the reference cache it will be added.  The cache is automatically pruned to
        /// prevent it from consuming excess memory.</remarks>
        public static void SwapReference(ref string baseline)
        {
            baseline = GetReference(baseline);
        }

        /// <summary>
        /// Get the reference value for the provided baseline value.
        /// </summary>
        /// <param name="baseline"></param>
        /// <returns></returns>
        public static string GetReference(string baseline)
        {
            if (s_DisableCache)
                return baseline;

            if (baseline == null)
                return null;  //can't do a reference to null anyway

            if (baseline.Length == 0)
            {
                return string.Empty;//this is a stock intered string.
            }

            string officialString = baseline; // We'll replace this with the official copy if there is one.
            try
            {
                object outerLock = s_Lock; // We need a copy we can null out when released.
                WeakStringCollection hashCodeCollisionNode = null; // Collects strings with the same hash code.
                
                try
                {
                    System.Threading.Monitor.Enter(outerLock); // Usually gets released in our finally block.

                    int baselineHashCode = baseline.GetHashCode();
                    if (s_StringReferences.TryGetValue(baselineHashCode, out hashCodeCollisionNode))
                    {
                        // The lookup by hash code gets us close, now have the little collection check for it.
                        if (hashCodeCollisionNode != null)
                        {
                            System.Threading.Monitor.Enter(hashCodeCollisionNode); // Lock the row so we can release the outer.
                            System.Threading.Monitor.Exit(outerLock); // Release outer lock early to reduce contention.
                            outerLock = null; // Mark the outer lock released so we don't release it again!

                            // This call will actually replace our tempString with any official copy if one is found.
                            hashCodeCollisionNode.PackAndOrAdd(ref officialString);
                            // Row lock will be released in the finally block.
                        }
                        else
                        {
                            // This case shouldn't happen, but we can replace the null entry with a new little collection.
                            s_StringReferences[baselineHashCode] = new WeakStringCollection(baseline);
                        }
                    }
                    else
                    {
                        // Didn't find anything for that hash code, so make a new little collection to hold the given string.
                        s_StringReferences.Add(baselineHashCode, new WeakStringCollection(baseline));
                        s_PeakReferenceSize = Math.Max(s_StringReferences.Count, s_PeakReferenceSize);
                    }
                }
                finally
                {
                    if (outerLock != null)
                        System.Threading.Monitor.Exit(outerLock);

                    if (hashCodeCollisionNode != null)
                        System.Threading.Monitor.Exit(hashCodeCollisionNode);
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                Debug.WriteLine(string.Format("While trying to get the string reference for \"{0}\" an exception was thrown: {1}", baseline, ex.Message));
#endif
                GC.KeepAlive(ex); //just here to avoid a compiler warn in release mode
            }

            return officialString;
        }

        /// <summary>
        /// Check the cache for garbage collected values.
        /// </summary>
        public static void Pack()
        {
            if (s_DisableCache)
                return;

            //Our caller has a right to expect to never get an exception from this.
            try
            {
#if DEBUG
                Stopwatch packTimer = Stopwatch.StartNew();
                long elapsedMilliseconds;
                int deadNodesCount;
                int singletonNodesCount = 0;
                int multipleNodesCount = 0;
                int collapsedNodesCount = 0; // Nodes that had multiple but reduced to singleton.
#endif
                int startNodesCount; // Declared here for use in DEBUG after the lock exits.
                KeyValuePair<int, WeakStringCollection>[] allNodes;
                List<int> deadNodes;
                lock (s_Lock)
                {
                    startNodesCount = s_StringReferences.Count;
                    if (startNodesCount == 0)
                        return; //nothing here, nothing to collect.

                    deadNodes = new List<int>((startNodesCount / 4) + 1); //assume we could wipe out 25% every time.

                    // Make a snapshot of the little collection nodes in our table, so we can reduce contention while we pack.
                    allNodes = new KeyValuePair<int, WeakStringCollection>[s_StringReferences.Count];
                    int currentIndex = 0;
                    foreach (KeyValuePair<int, WeakStringCollection> node in s_StringReferences)
                        allNodes[currentIndex++] = node;
                }

                foreach (KeyValuePair<int, WeakStringCollection> keyValuePair in allNodes)
                {
                    WeakStringCollection currentReferencesList = keyValuePair.Value;
                    if (currentReferencesList == null)
                    {
                        deadNodes.Add(keyValuePair.Key); // Hmmm, shouldn't be here.  Let's remove the null node.
                        continue; // Try the next node.
                    }

                    lock (currentReferencesList)
                    {
#if DEBUG
                        bool singletonNode = (currentReferencesList.Count == 1);
                        if (singletonNode)
                            singletonNodesCount++;
                        else
                            multipleNodesCount++;
#endif

                        if (currentReferencesList.Pack() <= 0)
                        {
                            deadNodes.Add(keyValuePair.Key);
                        }
#if DEBUG
                        else
                        {
                            if (singletonNode == false && currentReferencesList.Count == 1)
                                collapsedNodesCount++;
                        }
#endif
                    }
                }

                lock (s_Lock) // Get the outer lock again so we can remove the dead nodes.
                {
                    //and now kill off our dead nodes.
                    foreach (int deadNodeKey in deadNodes)
                    {
                        WeakStringCollection currentNode;
                        if (s_StringReferences.TryGetValue(deadNodeKey, out currentNode))
                        {
                            if (currentNode != null)
                            {
                                lock(currentNode)
                                {
                                    if (currentNode.Count <= 0) // Check that it's still dead!
                                        s_StringReferences.Remove(deadNodeKey);
                                }
                            }
                            else
                            {
                                // This case shouldn't happen, but if it does there's nothing to lock or check.  Just remove it.
                                s_StringReferences.Remove(deadNodeKey);
                            }
                        }
                    }

                    //Finally, if we have killed a good percentage off we really need to shrink the dictionary itself.
                    if (s_PeakReferenceSize > MinimumRebuildSize)
                    {
                        double fillRatio = (s_StringReferences.Count / (double)((s_PeakReferenceSize == 0) ? 1 : s_PeakReferenceSize));
                        if (fillRatio < FreeSpaceRebuildRatio)
                        {
                            //it's bad enough we want to free the dictionary & rebuild it to make it small again.
#if DEBUG
                            Debug.WriteLine(string.Format("StringReference:  Rebuilding collection because fill ratio is {0}", fillRatio));
#endif
                            Dictionary<int, WeakStringCollection> newCollection = new Dictionary<int, WeakStringCollection>(s_StringReferences);
                            s_StringReferences = newCollection;
                            s_PeakReferenceSize = newCollection.Count;
                        }
                        
                    }
#if DEBUG
                    packTimer.Stop();
                    elapsedMilliseconds = packTimer.ElapsedMilliseconds;
                    deadNodesCount = deadNodes.Count;
#endif
                    System.Threading.Monitor.PulseAll(s_Lock);
                }
#if DEBUG
                Debug.WriteLine(string.Format("StringReference:  Pack took {0} ms.", elapsedMilliseconds));
                Debug.WriteLine(string.Format("StringReference:  Removed {0} of {1} nodes from the cache ({2:F2} %).",
                                deadNodesCount, startNodesCount, (deadNodesCount * 100.0) / startNodesCount));
                Debug.WriteLine(string.Format("StringReference:  {0} singleton nodes vs {1} multiple nodes ({2} collapsed to singleton)",
                                              singletonNodesCount, multipleNodesCount, collapsedNodesCount));
#endif
            }
            catch (Exception ex)
            {
                GC.KeepAlive(ex); //just here to avoid a compiler warn in release mode
            }
        }

        #region Private Properties and Methods

        #endregion

        #region Private Helper Class WeakStringCollection

        /// <summary>
        /// Keeps one or more weak references to strings which all have the same hash code.
        /// </summary>
        /// <remarks>A single string (the vast majority of cases) will be kept as a single reference.  When collisions
        /// put more than one string in the same hash code the extras will be stored in an array.  The array will allocate
        /// in increments of 4 (allowing 3 more before the next expand).  Packing thus requires a linear walk of the array
        /// to move remaining references up, and this can also be done when simply looking up a reference to consider
        /// adding it to the collection (which requires a linear walk anyway).</remarks>
        public class WeakStringCollection // Note: Public for internal testing.  Can be made private for posting.
        {
            private WeakReference m_FirstString;
            private WeakReference[] m_ExtraStrings;
            private int m_ExtraCount;

            /// <summary>
            /// Construct a new collection with an initial string reference (required).
            /// </summary>
            /// <param name="firstString"></param>
            public WeakStringCollection(string firstString)
            {
                m_FirstString = new WeakReference(firstString);
                m_ExtraStrings = null; // Default to something small until we actually need more.
                m_ExtraCount = 0;
            }

            /// <summary>
            /// Get the total number of string references in this collection.  (Some references may no longer be valid.)
            /// </summary>
            public int Count
            {
                get
                {
                    lock(this)
                    {
                        return m_ExtraCount + 1;
                    }
                }
            }

            /// <summary>
            /// Perform a pack on this collection.
            /// </summary>
            /// <returns>The total number of string references remaining after the pack.</returns>
            public int Pack()
            {
                string refNullString = null;
                return PackAndOrAdd(ref refNullString);
            }

            /// <summary>
            /// Add an optional new string to this collection and perform a pack on this collection while doing so.
            /// </summary>
            /// <param name="newString">A new string to add, or null if only a pack is desired.  If the string already
            /// exists in this collection, the one from the collection will be stored in the reference.</param>
            /// <returns>The total number of string references remaining after the pack.</returns>
            public int PackAndOrAdd(ref string newString)
            {
                lock (this) // We are probably already in a lock, but we need to make sure.
                {
                    bool adding = (newString != null);

                    // These are indexes in the array, with -1 meaning the m_FirstString instead of the array.
                    int nextPackIndex = -1; // The index of the next slot to copy into when packing.

                    // The vast majority of the time we only have a single string in this collection (for a given hash code),
                    // and we're either just doing a pack or we're looking up the same string.  So make those scenarios fast.

                    if (CheckWeakReference(ref m_FirstString, ref newString))
                    {
                        // We found it!  We can skip the pack in this case for speed.  It can't match if we're only doing a pack.
                        return m_ExtraCount + 1;
                    }

                    if (m_FirstString != null) // It has been set to null by the check above if it isn't still valid.
                    {
                        nextPackIndex++; // He's valid, so advance to the next slot.
                    }

                    if (m_ExtraCount <= 0 && (adding == false || nextPackIndex < 0))
                    {
                        // There's no array and we don't need to add one to the extras, so let's do this quick and exit.
#if DEBUG
                        Debug.Assert(m_ExtraStrings == null); // Should always be the case if we clear it correctly down below.
#endif
                        if (adding) // Are we adding or only packing?
                        {
                            m_FirstString = new WeakReference(newString);
                            nextPackIndex++; // Now m_FirstString is valid.
                        }

                        m_ExtraCount = nextPackIndex; // Update the count for the loss and/or add.
                        return m_ExtraCount + 1; // No further work needed, as there's nothing to walk.
                    }

                    // Now we have to search the array, and we can pack it as we go.

                    string refNullString = null;
                    for (int currentIndex = 0; currentIndex < m_ExtraCount; currentIndex++)
                    {
                        if (adding)
                        {
                            if (CheckWeakReference(ref m_ExtraStrings[currentIndex], ref newString)) // We're searching...
                                adding = false; // We found it!  So we no longer need to search, just pack the rest.                            
                        }
                        else
                        {
                            CheckWeakReference(ref m_ExtraStrings[currentIndex], ref refNullString); // Faster check.
                        }

                        if (m_ExtraStrings[currentIndex] != null) // It would be set to null by the check above if not valid.
                        {
                            if (nextPackIndex < currentIndex) // Do we need to pack this reference earlier?
                            {
                                if (nextPackIndex < 0)
                                    m_FirstString = m_ExtraStrings[currentIndex];
                                else
                                    m_ExtraStrings[nextPackIndex] = m_ExtraStrings[currentIndex];
                            }
                            // Otherwise, it's already packed as-is up to this point, so we don't need to move it.

                            nextPackIndex++; // Either way, advance the nextPackIndex (and the loop will advance currentIndex).
                        }
                    }

                    // Okay, we've scanned the entire array (if there was one), removed invalid entries and packed the rest.
                    // If adding is still true at this point, then we didn't find a match and we need to add it.
                    // nextPackIndex tells us the count valid in the array (not counting m_FirstString).

                    int arraySizeNeeded = nextPackIndex + (adding ? 4 : 3); // Plus 3 to round up, and plus 1 more if adding.
                    arraySizeNeeded -= arraySizeNeeded % 4; // Chop off the round-up at a multiple of 4.

                    if (arraySizeNeeded <= 0)
                    {
                        m_ExtraStrings = null; // We don't need the array, so make sure it's released.
                    }
                    else if (m_ExtraStrings == null || arraySizeNeeded > m_ExtraStrings.Length ||    // Not big enough or...
                             (newString == null && m_ExtraStrings.Length - arraySizeNeeded > 8))     // ...way too big (and just packing)
                    {
                        WeakReference[] newArray = new WeakReference[arraySizeNeeded]; // Allocate the new size needed.
                        if (m_ExtraStrings != null) // To keep the compiler happy, even though the loop check would catch it.
                        {
                            for (int copyIndex = 0; copyIndex < nextPackIndex; copyIndex++)
                                newArray[copyIndex] = m_ExtraStrings[copyIndex]; // Copy the (packed) references over.
                        }
                        m_ExtraStrings = newArray;
                    }
                    else
                    {
                        // We kept the same m_ExtraStrings array, so we need to make sure any moved references are cleared
                        // from their old positions.  Otherwise these could keep WeakReference objects around wasting space.
                        for (int currentIndex = (nextPackIndex < 0) ? 0 : nextPackIndex; currentIndex < m_ExtraCount; currentIndex++)
                            m_ExtraStrings[currentIndex] = null;
                    }

                    if (adding)
                    {
                        // We never found a match, so we need to add it to the end.  We already made sure there's room.
                        if (nextPackIndex < 0)
                            m_FirstString = new WeakReference(newString);
                        else if (m_ExtraStrings != null) // Always true, but compiler doesn't follow the math above to know.
                            m_ExtraStrings[nextPackIndex] = new WeakReference(newString); // Add the new string.

                        nextPackIndex++; // Increment to the next spot, because we just used that one.
                    }
                    else if (nextPackIndex < 0)
                    {
                        m_FirstString = null; // Everything got invalidated, so clear this reference as well (we hadn't above).
                    }

                    m_ExtraCount = nextPackIndex; // That gives us our final new m_ExtraCount.
                    return m_ExtraCount + 1; // And return the total Count so our caller knows if we can be removed entirely.
                }
            }

            /// <summary>
            /// Check a reference for validity and optional match against a given string.
            /// </summary>
            /// <param name="reference">The WeakReference to check.  Will be set to null if invalid.</param>
            /// <param name="newString">The string to match against.  Will be replaced with stored copy if an equal match.</param>
            /// <returns>True if it was an equal match, false if not a match.</returns>
            private static bool CheckWeakReference(ref WeakReference reference, ref string newString)
            {
                if (reference == null)
                    return false;

                if (newString != null)
                {
                    // We have an actual lookup to do, so it check against the reference.
                    string outString = reference.Target as string; // We need a real reference to compare.
                    if (outString == null) // Have we lost the reference?
                    {
                        reference = null; // Clear it out since it's invalid.  We may still match a later entry.
                    }
                    else if (newString == outString)
                    {
                        newString = outString; // It's a match!  Replace it with the stored official copy.
                        return true;
                    }
                    // Otherwise, the reference was still valid but didn't match.
                }
                else if (reference.IsAlive == false)
                {
                    reference = null;
                }

                return false;
            }
        }

        #endregion
    }
}
