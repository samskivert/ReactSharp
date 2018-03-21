//
// ReactSharp - a library for async & FRP-ish programming in C#
// http://github.com/samskivert/ReactSharp/blob/master/LICENSE

using System;
using System.Collections.Generic;

namespace React {

  /// A view of a {@link Signal}, on which slots may listen, but to which one cannot emit events.
  /// This is generally used to provide signal-like views of changing entities. See {@link
  /// AbstractValue} for an example.
  public interface ISignal<out T> {

    /// Creates a signal that maps this signal via a function. When this signal emits a value, the
    /// mapped signal will emit that value as transformed by the supplied function. The mapped
    /// signal will retain a connection to this signal for as long as it has connections of its own.
    ISignal<M> Map<M> (Func<T, M> func);

    /// Creates a signal that emits a value only when the supplied filter function returns true. The
    /// filtered signal will retain a connection to this signal for as long as it has connections of
    /// its own.
    ISignal<T> Filter (Func<T, Boolean> pred);

    /// Returns a future that is completed with the next event emitted from this signal.
    IFuture<T> Next ();

    /// Connects this signal to the supplied slot, such that when an event is emitted from this
    /// signal, the slot will be notified.
    ///
    /// @return a handle can be used to cancel the connection.
    IDisposable OnEmit (Action<T> slot);

    /// Connects this signal to the supplied unit slot, such that when an event is emitted from this
    /// signal, the slot will be notified.
    ///
    /// @return a handle can be used to cancel the connection.
    IDisposable OnEmit (Action unitSlot);
  }

  /// A signal that emits events of type <c>T</c>. Listeners may be connected to a signal to be
  /// notified upon event emission.
  public class Signal<T> : AbstractSignal<T> {

    /// Causes this signal to emit the supplied event to connected slots.
    public void Emit (T value) {
      NotifyEmit(value);
    }
  }

  /// Handles the machinery of connecting slots to a signal and emitting events to them, without
  /// exposing a public interface for emitting events. This can be used by entities which wish to
  /// expose a signal-like interface for listening, without allowing external callers to emit
  /// signals.
  public class AbstractSignal<T> : ISignal<T> {

    /// Returns whether this signal has any active connections.
    public bool HasConnections () { return _onEmit != null; }

    public ISignal<M> Map<M> (Func<T, M> func) {
      return new MappedSignal<T,M>(this, func);
    }
    public ISignal<T> Filter (Func<T, bool> pred) {
      return new FilteredSignal<T>(this, pred);
    }
    public IFuture<T> Next () {
      Promise<T> result = new Promise<T>();
      IDisposable conn = null;
      conn = this.OnEmit(value => {
        conn.Dispose();
        result.Succeed(value);
      });
      return result;
    }
    public IDisposable OnEmit (Action unitSlot) {
      return OnEmit((T value) => { unitSlot(); });
    }
    public IDisposable OnEmit (Action<T> slot) {
      _onEmit += slot;
      ConnectionAdded();
      return new Connection(this, slot);
    }

    private void RemoveConnection (Action<T> slot) {
      _onEmit -= slot;
      ConnectionRemoved();
    }

    protected virtual void ConnectionAdded () {}
    protected virtual void ConnectionRemoved () {}

    private class Connection : IDisposable {
      private AbstractSignal<T> _signal;
      private Action<T> _listener;

      public Connection (AbstractSignal<T> signal, Action<T> listener) {
        _signal = signal;
        _listener = listener;
      }

      public void Dispose () {
        if (_listener != null) {
          _signal.RemoveConnection(_listener);
          _listener = null;
        }
      }
    }

    /// Emits the supplied event to all connected slots.
    protected void NotifyEmit (T value) {
      if (_onEmit != null) {
        var lners = _onEmit.GetInvocationList();
        List<Exception> errors = null;
        foreach (Action<T> lner in lners) {
          try {
            lner(value);
          } catch (Exception e) {
            if (errors == null) errors = new List<Exception>();
            errors.Add(e);
          }
        }
        if (errors != null) throw new AggregateException(errors);
      }
    }

    private Action<T> _onEmit;
  }

  /// Plumbing to implement dependent signals in such a way that they automatically manage a
  /// connection to their underlying signal. When the dependent signal adds its first connection, it
  /// establishes a connection to the underlying signal, and when it removes its last connection it
  /// clears its connection from the underlying signal.
  internal abstract class DependentSignal<T> : AbstractSignal<T> {

    /// Establishes a connection to our source signal. Called when go from zero to one listeners.
    /// When we go from one to zero listeners, the connection will automatically be cleared.
    /// @return the newly established connection.
    protected abstract IDisposable Connect ();

    override protected void ConnectionAdded () {
      base.ConnectionAdded();
      if (_conn == null) _conn = Connect();
    }

    override protected void ConnectionRemoved () {
      base.ConnectionRemoved();
      if (!HasConnections() && _conn != null) {
        _conn.Dispose();
        _conn = null;
      }
    }

    protected IDisposable _conn;
  }

  internal class MappedSignal<T,M> : DependentSignal<M> {
    private readonly AbstractSignal<T> _outer;
    private readonly Func<T, M> _func;

    public MappedSignal (AbstractSignal<T> outer, Func<T, M> func) {
      _outer = outer;
      _func = func;
    }

    override protected IDisposable Connect () {
      return _outer.OnEmit(value => NotifyEmit(_func(value)));
    }
  }

  internal class FilteredSignal<T> : DependentSignal<T> {
    private AbstractSignal<T> _outer;
    private Func<T, bool> _pred;

    public FilteredSignal (AbstractSignal<T> outer, Func<T, bool> pred) {
      _outer = outer;
      _pred = pred;
    }

    override protected IDisposable Connect () {
      return _outer.OnEmit(value => {
        if (_pred(value)) NotifyEmit(value);
      });
    }
  }
}
