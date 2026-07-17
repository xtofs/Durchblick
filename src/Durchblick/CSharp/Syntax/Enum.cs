namespace Durchblick.CSharp.Syntax;


public enum UnaryOperator { Negate, Not, Increment, Decrement }
public enum BinaryOperator { Add, Subtract, Multiply, Divide, And, Or, Equals, NotEquals, Less, Greater }
public enum RelationalOperator { Less, LessOrEqual, Greater, GreaterOrEqual }
public enum LogicalOperator { And, Or }
public enum TypeKind { Class, Struct, Interface, Enum, Record }
public enum MemberKind { Method, Property, Field, Event, Constructor }
public enum ModifierKind { Public, Private, Protected, Internal, Static, ReadOnly, Async, Unsafe }
public enum SymbolKind { Local, Parameter, Field, Property, Method, Type }
public enum PatternKind { Type, Constant, Relational, Logical, Recursive }
