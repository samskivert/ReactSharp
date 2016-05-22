ReactSharp
==========

ReactSharp is a library that provides async and [functional reactive programming]-like primitives.
It can serve as the basis for a user interface toolkit, or any other library that has a model on
which clients will listen and to which they will react.

* [API docs](http://samskivert.github.com/ReactSharp/apidocs/) are available.

Core Concepts
-------------

ReactSharp provides three main abstractions:

* Future/Promise - asynchronous computations which eventually compute a value, along with
  mechanisms to chain and combine async computations.
* Value - a reactive value which reports to observers when it changes.
* Signal - a source of events which can be filtered, transformed, etc. Like a poor man's
  Observable, but works nicely with Value & Future.

The Java library from whence this came provides more reactive datatypes (Set, List, Map, Queue).
Perhaps ReactSharp will grow to include those as well some day.

Origins
-------

ReactSharp was ported from the [Java react] library (same author for both libraries) and made as
C-sharpy as possible given the limitated C# experience of a Java programmer forced to work in the
Unity salt mines.

Distribution
------------

ReactSharp is released under the New BSD License. The most recent version of the library is
available at http://github.com/samskivert/ReactSharp

Contact
-------

Questions, comments, and other communications should be directed to the [OOO Libraries] Google
Group.

[functional reactive programming]: http://en.wikipedia.org/wiki/Functional_reactive_programming
[Java react]: https://github.com/threerings/react
[OOO Libraries]: http://groups.google.com/group/ooo-libs
