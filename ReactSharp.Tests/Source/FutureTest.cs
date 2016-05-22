//
// ReactSharp - a library for async & FRP-ish programming in C#
// http://github.com/samskivert/ReactSharp/blob/master/LICENSE

using NUnit.Framework;
using System.Collections.Generic;
using System;
using System.Diagnostics;

namespace React {

  /// Tests futures and promises.
  [TestFixture] public class FutureTest {

    public class FutureCounter {
      public int successes;
      public int failures;
      public int completes;

      public void Bind<T> (IFuture<T> future) {
        Reset();
        future.OnSuccess(v => { successes += 1; });
        future.OnFailure(e => { failures += 1; });
        future.OnComplete(r => { completes += 1; });
      }

      public void Check (String state, int scount, int fcount, int ccount) {
        Assert.AreEqual(scount, successes, "Successes");
        Assert.AreEqual(fcount, failures, "Failures");
        Assert.AreEqual(ccount, completes, "Completes");
      }

      public void Reset () {
        successes = 0;
        failures = 0;
        completes = 0;
      }
    }

    public static readonly Func<string,bool> NotNull = v => { return v != null; };

    public static List<T> List<T> (params T[] elems) {
      return new List<T>(elems);
    }

    [Test] public void TestImmediate () {
      var counter = new FutureCounter();

      var success = Future.Success("Yay!");
      counter.Bind(success);
      counter.Check("immediate succeed", 1, 0, 1);

      var failure = Future.Failure<string>(new Exception("Boo!"));
      counter.Bind(failure);
      counter.Check("immediate failure", 0, 1, 1);
    }

    [Test] public void testDeferred () {
      var counter = new FutureCounter();

      var success = new Promise<string>();
      counter.Bind(success);
      counter.Check("before succeed", 0, 0, 0);
      success.Succeed("Yay!");
      counter.Check("after succeed", 1, 0, 1);

      var failure = new Promise<string>();
      counter.Bind(failure);
      counter.Check("before fail", 0, 0, 0);
      failure.Fail(new Exception("Boo!"));
      counter.Check("after fail", 0, 1, 1);

      Assert.False(success.HasConnections());
      Assert.False(failure.HasConnections());
    }

    [Test] public void testMappedImmediate () {
      var counter = new FutureCounter();

      var success = Future.Success("Yay!");
      counter.Bind(success.Map(NotNull));
      counter.Check("immediate succeed", 1, 0, 1);

      var failure = Future.Failure<string>(new Exception("Boo!"));
      counter.Bind(failure.Map(NotNull));
      counter.Check("immediate failure", 0, 1, 1);
    }

    [Test] public void testMappedDeferred () {
      var counter = new FutureCounter();

      var success = new Promise<string>();
      counter.Bind(success.Map(NotNull));
      counter.Check("before succeed", 0, 0, 0);
      success.Succeed("Yay!");
      counter.Check("after succeed", 1, 0, 1);

      var failure = new Promise<string>();
      counter.Bind(failure.Map(NotNull));
      counter.Check("before fail", 0, 0, 0);
      failure.Fail(new Exception("Boo!"));
      counter.Check("after fail", 0, 1, 1);

      Assert.False(success.HasConnections());
      Assert.False(failure.HasConnections());
    }

    [Test] public void testFlatMappedImmediate () {
      var scounter = new FutureCounter();
      var fcounter = new FutureCounter();
      var ccounter = new FutureCounter();
      Func<string,IFuture<bool>> successMap = value => {
        return Future.Success(value != null);
      };
      Func<string,IFuture<bool>> failMap = value => {
        return Future.Failure<bool>(new Exception("Barzle!"));
      };
      Func<string,IFuture<bool>> crashMap = value => {
        throw new Exception("Barzle!");
      };

      var success = Future.Success("Yay!");
      scounter.Bind(success.FlatMap(successMap));
      fcounter.Bind(success.FlatMap(failMap));
      ccounter.Bind(success.FlatMap(crashMap));
      scounter.Check("immediate success/success", 1, 0, 1);
      fcounter.Check("immediate success/failure", 0, 1, 1);
      ccounter.Check("immediate success/crash",   0, 1, 1);

      var failure = Future.Failure<string>(new Exception("Boo!"));
      scounter.Bind(failure.FlatMap(successMap));
      fcounter.Bind(failure.FlatMap(failMap));
      ccounter.Bind(failure.FlatMap(crashMap));
      scounter.Check("immediate failure/success", 0, 1, 1);
      fcounter.Check("immediate failure/failure", 0, 1, 1);
      ccounter.Check("immediate failure/crash",   0, 1, 1);
    }

    [Test] public void testFlatMappedDeferred () {
      var scounter = new FutureCounter();
      var fcounter = new FutureCounter();
      Func<string,IFuture<bool>> successMap = value => {
        return Future.Success(value != null);
      };
      Func<string,IFuture<bool>> failMap = value => {
        return Future.Failure<bool>(new Exception("Barzle!"));
      };

      var success = new Promise<string>();
      scounter.Bind(success.FlatMap(successMap));
      scounter.Check("before succeed/succeed", 0, 0, 0);
      fcounter.Bind(success.FlatMap(failMap));
      fcounter.Check("before succeed/fail", 0, 0, 0);
      success.Succeed("Yay!");
      scounter.Check("after succeed/succeed", 1, 0, 1);
      fcounter.Check("after succeed/fail", 0, 1, 1);

      var failure = new Promise<string>();
      scounter.Bind(failure.FlatMap(successMap));
      fcounter.Bind(failure.FlatMap(failMap));
      scounter.Check("before fail/success", 0, 0, 0);
      fcounter.Check("before fail/failure", 0, 0, 0);
      failure.Fail(new Exception("Boo!"));
      scounter.Check("after fail/success", 0, 1, 1);
      fcounter.Check("after fail/failure", 0, 1, 1);

      Assert.False(success.HasConnections());
      Assert.False(failure.HasConnections());
    }

    [Test] public void testFlatMappedDoubleDeferred () {
      var scounter = new FutureCounter();
      var fcounter = new FutureCounter();

      {
        var success = new Promise<string>();
        var innerSuccessSuccess = new Promise<bool>();
        scounter.Bind(success.FlatMap(value => innerSuccessSuccess));
        scounter.Check("before succeed/succeed", 0, 0, 0);
        var innerSuccessFailure = new Promise<string>();
        fcounter.Bind(success.FlatMap(value => innerSuccessFailure));
        fcounter.Check("before succeed/fail", 0, 0, 0);

        success.Succeed("Yay!");
        scounter.Check("after first succeed/succeed", 0, 0, 0);
        fcounter.Check("after first succeed/fail", 0, 0, 0);
        innerSuccessSuccess.Succeed(true);
        scounter.Check("after second succeed/succeed", 1, 0, 1);
        innerSuccessFailure.Fail(new Exception("Boo hoo!"));
        fcounter.Check("after second succeed/fail", 0, 1, 1);

        Assert.False(success.HasConnections());
        Assert.False(innerSuccessSuccess.HasConnections());
        Assert.False(innerSuccessFailure.HasConnections());
      }

      {
        var failure = new Promise<string>();
        var innerFailureSuccess = new Promise<bool>();
        scounter.Bind(failure.FlatMap(value => innerFailureSuccess));
        scounter.Check("before fail/succeed", 0, 0, 0);
        var innerFailureFailure = new Promise<bool>();
        fcounter.Bind(failure.FlatMap(value => innerFailureFailure));
        fcounter.Check("before fail/fail", 0, 0, 0);

        failure.Fail(new Exception("Boo!"));
        scounter.Check("after first fail/succeed", 0, 1, 1);
        fcounter.Check("after first fail/fail", 0, 1, 1);
        innerFailureSuccess.Succeed(true);
        scounter.Check("after second fail/succeed", 0, 1, 1);
        innerFailureFailure.Fail(new Exception("Is this thing on?"));
        fcounter.Check("after second fail/fail", 0, 1, 1);

        Assert.False(failure.HasConnections());
        Assert.False(innerFailureSuccess.HasConnections());
        Assert.False(innerFailureFailure.HasConnections());
      }
    }

    [Test] public void testSequenceImmediate () {
      var counter = new FutureCounter();

      var success1 = Future.Success("Yay 1!");
      var success2 = Future.Success("Yay 2!");

      var failure1 = Future.Failure<string>(new Exception("Boo 1!"));
      var failure2 = Future.Failure<string>(new Exception("Boo 2!"));

      var sucseq = Future.Sequence(List(success1, success2));
      counter.Bind(sucseq);
      sucseq.OnSuccess(results => {
        Assert.AreEqual(List("Yay 1!", "Yay 2!"), results);
      });
      counter.Check("immediate seq success/success", 1, 0, 1);

      counter.Bind(Future.Sequence(List(success1, failure1)));
      counter.Check("immediate seq success/failure", 0, 1, 1);

      counter.Bind(Future.Sequence(List(failure1, success2)));
      counter.Check("immediate seq failure/success", 0, 1, 1);

      counter.Bind(Future.Sequence(List(failure1, failure2)));
      counter.Check("immediate seq failure/failure", 0, 1, 1);
    }

    [Test] public void testSequenceDeferred () {
      var counter = new FutureCounter();

      var success1 = new Promise<string>();
      var success2 = new Promise<string>();
      var failure1 = new Promise<string>();
      var failure2 = new Promise<string>();

      var suc2seq = Future.Sequence(List<IFuture<string>>(success1, success2));
      counter.Bind(suc2seq);
      suc2seq.OnSuccess(results =>{
        Assert.AreEqual(List("Yay 1!", "Yay 2!"), results);
      });
      counter.Check("before seq succeed/succeed", 0, 0, 0);
      success1.Succeed("Yay 1!");
      success2.Succeed("Yay 2!");
      counter.Check("after seq succeed/succeed", 1, 0, 1);

      IFuture<List<string>> sucfailseq = Future.Sequence(List<IFuture<string>>(success1, failure1));
      sucfailseq.OnFailure(cause => {
        var agg = cause as AggregateException;
        Assert.NotNull(agg);
        Assert.AreEqual(1, agg.InnerExceptions.Count);
      });
      counter.Bind(sucfailseq);
      counter.Check("before seq succeed/fail", 0, 0, 0);
      failure1.Fail(new Exception("Boo 1!"));
      counter.Check("after seq succeed/fail", 0, 1, 1);

      IFuture<List<string>> failsucseq = Future.Sequence(List<IFuture<string>>(failure1, success2));
      failsucseq.OnFailure(cause => {
        var agg = cause as AggregateException;
        Assert.NotNull(agg);
        Assert.AreEqual(1, agg.InnerExceptions.Count);
      });
      counter.Bind(failsucseq);
      counter.Check("after seq fail/succeed", 0, 1, 1);

      IFuture<List<string>> fail2seq = Future.Sequence(List<IFuture<string>>(failure1, failure2));
      fail2seq.OnFailure(cause => {
        var agg = cause as AggregateException;
        Assert.NotNull(agg);
        Assert.AreEqual(2, agg.InnerExceptions.Count);
      });
      counter.Bind(fail2seq);
      counter.Check("before seq fail/fail", 0, 0, 0);
      failure2.Fail(new Exception("Boo 2!"));
      counter.Check("after seq fail/fail", 0, 1, 1);
    }

    [Test] public void testSequenceEmpty () {
      FutureCounter counter = new FutureCounter();
      IFuture<List<string>> seq = Future.Sequence(List<IFuture<string>>());
      counter.Bind(seq);
      counter.Check("sequence empty list succeeds", 1, 0, 1);
    }

    [Test] public void testSequenceTuple () {
      var counter = new FutureCounter();
      var str = Future.Success("string");
      var integer = Future.Success(42);

      var sucsuc = Future.Sequence(str, integer);
      sucsuc.OnSuccess(tup => {
        Assert.AreEqual("string", tup.Item1);
        Assert.AreEqual(42, tup.Item2);
      });
      counter.Bind(sucsuc);
      counter.Check("tuple2 seq success/success", 1, 0, 1);

      var fail = Future.Failure<bool>(new Exception("Alas, poor Yorrick."));
      var sucfail = Future.Sequence(str, fail);
      counter.Bind(sucfail);
      counter.Check("tuple2 seq success/fail", 0, 1, 1);

      var failsuc = Future.Sequence(fail, str);
      counter.Bind(failsuc);
      counter.Check("tuple2 seq fail/success", 0, 1, 1);
    }

    [Test] public void testCollectEmpty () {
      FutureCounter counter = new FutureCounter();
      IFuture<ICollection<string>> seq = Future.Collect(List<IFuture<string>>());
      counter.Bind(seq);
      counter.Check("collect empty list succeeds", 1, 0, 1);
    }
  }
}
