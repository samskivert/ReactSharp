//
// ReactSharp - a library for async & FRP-ish programming in C#
// http://github.com/samskivert/ReactSharp/blob/master/LICENSE

using System;
using System.Collections.Generic;

namespace react {

  /// Maintains a list of disposables to allow mass operations on them.
  public class DisposableList : IDisposable {

    /// Disposes all connections in this set and empties it.
    public void Dispose () {
      if (_list != null) {
        List<Exception> errors = null;
        foreach (var d in _list) try {
          d.Dispose();
        } catch (Exception e) {
          if (errors == null) errors = new List<Exception>();
          errors.Add(e);
        }
        _list.Clear();
        if (errors != null) throw new AggregateException(errors);
      }
    }

    /// Adds the supplied connection to this set.
    /// @return the supplied connection.
    public T Add<T> (T d) where T : IDisposable {
      if (_list == null) _list = new List<IDisposable>();
      _list.Add(d);
      return d;
    }

    /// Removes a disposable from this list while leaving its status unchanged.
    public void Remove (IDisposable d) {
      if (_list != null) _list.Remove(d);
    }

    private List<IDisposable> _list; // lazily created
  }

  /// Provides some {@link Disposable}-related utilities.
  public class DisposableUtil {

    /// A disposable which no-ops on {@link #Dispose} and throws an exception for all other methods.
    /// This is for the following code pattern:
    ///
    /// <pre>{@code
    /// IDisposable _conn = DisposableUtil.NOOP;
    /// void Open () {
    ///    _conn = whatever.connect(...);
    /// }
    /// void Dispose () {
    ///    _conn = DisposableUtil.Dispose(_conn);
    /// }
    /// }</pre>
    ///
    /// In that it allows {@code Dispose} to avoid a null check if it's possible for {@code Dispose}
    /// to be called with no call to {@code Open} or repeatedly.
    public static readonly IDisposable NOOP = Join();

    /// Creates a closable that closes multiple connections at once. */
    public static IDisposable Join (params IDisposable[] disps) {
      return new JoinedDisposables(disps);
    }

    /// Disposes {@code disp} and returns {@link #NOOP}. This enables code like:
    /// {@code disp = DisposableUtil.Dispose(disp);} which simplifies disconnecting and
    /// resetting to {@link #NOOP}, a given connection reference.
    public static IDisposable Dispose (IDisposable disp) {
      disp.Dispose();
      return NOOP;
    }
  }

  internal class JoinedDisposables : IDisposable {
    private IDisposable[] _disps;

    public JoinedDisposables (IDisposable[] disps) {
      _disps = disps;
    }

    public void Dispose () {
      for (var ii = 0; ii < _disps.Length; ii++) {
        var d = _disps[ii];
        if (d != null) {
          _disps[ii] = null;
          d.Dispose();
        }
      }
    }
  }
}
