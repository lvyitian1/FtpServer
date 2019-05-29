// <copyright file="SecureConnectionAdapter.cs" company="Fubar Development Junker">
// Copyright (c) Fubar Development Junker. All rights reserved.
// </copyright>

using System;
using System.IO.Pipelines;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

using FubarDev.FtpServer.Authentication;

using JetBrains.Annotations;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FubarDev.FtpServer.ConnectionHandlers
{
    /// <summary>
    /// A connection adapter that allows enabling and resetting of an SSL/TLS connection.
    /// </summary>
    internal class SecureConnectionAdapter : IFtpSecureConnectionAdapter
    {
        [NotNull]
        private readonly IDuplexPipe _socketPipe;

        [NotNull]
        private readonly IDuplexPipe _connectionPipe;

        [NotNull]
        private readonly ISslStreamWrapperFactory _sslStreamWrapperFactory;

        [NotNull]
        private readonly IServiceProvider _serviceProvider;

        private readonly CancellationToken _connectionClosed;

        [CanBeNull]
        private readonly ILoggerFactory _loggerFactory;

        [NotNull]
        private IFtpConnectionAdapter _activeCommunicationService;

        /// <summary>
        /// Initializes a new instance of the <see cref="SecureConnectionAdapter"/> class.
        /// </summary>
        /// <param name="socketPipe">The pipe from the socket.</param>
        /// <param name="connectionPipe">The pipe to the connection object.</param>
        /// <param name="sslStreamWrapperFactory">The SSL stream wrapper factory.</param>
        /// <param name="serviceProvider">The service provider.</param>
        /// <param name="connectionClosed">The cancellation token for a closed connection.</param>
        public SecureConnectionAdapter(
            [NotNull] IDuplexPipe socketPipe,
            [NotNull] IDuplexPipe connectionPipe,
            [NotNull] ISslStreamWrapperFactory sslStreamWrapperFactory,
            [NotNull] IServiceProvider serviceProvider,
            CancellationToken connectionClosed)
        {
            _socketPipe = socketPipe;
            _connectionPipe = connectionPipe;
            _sslStreamWrapperFactory = sslStreamWrapperFactory;
            _serviceProvider = serviceProvider;
            _connectionClosed = connectionClosed;
            _loggerFactory = serviceProvider.GetService<ILoggerFactory>();
            _activeCommunicationService = new PassThroughConnectionAdapter(
                socketPipe,
                connectionPipe,
                connectionClosed,
                _loggerFactory);
        }

        /// <inheritdoc />
        public IFtpService Sender => _activeCommunicationService.Sender;

        /// <inheritdoc />
        public IPausableFtpService Receiver => _activeCommunicationService.Receiver;

        /// <inheritdoc />
        public async Task ResetAsync(CancellationToken cancellationToken)
        {
            await StopAsync(cancellationToken)
               .ConfigureAwait(false);
            _activeCommunicationService = new PassThroughConnectionAdapter(
                _socketPipe,
                _connectionPipe,
                _connectionClosed,
                _loggerFactory);
            await StartAsync(cancellationToken)
               .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task EnableSslStreamAsync(X509Certificate2 certificate, CancellationToken cancellationToken)
        {
            await StopAsync(cancellationToken)
               .ConfigureAwait(false);
            try
            {
                _activeCommunicationService = new SslStreamConnectionAdapter(
                    _socketPipe,
                    _connectionPipe,
                    _serviceProvider,
                    _sslStreamWrapperFactory,
                    certificate,
                    _connectionClosed,
                    _loggerFactory);
            }
            catch
            {
                _activeCommunicationService = new PassThroughConnectionAdapter(
                    _socketPipe,
                    _connectionPipe,
                    _connectionClosed,
                    _loggerFactory);
                throw;
            }
            finally
            {
                await StartAsync(cancellationToken)
                   .ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken)
        {
            return _activeCommunicationService.StartAsync(cancellationToken);
        }

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken)
        {
            return _activeCommunicationService.StopAsync(cancellationToken);
        }
    }
}