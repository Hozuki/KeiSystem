﻿#region GPLv2

/*
KeiSystem
Copyright (C) 2015 MIC Studio
Developer homepage: https://github.com/GridSciense

This program is free software; you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation; either version 2 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License along
with this program; if not, write to the Free Software Foundation, Inc.,
51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.
*/

/*
This file is based on HttpServer in SimpleHttpServer developed by David Jeske.
*/

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;

namespace Kei.KTracker
{
    /// <summary>
    /// 表示一个简单的 HTTP 服务器类。此类必须被继承。
    /// </summary>
    public abstract class HttpServer
    {

        /// <summary>
        /// 为该服务器端口所绑定的 <see cref="System.Net.TcpListener"/>。
        /// </summary>
        protected TcpListener _listener;

        /// <summary>
        /// 设置一个 <see cref="System.Boolean"/>，指定服务器是否应该继续运行。
        /// <para>注意，由于 TcpListener.AcceptTcpClient() 是一个同步等待方法，因此即使更新了该值，在服务器收到下次请求并新建处理实例前状态仍然不会更新。</para>
        /// </summary>
        protected bool _isActive = true;

        /// <summary>
        /// 运行的主线程。
        /// </summary>
        protected Thread _thread = null;

        /// <summary>
        /// 本 HTTP 服务器使用的 <see cref="Kei.ILogger"/>。
        /// </summary>
        private ILogger _logger;

        /// <summary>
        /// 本 HTTP 服务器的端点，地址是本机局域网地址（不是环回地址），端口是监听端口。
        /// </summary>
        private IPEndPoint _localEndPoint;

        /// <summary>
        /// 新建一个 <see cref="Kei.KTracker.HttpServer"/> 并将端点设置为一个指定端点。
        /// </summary>
        /// <param name="localEndPoint">指定的端点。</param>
        protected HttpServer(IPEndPoint localEndPoint)
        {
            _localEndPoint = localEndPoint;
            _listener = new TcpListener(new IPEndPoint(IPAddress.Loopback, _localEndPoint.Port));
        }

        /// <summary>
        /// 获取本 HTTP 服务器的端点，地址是本机局域网地址（不是环回地址），端口是监听端口。此属性为只读。
        /// </summary>
        public IPEndPoint LocalEndPoint
        {
            get
            {
                return _localEndPoint;
            }
        }

        /// <summary>
        /// 设置本 HTTP 服务器的端点。
        /// </summary>
        /// <param name="ep">要设置为的端点。</param>
        /// <exception cref="System.ArgumentNullException">ep 为 null 时发生。</exception>
        public void SetLocalEndPoint(IPEndPoint ep)
        {
            if (ep == null)
            {
                throw new ArgumentNullException("ep");
            }
            _localEndPoint = ep;
        }

        /// <summary>
        /// 获取或设置本 HTTP 服务器使用的 <see cref="Kei.ILogger"/>。
        /// </summary>
        public ILogger Logger
        {
            get
            {
                if (_logger == null)
                {
                    _logger = Kei.Logger.Null;
                }
                return _logger;
            }
            set
            {
                _logger = value == null ? Kei.Logger.Null : value;
            }
        }

        /// <summary>
        /// 尝试将该 <see cref="Kei.KTracker.HttpServer"/> 和一个端点绑定，用来测试该端点的可用性。
        /// </summary>
        /// <param name="endPoint">要绑定的 <see cref="System.Net.IPEndPoint"/>。</param>
        /// <returns>返回 true 表示端点可用，返回 false 表示端点不可用或者 socket 已经与一个 <see cref="System.Net.IPEndPoint"/> 绑定。</returns>
        [Obsolete("此方法存在错误，不要使用。")]
        private bool TestBind(IPEndPoint endPoint)
        {
            if (IsBound)
            {
                return false;
            }
            try
            {
                _listener.Server.Bind(endPoint);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 获取本 <see cref="Kei.KTracker.HttpServer"/> 是否已经与一个端点绑定。此属性为只读。
        /// </summary>
        [Obsolete("此属性存在错误，不要使用。")]
        private bool IsBound
        {
            get
            {
                return _listener.Server.IsBound;
            }
        }

        /// <summary>
        /// 将本 <see cref="Kei.KTracker.HttpServer"/> 绑定到设置的端口，并开始监听端口通信。
        /// </summary>
        /// <returns>一个 <see cref="System.Boolean"/>，表示启动是否成功。</returns>
        public bool Listen()
        {
            if (_thread == null)
            {
                Logger.Log("[HTTP]启动服务器。");
                try
                {
                    _listener.Stop();
                }
                catch (Exception ex)
                {
                    Logger.Log("[HTTP]先停止 TcpListener 时发生异常: " + ex.Message);
                }
                try
                {
                    _listener.Start();
                }
                catch (Exception ex)
                {
                    Logger.Log("[HTTP]启动时发生异常: " + ex.Message);
                    return false;
                }
                _thread = new Thread(delegate()
                {
                    while (_isActive)
                    {
                        Logger.Log("[HTTP]尝试接受连接。");
                        IAsyncResult iar;
                        iar = _listener.BeginAcceptTcpClient(null, null);
                        while (!(iar.CompletedSynchronously || iar.IsCompleted) && _isActive)
                        {
                            iar.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(1));
                        }
                        if (iar.IsCompleted && _isActive)
                        {
                            Logger.Log("[HTTP]收到连接。");
                            TcpClient s = _listener.EndAcceptTcpClient(iar);
                            var remoteAddress = ((IPEndPoint)s.Client.RemoteEndPoint).Address;
                            if (IPAddress.IsLoopback(remoteAddress) || remoteAddress.Equals(_localEndPoint.Address))
                            {
                                // 只处理本机发来的请求
                                HttpProcessor processor = new HttpProcessor(s, this);
                                Thread thread = new Thread(processor.Process);
                                thread.IsBackground = true;
                                thread.Start();
                                Thread.Sleep(1);
                            }
                            else
                            {
                                // 拒绝处理
                                Logger.Log("[HTTP]远程请求 (" + s.Client.RemoteEndPoint.ToString() + ")，拒绝处理。");
                                s.Close();
                            }
                        }
                    }

                    _thread = null;
                });
                _thread.IsBackground = true;
                _thread.Start();
            }
            return true;
        }

        /// <summary>
        /// 尝试关闭本 <see cref="Kei.KTracker.HttpServer"/>。
        /// </summary>
        public void Shutdown()
        {
            if (_thread != null)
            {
                try
                {
                    _isActive = false;
                    _listener.Stop();
                }
                catch (Exception ex)
                {
                    Logger.Log("[HTTP]停止时发生异常: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// 在子类中重写时，负责处理 GET 请求。
        /// <para>注意，这是一个回调方法。</para>
        /// </summary>
        /// <param name="processor">为该请求新建的 <see cref="Kei.KTracker.HttpProcessor"/>。</param>
        /// <param name="ioStream"><see cref="Kei.KTracker.HttpProcessor"/> 内为此请求新建的 <see cref="System.IO.Stream"/>。</param>
        internal abstract void HandleGetRequest(HttpProcessor processor, Stream ioStream);

        /// <summary>
        /// 在子类中重写时，负责处理 POST 请求。
        /// <para>注意，这是一个回调方法。</para>
        /// </summary>
        /// <param name="processor">为该请求新建的 <see cref="Kei.KTracker.HttpProcessor"/>。</param>
        /// <param name="ioStream"><see cref="Kei.KTracker.HttpProcessor"/> 内为此请求新建的 <see cref="System.IO.Stream"/>。</param>
        internal abstract void HandlePostRequest(HttpProcessor processor, Stream ioStream);
    }
}
