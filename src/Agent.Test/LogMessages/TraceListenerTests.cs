using System;
using System.Collections.Generic;
using System.Diagnostics;
using Gibraltar.Agent;
using Gibraltar.Monitor.Net;
using NUnit.Framework;

namespace Loupe.Agent.Test.LogMessages
{
    [TestFixture]
    public class TraceListenerTests
    {
        private static void EnsureTraceListenerRegistered()
        {
            //Initialize our process to use our trace listener explicitly.
            if (IsListenerRegistered(typeof(LogListener)) == false)
            {
                //there isn't one registered yet, go ahead and register it
                Trace.Listeners.Add(new LogListener());
            }
        }

        private static void EnsureTraceListenerUnregistered()
        {
            List<TraceListener> victims = new List<TraceListener>();
            foreach (TraceListener traceListener in Trace.Listeners)
            {
                if (traceListener is LogListener)
                {
                    //this is one of ours, we need to remove it
                    victims.Add(traceListener); // so we can remove it after completing the iteration.
                }
            }

            //now unregister every victim
            foreach (TraceListener victim in victims)
            {
                Trace.Listeners.Remove(victim);
            }
        }

        private static bool IsListenerRegistered(Type candidate)
        {
            bool foundTraceListener = false;
            foreach (TraceListener traceListener in Trace.Listeners)
            {
                if (traceListener.GetType() == candidate)
                {
                    //yeah, we found one.
                    foundTraceListener = true;
                }
            }

            return foundTraceListener;
        }

        private static bool IsListenerRegistered(TraceListener candidate)
        {
            bool foundTraceListener = false;
            foreach (TraceListener traceListener in Trace.Listeners)
            {
                //test that it is exactly our listener object, not just a question of type.
                if (ReferenceEquals(traceListener, candidate))
                {
                    //yeah, we found one.
                    foundTraceListener = true;
                }
            }

            return foundTraceListener;
        }

        [Test]
        public void TraceListenerRegistration()
        {
            //Is it already registered?  (shouldn't be for our test to be good)
            if (IsListenerRegistered(typeof(LogListener)))
            {
                Log.TraceWarning("There is already a log listener registered, some tests may not return correct results.");
            }

            //now go and create a new one.
            LogListener newListener = new LogListener();

            Trace.Listeners.Add(newListener);

            Assert.IsTrue(IsListenerRegistered(newListener), "The log listener was not found in the trace listener collection.");

            //and now remove it
            Trace.Listeners.Remove(newListener);

            //is it there?
            Assert.IsFalse(IsListenerRegistered(newListener), "The log listener was still found in the trace listener collection after being removed.");
        }

        [Test]
        public void WriteTrace()
        {
            EnsureTraceListenerRegistered();

            LogListener currentListener = null;
            foreach (TraceListener traceListener in Trace.Listeners)
            {
                if (traceListener is LogListener listener)
                {
                    currentListener = listener;
                    break;
                }
            }

            Log.TraceInformation("Writing out trace listener messages...");
            Trace.Indent();
            Trace.TraceInformation("This is a trace information message");
            Trace.Indent();
            Trace.TraceInformation("This is a trace information message with two insertions: #1:{0} #2:{1}", "First insertion", "Second insertion");
            Trace.Indent();
            Trace.TraceWarning("This is a trace warning message");
            Trace.Indent();
            Trace.TraceWarning("This is a trace warning message with two insertions: #1:{0} #2:{1}", "First insertion", "Second insertion");
            Trace.Indent();
            Trace.TraceError("This is a trace error message");
            Trace.Indent();
            Trace.TraceError("This is a trace error message with two insertions: #1:{0} #2:{1}", "First insertion", "Second insertion");
            Trace.Unindent();
            Trace.Unindent();
            Trace.Unindent();
            Trace.Unindent();
            Trace.Unindent();
            Trace.Unindent();
            Trace.Write("This is a string message, fully unindented");
            Trace.Write(new ArgumentException("This is an argument exception"));
            Trace.Write("This is a trace write message", "This is a trace write category");
            Trace.WriteIf(false, "this is a string message");
            Trace.WriteIf(false, new ArgumentException("This is an argument exception"));
            Trace.WriteIf(false, "This is a trace write message", "This is a trace write category");

            Trace.CorrelationManager.StartLogicalOperation(1);
            Trace.TraceInformation("We started LO 1");
            currentListener.TraceOutputOptions = TraceOptions.LogicalOperationStack & TraceOptions.Callstack;
            Trace.CorrelationManager.StartLogicalOperation(2);
            Trace.TraceInformation("We started LO 2. This information will have logical operation and call stack info.");
            currentListener.TraceOutputOptions = TraceOptions.LogicalOperationStack;
            Trace.CorrelationManager.StopLogicalOperation();
            Trace.TraceInformation("We stopped LO 2. This information will have logical operation info.");
            currentListener.TraceOutputOptions = TraceOptions.Callstack;
            Trace.CorrelationManager.StopLogicalOperation();
            Trace.TraceInformation("We stopped LO 1 This information will have call stack info.");

            Log.TraceInformation("There should have been 18 trace listener messages written out.");
        }

        //test trace scenarios that freak NUnit
        [Test]
        [Explicit("These tests interfere with NUnit and will always appear to fail")]
        public void WriteFailTrace()
        {
            EnsureTraceListenerRegistered();

            //these scenarios are here because they interfere with NUnit - it treats these as causing the test to fail regardless.
            Trace.Fail("This is a trace Fail message");
            Trace.Fail("This is a trace fail message", "This is the detail message for the trace fail message");
            Trace.Assert(false);
            Trace.Assert(false, "This is a false trace assertion message");
            Trace.Assert(false, "This is a false trace assertion message", "This is the detail message for the trace fail message");
            
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            EnsureTraceListenerUnregistered();
        }
    }
}
