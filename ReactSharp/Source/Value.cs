//
// ReactSharp - a library for async & FRP-ish programming in C#
// http://github.com/samskivert/ReactSharp/blob/master/LICENSE

using System;
using System.Collections.Generic;

namespace React {

  /// Called when the value to which this delegate is connected has changed.
  /// @param newValue the value after the change.
  /// @param oldValue the value before the change.
  public delegate void OnChange<in T> (T newValue, T oldValue);

  /// A view of a {@link Value}, to which listeners may be added, but which one cannot update. This
  /// can be used in combination with {@link AbstractValue} to provide {@link Value} semantics to an
  /// entity which dispatches value changes in a custom manner (like over the network). Value
  /// consumers should require only a view on a value, rather than a concrete value.
  public interface IValue<out T> {

    /// Returns the current value.
    T Current {
      get;
    }

    /// Creates a value that maps this value via a function. When this value changes, the mapped
    /// listeners will be notified, regardless of whether the new and old mapped values differ. The
    /// mapped value will retain a connection to this value for as long as it has connections of its
    /// own.
    IValue<M> Map<M> (Func<T, M> func);

    /// Creates a value that flat maps (monadic binds) this value via a function. When this value
    /// changes, the mapping function is called to obtain a new reactive value. All of the listeners
    /// to the flat mapped value are "transferred" to the new reactive value. The mapped value will
    /// retain a connection to the most recent reactive value for as long as it has connections of
    /// its own.
    IValue<M> FlatMap<M> (Func<T, IValue<M>> func);

    /// Returns a signal that emits events when this value changes.
    ISignal<T> Changes ();

    /// Connects the supplied listener to this value. It will be notified when this value changes.
    /// The listener is held by a strong reference, so it's held in memory by virtue of being
    /// connected.
    /// @return a handle which can be used to cancel the connection.
    IDisposable OnChange (OnChange<T> listener);

    /// Connects the supplied listener to this value, such that it will be notified when this value
    /// changes. Also immediately notifies the listener of the current value. Note that the previous
    /// value supplied with this notification will be null. If the notification triggers an
    /// unchecked exception, the slot will automatically be disconnected and the caller need not
    /// worry about cleaning up after itself.
    /// @return a handle which can be used to cancel the connection.
    IDisposable OnChangeNotify (OnChange<T> listener);
  }

  /// A container for a single value, which may be observed for changes.
  public class Value<T> : AbstractValue<T> {

    /// Creates an instance with the supplied starting value.
    public Value (T init) {
      // we can't have any listeners at this point, so no need to notify
      _value = init;
    }

    /// Updates this instance with the supplied value. Registered listeners are notified only if the
    /// value differs from the current value, as determined via {@link Object#equals}.
    /// @return the previous value contained by this instance.
    public T Update (T value) {
      return UpdateAndNotifyIf(value);
    }

    /// Updates this instance with the supplied value. Registered listeners are notified regardless
    /// of whether the new value is equal to the old value.
    /// @return the previous value contained by this instance.
    public T UpdateForce (T value) {
      return UpdateAndNotify(value);
    }

    override public T Current {
      get { return _value; }
    }

    override protected T UpdateLocal (T value) {
      T oldValue = _value;
      _value = value;
      return oldValue;
    }

    private T _value;
  }

  /// Handles the machinery of connecting listeners to a value and notifying them, without exposing
  /// a public interface for updating the value. This can be used by libraries which wish to provide
  /// observable values, but must manage the maintenance and distribution of value updates
  /// themselves (so that they may send them over the network, for example).
  public abstract class AbstractValue<T> : IValue<T> {

    /// Returns whether this signal has any active connections.
    public bool HasConnections () { return _onChange != null; }

    public abstract T Current {
      get;
    }

    public IValue<M> Map<M> (Func<T, M> func) {
      return new MappedValue<T,M>(this, func);
    }

    public IValue<M> FlatMap<M> (Func<T, IValue<M>> func) {
      return new FlatMappedValue<T,M>(Map(func));
    }

    public ISignal<T> Changes () {
      return new ChangesSignal<T>(this);
    }

    public IDisposable OnChange (OnChange<T> listener) {
      _onChange += listener;
      ConnectionAdded();
      return new Connection(this, listener);
    }

    public IDisposable OnChangeNotify (OnChange<T> listener) {
      // connect before calling emit; if the listener changes the value in the body of OnChange, it
      // will expect to be notified of that change; however if OnChange throws a runtime exception,
      // we need to take care of disconnecting the listener because the returned connection instance
      // will never reach the caller
      IDisposable conn = OnChange(listener);
      try {
        listener(Current, default(T));
        return conn;
      } catch (Exception e) {
        conn.Dispose();
        throw e;
      }
    }

    override public int GetHashCode () {
      T value = Current;
      return (value == null) ? 0 : value.GetHashCode();
    }

    override public bool Equals (Object other) {
      if (other == null) return false;
      AbstractValue<T> ot = (other as AbstractValue<T>);
      if (ot == null) return false;
      return EqualityComparer<T>.Default.Equals(Current, ot.Current);
    }

    override public String ToString () {
      return GetType().Name + "(" + Current + ")";
    }

    protected virtual void ConnectionAdded () {}
    protected virtual void ConnectionRemoved () {}

    private void RemoveConnection (OnChange<T> slot) {
      _onChange -= slot;
      ConnectionRemoved();
    }

    private class Connection : IDisposable {
      private AbstractValue<T> _value;
      private OnChange<T> _listener;

      public Connection (AbstractValue<T> value, OnChange<T> listener) {
        _value = value;
        _listener = listener;
      }

      public void Dispose () {
        if (_listener != null) {
          _value.RemoveConnection(_listener);
          _listener = null;
        }
      }
    }

    /// Updates the value contained in this instance and notifies registered listeners iff said
    /// value is not equal to the value already contained in this instance (per {@link #areEqual}).
    protected T UpdateAndNotifyIf (T newValue) {
      return UpdateAndNotify(newValue, false);
    }

    /// Updates the value contained in this instance and notifies registered listeners.
    /// @return the previously contained value.
    protected T UpdateAndNotify (T newValue) {
      return UpdateAndNotify(newValue, true);
    }

    /// Updates the value contained in this instance and notifies registered listeners.
    /// @param force if true, the listeners will always be notified, if false the will be notified
    /// only if the new value is not equal to the old value (per {@link #areEqual}).
    /// @return the previously contained value.
    private T UpdateAndNotify (T value, bool force) {
      T ovalue = UpdateLocal(value);
      if (force || !(value == null ? ovalue == null : value.Equals(ovalue))) {
        NotifyChange(value, ovalue);
      }
      return ovalue;
    }

    /// Notifies our listeners of a value change.
    protected void NotifyChange (T newValue, T oldValue) {
      if (_onChange != null) {
        var lners = _onChange.GetInvocationList();
        List<Exception> errors = null;
        foreach (OnChange<T> lner in lners) {
          try {
            lner(newValue, oldValue);
          } catch (Exception e) {
            if (errors == null) errors = new List<Exception>();
            errors.Add(e);
          }
        }
        if (errors != null) throw new AggregateException(errors);
      }
    }

    /// Updates our locally stored value. Default implementation throws unsupported operation.
    /// @return the previously stored value.
    protected virtual T UpdateLocal (T value) {
      throw new InvalidOperationException();
    }

    private OnChange<T> _onChange;
  }

  /// Plumbing to implement dependent values in such a way that they automatically manage a
  /// connection to their underlying value. When the dependent value adds its first connection, it
  /// establishes a connection to the underlying value, and when it removes its last connection it
  /// clears its connection from the underlying value.
  internal abstract class DependentValue<T> : AbstractValue<T> {

    /// Establishes a connection to our source value. Called when go from zero to one listeners.
    /// When we go from one to zero listeners, the connection will automatically be cleared.
    /// @return the newly established connection.
    protected abstract IDisposable Connect ();

    protected virtual void Disconnect () {
      if (_conn != null) {
        _conn.Dispose();
        _conn = null;
      }
    }

    protected void Reconnect () {
      Disconnect();
      _conn = Connect();
    }

    override protected void ConnectionAdded () {
      base.ConnectionAdded();
      if (_conn == null) _conn = Connect();
    }

    override protected void ConnectionRemoved () {
      base.ConnectionRemoved();
      if (!HasConnections()) Disconnect();
    }

    protected IDisposable _conn;
  }

  internal class MappedValue<T,M> : DependentValue<M> {
    private readonly AbstractValue<T> _outer;
    private readonly Func<T, M> _func;

    public MappedValue (AbstractValue<T> outer, Func<T, M> func) {
      _outer = outer;
      _func = func;
    }

    override public M Current {
      get { return _func(_outer.Current); }
    }

    override protected IDisposable Connect () {
      return _outer.OnChange((newValue, oldValue) => {
        NotifyChange(_func(newValue), _func(oldValue));
      });
    }
  }

  internal class FlatMappedValue<T,M> : DependentValue<M> {
    private readonly IValue<IValue<M>> _mapped;
    private IDisposable _mappedConn;

    public FlatMappedValue (IValue<IValue<M>> mapped) {
      _mapped = mapped;
    }

    override public M Current {
      get { return _mapped.Current.Current; }
    }

    override protected IDisposable Connect () {
      _mappedConn = _mapped.OnChange((newValue, oldValue) => Reconnect());
      return _mapped.Current.OnChange((newValue, oldValue) => {
        NotifyChange(newValue, oldValue);
      });
    }

    override protected void Disconnect () {
      base.Disconnect();
      if (_mappedConn != null) {
        _mappedConn.Dispose();
        _mappedConn = null;
      }
    }
  }

  internal class ChangesSignal<T> : DependentSignal<T> {
    private readonly IValue<T> _value;

    public ChangesSignal (IValue<T> value) {
      _value = value;
    }

    override protected IDisposable Connect () {
      return _value.OnChange((newValue, oldValue) => NotifyEmit(newValue));
    }
  }
}
