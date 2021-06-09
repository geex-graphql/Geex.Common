using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Geex.Common.Abstractions;
using HotChocolate.Execution;
using HotChocolate.Execution.Instrumentation;
using HotChocolate.Execution.Options;
using HotChocolate.Execution.Processing;
using HotChocolate.Resolvers;
using HotChocolate.Types;
using Microsoft.Extensions.Logging;

namespace Geex.Common.Gql
{
    internal class GeexApolloTracingDiagnosticEventListener : DiagnosticEventListener
    {
        private const string _extensionKey = "tracing";
        private readonly ILogger<GeexApolloTracingDiagnosticEventListener> logger;
        private readonly TracingPreference _tracingPreference;
        private readonly ITimestampProvider _timestampProvider;

        public GeexApolloTracingDiagnosticEventListener(
          ILogger<GeexApolloTracingDiagnosticEventListener> logger,
          TracingPreference tracingPreference = TracingPreference.OnDemand,
          ITimestampProvider? timestampProvider = null)
        {
            this.logger = logger;
            this._tracingPreference = tracingPreference;
            this._timestampProvider = timestampProvider ?? (ITimestampProvider)new DefaultTimestampProvider();
        }

        public override bool EnableResolveFieldValue => true;

        public override IActivityScope ExecuteRequest(IRequestContext context)
        {
            if (!this.IsEnabled(context.ContextData))
                return this.EmptyScope;
            DateTime startTime = this._timestampProvider.UtcNow();
            GeexApolloTracingResultBuilder builder = GeexApolloTracingDiagnosticEventListener.CreateBuilder(context.ContextData, logger);
            builder.SetRequestStartTime((DateTimeOffset)startTime, this._timestampProvider.NowInNanoseconds());
            return (IActivityScope)new GeexApolloTracingDiagnosticEventListener.GeexApolloTracingRequestScope(context, logger, startTime, builder, this._timestampProvider);
        }

        public override IActivityScope ParseDocument(IRequestContext context)
        {
            GeexApolloTracingResultBuilder builder;
            return !GeexApolloTracingDiagnosticEventListener.TryGetBuilder(context.ContextData, out builder) ? this.EmptyScope : (IActivityScope)new GeexApolloTracingDiagnosticEventListener.ParseDocumentScope(builder, this._timestampProvider);
        }

        public override IActivityScope ValidateDocument(IRequestContext context)
        {
            GeexApolloTracingResultBuilder builder;
            return !GeexApolloTracingDiagnosticEventListener.TryGetBuilder(context.ContextData, out builder) ? this.EmptyScope : (IActivityScope)new GeexApolloTracingDiagnosticEventListener.ValidateDocumentScope(builder, this._timestampProvider);
        }

        public override IActivityScope ResolveFieldValue(IMiddlewareContext context)
        {
            GeexApolloTracingResultBuilder builder;
            return !GeexApolloTracingDiagnosticEventListener.TryGetBuilder(context.ContextData, out builder) ? this.EmptyScope : (IActivityScope)new GeexApolloTracingDiagnosticEventListener.ResolveFieldValueScope(context, builder, this._timestampProvider);
        }

        private static GeexApolloTracingResultBuilder CreateBuilder(IDictionary<string, object?> contextData,
            ILogger<GeexApolloTracingDiagnosticEventListener> logger)
        {
            GeexApolloTracingResultBuilder tracingResultBuilder = new GeexApolloTracingResultBuilder(logger);
            contextData["ApolloTracingResultBuilder"] = (object)tracingResultBuilder;
            return tracingResultBuilder;
        }

        private static bool TryGetBuilder(
          IDictionary<string, object?> contextData,
          [NotNullWhen(true)] out GeexApolloTracingResultBuilder? builder)
        {
            object obj;
            if (contextData.TryGetValue("ApolloTracingResultBuilder", out obj) && obj is GeexApolloTracingResultBuilder tracingResultBuilder)
            {
                builder = tracingResultBuilder;
                return true;
            }
            builder = (GeexApolloTracingResultBuilder)null;
            return false;
        }

        private bool IsEnabled(IDictionary<string, object?> contextData)
        {
            if (this._tracingPreference == TracingPreference.Always)
                return true;
            return this._tracingPreference == TracingPreference.OnDemand && contextData.ContainsKey("HotChocolate.Execution.EnableTracing");
        }

        private class GeexApolloTracingRequestScope : IActivityScope, IDisposable
        {
            private readonly IRequestContext _context;
            private readonly ILogger<GeexApolloTracingDiagnosticEventListener> _logger;
            private readonly DateTime _startTime;
            private readonly GeexApolloTracingResultBuilder _builder;
            private readonly ITimestampProvider _timestampProvider;
            private bool _disposed;

            public GeexApolloTracingRequestScope(IRequestContext context,
                ILogger<GeexApolloTracingDiagnosticEventListener> logger,
                DateTime startTime,
                GeexApolloTracingResultBuilder builder,
                ITimestampProvider timestampProvider)
            {
                this._context = context;
                this._logger = logger;
                this._startTime = startTime;
                this._builder = builder;
                this._timestampProvider = timestampProvider;
            }

            public void Dispose()
            {
                if (this._disposed)
                    return;
                this._builder.SetRequestDuration(this._timestampProvider.UtcNow() - this._startTime);
                if (this._context.Result is IReadOnlyQueryResult result)
                {
                    var resultMap = this._builder.Build();
                    this._logger.LogTraceWithData(GeexboxEventId.ApolloTracing, null, resultMap);
                    this._context.Result = (IExecutionResult)QueryResultBuilder.FromResult((IQueryResult)result).AddExtension("tracing", (object)resultMap).Create();
                }

                this._disposed = true;
            }
        }

        private class ParseDocumentScope : IActivityScope, IDisposable
        {
            private readonly GeexApolloTracingResultBuilder _builder;
            private readonly ITimestampProvider _timestampProvider;
            private readonly long _startTimestamp;
            private bool _disposed;

            public ParseDocumentScope(
              GeexApolloTracingResultBuilder builder,
              ITimestampProvider timestampProvider)
            {
                this._builder = builder;
                this._timestampProvider = timestampProvider;
                this._startTimestamp = timestampProvider.NowInNanoseconds();
            }

            public void Dispose()
            {
                if (this._disposed)
                    return;
                this._builder.SetParsingResult(this._startTimestamp, this._timestampProvider.NowInNanoseconds());
                this._disposed = true;
            }
        }

        private class ValidateDocumentScope : IActivityScope, IDisposable
        {
            private readonly GeexApolloTracingResultBuilder _builder;
            private readonly ITimestampProvider _timestampProvider;
            private readonly long _startTimestamp;
            private bool _disposed;

            public ValidateDocumentScope(
              GeexApolloTracingResultBuilder builder,
              ITimestampProvider timestampProvider)
            {
                this._builder = builder;
                this._timestampProvider = timestampProvider;
                this._startTimestamp = timestampProvider.NowInNanoseconds();
            }

            public void Dispose()
            {
                if (this._disposed)
                    return;
                this._builder.SetValidationResult(this._startTimestamp, this._timestampProvider.NowInNanoseconds());
                this._disposed = true;
            }
        }

        private class ResolveFieldValueScope : IActivityScope, IDisposable
        {
            private readonly IMiddlewareContext _context;
            private readonly GeexApolloTracingResultBuilder _builder;
            private readonly ITimestampProvider _timestampProvider;
            private readonly long _startTimestamp;
            private bool _disposed;

            public ResolveFieldValueScope(
              IMiddlewareContext context,
              GeexApolloTracingResultBuilder builder,
              ITimestampProvider timestampProvider)
            {
                this._context = context;
                this._builder = builder;
                this._timestampProvider = timestampProvider;
                this._startTimestamp = timestampProvider.NowInNanoseconds();
            }

            public void Dispose()
            {
                if (this._disposed)
                    return;
                this._builder.AddResolverResult(new GeexApolloTracingResolverRecord((IResolverContext)this._context, this._startTimestamp, this._timestampProvider.NowInNanoseconds()));
                this._disposed = true;
            }
        }
    }

    internal class GeexApolloTracingResultBuilder
    {
        private readonly ILogger<GeexApolloTracingDiagnosticEventListener> logger;
        private const int _apolloTracingVersion = 1;
        private const long _ticksToNanosecondsMultiplicator = 100;
        private readonly ConcurrentQueue<GeexApolloTracingResolverRecord> _resolverRecords = new ConcurrentQueue<GeexApolloTracingResolverRecord>();
        private TimeSpan _duration;
        private ResultMap? _parsingResult;
        private DateTimeOffset _startTime;
        private long _startTimestamp;
        private ResultMap? _validationResult;

        public GeexApolloTracingResultBuilder(ILogger<GeexApolloTracingDiagnosticEventListener> logger)
        {
            this.logger = logger;
        }

        public void SetRequestStartTime(DateTimeOffset startTime, long startTimestamp)
        {
            this._startTime = startTime;
            this._startTimestamp = startTimestamp;
            logger.LogTrace(GeexboxEventId.ApolloTracing, "request started.");
        }

        public void SetParsingResult(long startTimestamp, long endTimestamp)
        {
            this._parsingResult = new ResultMap();
            this._parsingResult.EnsureCapacity(2);
            this._parsingResult.SetValue(0, "startOffset", (object)(startTimestamp - this._startTimestamp));
            this._parsingResult.SetValue(1, "duration", (object)(endTimestamp - startTimestamp));
        }

        public void SetValidationResult(long startTimestamp, long endTimestamp)
        {
            this._validationResult = new ResultMap();
            this._validationResult.EnsureCapacity(2);
            this._validationResult.SetValue(0, "startOffset", (object)(startTimestamp - this._startTimestamp));
            this._validationResult.SetValue(1, "duration", (object)(endTimestamp - startTimestamp));
        }

        public void AddResolverResult(GeexApolloTracingResolverRecord record) => this._resolverRecords.Enqueue(record);

        public void SetRequestDuration(TimeSpan duration) => this._duration = duration;

        public IResultMap Build()
        {
            if (this._parsingResult == null)
                this.SetParsingResult(this._startTimestamp, this._startTimestamp);
            if (this._validationResult == null)
                this.SetValidationResult(this._startTimestamp, this._startTimestamp);
            ResultMap resultMap1 = new ResultMap();
            resultMap1.EnsureCapacity(1);
            resultMap1.SetValue(0, "resolvers", (object)this.BuildResolverResults());
            ResultMap resultMap2 = new ResultMap();
            resultMap2.EnsureCapacity(7);
            resultMap2.SetValue(0, "version", (object)1);
            resultMap2.SetValue(1, "startTime", (object)this._startTime.ToUnixTimeSeconds());
            resultMap2.SetValue(2, "endTime", (object)this._startTime.Add(this._duration).ToUnixTimeSeconds());
            resultMap2.SetValue(3, "duration", (object)(this._duration.TotalSeconds));
            resultMap2.SetValue(4, "parsing", (object)this._parsingResult);
            resultMap2.SetValue(5, "validation", (object)this._validationResult);
            resultMap2.SetValue(6, "execution", (object)resultMap1);
            return (IResultMap)resultMap2;
        }

        private ResultMap[] BuildResolverResults()
        {
            int num = 0;
            ResultMap[] resultMapArray = new ResultMap[this._resolverRecords.Count];
            foreach (GeexApolloTracingResolverRecord resolverRecord in this._resolverRecords)
            {
                ResultMap resultMap = new ResultMap();
                resultMap.EnsureCapacity(6);
                resultMap.SetValue(0, "path", (object)resolverRecord.Path);
                resultMap.SetValue(1, "parentType", (object)resolverRecord.ParentType);
                resultMap.SetValue(2, "fieldName", (object)resolverRecord.FieldName);
                resultMap.SetValue(3, "returnType", (object)resolverRecord.ReturnType);
                resultMap.SetValue(4, "startOffset", (object)(resolverRecord.StartTimestamp - this._startTimestamp));
                resultMap.SetValue(5, "duration", (object)(resolverRecord.EndTimestamp - resolverRecord.StartTimestamp));
                resultMapArray[num++] = resultMap;
            }
            return resultMapArray;
        }
    }

    internal class GeexApolloTracingResolverRecord
    {
        public GeexApolloTracingResolverRecord(
          IResolverContext context,
          long startTimestamp,
          long endTimestamp)
        {
            this.Path = context.Path.ToList();
            this.ParentType = (string)context.ObjectType.Name;
            this.FieldName = (string)context.Field.Name;
            this.ReturnType = (string)context.Field.Type.TypeName();
            this.StartTimestamp = startTimestamp;
            this.EndTimestamp = endTimestamp;
        }

        public IReadOnlyList<object> Path { get; }

        public string ParentType { get; }

        public string FieldName { get; }

        public string ReturnType { get; }

        public long StartTimestamp { get; }

        public long EndTimestamp { get; }
    }
}
