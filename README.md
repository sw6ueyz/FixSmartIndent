
# What is this ?

In javascript/typescript editor, if you disable auto formatting and enable smart indent, closing brace ('}') does not correctly dedents.

See :

https://developercommunity.visualstudio.com/t/closing-brace-does-not-dedent-in-javascr/1110276

It looks like they decided not to fix the problem even in VS2022, so I made a simple patch myself.

This program was created for personal use, but I am sharing it because there may be others who are annoyed by the same problem.


# How to use ?

This program just fixes the following problem and does not have any settings.

If you disable auto formatting and enable smart indent,

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

This program only triggers only if you press '}' and its matching '{' appears end of a previous line.

It also recognizes nested parentheses or string literals :

~~~
    if ( true &&
        "})\"" ) {
    }
//  ^                   pressing '}' dedents to here
~~~
