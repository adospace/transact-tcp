# TransactTcp
TransactTcp is a .NET standard library that implements asynchronous thread-safe TCP communication for highly concurrent environments

[![Build status](https://ci.appveyor.com/api/projects/status/hifsb3m5ifxadwn1?svg=true)](https://ci.appveyor.com/project/adospace/transact-tcp) [![Nuget](https://img.shields.io/nuget/v/TransactTcp.svg)](https://www.nuget.org/packages/TransactTcp)

## Features
- Support of Server <-> Client and Server <-> Multiple Clients connections
- Integrated auto reconnection, keep alive management and message framing
- SSL/TSL support
- Great perfomance without locks (thanks to ServiceActor https://github.com/adospace/ServiceActor) 
- Support for `Memory<byte>` struct
- Rock solid implementation with extensive tests
- Support for redundant balanced communication
- Named pipe support

## Get started

```c#
Install-Package TransactTcp
```

### Point to point connections i.e. server accepting only one client connection

Create a TCP server bound to a port:
```c#
var server = TcpConnectionFactory.CreateServer(15000);
```

Create a TCP client that connect to the server:
```c#
var client = TcpConnectionFactory.CreateClient(IPAddress.Loopback, 15000);
```

Starts the server:
```c#
server.Start(connectionStateChangedAction: (connection, fromState, toState) =>
    {
        Debug.WriteLine($"Server state changed to {toState}");
    },
    receivedAction: (c, data) => 
    {
        Debug.WriteLine($"Server received {data.Length} bytes");
    });
```
and the client:
```c#
client.Start(connectionStateChangedAction: (connection, fromState, toState) =>
    {
        Debug.WriteLine($"Client state changed to {toState}");
    },
    receivedAction: (c, data) => 
    {
        Debug.WriteLine($"Client received {data.Length} bytes");
    });
```

To send data to other peer:
```c#
await server(or client).SendDataAsync(data);
```

Some notes:

- There is no need to start the server before the client, client will just re-connect automatically.
- Stopping and restarting the server causes the client to reconnect as well
- In the most simple scenario, peers send and receive arrays of bytes like the above example: received actions are called after all bytes are received i.e. message framing is enabled by default.


### Server accepting multiple client connections
Create a TcpListener that accept connections like this:
```c#
var serverListener = TcpConnectionFactory.CreateMultiPeerServer(14000);
```
Starts the listener and provide an action to handle the new connection from client
```c#
serverListener.Start((listener, newConnection) =>
{
    newConnection.Start(
        connectionStateChangedAction: (c, fromState, toState) => 
        {
            Debug.WriteLine($"Server connection state changed to {toState}");
        },
        receivedAction: (c, data) =>
        {
            Debug.WriteLine($"Server connection received {data.Length} bytes");
        });
});
```
Client starts just like the first example.

Some notes:

- Listener accepts connections from clients but it doesn't maintain a reference to them: it's up to your code how manage them after creation (you can even stop a connection after is created!).

