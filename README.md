# TransactTcp
TransactTcp is a .NET standard library that implements asynchronous thread-safe TCP communication for highly concurrent environments

[![Build status](https://ci.appveyor.com/api/projects/status/hifsb3m5ifxadwn1?svg=true)](https://ci.appveyor.com/project/adospace/transact-tcp) [![Nuget](https://img.shields.io/nuget/v/TransactTcp.svg)](https://www.nuget.org/packages/TransactTcp)

## Features
- Support of Server<->Client and Server<->MultipleClients connections
- Integrated auto reconnection, keep alive management and message framing
- SSL/TSL support
- Great perfomance without locks (thanks to ServiceActor https://github.com/adospace/ServiceActor) 
- Support for `Memory<byte>` struct
- Rock solid implementation with extensive tests
- Support for redundant balanced communication
- Minimal API

## Get started

```c#

```
