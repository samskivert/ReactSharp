//
// ReactSharp - a library for async & FRP-ish programming in C#
// http://github.com/samskivert/ReactSharp/blob/master/LICENSE

using NUnit.Framework;
using System.Collections.Generic;
using System;
using System.Diagnostics;

namespace React {

  /// Tests basic signals and slots behavior.
  [TestFixture] public class SignalTest {

    public class Counter {
      public int notifies;
      public OnValue<T> Increment<T> () {
        return (value) => {
          notifies += 1;
        };
      }

      public Action Increment () {
        return () => {
          notifies += 1;
        };
      }
    }

    public class Accum<T> {
      public List<T> values = new List<T>();

      public OnValue<T> Adder () {
        return (value) => {
          values.Add(value);
        };
      }

      public void AssertContains (params T[] values) {
        var expect = new List<T>(values);
        Assert.AreEqual(expect, this.values);
      }
    }

    public static OnValue<T> Require<T> (T reqValue) {
      return (value) => {
        Assert.AreEqual(reqValue, value);
      };
    }

    [SetUp] public void ShowDebugLogging () {
      Trace.Listeners.Add(new ConsoleTraceListener(true));
    }

    [Test] public void testSignalToSlot () {
      var signal = new Signal<int>();
      var accum = new Accum<int>();
      signal.OnEmit(accum.Adder());
      signal.Emit(1);
      signal.Emit(2);
      signal.Emit(3);
      accum.AssertContains(1, 2, 3);
    }

    [Test]
    public void testSignalNext () {
      var signal = new Signal<int>();
      var accum = new Accum<int>();
      var accum3 = new Accum<int>();

      signal.Next().OnSuccess(accum.Adder());
      signal.Filter(v => v == 3).Next().OnSuccess(accum3.Adder());

      signal.Emit(1); // adder should only receive this value
      accum.AssertContains(1);
      accum3.AssertContains();

      signal.Emit(2);
      accum.AssertContains(1);
      accum3.AssertContains();

      signal.Emit(3);
      accum.AssertContains(1);
      accum3.AssertContains(3);

      // signal should no longer have connections at this point
      Assert.False(signal.HasConnections());

      signal.Emit(3); // adder3 should not receive multiple threes
      accum3.AssertContains(3);
    }

    [Test] public void testAddDuringDispatch () {
      var signal = new Signal<int>();
      var toAdd = new Accum<int>();

      IDisposable once = null;
      once = signal.OnEmit(value => {
        signal.OnEmit(toAdd.Adder());
        once.Dispose();
      });

      // this will connect our new signal but not dispatch to it
      signal.Emit(5);
      Assert.AreEqual(0, toAdd.values.Count);

      // now dispatch an event that should go to the added signal
      signal.Emit(42);
      toAdd.AssertContains(42);
    }

    [Test] public void testRemoveDuringDispatch () {
      var signal = new Signal<int>();
      var toPreRemove = new Accum<int>();
      var preConn = signal.OnEmit(toPreRemove.Adder());

      var toPostRemove = new Accum<int>();
      IDisposable postConn = null;

      // dispatch one event and make sure it's...
      signal.Emit(5);
      toPreRemove.AssertContains(5); // received
      toPostRemove.AssertContains(); // not received

      // add our remover and then our accum after our remover
      signal.OnEmit(value => {
        preConn.Dispose();
        postConn.Dispose();
      });
      postConn = signal.OnEmit(toPostRemove.Adder());

      // now emit a signal
      signal.Emit(42);

      // both listeners should receive the event because dispatch takes place on a copy of the
      // listener list created prior to dispatch
      toPreRemove.AssertContains(5, 42);
      toPostRemove.AssertContains(42);

      // finally dispatch one more event and make sure no one gets it (i.e. we were really removed)
      signal.Emit(9);
      toPreRemove.AssertContains(5, 42);
      toPostRemove.AssertContains(42);
    }

    [Test] public void testAddAndRemoveDuringDispatch () {
      var signal = new Signal<int>();
      var toAdd = new Accum<int>();
      var toRemove = new Accum<int>();
      var rconn = signal.OnEmit(toRemove.Adder());

      // dispatch one event and make sure it's received by toRemove
      signal.Emit(5);
      toRemove.AssertContains(5);

      // now add our adder/remover signal, and dispatch again
      signal.OnEmit(value => {
        rconn.Dispose();
        signal.OnEmit(toAdd.Adder());
      });
      signal.Emit(42);

      // make sure toRemove got this event and toAdd didn't
      toRemove.AssertContains(5, 42);
      toAdd.AssertContains();

      // finally emit one more and ensure that toAdd got it and toRemove didn't
      signal.Emit(9);
      toAdd.AssertContains(9);
      toRemove.AssertContains(5, 42);
    }

    [Test] public void testDispatchDuringDispatch () {
      var signal = new Signal<int>();
      var counter = new Accum<int>();
      signal.OnEmit(counter.Adder());

      // connect a slot that will emit during dispatch
      IDisposable conn = null;
      conn = signal.OnEmit(value => {
        conn.Dispose();
        if (value == 5) signal.Emit(value*2);
        // ensure that we're not notified twice even though we emit during dispatch
        else Assert.Fail("lner notified after Dispose()");
      });

      // dispatch one event and make sure that both events are received
      signal.Emit(5);
      counter.AssertContains(5, 10);
    }

    [Test] public void testSingleFailure () {
      var signal = new Signal<int>();
      var preCounter = new Accum<int>();
      signal.OnEmit(preCounter.Adder());
      signal.OnEmit(value => {
        throw new InvalidOperationException("Bang!");
      });
      var postCounter = new Accum<int>();
      signal.OnEmit(postCounter.Adder());
      try {
        signal.Emit(0);
        Assert.Fail("Emit should have thrown.");
      } catch (AggregateException e) {
        Assert.AreEqual(1, e.InnerExceptions.Count);
      }

      // both pre and post counter should have received notifications
      preCounter.AssertContains(0);
      postCounter.AssertContains(0);
    }

    [Test] public void testMultiFailure () {
      var signal = new Signal<int>();
      signal.OnEmit(value => {
        throw new InvalidOperationException("Bing!");
      });
      signal.OnEmit(value => {
        throw new InvalidOperationException("Bang!");
      });
      try {
        signal.Emit(0);
        Assert.Fail("Emit should have thrown.");
      } catch (AggregateException e) {
        Assert.AreEqual(2, e.InnerExceptions.Count);
      }
    }

    [Test] public void testMappedSignal () {
      var signal = new Signal<int>();
      var mapped = signal.Map(value => value.ToString());

      var counter = new Counter();
      var c1 = mapped.OnEmit(counter.Increment<string>());
      var c2 = mapped.OnEmit(value => Assert.AreEqual("15", value));

      signal.Emit(15);
      Assert.AreEqual(1, counter.notifies);
      signal.Emit(15);
      Assert.AreEqual(2, counter.notifies);

      // disconnect from the mapped signal and ensure that it clears its connection
      c1.Dispose();
      c2.Dispose();
      Assert.False(signal.HasConnections());
    }

    [Test] public void testFilter () {
      var signal = new Signal<string>();
      var filtered = signal.Filter(v => v != null);
      var counter = new Counter();

      filtered.OnEmit(v => Assert.False(v == null));
      filtered.OnEmit(counter.Increment<string>());

      signal.Emit(null);
      signal.Emit("foozle");
      Assert.AreEqual(1, counter.notifies);
    }

    [Test] public void testUnitSlot () {
      var signal = new Signal<string>();
      var counter = new Counter();

      var connection = signal.OnEmit(counter.Increment());
      signal.Emit("foozle");
      connection.Dispose();
      signal.Emit("barzle");
      Assert.AreEqual(1, counter.notifies);
    }
  }
}
