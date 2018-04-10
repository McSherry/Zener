|  Build  |  Status  |
|---------|----------|
|  Linux  | [![Linux Build Status](https://travis-ci.org/McSherry/Zener.svg?branch=develop)](https://travis-ci.org/McSherry/Zener)
| Windows | [![Windows Build status](https://ci.appveyor.com/api/projects/status/ywnl2go7njqeeik0/branch/develop?svg=true)](https://ci.appveyor.com/project/McSherry/zener) |

## What is Zener?

If your software uses an HTML renderer to present its GUI, and you're using frameworks such as the [Chromium Embedded Framework](https://code.google.com/p/chromiumembedded/) or [Awesomium](http://www.awesomium.com/), you don't want to have to deal with loading files and pushing them to the HTML renderer's view. Instead, you just want to get on with developing and making sure your software does what it's supposed to. This is where Zener comes in.

Zener is an embeddable HTTP server with supporting logic. It handles loading resources and provides a set of APIs that allows your front-end code (such as JavaScript or Dart) to communicate with your back-end logic. Zener can simplify development and means you can use the same client&harr;server mindset you would when writing web applications.

As well as being great for software using an HTML GUI, Zener is also a perfectly capable general-purpose web server.

Zener is written in C# 5.0, and has been tested against .NET 4 Client Profile and Mono 3.12.0. It is released under the BSD 3-Clause licence. Zener will also build under .NET Standard 2.0.

## Performance

Along with its large array of features, Zener also offers competitive performance. The below figures were measured using Zener version `0.11.0` on a Ubuntu 14.04 64-bit machine running Mono 3.2.8.  Both the sender and receiver machines have two Intel Xeon L5639 processors, and have 1GB and 2GB DDR3-1066 ECC, respectively.

```
root@testbox-1:~/Zener/bin/Release# ab -n 10000 -c 10 http://testbox-2:8080/
This is ApacheBench, Version 2.3 <$Revision: 1528965 $>
Copyright 1996 Adam Twiss, Zeus Technology Ltd, http://www.zeustech.net/
Licensed to The Apache Software Foundation, http://www.apache.org/

Benchmarking testbox-2 (be patient)
Completed 1000 requests
Completed 2000 requests
Completed 3000 requests
Completed 4000 requests
Completed 5000 requests
Completed 6000 requests
Completed 7000 requests
Completed 8000 requests
Completed 9000 requests
Completed 10000 requests
Finished 10000 requests


Server Software:        Zener/0.11.0
Server Hostname:        testbox-2
Server Port:            8080

Document Path:          /
Document Length:        28 bytes

Concurrency Level:      10
Time taken for tests:   1.605 seconds
Complete requests:      10000
Failed requests:        0
Total transferred:      1580000 bytes
HTML transferred:       280000 bytes
Requests per second:    6230.61 [#/sec] (mean)
Time per request:       1.605 [ms] (mean)
Time per request:       0.160 [ms] (mean, across all concurrent requests)
Transfer rate:          961.36 [Kbytes/sec] received

Connection Times (ms)
              min  mean[+/-sd] median   max
Connect:        0    0   0.2      0       4
Processing:     0    1   2.9      1     203
Waiting:        0    1   2.9      1     203
Total:          1    2   2.9      1     203

Percentage of the requests served within a certain time (ms)
  50%      1
  66%      2
  75%      2
  80%      2
  90%      2
  95%      3
  98%      3
  99%      4
 100%    203 (longest request)
```

The server was using the below code.

```c#
using McSherry.Zener;

using System;
using System.Net;

namespace ZenerTest
{
        class Program
        {
                static void Main()
                {
                        var ctx = new ZenerContext(
                                defaultAddress: IPAddress.Any,
                                defaultPort:    8080
                                );

                        var zc = new ZenerCore(ctx);

                        zc.DefaultHost.Routes.AddHandler(
                                "/",
                                (request, response, parameters) =>
                                {
                                        response.Write("Hello, World!");
                                });
                }
        }
}

```
