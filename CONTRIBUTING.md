# Contributing

Contributions to Zener are welcomed. This file provides the information you'll need to know for contributing.

1. [Style Guide](#style-guide)
    1. [Line Width](#line-width)
    2. [Indentation](#indentation)
2. [Figures](#figures)
3. [Contributor's Licence Agreement](#licence-agreement)

## Style Guide

In order to maintain code readability, any code contributions should follow this style guide. If this guide is not followed, contributions may be rejected.

- **Line widths:**. Lines should be around 80 columns in length, and should be no longer than 100 columns.
- **Indentation:** Indentation should be in the form of groups of four spaces. Tabs or lesser quantities of spaces should not be used.
- **Capitalisation:**
    - **Abbreviations:** Abbreviations be `PascalCased` (`Http`, `Tcp`, `IpAddress`).
    - **Non-Private:** Non-private members should use `PascalCase`.
    - **Private:** Private members should use `camelCase` with a prefixing underscore (`_someMember`).
    - **Method Arguments:** Method arguments should use `camelCase`.
    - **Private/Protected Constants:** Constants marked `private` or `protected` should be uppercase with underscores separating words (`CONST_VAL`).
- **Brace Style:** Braces should, generally, be on their own lines.
    - **Braced If/Else:** The braces and the keywords `if` and `else` should be on their own lines ([fig.1](#figure-1)).
    - **Braceless If/Else:** `if`/`else` statements without braces are permitted where the braces would add little to readability. The body must be on the same line as the `if`/`else` statement ([fig.2](#figure-2)).

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

## Licence Agreement

Before contributing, you must read and accept the below terms. If you do not accept these terms, you cannot contribute to Zener.

> You retain ownership of the copyright of Your contributions, and retain all rights to use and license Your contributions as you would otherwise have had without entering this agreement (the "Agreement").
>
> To the greatest extent of the law, You the Contributor grant SynapLink, LLC ("SynapLink") a perpetual, worldwide, non-exclusive, transferable, royalty-free, irrevocable copyright licence covering any and all of Your contributions to works developed and/or authored, wholly or in part, by SynapLink, with the right to sublicense such rights through multiple tiers of sublicensees, to reproduce, modify, display, perform, and to distribute the Your contributions.
>
> For all relevant patents for which You have the right to license, You grant to SynapLink a perpetual, worldwide, non-exclusive, transferable, royalty-free, irrevocable patent licence, with the right to sublicense these rights to multiple tiers of sublicensees, to make, have made, use, sell, offer for sale, import and otherwise transfer Your contributions.
>
> You permit SynapLink to include Your contributions in any other items developed and/or authored, wholly or in part, by SynapLink, and permit SynapLink to include Your contributions under any licence including, but not limited to, commercial, permissive, and copyleft licences.
>
> By entering in to the Agreement, you confirm that you: **(a)** have the legal authority to enter in to the Agreement **(b)** have the authority to license your contributions to SynapLink **(c)** your granting a licence to SynapLink does not infringe on the terms of an agreement between You and a third party (such as Your employer) **(d)** if your granting a licence would infringe on the terms of an agreement between You and a third party, you have consulted the third party and they have accepted the terms of the Agreement **(e)** if you are under the age of 18 years, you have had your parent and/or legal guardian read and accept the terms of the Agreement **(f)** have acknowledged and are aware that SynapLink is in no way obligated to include Your contributions in any items developed and/or authored, wholly or in part, by SynapLink.
