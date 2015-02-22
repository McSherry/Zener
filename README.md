|  Build  |  Status  |
|---------|----------|
|  Linux  | [![Linux Build Status](https://travis-ci.org/McSherry/Zener.svg?branch=master)](https://travis-ci.org/McSherry/Zener)
| Windows | [![Windows Build status](https://ci.appveyor.com/api/projects/status/ywnl2go7njqeeik0?svg=true)](https://ci.appveyor.com/project/McSherry/zener) |

## What is Zener?

If your software uses an HTML renderer to present its GUI, and you're using frameworks such as the [Chromium Embedded Framework](https://code.google.com/p/chromiumembedded/) or [Awesomium](http://www.awesomium.com/), you don't want to have to deal with loading files and pushing them to the HTML renderer's view. Instead, you just want to get on with developing and making sure your software does what it's supposed to. This is where Zener comes in.

Zener is an embeddable HTTP server with supporting logic. It handles loading resources and provides a set of APIs that allows your front-end code (such as JavaScript or Dart) to communicate with your back-end logic. Zener can simplify development and means you can use the same client&harr;server mindset you would when writing web applications.

Zener is written in C# 5.0, and has been tested against .NET 4.0 and Mono 3.12.0.


