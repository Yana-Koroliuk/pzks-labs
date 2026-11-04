namespace Pzks.Core.Ast;

/// <summary>Вузол дерева арифметичного виразу.</summary>
public abstract record ExprNode;

public sealed record NumberNode(string Text, double Value) : ExprNode;

public sealed record VariableNode(string Name) : ExprNode;

/// <summary>Унарна операція: '+' або '-'.</summary>
public sealed record UnaryNode(char Operator, ExprNode Operand) : ExprNode;

/// <summary>Бінарна операція: '+', '-', '*' або '/'.</summary>
public sealed record BinaryNode(char Operator, ExprNode Left, ExprNode Right) : ExprNode;

public sealed record FunctionNode(string Name, IReadOnlyList<ExprNode> Arguments) : ExprNode;
