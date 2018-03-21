//
// ReactSharp - a library for async & FRP-ish programming in C#
// http://github.com/samskivert/ReactSharp/blob/master/LICENSE

using System;

namespace React {

  /// An empty value that represents void
  public struct Unit : IEquatable<Unit> {
    public static Unit Default { get; } = new Unit();

    public bool Equals (Unit other) {
      return true;
    }

    public override bool Equals (object obj) {
      return obj is Unit;
    }

    public override int GetHashCode () {
      return 0;
    }

    public override string ToString () {
      return "()";
    }

    public static bool operator == (Unit a, Unit b) {
      return true;
    }

    public static bool operator != (Unit a, Unit b) {
      return false;
    }
  }

}
