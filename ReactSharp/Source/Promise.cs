//
// ReactSharp - a library for async & FRP-ish programming in C#
// http://github.com/samskivert/ReactSharp/blob/master/LICENSE

using System;
using System.Collections.Generic;

namespace React {

  /// Provides a concrete implementation of {@link Future} that can be updated with a success or
  /// failure result when it becomes available.
  ///
  /// <p>This implementation also guarantees a useful behavior, which is that all listeners added
  /// prior to the completion of the promise will be cleared when the promise is completed, and no
  /// further listeners will be retained. This allows the promise to be retained after is has been
  /// completed as a useful "box" for its underlying value, without concern that references to long
  /// satisfied listeners will be inadvertently retained.</p>
  public class Promise<T> : AbstractFuture<T> {

    private OnValue<ITry<T>> _onComplete;
    private ITry<T> _result;

    /// Causes this promise to be completed successfully with {@code value}.
    public void Succeed (T value) {
      Complete(Try.Success(value));
    }

    /// Causes this promise to be completed with failure caused by {@code cause}.
    public void Fail (Exception cause) {
      Complete(Try.Failure<T>(cause));
    }

    /// Causes this promise to be completed with {@code result}.
    public void Complete (ITry<T> result) {
      if (_result != null) throw new InvalidOperationException("Already completed");
      _result = result;

      if (_onComplete != null) {
        try {
          var lners = _onComplete.GetInvocationList();
          List<Exception> errors = null;
          foreach (OnValue<ITry<T>> lner in lners) {
            try {
              lner(_result);
            } catch (Exception e) {
              if (errors == null) errors = new List<Exception>();
              errors.Add(e);
            }
          }
          if (errors != null) throw new AggregateException(errors);
        } finally {
          _onComplete = null;
        }
      }
    }

    /// Returns whether this promise has any connections.
    public bool HasConnections () { return _onComplete != null; }

    public override bool IsComplete { get { return _result != null; } }

    public override IDisposable OnComplete (OnValue<ITry<T>> listener) {
      var result = _result;
      if (result != null) {
        listener(result);
        return Future.NOOP;
      } else {
        _onComplete += listener;
        ConnectionAdded();
        return new Connection(this, listener);
      }
    }

    protected virtual void ConnectionAdded () {}
    protected virtual void ConnectionRemoved () {}

    private void RemoveConnection (OnValue<ITry<T>> listener) {
      if (_onComplete != null) {
        _onComplete -= listener;
      }
      ConnectionRemoved();
    }

    private class Connection : IDisposable {
      private Promise<T> _promise;
      private OnValue<ITry<T>> _listener;

      public Connection (Promise<T> promise, OnValue<ITry<T>> listener) {
        _promise = promise;
        _listener = listener;
      }

      public void Dispose () {
        if (_listener != null) {
          _promise.RemoveConnection(_listener);
          _listener = null;
        }
      }
    }
  }
}
