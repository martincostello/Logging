// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions.Internal;

namespace Microsoft.Extensions.Logging
{
    internal class Logger : ILogger
    {
        public LoggerInformation[] Loggers { get; set; }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (Loggers == null)
            {
                return;
            }

            List<Exception> exceptions = null;
            foreach (var loggerInfo in Loggers)
            {
                if (!loggerInfo.IsEnabled(logLevel))
                {
                    continue;
                }

                try
                {
                    loggerInfo.Logger.Log(logLevel, eventId, state, exception, formatter);
                }
                catch (Exception ex)
                {
                    if (exceptions == null)
                    {
                        exceptions = new List<Exception>();
                    }

                    exceptions.Add(ex);
                }
            }

            if (exceptions != null && exceptions.Count > 0)
            {
                throw new AggregateException(
                    message: "An error occurred while writing to logger(s).", innerExceptions: exceptions);
            }
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            if (Loggers == null)
            {
                return false;
            }

            List<Exception> exceptions = null;
            foreach (var loggerInfo in Loggers)
            {
                if (!loggerInfo.IsEnabled(logLevel))
                {
                    return false;
                }

                try
                {
                    if (loggerInfo.Logger.IsEnabled(logLevel))
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    if (exceptions == null)
                    {
                        exceptions = new List<Exception>();
                    }

                    exceptions.Add(ex);
                }
            }

            if (exceptions != null && exceptions.Count > 0)
            {
                throw new AggregateException(
                    message: "An error occurred while writing to logger(s).",
                    innerExceptions: exceptions);
            }

            return false;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            if (Loggers == null)
            {
                return NullScope.Instance;
            }

            if (Loggers.Length == 1)
            {
                return Loggers[0].Logger.BeginScope(state);
            }

            var loggers = Loggers;

            var scope = new Scope(loggers.Length);
            List<Exception> exceptions = null;
            for (var index = 0; index < loggers.Length; index++)
            {
                try
                {
                    var disposable = loggers[index].Logger.BeginScope(state);
                    scope.SetDisposable(index, disposable);
                }
                catch (Exception ex)
                {
                    if (exceptions == null)
                    {
                        exceptions = new List<Exception>();
                    }

                    exceptions.Add(ex);
                }
            }

            if (exceptions != null && exceptions.Count > 0)
            {
                throw new AggregateException(
                    message: "An error occurred while writing to logger(s).", innerExceptions: exceptions);
            }

            return scope;
        }


        private class Scope : IDisposable
        {
            private bool _isDisposed;

            private IDisposable _disposable0;
            private IDisposable _disposable1;
            private readonly IDisposable[] _disposable;

            public Scope(int count)
            {
                if (count > 2)
                {
                    _disposable = new IDisposable[count - 2];
                }
            }

            public void SetDisposable(int index, IDisposable disposable)
            {
                if (index == 0)
                {
                    _disposable0 = disposable;
                }
                else if (index == 1)
                {
                    _disposable1 = disposable;
                }
                else
                {
                    _disposable[index - 2] = disposable;
                }
            }

            public void Dispose()
            {
                if (!_isDisposed)
                {
                    if (_disposable0 != null)
                    {
                        _disposable0.Dispose();
                    }
                    if (_disposable1 != null)
                    {
                        _disposable1.Dispose();
                    }
                    if (_disposable != null)
                    {
                        var count = _disposable.Length;
                        for (var index = 0; index != count; ++index)
                        {
                            if (_disposable[index] != null)
                            {
                                _disposable[index].Dispose();
                            }
                        }
                    }

                    _isDisposed = true;
                }
            }

            internal void Add(IDisposable disposable)
            {
                throw new NotImplementedException();
            }
        }

        public struct LoggerInformation
        {
            public ILogger Logger { get; set; }

            public string Category { get; set; }

            public string ProviderType { get; set; }

            public LogLevel? MinLevel { get; set; }

            public LogMessageFilter Filter { get; set; }

            public bool IsEnabled(LogLevel level)
            {
                if (MinLevel != null && level < MinLevel)
                {
                    return false;
                }

                if (Filter != null)
                {
                    return Filter(ProviderType, Category, level);
                }

                return true;
            }
        }
    }
}