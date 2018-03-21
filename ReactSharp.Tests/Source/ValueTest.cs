//
// ReactSharp - a library for async & FRP-ish programming in C#
// http://github.com/samskivert/ReactSharp/blob/master/LICENSE

using System;
using System.Diagnostics;
using NUnit.Framework;

namespace React {

  /// Tests aspects of the {@link Value} class.
  [TestFixture] public class ValueTest {

    public class Counter {
      public int notifies;
      public OnChange<T> OnChange<T> () {
        return (nvalue, ovalue) => {
          notifies += 1;
        };
      }
      public Action<T> Action<T> () {
        return (value) => {
          notifies += 1;
        };
      }
    }

    [SetUp] public void ShowDebugLogging () {
      Trace.Listeners.Add(new ConsoleTraceListener(true));
    }

    [Test] public void testSimpleListener () {
      var value = new Value<int>(42);
      var fired = false;
      value.OnChange((nvalue, ovalue) =>{
        Assert.AreEqual(42, ovalue);
        Assert.AreEqual(15, nvalue);
        fired = true;
      });
      Assert.AreEqual(42, value.Update(15));
      Assert.AreEqual(15, value.Current);
      Assert.True(fired);
    }

    [Test] public void testChanges () {
      var value = new Value<int>(42);
      var fired = false;
      value.Changes().OnEmit(v => {
        Assert.AreEqual(15, v);
        fired = true;
      });
      value.Update(15);
      Assert.True(fired);
    }

    [Test] public void testChangesNext () {
      var value = new Value<int>(42);
      var counter = new Counter();
      value.Changes().Next().OnSuccess(counter.Action<int>());
      value.Update(15);
      value.Update(42);
      Assert.AreEqual(1, counter.notifies);
    }

    [Test] public void testMappedValue () {
      var value = new Value<int>(42);
      var mapped = value.Map(v => v.ToString());

      var counter = new Counter();
      var c1 = mapped.OnChange(counter.OnChange<string>());
      var c2 = mapped.OnChange((nv, ov) => Assert.AreEqual("15", nv));

      value.Update(15);
      Assert.AreEqual(1, counter.notifies);
      value.Update(15);
      Assert.AreEqual(1, counter.notifies);
      value.UpdateForce(15);
      Assert.AreEqual(2, counter.notifies);

      // disconnect from the mapped value and ensure that it disconnects in turn
      c1.Dispose();
      c2.Dispose();
      Assert.False(value.HasConnections());
    }

    [Test] public void testFlatMappedValue () {
      var value1 = new Value<int>(42);
      var value2 = new Value<int>(24);
      var toggle = new Value<bool>(true);
      var flatMapped = toggle.FlatMap(t => t ? value1 : value2);

      var counter1 = new Counter();
      var counter2 = new Counter();
      var counterM = new Counter();
      var c1 = value1.OnChange(counter1.OnChange<int>());
      var c2 = value2.OnChange(counter2.OnChange<int>());
      var cM = flatMapped.OnChange(counterM.OnChange<int>());

      flatMapped.Changes().Next().OnSuccess(v => Assert.AreEqual(10, v));
      value1.Update(10);
      Assert.AreEqual(1, counter1.notifies);
      Assert.AreEqual(1, counterM.notifies);

      value2.Update(1);
      Assert.AreEqual(1, counter2.notifies);
      Assert.AreEqual(1, counterM.notifies); // not incremented

      flatMapped.Changes().Next().OnSuccess(v => Assert.AreEqual(15, v));
      toggle.Update(false);

      value2.Update(15);
      Assert.AreEqual(2, counter2.notifies);
      Assert.AreEqual(2, counterM.notifies); // is incremented

      // disconnect from the mapped value and ensure that it disconnects in turn
      c1.Dispose();
      c2.Dispose();
      cM.Dispose();
      Assert.False(value1.HasConnections());
      Assert.False(value2.HasConnections());
    }

    [Test] public void testConnectionlessFlatMappedValue () {
      var value1 = new Value<int>(42);
      var value2 = new Value<int>(24);
      var toggle = new Value<bool>(true);
      var flatMapped = toggle.FlatMap(t => t ? value1 : value2);
      Assert.AreEqual(42, flatMapped.Current);
      toggle.Update(false);
      Assert.AreEqual(24, flatMapped.Current);
    }

    [Test] public void testConnectNotify () {
      var value = new Value<int>(42);
      var fired = false;
      value.OnChangeNotify((nvalue, ovalue) => {
        Assert.AreEqual(42, nvalue);
        fired = true;
      });
      Assert.True(fired);
    }

    // [Test] public void testJoinedValue () {
    //   Value<Integer> number = Value.create(1);
    //   Value<String> string = Value.create("foo");
    //   ValueView<Values.T2<Integer,String>> both = Values.join(number, string);
    //   SignalTest.Counter counter = new SignalTest.Counter();
    //   both.connect(counter);
    //   number.update(2);
    //   Assert.AreEqual(1, counter.notifies);
    //   Assert.AreEqual(new Values.T2<>(2, "foo"), both.Current);
    //   string.update("bar");
    //   Assert.AreEqual(2, counter.notifies);
    //   number.update(2);
    //   Assert.AreEqual(2, counter.notifies);
    //   string.update("bar");
    //   Assert.AreEqual(2, counter.notifies);
    // }
  }
}
