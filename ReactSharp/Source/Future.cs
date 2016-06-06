//
// ReactSharp - a library for async & FRP-ish programming in C#
// http://github.com/samskivert/ReactSharp/blob/master/LICENSE

using System;
using System.Collections.Generic;

namespace React {

  /// Contains the untyped parts of IFuture.
  public interface IFuture {

    /// Causes <c>listener</c> to be notified if/when this future is completed with failure. If it
    /// has already failed, the listener will be notified immediately.
    /// @return a handle via which the listener can be disconnected.
    IDisposable OnFailure (OnValue<Exception> listener);

    /// Returns whether this future is complete right now.
    bool IsComplete { get; }
  }

  /// Represents an asynchronous result. You cannot block on this result. You can <c>Map</c> or
  /// <c>FlatMap</c> it, and listen for success or failure via the <c>OnSuccess</c> and
  /// <c>OnFailure</c> methods.
  ///
  /// <p> The benefit over just using <c>Callback</c> is that results can be composed. You can
  /// subscribe to an object, flatmap the result into a service call on that object which returns
  /// the address of another object, flat map that into a request to subscribe to that object, and
  /// finally pass the resulting object to some other code via a listener. Failure can be handled
  /// once for all of these operations and you avoid nesting yourself three callbacks deep. </p>
  public interface IFuture<out T> : IFuture {

    /// Causes <c>listener</c> to be notified if/when this future is completed with success. If it
    /// has already succeeded, the listener will be notified immediately.
    /// @return a handle via which the listener can be disconnected.
    IDisposable OnSuccess (OnValue<T> listener);

    /// Causes <c>listener</c> to be notified when this future is completed. If it has already
    /// completed, the listener will be notified immediately.
    /// @return a handle via which the listener can be disconnected.
    IDisposable OnComplete (OnValue<ITry<T>> listener);

    /// Causes <c>onSuccess</c> to be notified when this future is successfully or <c>onFailure</c>
    /// to be notified when this future fails. If it has already completed, the appropriate listener
    /// will be notified immediately.
    /// @return a handle via which the listener can be disconnected.
    IDisposable OnComplete (OnValue<T> onSuccess, OnValue<Exception> onFailure);

    /// Transforms this future by mapping its result upon arrival.
    IFuture<R> Transform<R> (Func<ITry<T>,ITry<R>> func);

    /// Maps the value of a successful result using <c>func</c> upon arrival.
    IFuture<R> Map<R> (Func<T, R> func);

    // /// Maps the value of a failed result using <c>func</c> upon arrival.
    // IFuture<U> Recover<U> (Func<Exception, U> func) where T : U;

    /// Maps a successful result to a new result using <c>func</c> when it arrives. Failure on the
    /// original result or the mapped result are both dispatched to the mapped result. This is
    /// useful for chaining asynchronous actions. It's also known as monadic bind.
    IFuture<R> FlatMap<R> (Func<T, IFuture<R>> func);
  }

  /// Helper functions for IFuture.
  public static class Future {

    /// Returns a future with a pre-existing success value.
    public static IFuture<T> Success<T> (T value) {
        return Result(Try.Success(value));
    }

    /// Returns a future with a pre-existing failure value.
    public static IFuture<T> Failure<T> (Exception cause) {
        return Result(Try.Failure<T>(cause));
    }

    /// Returns a future with an already-computed result.
    public static IFuture<T> Result<T> (ITry<T> result) {
      return new CompletedFuture<T>(result);
    }

    /// Returns a future containing a list of all success results from <c>futures</c> if all of the
    /// futures complete successfully, or a {@link AggregateException} aggregating all failures,
    /// if any of the futures fails.
    ///
    /// <p>If <c>futures</c> is an ordered collection, the resulting list will match the order of
    /// the futures. If not, result list is in <c>futures</c>' iteration order.</p>
    public static IFuture<List<T>> Sequence<T> (ICollection<IFuture<T>> futures) {
      // if we're passed an empty list of futures, succeed immediately with an empty list
      if (futures.Count == 0) return Future.Success(new List<T>());
      return new Sequencer<T>(futures).promise;
    }

    /// Returns a future containing the results of <c>a</c> and <c>b</c> if both futures complete
    /// successfully, or an {@link AggregateException} aggregating all failures, if either of the
    /// futures fails.
    public static IFuture<Tuple<A,B>> Sequence<A,B> (IFuture<A> a, IFuture<B> b) {
      return new Sequencer<A,B>(a, b).promise;
    }

    /// Returns a future containing a list of all success results from <c>futures</c>. Any failure
    /// results are simply omitted from the list. The success results are also in no particular
    /// order. If all of <c>futures</c> fail, the resulting list will be empty.
    public static IFuture<ICollection<T>> Collect<T> (ICollection<IFuture<T>> futures) {
      // if we're passed an empty list of futures, succeed immediately with an empty list
      if (futures.Count == 0) return Future.Success(new List<T>());

      var pseq = new Promise<ICollection<T>>();
      var results = new List<T>();
      var remain = futures.Count;
      OnValue<ITry<T>> collector = result => {
        if (result.IsSuccess) results.Add(result.Value);
        if (--remain == 0) pseq.Succeed(results);
      };
      foreach (var future in futures) future.OnComplete(collector);
      return pseq;
    }

    internal class NoopDisposable : IDisposable {
      public void Dispose () {}
    }
    internal static readonly IDisposable NOOP = new NoopDisposable();
  }

  /// Handles some of the standard plumbing for futures.
  public abstract class AbstractFuture<T> : IFuture<T> {

    public abstract IDisposable OnComplete (OnValue<ITry<T>> listener);

    public abstract bool IsComplete { get; }

    public IDisposable OnSuccess (OnValue<T> listener) {
      return OnComplete(result => {
        if (result.IsSuccess) listener(result.Value);
      });
    }

    public IDisposable OnFailure (OnValue<Exception> listener) {
      return OnComplete(result => {
        if (result.IsFailure) listener(result.Cause);
      });
    }

    public IDisposable OnComplete (OnValue<T> onSuccess, OnValue<Exception> onFailure) {
      return OnComplete(result => {
        if (result.IsSuccess) onSuccess(result.Value);
        else onFailure(result.Cause);
      });
    }

    public IFuture<R> Transform<R> (Func<ITry<T>,ITry<R>> func) {
      Promise<R> xf = new Promise<R>();
      OnComplete(result => {
        ITry<R> xresult;
        // catch any exception while transforming the result and fail the transformed future if
        // one is thrown, but...
        try {
          xresult = func(result);
        } catch (Exception t) {
          xf.Fail(t);
          return;
        }
        // ...don't catch exceptions caused thrown due to actually completing our transformed
        // future, let those propagate out to the completer of the original future just like any
        // other failing listener
        xf.Complete(xresult);
      });
      return xf;
    }

    public IFuture<R> Map<R> (Func<T, R> func) {
      return Transform(Try.Lift(func));
    }

    // public IFuture<U> Recover<U> (Func<Exception, U> func) where T : U {
    //   Func<ITry<T>,ITry<U>> lifted = result => result.Recover(func);
    //   return Transform(lifted);
    // }

    public IFuture<R> FlatMap<R> (Func<T, IFuture<R>> func) {
      var mapped = new Promise<R>();
      OnComplete(result => {
        if (result.IsFailure) mapped.Fail(result.Cause);
        else try {
          func(result.Value).OnComplete(r => mapped.Complete(r));
        } catch (Exception e) {
          mapped.Fail(e);
        }
      });
      return mapped;
    }
  }

  internal class CompletedFuture<T> : AbstractFuture<T> {
    private readonly ITry<T> _result;

    public CompletedFuture (ITry<T> result) {
      _result = result;
    }

    override public bool IsComplete { get { return true; } }
    override public IDisposable OnComplete (OnValue<ITry<T>> listener) {
      listener(_result);
      return Future.NOOP;
    }
  }

  internal class SequencerBase {
    protected List<Exception> _errors;
    protected int _remain;

    protected void OnFailure (Exception cause) {
      lock (this) {
        if (_errors == null) _errors = new List<Exception>();
        _errors.Add(cause);
      }
    }
  }

  internal class Sequencer<T> :SequencerBase {
    public readonly Promise<List<T>> promise = new Promise<List<T>>();
    private T[] _results;

    public Sequencer (ICollection<IFuture<T>> futures) {
      var count = futures.Count;
      _results = new T[count];
      _remain = count;
      var ii = 0 ; foreach (var future in futures) {
        int idx = ii;
        future.OnComplete(result => OnResult(idx, result));
        ii += 1;
      }
    }

    private void OnResult (int idx, ITry<T> result) {
      if (result.IsSuccess) {
        _results[idx] = result.Value;
      } else {
        OnFailure(result.Cause);
      }
      lock (this) {
        if (--_remain == 0) {
          if (_errors != null) promise.Fail(new AggregateException(_errors));
          else promise.Succeed(new List<T>(_results));
        }
      }
    }
  }

  internal class Sequencer<A,B> :SequencerBase {
    public readonly Promise<Tuple<A,B>> promise = new Promise<Tuple<A,B>>();
    private A _resultA;
    private B _resultB;

    public Sequencer (IFuture<A> a, IFuture<B> b) {
      _remain = 2;
      a.OnComplete(result => {
        if (result.IsSuccess) _resultA = result.Value;
        else OnFailure(result.Cause);
        OnComplete();
      });
      b.OnComplete(result => {
        if (result.IsSuccess) _resultB = result.Value;
        else OnFailure(result.Cause);
        OnComplete();
      });
    }

    private void OnComplete () {
      lock (this) {
        if (--_remain == 0) {
          if (_errors != null) promise.Fail(new AggregateException(_errors));
          else promise.Succeed(Tuple.Create(_resultA, _resultB));
        }
      }
    }
  }

  internal class Sequencer<A,B,C> :SequencerBase {
    public readonly Promise<Tuple<A,B,C>> promise = new Promise<Tuple<A,B,C>>();
    private A _resultA;
    private B _resultB;
    private C _resultC;

    public Sequencer (IFuture<A> a, IFuture<B> b, IFuture<C> c) {
      _remain = 3;
      a.OnComplete(result => {
        if (result.IsSuccess) _resultA = result.Value;
        else OnFailure(result.Cause);
        OnComplete();
      });
      b.OnComplete(result => {
        if (result.IsSuccess) _resultB = result.Value;
        else OnFailure(result.Cause);
        OnComplete();
      });
      c.OnComplete(result => {
        if (result.IsSuccess) _resultC = result.Value;
        else OnFailure(result.Cause);
        OnComplete();
      });
    }

    private void OnComplete () {
      lock (this) {
        if (--_remain == 0) {
          if (_errors != null) promise.Fail(new AggregateException(_errors));
          else promise.Succeed(Tuple.Create(_resultA, _resultB, _resultC));
        }
      }
    }
  }
}
