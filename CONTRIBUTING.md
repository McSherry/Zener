# Contributing

Contributions to Zener are welcomed. This file provides the information you'll need to know for contributing.

1. [Style Guide](#style-guide)
2. [Figures](#figures)
3. [Contributor's Licence Agreement](#licence-agreement)

## Style Guide

In order to maintain code readability, any code contributions should follow this style guide. If this guide is not followed, contributions may be rejected.

- **Line widths:**. Lines should be around 80 columns in length, and should be no longer than 100 columns.
- **Spaces v. Tabs:** Indentation should be in the form of groups of four spaces. Tabs or lesser quantities of spaces should not be used.
- **Capitalisation:**
    - **Abbreviations:** Abbreviations be `PascalCased` (`Http`, `Tcp`, `IpAddress`).
    - **Non-Private:** Non-private members should use `PascalCase`.
    - **Private:** Private members should use `camelCase` with a prefixing underscore (`_someMember`).
    - **Method Arguments:** Method arguments should use `camelCase`.
    - **Private/Protected Constants:** Constants marked `private` or `protected` should be uppercase with underscores separating words (`CONST_VAL`).
- **Indentation/Bracing Style:**
    - **If/Else:**
        - **Braced If/Else:** The braces and the keywords `if` and `else` should be on their own lines ([fig.1](#figure-1)).
        - **Braceless If/Else:** `if`/`else` statements without braces are permitted where the braces would add little to readability. The body must be on the same line as the `if`/`else` statement ([fig.2](#figure-2)).
        - **Ternary:** The ternary operator should be used sparingly. Where it is used, aim to keep it on a single line (`a ? b : c`). If keeping it on a single line would detract from readability, use the style in [fig.3](#figure-3). Nested ternary statements should not be used.
    - **While:** `while` statements should follow the same style as `if`/`else` in [fig.1](#figure-1).
    - **Do-While:** The braces and `do` and `while` (with condition) should each be on separate lines ([fig.4](#figure-4)).
    - **Methods:**
        - **Definition:** Method names, the method's modifiers, and its arguments should be on the same line where possible ([fig.5](#figure-5)). Where this line would exceed the line width limits, the style in [fig.6](#figure-6) should be followed.
        - **Calling:** Calling methods follows the same style as definition ([fig.5](#figure-5), [fig.6](#figure-6)). The only difference is that there is no body with a call. If a function is called so that its return value is used as an argument, the closing brackets should be grouped where possible ([fig.7](#figure-7)).
        - **Chaining:** Chained methods should be on their own lines ([fig.8](#figure-8)).
        - **Lambdas:**
            - **LINQ:** Lambdas in LINQ method calls should be short and simple. Aim to write code which does not require braces (`enumerable.Where(i => i != null && i.SomeBooleanProperty)`). If the lambda takes only a single argument, the argument name should not be enclosed in brackets.
            - **Otherwise:** Lambdas in other situations should follow the style given in [fig.9](#figure-9).
    - **Class/Struct/etc Member Order:** Classes/etc should follow roughly the same order so that any developer can easily find a member of the class. This order is as follows (from top to bottom).
        1. Private constants.
        2. Private statics.
        3. Static constructor.
        4. Public statics.
        5. Private instances.
        6. Constructor.
        7. Public instances.
        8. Explicitly-declared interface methods.
    - **Classes/Structs:** The class name with its modifiers should be on its own line, with the opening brace on the next line. If the class inherits, the colon and the name of the base class and any interfaces should be placed on the line after the class name. The opening brace for the class should then be on the next line after that ([fig.10](#figure-10)).
    - **Type Constraints:** Type constraints follow roughly the same style as shown for inheriting in [fig.10](#figure-10), with the `where` keyword on the same line as the colon and types.
- **Documentation:** All non-private members/classes/structs should have [XML documentation comments](https://msdn.microsoft.com/en-us/library/vstudio/b2s063f7%28v=vs.100%29.aspx). The required XML comment tags are `<summary>`, `<param>`, `<exception>`, and `<returns>` (where applicable). Code should also be amply commented using normal comments (`//`-prefixed lines or `/**/` blocks).

## Figures

Referenced figures.

### Figure #1

Braced if/else statements.
```c#
if (condition)
{
    doTheThing();
}
else
{
    doTheOtherThing();
}
```

### Figure #2

Braceless if/else.

```c#
int abs(int n)
{
    if (n < 0) return -n;
    else return n;
}
```

### Figure #3

Multi-line ternary statements.

```c#
int abs(int n)
{
    return n < 0
        ? -n
        : n
        ;
}
```

### Figure #4

Do-while.

```c#
do
{
    doTheThing();
}
while (condition);
```

### Figure #5

Single-line method definition.

```c#
public static void DoTheThing(int a, string b)
{
    // body
}
```

### Figure #6

Multi-line method definition.

```c#
protected internal static void ThisIsALongMethodName<T>(
    int andItHas, Dictionary<string, object> quiteAFew,
    bool argumentsWith, IOrderedEnumerable<T> longTypesAndNames
    )
{
    // Body
}
```

### Figure #7

Nested call bracket grouping.

```c#
// Nested call is not at the end, no grouping.
MethodAcceptingStringAndInt(
    String.Format("{0}, {1}", a, b)
    Int32.MaxValue
    );
    
// Nested call is at the end, group brackets.
MethodAcceptingIntAndString(
    Int32.MaxValue,
    String.Format(
        "{0}, {1]", a, b
    ));
```

### Figure 8

Method chaining.

```c#
// Standard chaining
var result = enumerable
    .Where(i => condition)
    .Select(i => i.Property)
    .OrderBy(p => p > 0)
    .ToList();

// Chaining with a multi-line method call
string[] fileNames = new string[] { ... };
var commaList = fileNames
    .Aggregate(
        new StringBuilder(),
        (sb, fn) => sb.AppendFormat("{0}, " fn.ToLower())
        )
    .ToString()
    .TrimEnd(' ', ',');
```

### Figure 9

Lambdas.

```c#
var thread = new Thread(o => {
    // Body
});
```

### Figure 10

Base classes, interfaces

```c#
public sealed class Cat
    : Animal, IPettable
{
    // Class definition
}
```

## Licence Agreement

Before contributing, you must read and accept the below terms. If you do not accept these terms, you cannot contribute to Zener.

> You retain ownership of the copyright of Your contributions, and retain all rights to use and license Your contributions as you would otherwise have had without entering this agreement (the "Agreement").
>
> To the greatest extent of the law, You grant SynapLink, LLC ("SynapLink") a perpetual, worldwide, non-exclusive, transferable, royalty-free, irrevocable copyright licence covering any and all of Your contributions to works developed and/or headed by SynapLink, with the right to sublicense such rights through multiple tiers of sublicensees, to reproduce, modify, display, perform, and to distribute Your contributions.
>
> For all relevant patents for which You have the right to license, You grant to SynapLink a perpetual, worldwide, non-exclusive, transferable, royalty-free, irrevocable patent licence, with the right to sublicense these rights to multiple tiers of sublicensees, to make, have made, use, sell, offer for sale, import, and otherwise transfer Your contributions.
>
> You permit SynapLink to include Your contributions in any other works developed and/or authored, wholly or in part, by SynapLink, and permit SynapLink to include Your contributions under any licence including, but not limited to, commercial, permissive, and copyleft licences.
>
> By entering in to the Agreement, You confirm that You: **(a)** have the legal authority to enter in to the Agreement **(b)** have the authority to license your contributions to SynapLink **(c)** Your granting a licence to SynapLink does not infringe on the terms of an agreement between You and a third party (such as Your employer) **(d)** if Your granting a licence would infringe on the terms of an agreement between You and a third party, You have consulted the third party and they have accepted the terms of the Agreement **(e)** if You are under the age of 18 years, You have had Your parent and/or legal guardian read and accept the terms of the Agreement **(f)** have acknowledged and are aware that SynapLink is in no way obligated to include Your contributions in any workss developed and/or authored, wholly or in part, by SynapLink.
