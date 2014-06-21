﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNet.Server.Kestrel;
using Microsoft.AspNet.Server.Kestrel.Networking;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.Infrastructure;
using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.AspNet.Server.KestralTests
{
    /// <summary>
    /// Summary description for NetworkingTests
    /// </summary>
    public class NetworkingTests
    {
        Libuv _uv;
        public NetworkingTests()
        {
            var engine = new KestrelEngine(LibraryManager);
            _uv = engine.Libuv;
        }

        ILibraryManager LibraryManager
        {
            get
            {
                var services = CallContextServiceLocator.Locator.ServiceProvider;
                return (ILibraryManager)services.GetService(typeof(ILibraryManager));
            }
        }

        [Fact]
        public async Task LoopCanBeInitAndClose()
        {
            var loop = new UvLoopHandle();
            loop.Init(_uv);
            loop.Run();
            loop.Dispose();
        }

        [Fact]
        public async Task AsyncCanBeSent()
        {
            var loop = new UvLoopHandle();
            loop.Init(_uv);
            var trigger = new UvAsyncHandle();
            var called = false;
            trigger.Init(loop, () =>
            {
                called = true;
                trigger.Dispose();
            });
            trigger.Send();
            loop.Run();
            loop.Dispose();
            Assert.True(called);
        }

        [Fact]
        public async Task SocketCanBeInitAndClose()
        {
            var loop = new UvLoopHandle();
            loop.Init(_uv);
            var tcp = new UvTcpHandle();
            tcp.Init(loop);
            tcp.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            tcp.Dispose();
            loop.Run();
            loop.Dispose();
        }


        [Fact]
        public async Task SocketCanListenAndAccept()
        {
            var loop = new UvLoopHandle();
            loop.Init(_uv);
            var tcp = new UvTcpHandle();
            tcp.Init(loop);
            tcp.Bind(new IPEndPoint(IPAddress.Loopback, 54321));
            tcp.Listen(10, (stream, status, state) =>
            {
                var tcp2 = new UvTcpHandle();
                tcp2.Init(loop);
                stream.Accept(tcp2);
                tcp2.Dispose();
                stream.Dispose();
            }, null);
            var t = Task.Run(async () =>
            {
                var socket = new Socket(
                    AddressFamily.InterNetwork,
                    SocketType.Stream,
                    ProtocolType.Tcp);
                await Task.Factory.FromAsync(
                    socket.BeginConnect,
                    socket.EndConnect,
                    new IPEndPoint(IPAddress.Loopback, 54321),
                    null,
                    TaskCreationOptions.None);
                socket.Dispose();
            });
            loop.Run();
            loop.Dispose();
            await t;
        }


        [Fact]
        public async Task SocketCanRead()
        {
            int bytesRead = 0;
            var loop = new UvLoopHandle();
            loop.Init(_uv);
            var tcp = new UvTcpHandle();
            tcp.Init(loop);
            tcp.Bind(new IPEndPoint(IPAddress.Loopback, 54321));
            tcp.Listen(10, (_, status, state) =>
            {
                var tcp2 = new UvTcpHandle();
                tcp2.Init(loop);
                tcp.Accept(tcp2);
                var data = Marshal.AllocCoTaskMem(500);
                tcp2.ReadStart(
                    (a, b, c) => new Libuv.uv_buf_t { memory = data, len = 500 },
                    (__, nread, state2) =>
                    {
                        bytesRead += nread;
                        if (nread == 0)
                        {
                            tcp2.Dispose();
                        }
                    },
                    null);
                tcp.Dispose();
            }, null);
            var t = Task.Run(async () =>
            {
                var socket = new Socket(
                    AddressFamily.InterNetwork,
                    SocketType.Stream,
                    ProtocolType.Tcp);
                await Task.Factory.FromAsync(
                    socket.BeginConnect,
                    socket.EndConnect,
                    new IPEndPoint(IPAddress.Loopback, 54321),
                    null,
                    TaskCreationOptions.None);
                await Task.Factory.FromAsync(
                    socket.BeginSend,
                    socket.EndSend,
                    new[] { new ArraySegment<byte>(new byte[] { 1, 2, 3, 4, 5 }) },
                    SocketFlags.None,
                    null,
                    TaskCreationOptions.None);
                socket.Dispose();
            });
            loop.Run();
            loop.Dispose();
            await t;
        }
    }
}