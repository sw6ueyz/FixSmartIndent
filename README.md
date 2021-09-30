
# What is this ?

In javascript/typescript editor, if you disable auto formatting and enable smart indent, braces ({}) does not correctly dedents.

See :

https://developercommunity.visualstudio.com/t/closing-brace-does-not-dedent-in-javascr/1110276

It looks like they decided not to fix the problem even in VS2022, so I made a simple patch myself.

This program was created for personal use, but I am sharing it because there may be others who are annoyed by the same problem.


# How to use ?

This program just fixes the following problem and does not have any settings.

When you disable auto formatting and enable smart indent in IDE editor,

before fix :

~~~
if ( true ) {           // line ends with '{'.
    someCode();
    }                   // pressing '}' does not dedent properly.
~~~

after fix :

~~~
if ( true ) {
    someCode();
}                       // pressing '}' now dedents.
~~~

This triggers only if you press '}' and its matching '{' appears at the end of one of the previous lines.

It also recognizes nested parentheses or string literals :

~~~
    if ( true &&
        "})\"" ) {
    }
//  ^                   pressing '}' dedents to here
~~~

In version 1.1, following fix is added :

~~~
class Test
    {              // pressing '{' does not dedent properly.
~~~

after fix :

~~~
class Test
{                  // pressing '}' now dedents.
~~~


# Notice

Please notify me if they fix the bug in Visual Studio. I'll happly remove this program from here.

source code :

https://github.com/sw6ueyz/FixSmartIndent
