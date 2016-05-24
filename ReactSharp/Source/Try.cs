//
// ReactSharp - a library for async & FRP-ish programming in C#
// http://github.com/samskivert/ReactSharp/blob/master/LICENSE

using System;

namespace React {

  /// Contains the untyped parts of ITry.
  public interface ITry {

    /// Returns the cause of failure for a failed try. Throws <c>InvalidOperationException</c> if
    /// called on a successful try.
    Exception Cause { get; }

    /// Returns try if this is a successful try, false if it is a failed try.
    bool IsSuccess { get; }

    /// Returns try if this is a failed try, false if it is a successful try.
    bool IsFailure { get; }
  }

  /// Represents a computation that either provided a result, or failed with an exception. Monadic
  /// methods are provided that allow one to map and compose tries in ways that propagate failure.
  /// This class is not itself "reactive", but it facilitates a more straightforward interface and
  /// implementation for {@link IFuture} and {@link Promise}.
  public interface ITry<out T> : ITry {

    /// Returns the value associated with a successful try, or rethrows the exception if the try
    /// failed.
    T Value { get; }

    /// Maps successful tries through <c>func</c>, passees failure through as is.
    ITry<R> Map<R> (Func<T, R> func);

    /// Maps failed tries through <c>func</c>, passes success through as is. Note: if <c>func</c>
    /// throws an exception, you will get back a failure try with the new failure.
    // ITry<U> Recover<U> (Func<Exception, U> func) where T : U;

    /// Maps successful tries through <c>func</c>, passes failure through as is.
    ITry<R> FlatMap<R> (Func<T, ITry<R>> func);
  }

  /// Helper methods for ITry.
  public static class Try {

    /// Creates a successful try.
    public static ITry<T> Success<T> (T value) { return new Success<T>(value); }

    /// Creates a failed try.
    public static ITry<T> Failure<T> (Exception cause) { return new Failure<T>(cause); }

    /// Lifts <c>func</c>, a function on values, to a function on tries.
    public static Func<ITry<T>,ITry<R>> Lift<T,R> (Func<T, R> func) {
      return result => result.Map(func);
    }
  }

  /// Represents a successful try. Contains the successful result.
  internal class Success<T> : ITry<T> {
    private T _value;

    public Success (T value) {
      _value = value;
    }

    public T Value { get { return _value; } }
    public Exception Cause { get { throw new InvalidOperationException(); } }
    public bool IsSuccess { get { return true; } }
    public bool IsFailure { get { return false; } }

    public ITry<R> Map<R> (Func<T, R> func) {
      try {
        return Try.Success(func(_value));
      } catch (Exception e) {
        return Try.Failure<R>(e);
      }
    }
    // public ITry<U> Recover<U> (Func<Exception, U> func) where T : U {
    //   return this;
    // }
    public ITry<R> FlatMap<R> (Func<T, ITry<R>> func) {
      try {
        return func(_value);
      } catch (Exception t) {
        return Try.Failure<R>(t);
      }
    }

    override public string ToString () { return "Success(" + _value + ")"; }
  }

  /// Represents a failed try. Contains the cause of failure.
  internal class Failure<T> : ITry<T> {
    private Exception _cause;

    public Failure (Exception cause) {
      _cause = cause;
    }

    public T Value { get { throw _cause; } }
    public Exception Cause { get { return _cause; } }
    public bool IsSuccess { get { return false; } }
    public bool IsFailure { get { return true; } }

    public ITry<R> Map<R> (Func<T, R> func) {
      return Try.Failure<R>(_cause);
    }
    // public ITry<U> Recover<U> (Func<Exception, U> func) where T : U {
    //   try {
    //     return Try.Success(func(_cause));
    //   } catch (Exception t) {
    //     return Try.Failure<T>(t);
    //   }
    // }
    public ITry<R> FlatMap<R> (Func<T, ITry<R>> func) {
      return Try.Failure<R>(_cause);
    }

    override public string ToString () { return "Failure(" + _cause + ")"; }
  }
}
